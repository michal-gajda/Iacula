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
        await this.ApplySchemaChangesAsync(cancellationToken);
    }

    private async Task ApplySchemaChangesAsync(CancellationToken cancellationToken)
    {
        var attemptCountExists = await this.dbContext.Database
            .SqlQuery<int>($"""
                SELECT COUNT(*) AS "Value"
                FROM user_tab_columns
                WHERE UPPER(table_name) = 'FORMS'
                  AND UPPER(column_name) = 'ATTEMPTCOUNT'
                """)
            .SingleAsync(cancellationToken);

        if (attemptCountExists == 0)
        {
            await this.dbContext.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "Forms" ADD "AttemptCount" NUMBER(10,0) DEFAULT 0 NOT NULL""",
                cancellationToken);
        }
    }
}
