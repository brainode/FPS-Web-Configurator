using control_panel.Data;
using control_panel.Models;
using control_panel.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace control_panel.Services;

public sealed class DatabaseInitializer(
    ControlPanelDbContext dbContext,
    IPasswordHasher passwordHasher,
    IOptions<PanelAuthOptions> authOptions,
    IConfigurationStore configurationStore,
    IModuleVisibilityService moduleVisibilityService,
    IEnumerable<IGameAdapter> gameAdapters,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsurePanelSettingsTableAsync(cancellationToken);

        var options = authOptions.Value;
        var existingAdmin = await dbContext.Users
            .SingleOrDefaultAsync(x => x.Username == options.SeedAdminUsername, cancellationToken);

        if (existingAdmin is null)
        {
            dbContext.Users.Add(new PanelUser
            {
                Username = options.SeedAdminUsername,
                PasswordHash = passwordHasher.HashPassword(options.SeedAdminPassword),
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded default control-panel admin user '{Username}'.", options.SeedAdminUsername);
        }

        foreach (var adapter in gameAdapters)
        {
            await configurationStore.GetOrCreateAsync(adapter.GameKey, cancellationToken);
        }

        await moduleVisibilityService.GetAsync(cancellationToken);
    }

    private async Task EnsurePanelSettingsTableAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS PanelSettings (
                Id INTEGER NOT NULL CONSTRAINT PK_PanelSettings PRIMARY KEY AUTOINCREMENT,
                SettingKey TEXT NOT NULL,
                JsonContent TEXT NOT NULL,
                UpdatedUtc TEXT NOT NULL,
                UpdatedBy TEXT NULL
            );
            """, cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_PanelSettings_SettingKey
            ON PanelSettings (SettingKey);
            """, cancellationToken);
    }
}
