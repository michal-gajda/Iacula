namespace Iacula.Application.FunctionalTests.Forms;

using System.Text.Json;
using global::MassTransit;
using Iacula.Application;
using Iacula.Domain.Types;
using Iacula.Infrastructure;
using Iacula.Infrastructure.EntityFramework;
using Iacula.Infrastructure.EntityFramework.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

[TestClass]
public class OutboxPublisherServiceTests : IAsyncDisposable
{
    private ServiceProvider provider = null!;
    private string testDbPath = null!;

    [TestInitialize]
    public void Setup()
    {
        this.testDbPath = Path.Combine(Path.GetTempPath(), $"iacula_test_{Guid.NewGuid()}.db");

        // Note: Using clean connection string without Mode and Timeout
        // EF Core will add these from the SqliteDbContextOptions when needed
        var connectionString = $"Data Source={this.testDbPath}";

        var config = new Dictionary<string, string?>
        {
            {"ConnectionStrings:DefaultConnection", connectionString},
            {"Logging:LogLevel:Default", "Information"},
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        this.provider = services.BuildServiceProvider(validateScopes: true);

        using var scope = this.provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IaculaDbContext>();
        dbContext.Database.EnsureCreated();
        dbContext.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        dbContext.Database.ExecuteSqlRaw("PRAGMA busy_timeout=30000;");
    }

    [TestMethod]
    public async Task MultipleInstances_Should_Not_ProcessSameItemTwice()
    {
        // Arrange: Create 100 outbox items
        await using var initialScope = this.provider.CreateAsyncScope();
        var dbContext = initialScope.ServiceProvider.GetRequiredService<IaculaDbContext>();
        var now = DateTime.UtcNow;

        var outboxItems = Enumerable.Range(0, 100)
            .Select(i => new OutboxDbEntity
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = now,
                MessageType = typeof(TestMessage).FullName!,
                PayloadJson = JsonSerializer.Serialize(new TestMessage { Value = i }),
                AttemptCount = 0,
            })
            .ToList();

        await dbContext.Outbox.AddRangeAsync(outboxItems);
        await dbContext.SaveChangesAsync();

        // Act: Simulate 5 app instances running PublishBatchAsync concurrently
        var publishTasks = Enumerable.Range(0, 5)
            .Select(async instanceId =>
            {
                await using var scope = this.provider.CreateAsyncScope();
                var ctx = scope.ServiceProvider.GetRequiredService<IaculaDbContext>();

                // Simulate PublishBatchAsync logic
                var lockUntilUtc = now.AddSeconds(30);
                var publishedIds = new List<Guid>();

                var candidateIds = await ctx.Outbox
                    .AsNoTracking()
                    .Where(item => item.SentAtUtc == null)
                    .Where(item => item.FailedAtUtc == null)
                    .Where(item => item.LockedUntilUtc == null || item.LockedUntilUtc <= now)
                    .OrderBy(item => item.CreatedAtUtc)
                    .Select(item => item.Id)
                    .Take(50)
                    .ToListAsync();

                foreach (var candidateId in candidateIds)
                {
                    // Atomic claim: try to lock the item
                    var rowsAffected = await ctx.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE "Outbox"
                        SET "LockedUntilUtc" = {lockUntilUtc}
                        WHERE "Id" = {candidateId}
                          AND "SentAtUtc" IS NULL
                          AND "FailedAtUtc" IS NULL
                          AND ("LockedUntilUtc" IS NULL OR "LockedUntilUtc" <= {now});
                        """);

                    if (rowsAffected == 1)
                    {
                        publishedIds.Add(candidateId);
                    }
                }

                // Mark as sent
                foreach (var publishedId in publishedIds)
                {
                    var item = await ctx.Outbox.SingleAsync(x => x.Id == publishedId);
                    item.SentAtUtc = now;
                    item.LockedUntilUtc = null;
                    await ctx.SaveChangesAsync();
                }

                return publishedIds;
            })
            .ToList();

        var results = await Task.WhenAll(publishTasks);

        // Assert: All items should be published exactly once
        await using var verifyScope = this.provider.CreateAsyncScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<IaculaDbContext>();

        var allProcessedIds = results.SelectMany(x => x).ToList();
        var uniqueProcessedIds = allProcessedIds.Distinct().ToList();

        allProcessedIds.Count.ShouldBe(100, "All 100 items should be processed");
        uniqueProcessedIds.Count.ShouldBe(100, "No item should be processed twice");

        var sentItems = await verifyDbContext.Outbox
            .Where(x => x.SentAtUtc != null)
            .ToListAsync();

        sentItems.Count.ShouldBe(100, "All items should be marked as sent");

        foreach (var item in sentItems)
        {
            item.LockedUntilUtc.ShouldBeNull("Locks should be cleared after processing");
        }
    }

    [TestMethod]
    public async Task FailedItem_Should_Not_Block_Others()
    {
        // Arrange: Create some items, mark one as failed
        await using var initialScope = this.provider.CreateAsyncScope();
        var dbContext = initialScope.ServiceProvider.GetRequiredService<IaculaDbContext>();
        var now = DateTime.UtcNow;

        var failedItem = new OutboxDbEntity
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = now.AddSeconds(-100),
            MessageType = typeof(TestMessage).FullName!,
            PayloadJson = "{}",
            AttemptCount = 10,
            FailedAtUtc = now,
        };

        var goodItems = Enumerable.Range(0, 50)
            .Select(i => new OutboxDbEntity
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = now,
                MessageType = typeof(TestMessage).FullName!,
                PayloadJson = JsonSerializer.Serialize(new TestMessage { Value = i }),
                AttemptCount = 0,
            })
            .ToList();

        await dbContext.Outbox.AddAsync(failedItem);
        await dbContext.Outbox.AddRangeAsync(goodItems);
        await dbContext.SaveChangesAsync();

        // Act: Try to process
        await using var scope = this.provider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IaculaDbContext>();

        var candidateIds = await ctx.Outbox
            .AsNoTracking()
            .Where(item => item.SentAtUtc == null)
            .Where(item => item.FailedAtUtc == null)
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => item.Id)
            .ToListAsync();

        // Assert: Failed item should not be in candidates
        candidateIds.ShouldNotContain(failedItem.Id);
        candidateIds.Count.ShouldBe(50);
    }

    public async ValueTask DisposeAsync()
    {
        await this.provider.DisposeAsync();

        try
        {
            if (File.Exists(this.testDbPath))
                File.Delete(this.testDbPath);

            var walFile = $"{this.testDbPath}-wal";
            if (File.Exists(walFile))
                File.Delete(walFile);

            var shmFile = $"{this.testDbPath}-shm";
            if (File.Exists(shmFile))
                File.Delete(shmFile);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private record TestMessage
    {
        public int Value { get; set; }
    }
}
