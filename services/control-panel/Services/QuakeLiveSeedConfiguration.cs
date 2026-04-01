using control_panel.Models;

namespace control_panel.Services;

public static class QuakeLiveSeedConfiguration
{
    public static string CreateDefaultJson() => QuakeLiveConfigurationSerializer.Serialize(
        new QuakeLiveServerSettings
        {
            Hostname = "Quake Live Standalone Test",
            Factory = "duel",
            MapList = QuakeLiveModuleCatalog.GetRecommendedMaps("duel").ToList(),
            MaxClients = 16,
            ServerType = 2,
            ZmqRconEnabled = false,
            ZmqRconPort = 28960,
            ZmqRconPassword = string.Empty,
            ZmqStatsEnabled = false,
            ZmqStatsPort = 27960,
            ZmqStatsPassword = string.Empty,
            ServerPassword = string.Empty,
            Tags = "standalone",
        });
}
