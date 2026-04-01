// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Models;

namespace control_panel.Services;

public sealed class WarsowGameAdapter : IGameAdapter
{
    public string GameKey => "warsow";
    public string DisplayName => "Warsow";
    public string ConfigurationPagePath => "/Configuration/Warsow";

    public GameSummary GetSummary(string? jsonSettings)
    {
        var s = WarsowConfigurationSerializer.Deserialize(jsonSettings);

        var flags = new List<string>();
        if (s.Instagib) flags.Add("Instagib");
        if (s.Instajump) flags.Add("Instajump");
        if (s.Instashield) flags.Add("Instashield");

        return new GameSummary(
            ModeName: WarsowModuleCatalog.GetGametypeLabel(s.Gametype),
            ModeFlags: flags.Count > 0 ? string.Join(" \u2022 ", flags) : "Standard ruleset",
            StartMap: s.StartMap.ToUpperInvariant(),
            MapCountLabel: $"{s.MapList.Count} map(s) selected",
            RotationPreview: s.MapList.Count == 0
                ? "No maps selected"
                : string.Join(", ", s.MapList.Take(6).Select(m => m.ToUpperInvariant())),
            LimitsSummary: $"{s.Scorelimit} / {s.Timelimit}",
            AccessLabel: string.IsNullOrWhiteSpace(s.ServerPassword) ? "Open lobby" : "Password protected",
            RconLabel: string.IsNullOrWhiteSpace(s.RconPassword) ? "Required" : "Configured"
        );
    }

    public IReadOnlyDictionary<string, string> GetContainerEnv(string? jsonSettings)
    {
        var s = WarsowConfigurationSerializer.Deserialize(jsonSettings);
        return new Dictionary<string, string>
        {
            ["WARSOW_GAMETYPE"] = s.Gametype,
            ["WARSOW_START_MAP"] = s.StartMap,
            ["WARSOW_MAPLIST"] = string.Join(" ", s.MapList),
            ["WARSOW_INSTAGIB"] = s.Instagib ? "1" : "0",
            ["WARSOW_INSTAJUMP"] = s.Instajump ? "1" : "0",
            ["WARSOW_INSTASHIELD"] = s.Instashield ? "1" : "0",
            ["WARSOW_SCORELIMIT"] = s.Scorelimit.ToString(),
            ["WARSOW_TIMELIMIT"] = s.Timelimit.ToString(),
            ["WARSOW_RCON_PASSWORD"] = s.RconPassword,
            ["WARSOW_PASSWORD"] = s.ServerPassword
        };
    }

    public string CreateDefaultJson() => WarsowSeedConfiguration.CreateDefaultJson();
}
