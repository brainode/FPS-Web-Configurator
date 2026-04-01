using System.Text.Json;
using control_panel.Models;
using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class WarsowConfigurationSerializerTests
{
    [Fact]
    public void Deserialize_ReadsLegacyStringBasedWarsowConfiguration()
    {
        const string json = """
        {
          "sv_defaultmap": "wdm4",
          "g_maplist": "wdm4 wdm7 wdm9",
          "g_gametype": "dm",
          "g_instagib": "1",
          "g_instajump": "0",
          "g_instashield": "1",
          "g_scorelimit": "50",
          "g_timelimit": "15",
          "rcon_password": "secret",
          "password": "joinme"
        }
        """;

        var settings = WarsowConfigurationSerializer.Deserialize(json);

        Assert.Equal("wdm4", settings.StartMap);
        Assert.Equal("dm", settings.Gametype);
        Assert.Equal(["wdm4", "wdm7", "wdm9"], settings.MapList);
        Assert.True(settings.Instagib);
        Assert.False(settings.Instajump);
        Assert.True(settings.Instashield);
        Assert.Equal(50, settings.Scorelimit);
        Assert.Equal(15, settings.Timelimit);
        Assert.Equal("secret", settings.RconPassword);
        Assert.Equal("joinme", settings.ServerPassword);
    }

    [Fact]
    public void Deserialize_UsesGametypeRecommendations_WhenMapListIsEmpty()
    {
        const string json = """
        {
          "sv_defaultmap": "wdm1",
          "g_maplist": "",
          "g_gametype": "dm"
        }
        """;

        var settings = WarsowConfigurationSerializer.Deserialize(json);

        Assert.Contains("wdm1", settings.MapList);
        Assert.Contains("wdm2", settings.MapList);
    }

    [Fact]
    public void Serialize_WritesWarsowValues_AsExpectedByCurrentConfigStore()
    {
        var settings = new WarsowServerSettings
        {
            Gametype = "ca",
            StartMap = "wca1",
            MapList = ["wca1"],
            Instagib = true,
            Instajump = false,
            Instashield = true,
            Scorelimit = 20,
            Timelimit = 12,
            RconPassword = "admin",
            ServerPassword = "friends"
        };

        var json = WarsowConfigurationSerializer.Serialize(settings);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("wca1", root.GetProperty("sv_defaultmap").GetString());
        Assert.Equal("wca1", root.GetProperty("g_maplist").GetString());
        Assert.Equal("1", root.GetProperty("g_instagib").GetString());
        Assert.Equal("0", root.GetProperty("g_instajump").GetString());
        Assert.Equal("1", root.GetProperty("g_instashield").GetString());
        Assert.Equal("20", root.GetProperty("g_scorelimit").GetString());
        Assert.Equal("12", root.GetProperty("g_timelimit").GetString());
    }

    [Fact]
    public void Serialize_ResolvesStartMap_ToFirstPoolMap_WhenStartMapIsOutsidePool()
    {
        var settings = new WarsowServerSettings
        {
            Gametype = "dm",
            StartMap = "wctf1",
            MapList = ["wdm4", "wdm7", "wdm9"]
        };

        var json = WarsowConfigurationSerializer.Serialize(settings);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("wdm4", root.GetProperty("sv_defaultmap").GetString());
        Assert.Equal("wdm4 wdm7 wdm9", root.GetProperty("g_maplist").GetString());
    }

    [Fact]
    public void Serialize_AndDeserialize_RoundTripsPasswords()
    {
        var settings = new WarsowServerSettings
        {
            Gametype = "ca",
            StartMap = "wca1",
            MapList = ["wca1", "wca2"],
            RconPassword = "rcon-secret",
            ServerPassword = "join-secret"
        };

        var json = WarsowConfigurationSerializer.Serialize(settings);
        var restored = WarsowConfigurationSerializer.Deserialize(json);

        Assert.Equal("rcon-secret", restored.RconPassword);
        Assert.Equal("join-secret", restored.ServerPassword);
    }
}
