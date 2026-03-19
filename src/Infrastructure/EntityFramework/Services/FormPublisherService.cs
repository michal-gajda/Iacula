namespace Iacula.Infrastructure.EntityFramework.Services;

using global::MassTransit;
using Iacula.Infrastructure.EntityFramework;
using Iacula.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal sealed class FormPublisherService : BackgroundService
{
    private const int BATCH_SIZE = 50;
    private const int MAX_ATTEMPTS = 10;

    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ILogger<FormPublisherService> logger;
    private readonly TimeProvider timeProvider;

    public FormPublisherService(IServiceScopeFactory serviceScopeFactory, ILogger<FormPublisherService> logger, TimeProvider timeProvider)
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
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                this.logger.LogError(exception, "Form publisher failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task PublishBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = this.serviceScopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<IaculaDbContext>();
        var sendEndpointProvider = scope.ServiceProvider.GetRequiredService<ISendEndpointProvider>();

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
                    SELECT "Id" FROM "Forms"
                    WHERE "Status" IN (:p_created, :p_failed)
                      AND ROWNUM <= :p_batch
                    FOR UPDATE SKIP LOCKED
                    """;

                var pCreated = selectCmd.CreateParameter();
                pCreated.ParameterName = "p_created";
                pCreated.Value = (int)MessageStatus.Created;
                selectCmd.Parameters.Add(pCreated);

                var pFailed = selectCmd.CreateParameter();
                pFailed.ParameterName = "p_failed";
                pFailed.Value = (int)MessageStatus.Failed;
                selectCmd.Parameters.Add(pFailed);

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

            await dbContext.Forms
                .Where(x => lockedIds.Contains(x.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, (int)MessageStatus.InProgress), cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        var forms = await dbContext.Forms
            .Where(x => lockedIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:send-form"));

        foreach (var form in forms)
        {
            await this.PublishSingleAsync(dbContext, endpoint, form, cancellationToken);
        }
    }

    private async Task PublishSingleAsync(IaculaDbContext dbContext, ISendEndpoint endpoint, FormDbEntity form, CancellationToken cancellationToken)
    {
        try
        {
            var message = new Iacula.Shared.SendForm
            {
                Id = form.Id,
                Payload = form.Payload,
            };

            await endpoint.Send(message, cancellationToken);

            form.Status = (int)MessageStatus.Published;
        }
        catch (Exception exception)
        {
            form.AttemptCount++;

            if (form.AttemptCount >= MAX_ATTEMPTS)
            {
                this.logger.LogError(exception, "Form {FormId} permanently failed after {Attempts} attempts", form.Id, form.AttemptCount);
                form.Status = (int)MessageStatus.PermanentlyFailed;
            }
            else
            {
                this.logger.LogWarning(exception, "Failed to publish form {FormId}, attempt {Attempt}/{Max}", form.Id, form.AttemptCount, MAX_ATTEMPTS);
                form.Status = (int)MessageStatus.Failed;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
