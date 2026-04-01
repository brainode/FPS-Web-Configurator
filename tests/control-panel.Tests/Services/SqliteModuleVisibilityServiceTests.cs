using control_panel.Data;
using control_panel.Models;
using control_panel.Services;
using control_panel.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace control_panel.Tests.Services;

public sealed class SqliteModuleVisibilityServiceTests
{
    [Fact]
    public async Task GetAsync_DefaultsToAllModulesVisibleInCatalogOrder()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        await using var dbContext = CreateDbContext(tempDirectory.Path);
        await dbContext.Database.EnsureCreatedAsync();
        var catalog = CreateCatalog();
        var service = new SqliteModuleVisibilityService(dbContext, catalog);

        var snapshot = await service.GetAsync();

        Assert.Equal(["warsow", "warfork", "quake-live", "reflex-arena"], snapshot.Settings.EnabledGameKeys);
    }

    [Fact]
    public async Task SaveAsync_FiltersUnknownKeys_AndPreservesCatalogOrder()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        await using var dbContext = CreateDbContext(tempDirectory.Path);
        await dbContext.Database.EnsureCreatedAsync();
        var catalog = CreateCatalog();
        var service = new SqliteModuleVisibilityService(dbContext, catalog);

        var snapshot = await service.SaveAsync(["reflex-arena", "unknown", "warsow"], "tester");

        Assert.Equal(["warsow", "reflex-arena"], snapshot.Settings.EnabledGameKeys);
        Assert.Equal("tester", snapshot.UpdatedBy);
    }

    [Fact]
    public async Task GetVisibleModulesAsync_ReturnsOnlyEnabledModules()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        await using var dbContext = CreateDbContext(tempDirectory.Path);
        await dbContext.Database.EnsureCreatedAsync();
        var catalog = CreateCatalog();
        var service = new SqliteModuleVisibilityService(dbContext, catalog);

        await service.SaveAsync(["warfork", "quake-live"], "tester");
        var visibleModules = await service.GetVisibleModulesAsync();

        Assert.Equal(["warfork", "quake-live"], visibleModules.Select(module => module.GameKey).ToArray());
    }

    [Fact]
    public async Task GetAsync_InvalidJson_FallsBackToAllModulesVisible()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        await using var dbContext = CreateDbContext(tempDirectory.Path);
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.PanelSettings.Add(new PanelSetting
        {
            SettingKey = "module-visibility",
            JsonContent = "{ invalid json }",
            UpdatedUtc = DateTimeOffset.UtcNow,
            UpdatedBy = "tester"
        });
        await dbContext.SaveChangesAsync();

        var service = new SqliteModuleVisibilityService(dbContext, CreateCatalog());
        var snapshot = await service.GetAsync();

        Assert.Equal(["warsow", "warfork", "quake-live", "reflex-arena"], snapshot.Settings.EnabledGameKeys);
    }

    private static PanelGameModuleCatalog CreateCatalog() =>
        new PanelGameModuleCatalog(new IGameAdapter[]
        {
            new WarsowGameAdapter(),
            new WarforkGameAdapter(),
            new QuakeLiveGameAdapter(),
            new ReflexArenaGameAdapter()
        });

    private static ControlPanelDbContext CreateDbContext(string tempRootPath)
    {
        var databasePath = Path.Combine(tempRootPath, "module-visibility-tests.db");
        var options = new DbContextOptionsBuilder<ControlPanelDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;

        return new ControlPanelDbContext(options);
    }
}
