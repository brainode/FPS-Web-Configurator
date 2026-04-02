// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Models;

namespace control_panel.Services;

public sealed class ReflexArenaGameAdapter : IGameAdapter
{
    public string GameKey => "reflex-arena";
    public string DisplayName => "Reflex Arena";
    public string ConfigurationPagePath => "/Configuration/ReflexArena";

    public GameSummary GetSummary(string? jsonSettings)
    {
        var settings = ReflexArenaConfigurationSerializer.Deserialize(jsonSettings);
        var selectedMutators = settings.Mutators
            .Select(mutator => ReflexArenaModuleCatalog.FindMutator(mutator)?.Label ?? mutator)
            .ToArray();
        var supportedMapsPreview = ReflexArenaModuleCatalog.GetSupportedMapsForMode(settings.Mode)
            .Take(6)
            .Select(ReflexArenaModuleCatalog.GetMapLabel)
            .ToArray();

        var limitsSummary = settings.TimeLimitOverride > 0
            ? $"Max {settings.MaxClients} players · {settings.TimeLimitOverride} min"
            : $"Max {settings.MaxClients} players";

        return new GameSummary(
            ModeName: ReflexArenaModuleCatalog.GetModeLabel(settings.Mode),
            ModeFlags: selectedMutators.Length == 0
                ? "Standard ruleset"
                : string.Join(", ", selectedMutators),
            StartMap: ReflexArenaModuleCatalog.GetMapLabel(settings.StartMap),
            MapCountLabel: "Single-map startup",
            RotationPreview: supportedMapsPreview.Length == 0
                ? "No stock map metadata available"
                : string.Join(", ", supportedMapsPreview),
            LimitsSummary: limitsSummary,
            AccessLabel: string.IsNullOrWhiteSpace(settings.ServerPassword) ? "Open lobby" : "Password protected",
            RconLabel: string.IsNullOrWhiteSpace(settings.RefPassword) ? "Optional" : "Configured");
    }

    public IReadOnlyDictionary<string, string> GetContainerEnv(string? jsonSettings)
    {
        var settings = ReflexArenaConfigurationSerializer.Deserialize(jsonSettings);
        var env = new Dictionary<string, string>
        {
            ["REFLEX_HOSTNAME"] = settings.Hostname,
            ["REFLEX_MODE"] = settings.Mode,
            ["REFLEX_START_MAP"] = settings.StartMap,
            ["REFLEX_START_MUTATORS"] = string.Join(" ", settings.Mutators),
            ["REFLEX_MAXCLIENTS"] = settings.MaxClients.ToString(),
            ["REFLEX_STEAM"] = settings.SteamEnabled ? "1" : "0",
            ["REFLEX_COUNTRY"] = settings.Country,
            ["REFLEX_TIMELIMIT_OVERRIDE"] = settings.TimeLimitOverride.ToString(),
            ["REFLEX_PASSWORD"] = settings.ServerPassword,
            ["REFLEX_REF_PASSWORD"] = settings.RefPassword,
        };

        var workshopStartMapId = ReflexArenaModuleCatalog.GetWorkshopMapId(settings.StartMap);
        if (!string.IsNullOrWhiteSpace(workshopStartMapId))
        {
            env["REFLEX_START_WORKSHOP_MAP"] = workshopStartMapId;
        }

        return env;
    }

    public string CreateDefaultJson() => ReflexArenaSeedConfiguration.CreateDefaultJson();
}
