using control_panel.Data;
using control_panel.Services;
using control_panel.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace control_panel.Tests.Services;

public sealed class SqliteConfigurationStoreTests
{
    [Fact]
    public async Task GetOrCreateAsync_SeedsWarsowConfiguration()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        await using var dbContext = CreateDbContext(tempDirectory.Path);
        await dbContext.Database.EnsureCreatedAsync();
        var store = new SqliteConfigurationStore(dbContext, [new WarsowGameAdapter()]);

        var configuration = await store.GetOrCreateAsync("warsow");

        Assert.Equal("warsow", configuration.GameKey);
        Assert.Equal("Warsow", configuration.DisplayName);
        Assert.Contains("\"g_gametype\"", configuration.JsonContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_UpdatesJsonAndAuthor()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        await using var dbContext = CreateDbContext(tempDirectory.Path);
        await dbContext.Database.EnsureCreatedAsync();
        var store = new SqliteConfigurationStore(dbContext, [new WarsowGameAdapter()]);

        var updated = await store.SaveAsync("warsow", "{\"sv_defaultmap\":\"wdm1\"}", "tester");

        Assert.Equal("{\"sv_defaultmap\":\"wdm1\"}", updated.JsonContent);
        Assert.Equal("tester", updated.UpdatedBy);
    }

    [Fact]
    public async Task GetOrCreateAsync_UnknownGame_UsesGenericSeed()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        await using var dbContext = CreateDbContext(tempDirectory.Path);
        await dbContext.Database.EnsureCreatedAsync();
        var store = new SqliteConfigurationStore(dbContext, []);

        var configuration = await store.GetOrCreateAsync("unknown-game");

        Assert.Equal("unknown-game", configuration.GameKey);
        Assert.Equal("unknown-game", configuration.DisplayName);
    }

    private static ControlPanelDbContext CreateDbContext(string tempRootPath)
    {
        var databasePath = System.IO.Path.Combine(tempRootPath, "control-panel-tests.db");
        var options = new DbContextOptionsBuilder<ControlPanelDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;

        return new ControlPanelDbContext(options);
    }
}
