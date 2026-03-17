namespace Iacula.Infrastructure.EntityFramework.Services;

using System.Text.Json;
using global::MassTransit;
using Iacula.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal sealed class OutboxPublisherService : BackgroundService
{
    private const int BATCH_SIZE = 50;

    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ILogger<OutboxPublisherService> logger;
    private readonly TimeProvider timeProvider;

    public OutboxPublisherService(IServiceScopeFactory serviceScopeFactory, ILogger<OutboxPublisherService> logger, TimeProvider timeProvider)
    {
        this.serviceScopeFactory = serviceScopeFactory;
        this.logger = logger;
        this.timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (stoppingToken.IsCancellationRequested is false)
        {
            try
            {
                await this.PublishBatchAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                this.logger.LogError(exception, "Outbox publisher failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task PublishBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = this.serviceScopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<IaculaDbContext>();
        var sendEndpointProvider = scope.ServiceProvider.GetRequiredService<ISendEndpointProvider>();

        var nowUtc = this.timeProvider.GetUtcNow().UtcDateTime;
        var lockUntilUtc = nowUtc.AddSeconds(30);

        var candidateIds = await dbContext.Outbox
            .AsNoTracking()
            .Where(item => item.SentAtUtc == null)
            .Where(item => item.FailedAtUtc == null)
            .Where(item => item.ProcessAfterUtc == null || item.ProcessAfterUtc <= nowUtc)
            .Where(item => item.LockedUntilUtc == null || item.LockedUntilUtc <= nowUtc)
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => item.Id)
            .Take(BATCH_SIZE)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
        {
            return;
        }

        var lockedIds = new List<Guid>(candidateIds.Count);

        foreach (var candidateId in candidateIds)
        {
            // Atomic claim prevents two app instances from locking and publishing the same outbox row.
            var rowsAffected = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE "Outbox"
                SET "LockedUntilUtc" = {lockUntilUtc}
                WHERE "Id" = {candidateId}
                  AND "SentAtUtc" IS NULL
                  AND "FailedAtUtc" IS NULL
                  AND ("ProcessAfterUtc" IS NULL OR "ProcessAfterUtc" <= {nowUtc})
                  AND ("LockedUntilUtc" IS NULL OR "LockedUntilUtc" <= {nowUtc});
                """,
                cancellationToken);

            if (rowsAffected == 1)
            {
                lockedIds.Add(candidateId);
            }
        }

        foreach (var lockedId in lockedIds)
        {
            await this.PublishSingleAsync(dbContext, sendEndpointProvider, lockedId, cancellationToken);
        }
    }

    private async Task PublishSingleAsync(IaculaDbContext dbContext, ISendEndpointProvider sendEndpointProvider, Guid outboxId, CancellationToken cancellationToken)
    {
        var outboxItem = await dbContext.Outbox.SingleAsync(item => item.Id == outboxId, cancellationToken);

        try
        {
            var messageType = Type.GetType(outboxItem.MessageType, throwOnError: true)!;
            var messageObject = JsonSerializer.Deserialize(outboxItem.PayloadJson, messageType)!;

            var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:send-form"));
            await endpoint.Send(messageObject, messageType, cancellationToken);

            outboxItem.SentAtUtc = this.timeProvider.GetUtcNow().UtcDateTime;
            outboxItem.LockedUntilUtc = null;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            outboxItem.AttemptCount++;
            outboxItem.LastError = exception.ToString();
            outboxItem.LockedUntilUtc = null;

            outboxItem.ProcessAfterUtc = this.timeProvider.GetUtcNow().UtcDateTime.AddSeconds(Math.Min(60, 2 * outboxItem.AttemptCount));

            if (outboxItem.AttemptCount >= 10)
            {
                outboxItem.FailedAtUtc = this.timeProvider.GetUtcNow().UtcDateTime;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
