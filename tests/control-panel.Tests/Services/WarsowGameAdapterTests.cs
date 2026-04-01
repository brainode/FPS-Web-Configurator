using control_panel.Models;
using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class WarsowGameAdapterTests
{
    private readonly WarsowGameAdapter _adapter = new();

    [Fact]
    public void GameKey_IsWarsow()
    {
        Assert.Equal("warsow", _adapter.GameKey);
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
        var json = WarsowConfigurationSerializer.Serialize(new WarsowServerSettings
        {
            Gametype = "ca",
            StartMap = "wca1",
            MapList = ["wca1"],
            Instagib = true,
            Instajump = true,
            RconPassword = "rcon"
        });

        var summary = _adapter.GetSummary(json);

        Assert.Contains("Instagib", summary.ModeFlags);
        Assert.Contains("Instajump", summary.ModeFlags);
    }

    [Fact]
    public void GetSummary_NoFlags_ReturnsStandardRuleset()
    {
        var json = WarsowConfigurationSerializer.Serialize(new WarsowServerSettings
        {
            Gametype = "ca",
            StartMap = "wca1",
            MapList = ["wca1"],
            RconPassword = "rcon"
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("Standard ruleset", summary.ModeFlags);
    }

    [Fact]
    public void GetSummary_WithServerPassword_ShowsPasswordProtected()
    {
        var json = WarsowConfigurationSerializer.Serialize(new WarsowServerSettings
        {
            Gametype = "ca",
            StartMap = "wca1",
            MapList = ["wca1"],
            RconPassword = "rcon",
            ServerPassword = "secret"
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("Password protected", summary.AccessLabel);
    }

    [Fact]
    public void GetSummary_NoServerPassword_ShowsOpenLobby()
    {
        var json = WarsowConfigurationSerializer.Serialize(new WarsowServerSettings
        {
            Gametype = "ca",
            StartMap = "wca1",
            MapList = ["wca1"],
            RconPassword = "rcon"
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("Open lobby", summary.AccessLabel);
    }

    [Fact]
    public void GetSummary_RconConfigured_ShowsConfigured()
    {
        var json = WarsowConfigurationSerializer.Serialize(new WarsowServerSettings
        {
            Gametype = "ca",
            StartMap = "wca1",
            MapList = ["wca1"],
            RconPassword = "secret"
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("Configured", summary.RconLabel);
    }

    [Fact]
    public void GetSummary_RconEmpty_ShowsRequired()
    {
        var json = WarsowConfigurationSerializer.Serialize(new WarsowServerSettings
        {
            Gametype = "ca",
            StartMap = "wca1",
            MapList = ["wca1"],
            RconPassword = string.Empty
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("Required", summary.RconLabel);
    }

    [Fact]
    public void GetSummary_StartMap_IsUpperCase()
    {
        var json = WarsowConfigurationSerializer.Serialize(new WarsowServerSettings
        {
            StartMap = "wca1",
            MapList = ["wca1"],
            RconPassword = "rcon"
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("WCA1", summary.StartMap);
    }

    [Fact]
    public void GetSummary_MapCountLabel_IncludesCount()
    {
        var json = WarsowConfigurationSerializer.Serialize(new WarsowServerSettings
        {
            Gametype = "ca",
            StartMap = "wca1",
            MapList = ["wca1", "wdm1", "wdm2"],
            RconPassword = "rcon"
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("3 map(s) selected", summary.MapCountLabel);
    }

    [Fact]
    public void CreateDefaultJson_ProducesDeserializableSettings()
    {
        var json = _adapter.CreateDefaultJson();
        var settings = WarsowConfigurationSerializer.Deserialize(json);

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

        Assert.Contains("WARSOW_GAMETYPE", env.Keys);
        Assert.Contains("WARSOW_START_MAP", env.Keys);
        Assert.Contains("WARSOW_MAPLIST", env.Keys);
        Assert.Contains("WARSOW_INSTAGIB", env.Keys);
        Assert.Contains("WARSOW_INSTAJUMP", env.Keys);
        Assert.Contains("WARSOW_INSTASHIELD", env.Keys);
        Assert.Contains("WARSOW_SCORELIMIT", env.Keys);
        Assert.Contains("WARSOW_TIMELIMIT", env.Keys);
        Assert.Contains("WARSOW_RCON_PASSWORD", env.Keys);
        Assert.Contains("WARSOW_PASSWORD", env.Keys);
    }

    [Fact]
    public void GetContainerEnv_MapsGametypeCorrectly()
    {
        var json = WarsowConfigurationSerializer.Serialize(new WarsowServerSettings
        {
            Gametype = "ctf",
            StartMap = "wctf1",
            MapList = ["wctf1"],
            RconPassword = "rcon"
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("ctf", env["WARSOW_GAMETYPE"]);
        Assert.Equal("WctF1".ToLowerInvariant(), env["WARSOW_START_MAP"].ToLowerInvariant());
    }

    [Fact]
    public void GetContainerEnv_MapList_IsSpaceSeparated()
    {
        var json = WarsowConfigurationSerializer.Serialize(new WarsowServerSettings
        {
            Gametype = "ca",
            StartMap = "wca1",
            MapList = ["wca1", "wdm1", "wdm2"],
            RconPassword = "rcon"
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("wca1 wdm1 wdm2", env["WARSOW_MAPLIST"]);
    }

    [Fact]
    public void GetContainerEnv_InstagibTrue_SetsEnvVarToOne()
    {
        var json = WarsowConfigurationSerializer.Serialize(new WarsowServerSettings
        {
            Gametype = "ca",
            StartMap = "wca1",
            MapList = ["wca1"],
            Instagib = true,
            Instajump = false,
            RconPassword = "rcon"
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("1", env["WARSOW_INSTAGIB"]);
        Assert.Equal("0", env["WARSOW_INSTAJUMP"]);
    }

    [Fact]
    public void GetContainerEnv_Scorelimit_IsPropagated()
    {
        var json = WarsowConfigurationSerializer.Serialize(new WarsowServerSettings
        {
            Gametype = "ca",
            StartMap = "wca1",
            MapList = ["wca1"],
            Scorelimit = 15,
            Timelimit = 20,
            RconPassword = "rcon"
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("15", env["WARSOW_SCORELIMIT"]);
        Assert.Equal("20", env["WARSOW_TIMELIMIT"]);
    }

    [Fact]
    public void GetContainerEnv_RconPassword_IsPropagated()
    {
        var json = WarsowConfigurationSerializer.Serialize(new WarsowServerSettings
        {
            Gametype = "ca",
            StartMap = "wca1",
            MapList = ["wca1"],
            RconPassword = "my-rcon-pw",
            ServerPassword = "join-pw"
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("my-rcon-pw", env["WARSOW_RCON_PASSWORD"]);
        Assert.Equal("join-pw", env["WARSOW_PASSWORD"]);
    }

    [Fact]
    public void GetContainerEnv_NullJson_ReturnsNonEmptyDictionary()
    {
        var env = _adapter.GetContainerEnv(null);

        Assert.NotEmpty(env);
    }
}
