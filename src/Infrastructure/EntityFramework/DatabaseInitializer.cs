namespace Iacula.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore;

internal sealed class DatabaseInitializer
{
    private readonly IaculaDbContext dbContext;

    public DatabaseInitializer(IaculaDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await this.dbContext.Database.EnsureCreatedAsync(cancellationToken);

        await this.dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "Outbox" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Outbox" PRIMARY KEY,
                "CreatedAtUtc" TEXT NOT NULL,
                "ProcessAfterUtc" TEXT NULL,
                "LockedUntilUtc" TEXT NULL,
                "AttemptCount" INTEGER NOT NULL,
                "SentAtUtc" TEXT NULL,
                "FailedAtUtc" TEXT NULL,
                "MessageType" TEXT NOT NULL,
                "PayloadJson" TEXT NOT NULL,
                "LastError" TEXT NULL
            );
            """,
            cancellationToken);
    }
}
