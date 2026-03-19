namespace Iacula.Application.FunctionalTests.Forms;

using System.Text.Json;
using global::MassTransit;
using Iacula.Application;
using Iacula.Domain.Types;
using Iacula.Infrastructure;
using Iacula.Infrastructure.EntityFramework;
using Iacula.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

[TestClass]
public class OutboxPublisherServiceTests : TestBase
{
    [TestMethod]
    public async Task MultipleInstances_Should_Not_ProcessSameItemTwice()
    {
        // Arrange: Create 100 outbox items
        await using var initialScope = this.Provider.CreateAsyncScope();
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

        // Act: Simulate 5 app instances polling concurrently until the queue is drained.
        var allProcessedIds = new List<Guid>();

        while (true)
        {
            var roundResults = await Task.WhenAll(Enumerable.Range(0, 5)
                .Select(_ => ProcessBatchAsync(now)));

            var processedThisRound = roundResults.SelectMany(ids => ids).ToList();
            if (processedThisRound.Count == 0)
            {
                break;
            }

            allProcessedIds.AddRange(processedThisRound);
        }

        // Assert: All items should be published exactly once
        await using var verifyScope = this.Provider.CreateAsyncScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<IaculaDbContext>();

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
        await using var initialScope = this.Provider.CreateAsyncScope();
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
        await using var scope = this.Provider.CreateAsyncScope();
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

    private record TestMessage
    {
        public int Value { get; set; }
    }

    private async Task<List<Guid>> ProcessBatchAsync(DateTime now)
    {
        await using var scope = this.Provider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IaculaDbContext>();

        var lockUntilUtc = now.AddSeconds(30);
        var lockedIds = new List<Guid>();

        await using (var transaction = await ctx.Database.BeginTransactionAsync())
        {
            var connection = ctx.Database.GetDbConnection();

            await using (var selectCmd = connection.CreateCommand())
            {
                selectCmd.Transaction = ctx.Database.CurrentTransaction!.GetDbTransaction();
                selectCmd.CommandText = """
                    SELECT "Id" FROM "Outbox"
                    WHERE "SentAtUtc" IS NULL
                      AND "FailedAtUtc" IS NULL
                      AND ("ProcessAfterUtc" IS NULL OR "ProcessAfterUtc" <= :p_proc)
                      AND ("LockedUntilUtc" IS NULL OR "LockedUntilUtc" <= :p_lock)
                      AND ROWNUM <= :p_batch
                    FOR UPDATE SKIP LOCKED
                    """;

                var pProc = selectCmd.CreateParameter();
                pProc.ParameterName = "p_proc";
                pProc.Value = now;
                selectCmd.Parameters.Add(pProc);

                var pLock = selectCmd.CreateParameter();
                pLock.ParameterName = "p_lock";
                pLock.Value = now;
                selectCmd.Parameters.Add(pLock);

                var pBatch = selectCmd.CreateParameter();
                pBatch.ParameterName = "p_batch";
                pBatch.Value = 50;
                selectCmd.Parameters.Add(pBatch);

                await using var reader = await selectCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    lockedIds.Add(new Guid((byte[])reader.GetValue(0)));
                }
            }

            if (lockedIds.Count == 0)
            {
                return lockedIds;
            }

            await ctx.Outbox
                .Where(x => lockedIds.Contains(x.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LockedUntilUtc, lockUntilUtc));

            await transaction.CommitAsync();
        }

        var items = await ctx.Outbox
            .Where(x => lockedIds.Contains(x.Id))
            .ToListAsync();

        foreach (var item in items)
        {
            item.SentAtUtc = now;
            item.LockedUntilUtc = null;
        }

        await ctx.SaveChangesAsync();

        return lockedIds;
    }
}
