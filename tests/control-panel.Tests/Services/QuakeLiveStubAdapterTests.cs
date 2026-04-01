using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class QuakeLiveGameAdapterTests
{
    private readonly QuakeLiveGameAdapter _adapter = new();

    [Fact]
    public void GameKey_IsQuakeLive()
    {
        Assert.Equal("quake-live", _adapter.GameKey);
    }

    [Fact]
    public void Adapter_ImplementsIGameAdapter()
    {
        Assert.IsAssignableFrom<IGameAdapter>(_adapter);
    }

    [Fact]
    public void GetSummary_ReturnsNonNullSummary()
    {
        var summary = _adapter.GetSummary(null);

        Assert.NotNull(summary);
    }

    [Fact]
    public void GetSummary_ShowsDisabledRcon_WhenRconIsOff()
    {
        var summary = _adapter.GetSummary(null);

        Assert.Equal("Disabled", summary.RconLabel);
    }

    [Fact]
    public void CreateDefaultJson_ReturnsNonEmptyString()
    {
        var json = _adapter.CreateDefaultJson();

        Assert.NotEmpty(json);
    }

    [Fact]
    public void GetContainerEnv_ReturnsNonEmptyDictionary()
    {
        var env = _adapter.GetContainerEnv(null);

        Assert.NotEmpty(env);
    }
}
