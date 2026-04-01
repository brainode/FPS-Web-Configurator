using control_panel.Services;
using control_panel.Tests.TestSupport;

namespace control_panel.Tests.Services;

public sealed class PanelStoragePathResolverTests
{
    [Fact]
    public void ResolveRootPath_UsesConfiguredPath_WhenProvided()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var configuredPath = System.IO.Path.Combine(tempDirectory.Path, "panel-data");

        var result = PanelStoragePathResolver.ResolveRootPath(@"C:\ignored", configuredPath);

        Assert.Equal(System.IO.Path.GetFullPath(configuredPath), result);
    }

    [Fact]
    public void ResolveRootPath_UsesRepositoryDataFolder_WhenContentRootLooksLikeRepoRoot()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var repoRoot = System.IO.Path.Combine(tempDirectory.Path, "repo-root");
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(System.IO.Path.Combine(repoRoot, "services"));
        File.WriteAllText(System.IO.Path.Combine(repoRoot, "TASKS.md"), "# test");

        var result = PanelStoragePathResolver.ResolveRootPath(repoRoot, configuredRootPath: null);

        Assert.Equal(
            System.IO.Path.GetFullPath(System.IO.Path.Combine(repoRoot, "data", "control-panel")),
            result);
    }

    [Fact]
    public void ResolveRootPath_UsesRepositoryDataFolder_WhenContentRootLooksLikeProjectRoot()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var repoRoot = System.IO.Path.Combine(tempDirectory.Path, "repo-root");
        var projectRoot = System.IO.Path.Combine(repoRoot, "services", "control-panel");
        Directory.CreateDirectory(System.IO.Path.Combine(projectRoot, "Pages"));
        File.WriteAllText(System.IO.Path.Combine(projectRoot, "control-panel.csproj"), "<Project />");

        var result = PanelStoragePathResolver.ResolveRootPath(projectRoot, configuredRootPath: null);

        Assert.Equal(
            System.IO.Path.GetFullPath(System.IO.Path.Combine(repoRoot, "data", "control-panel")),
            result);
    }
}
