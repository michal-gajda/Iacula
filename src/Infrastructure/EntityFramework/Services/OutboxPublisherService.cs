namespace Iacula.Infrastructure.EntityFramework.Services;

using System.Text.Json;
using global::MassTransit;
using Iacula.Infrastructure.EntityFramework;
using Iacula.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

        // Oracle SELECT FOR UPDATE SKIP LOCKED atomically claims a disjoint set of rows per
        // database session — concurrent app instances each receive unique rows with no contention.
        // Raw ADO.NET is required because Oracle rejects FOR UPDATE SKIP LOCKED on the derived
        // table that EF Core's FromSql wrapping would introduce (ORA-02014).
        var lockedIds = new List<Guid>();

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var connection = dbContext.Database.GetDbConnection();

            await using (var selectCmd = connection.CreateCommand())
            {
                selectCmd.Transaction = dbContext.Database.CurrentTransaction!.GetDbTransaction();
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
                pProc.Value = nowUtc;
                selectCmd.Parameters.Add(pProc);

                var pLock = selectCmd.CreateParameter();
                pLock.ParameterName = "p_lock";
                pLock.Value = nowUtc;
                selectCmd.Parameters.Add(pLock);

                var pBatch = selectCmd.CreateParameter();
                pBatch.ParameterName = "p_batch";
                pBatch.Value = BATCH_SIZE;
                selectCmd.Parameters.Add(pBatch);

                await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    lockedIds.Add(new Guid((byte[])reader.GetValue(0)));
                }
            }

            if (lockedIds.Count == 0)
            {
                return;
            }

            await dbContext.Outbox
                .Where(x => lockedIds.Contains(x.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LockedUntilUtc, lockUntilUtc), cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        var outboxItems = await dbContext.Outbox
            .Where(x => lockedIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:send-form"));

        foreach (var outboxItem in outboxItems)
        {
            await PublishSingleAsync(dbContext, endpoint, outboxItem, nowUtc, cancellationToken);
        }
    }

    private static async Task PublishSingleAsync(IaculaDbContext dbContext, ISendEndpoint endpoint, OutboxDbEntity outboxItem, DateTime nowUtc, CancellationToken cancellationToken)
    {
        try
        {
            var messageType = Type.GetType(outboxItem.MessageType, throwOnError: true)!;
            var messageObject = JsonSerializer.Deserialize(outboxItem.PayloadJson, messageType)!;

            await endpoint.Send(messageObject, messageType, cancellationToken);

            outboxItem.SentAtUtc = nowUtc;
            outboxItem.LockedUntilUtc = null;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            outboxItem.AttemptCount++;
            outboxItem.LastError = exception.ToString();
            outboxItem.LockedUntilUtc = null;
            outboxItem.ProcessAfterUtc = nowUtc.AddSeconds(Math.Min(60, 2 * outboxItem.AttemptCount));

            if (outboxItem.AttemptCount >= 10)
            {
                outboxItem.FailedAtUtc = nowUtc;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
