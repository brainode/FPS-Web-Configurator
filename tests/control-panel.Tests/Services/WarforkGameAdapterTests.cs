using control_panel.Models;
using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class WarforkGameAdapterTests
{
    private readonly WarforkGameAdapter _adapter = new();

    [Fact]
    public void GameKey_IsWarfork()
    {
        Assert.Equal("warfork", _adapter.GameKey);
    }

    [Fact]
    public void GetSummary_NullJson_ReturnsDefaultSummary()
    {
        var summary = _adapter.GetSummary(null);

        Assert.NotEmpty(summary.ModeName);
        Assert.NotEmpty(summary.StartMap);
    }

    [Fact]
    public void GetSummary_WithInstagibAndInstajump_ShowsBothInModeFlags()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"],
            Instagib = true,
            Instajump = true
        });

        var summary = _adapter.GetSummary(json);

        Assert.Contains("Instagib", summary.ModeFlags);
        Assert.Contains("Instajump", summary.ModeFlags);
    }

    [Fact]
    public void GetSummary_NoFlags_ReturnsStandardRuleset()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"]
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("Standard ruleset", summary.ModeFlags);
    }

    [Fact]
    public void GetSummary_WithServerPassword_ShowsPasswordProtected()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"],
            ServerPassword = "secret"
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("Password protected", summary.AccessLabel);
    }

    [Fact]
    public void GetSummary_NoServerPassword_ShowsOpenLobby()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"]
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("Open lobby", summary.AccessLabel);
    }

    [Fact]
    public void GetSummary_RconConfigured_ShowsConfigured()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"],
            RconPassword = "secret"
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("Configured", summary.RconLabel);
    }

    [Fact]
    public void GetSummary_RconEmpty_ShowsOptional()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"],
            RconPassword = string.Empty
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("Optional", summary.RconLabel);
    }

    [Fact]
    public void GetSummary_StartMap_IsUpperCase()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            StartMap = "return",
            MapList = ["return"]
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("RETURN", summary.StartMap);
    }

    [Fact]
    public void GetSummary_MapCountLabel_IncludesCount()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return", "pressure"]
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("2 map(s) selected", summary.MapCountLabel);
    }

    [Fact]
    public void CreateDefaultJson_ProducesDeserializableSettings()
    {
        var json = _adapter.CreateDefaultJson();
        var settings = WarforkConfigurationSerializer.Deserialize(json);

        Assert.NotNull(settings);
        Assert.NotEmpty(settings.Gametype);
    }

    [Fact]
    public void Adapter_ImplementsIGameAdapter()
    {
        Assert.IsAssignableFrom<IGameAdapter>(_adapter);
    }

    [Fact]
    public void GetContainerEnv_ContainsAllExpectedKeys()
    {
        var env = _adapter.GetContainerEnv(null);

        Assert.Contains("WARFORK_GAMETYPE", env.Keys);
        Assert.Contains("WARFORK_START_MAP", env.Keys);
        Assert.Contains("WARFORK_MAPLIST", env.Keys);
        Assert.Contains("WARFORK_INSTAGIB", env.Keys);
        Assert.Contains("WARFORK_INSTAJUMP", env.Keys);
        Assert.Contains("WARFORK_INSTASHIELD", env.Keys);
        Assert.Contains("WARFORK_SCORELIMIT", env.Keys);
        Assert.Contains("WARFORK_TIMELIMIT", env.Keys);
        Assert.Contains("WARFORK_RCON_PASSWORD", env.Keys);
        Assert.Contains("WARFORK_PASSWORD", env.Keys);
    }

    [Fact]
    public void GetContainerEnv_MapsGametypeCorrectly()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ctf",
            StartMap = "wfctf1",
            MapList = ["wfctf1"]
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("ctf", env["WARFORK_GAMETYPE"]);
        Assert.Equal("wfctf1", env["WARFORK_START_MAP"]);
    }

    [Fact]
    public void GetContainerEnv_MapList_IsSpaceSeparated()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return", "pressure"]
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("return pressure", env["WARFORK_MAPLIST"]);
    }
}
