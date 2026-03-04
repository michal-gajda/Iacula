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

    public OutboxPublisherService(IServiceScopeFactory serviceScopeFactory, ILogger<OutboxPublisherService> logger)
    {
        this.serviceScopeFactory = serviceScopeFactory;
        this.logger = logger;
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

        var nowUtc = DateTime.UtcNow;
        var lockUntilUtc = nowUtc.AddSeconds(30);

        var candidates = await dbContext.Outbox
            .AsNoTracking()
            .Where(item => item.SentAtUtc == null)
            .Where(item => item.FailedAtUtc == null)
            .Where(item => item.ProcessAfterUtc == null || item.ProcessAfterUtc <= nowUtc)
            .Where(item => item.LockedUntilUtc == null || item.LockedUntilUtc <= nowUtc)
            .OrderBy(item => item.CreatedAtUtc)
            .Take(BATCH_SIZE)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return;
        }

        foreach (var candidate in candidates)
        {
            var tracked = await dbContext.Outbox.SingleAsync(item => item.Id == candidate.Id, cancellationToken);
            tracked.LockedUntilUtc = lockUntilUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            await this.PublishSingleAsync(dbContext, sendEndpointProvider, candidate.Id, cancellationToken);
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

            outboxItem.SentAtUtc = DateTime.UtcNow;
            outboxItem.LockedUntilUtc = null;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            outboxItem.AttemptCount++;
            outboxItem.LastError = exception.ToString();
            outboxItem.LockedUntilUtc = null;

            outboxItem.ProcessAfterUtc = DateTime.UtcNow.AddSeconds(Math.Min(60, 2 * outboxItem.AttemptCount));

            if (outboxItem.AttemptCount >= 10)
            {
                outboxItem.FailedAtUtc = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
