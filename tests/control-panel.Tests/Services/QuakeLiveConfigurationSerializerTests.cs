using control_panel.Models;
using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class QuakeLiveConfigurationSerializerTests
{
    [Fact]
    public void Deserialize_Null_ReturnsDefaultSettings()
    {
        var settings = QuakeLiveConfigurationSerializer.Deserialize(null);

        Assert.Equal("duel", settings.Factory);
        Assert.NotEmpty(settings.MapList);
        Assert.Equal(16, settings.MaxClients);
        Assert.Equal(2, settings.ServerType);
        Assert.False(settings.ZmqRconEnabled);
        Assert.Equal(28960, settings.ZmqRconPort);
    }

    [Fact]
    public void Deserialize_EmptyString_ReturnsDefaultSettings()
    {
        var settings = QuakeLiveConfigurationSerializer.Deserialize(string.Empty);

        Assert.Equal("duel", settings.Factory);
    }

    [Fact]
    public void Serialize_Deserialize_Roundtrip()
    {
        var original = new QuakeLiveServerSettings
        {
            Hostname = "Test Server",
            Factory = "ffa",
            MapList = ["campgrounds", "almostlost"],
            MaxClients = 12,
            ServerType = 1,
            ZmqRconEnabled = true,
            ZmqRconPort = 28961,
            ZmqRconPassword = "secret123",
            ZmqStatsEnabled = true,
            ZmqStatsPort = 27961,
            ZmqStatsPassword = "stats456",
            ServerPassword = "joinme",
            Tags = "ffa,eu",
        };

        var json = QuakeLiveConfigurationSerializer.Serialize(original);
        var restored = QuakeLiveConfigurationSerializer.Deserialize(json);

        Assert.Equal(original.Hostname, restored.Hostname);
        Assert.Equal(original.Factory, restored.Factory);
        Assert.Equal(original.MapList, restored.MapList);
        Assert.Equal(original.MaxClients, restored.MaxClients);
        Assert.Equal(original.ServerType, restored.ServerType);
        Assert.Equal(original.ZmqRconEnabled, restored.ZmqRconEnabled);
        Assert.Equal(original.ZmqRconPort, restored.ZmqRconPort);
        Assert.Equal(original.ZmqRconPassword, restored.ZmqRconPassword);
        Assert.Equal(original.ZmqStatsEnabled, restored.ZmqStatsEnabled);
        Assert.Equal(original.ZmqStatsPort, restored.ZmqStatsPort);
        Assert.Equal(original.ZmqStatsPassword, restored.ZmqStatsPassword);
        Assert.Equal(original.ServerPassword, restored.ServerPassword);
        Assert.Equal(original.Tags, restored.Tags);
    }

    [Fact]
    public void Serialize_ProducesValidJson()
    {
        var settings = new QuakeLiveServerSettings();
        var json = QuakeLiveConfigurationSerializer.Serialize(settings);

        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.Contains("g_factory", json);
        Assert.Contains("g_maplist", json);
        Assert.Contains("zmq_rcon_enable", json);
    }

    [Fact]
    public void Deserialize_InvalidJson_ReturnsDefaultSettings()
    {
        var settings = QuakeLiveConfigurationSerializer.Deserialize("{not valid json}");

        Assert.Equal("duel", settings.Factory);
    }

    [Fact]
    public void Deserialize_SpaceSeparatedMapList_ParsesCorrectly()
    {
        var json = """{"g_factory":"tdm","g_maplist":"wargrounds theedge warehouse"}""";

        var settings = QuakeLiveConfigurationSerializer.Deserialize(json);

        Assert.Contains("wargrounds", settings.MapList);
        Assert.Contains("theedge", settings.MapList);
        Assert.Contains("warehouse", settings.MapList);
    }

    [Fact]
    public void Deserialize_FiltersInvalidMaps()
    {
        var json = """{"g_factory":"ffa","g_maplist":"campgrounds not_a_map almostlost"}""";

        var settings = QuakeLiveConfigurationSerializer.Deserialize(json);

        Assert.Contains("campgrounds", settings.MapList);
        Assert.Contains("almostlost", settings.MapList);
        Assert.DoesNotContain("not_a_map", settings.MapList);
    }

    [Fact]
    public void GetContainerEnv_ContainsAllRequiredKeys()
    {
        var adapter = new QuakeLiveGameAdapter();
        var env = adapter.GetContainerEnv(null);

        Assert.True(env.ContainsKey("QL_FACTORY"));
        Assert.True(env.ContainsKey("QL_MAPLIST"));
        Assert.True(env.ContainsKey("QL_MAXCLIENTS"));
        Assert.True(env.ContainsKey("QL_ZMQ_RCON_ENABLE"));
        Assert.True(env.ContainsKey("QL_ZMQ_RCON_PASSWORD"));
        Assert.True(env.ContainsKey("QL_ZMQ_STATS_ENABLE"));
        Assert.True(env.ContainsKey("QL_PASSWORD"));
    }
}
