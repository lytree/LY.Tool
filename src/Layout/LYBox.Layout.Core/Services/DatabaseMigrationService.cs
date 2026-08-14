using LYBox.Layout.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LYBox.Layout.Core.Services;

public sealed class DatabaseMigrationService
{
    private readonly IDbContextFactory<AppDbContext> dbFactory;
    private readonly ILogger<DatabaseMigrationService> logger;

    public DatabaseMigrationService(
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<DatabaseMigrationService> logger)
    {
        this.dbFactory = dbFactory;
        this.logger = logger;
    }

    public void Migrate()
    {
        using var db = dbFactory.CreateDbContext();
        var pendingMigrations = db.Database.GetPendingMigrations().ToArray();

        if (pendingMigrations.Length == 0)
        {
            logger.LogInformation("Application database is up to date.");
            return;
        }

        logger.LogInformation("Applying {Count} database migration(s): {Migrations}", pendingMigrations.Length, string.Join(", ", pendingMigrations));
        db.Database.Migrate();
        logger.LogInformation("Application database migrations applied.");
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var pendingMigrations = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        var migrations = pendingMigrations.ToArray();

        if (migrations.Length == 0)
        {
            logger.LogInformation("Application database is up to date.");
            return;
        }

        logger.LogInformation("Applying {Count} database migration(s): {Migrations}", migrations.Length, string.Join(", ", migrations));
        await db.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Application database migrations applied.");
    }
}
