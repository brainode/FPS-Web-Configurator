// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Models;

namespace control_panel.Services;

public sealed class QuakeLiveGameAdapter : IGameAdapter
{
    public string GameKey => "quake-live";
    public string DisplayName => "Quake Live";
    public string ConfigurationPagePath => "/Configuration/QuakeLive";

    public GameSummary GetSummary(string? jsonSettings)
    {
        var s = QuakeLiveConfigurationSerializer.Deserialize(jsonSettings);

        var serverTypeLabel = s.ServerType switch
        {
            1 => "LAN",
            2 => "Internet",
            _ => "Offline"
        };

        return new GameSummary(
            ModeName: QuakeLiveModuleCatalog.GetFactoryLabel(s.Factory),
            ModeFlags: serverTypeLabel,
            StartMap: s.MapList.Count > 0 ? s.MapList[0].ToUpperInvariant() : "\u2014",
            MapCountLabel: $"{s.MapList.Count} map(s) selected",
            RotationPreview: s.MapList.Count == 0
                ? "No maps selected"
                : string.Join(", ", s.MapList.Take(6).Select(m => m.ToUpperInvariant())),
            LimitsSummary: $"Max {s.MaxClients} players",
            AccessLabel: string.IsNullOrWhiteSpace(s.ServerPassword) ? "Open lobby" : "Password protected",
            RconLabel: !s.ZmqRconEnabled
                ? "Disabled"
                : string.IsNullOrWhiteSpace(s.ZmqRconPassword) ? "Required" : "Configured"
        );
    }

    public IReadOnlyDictionary<string, string> GetContainerEnv(string? jsonSettings)
    {
        var s = QuakeLiveConfigurationSerializer.Deserialize(jsonSettings);
        return new Dictionary<string, string>
        {
            ["QL_HOSTNAME"] = s.Hostname,
            ["QL_FACTORY"] = s.Factory,
            ["QL_MAPLIST"] = string.Join(" ", s.MapList),
            ["QL_MAXCLIENTS"] = s.MaxClients.ToString(),
            ["QL_SERVER_TYPE"] = s.ServerType.ToString(),
            ["QL_ZMQ_RCON_ENABLE"] = s.ZmqRconEnabled ? "1" : "0",
            ["QL_ZMQ_RCON_PORT"] = s.ZmqRconPort.ToString(),
            ["QL_ZMQ_RCON_PASSWORD"] = s.ZmqRconPassword,
            ["QL_ZMQ_STATS_ENABLE"] = s.ZmqStatsEnabled ? "1" : "0",
            ["QL_ZMQ_STATS_PORT"] = s.ZmqStatsPort.ToString(),
            ["QL_ZMQ_STATS_PASSWORD"] = s.ZmqStatsPassword,
            ["QL_PASSWORD"] = s.ServerPassword,
            ["QL_TAGS"] = s.Tags,
        };
    }

    public string CreateDefaultJson() => QuakeLiveSeedConfiguration.CreateDefaultJson();
}
