// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Models;

namespace control_panel.Services;

public sealed class WarforkGameAdapter : IGameAdapter
{
    private const string CustomClanArenaGametype = "panelca";

    public string GameKey => "warfork";
    public string DisplayName => "Warfork";
    public string ConfigurationPagePath => "/Configuration/Warfork";

    public GameSummary GetSummary(string? jsonSettings)
    {
        var s = WarforkConfigurationSerializer.Deserialize(jsonSettings);

        var flags = new List<string>();
        if (s.Instagib) flags.Add("Instagib");
        if (s.Instajump) flags.Add("Instajump");
        if (s.Instashield) flags.Add("Instashield");
        if (s.CustomRules is { Enabled: true })
        {
            var hasAnyCustomRuleFlag = false;

            if (string.Equals(s.Gametype, "ca", StringComparison.OrdinalIgnoreCase) &&
                s.CustomRules.ClanArenaLoadoutEnabled &&
                s.CustomRules.ClanArenaLoadout.Count > 0)
            {
                flags.Add($"CA loadout ({s.CustomRules.ClanArenaLoadout.Count} weapons)");
                hasAnyCustomRuleFlag = true;
            }

            if (string.Equals(s.Gametype, "ca", StringComparison.OrdinalIgnoreCase) &&
                s.CustomRules.ClanArenaLoadoutEnabled &&
                s.CustomRules.ClanArenaLoadout.Any(rule => rule.DamageOverride is > 0 || rule.HealOnHit))
            {
                flags.Add("Weapon tuning");
                hasAnyCustomRuleFlag = true;
            }

            if (s.CustomRules.AllowedWeapons.Count > 0)
            {
                flags.Add($"Weapon arena ({s.CustomRules.AllowedWeapons.Count})");
                hasAnyCustomRuleFlag = true;
            }

            if (!hasAnyCustomRuleFlag)
            {
                flags.Add("Custom rules");
            }
        }

        return new GameSummary(
            ModeName: WarforkModuleCatalog.GetGametypeLabel(s.Gametype),
            ModeFlags: flags.Count > 0 ? string.Join(" \u2022 ", flags) : "Standard ruleset",
            StartMap: s.StartMap.ToUpperInvariant(),
            MapCountLabel: $"{s.MapList.Count} map(s) selected",
            RotationPreview: s.MapList.Count == 0
                ? "No maps selected"
                : string.Join(", ", s.MapList.Take(6).Select(m => m.ToUpperInvariant())),
            LimitsSummary: $"{s.Scorelimit} / {s.Timelimit}",
            AccessLabel: string.IsNullOrWhiteSpace(s.ServerPassword) ? "Open lobby" : "Password protected",
            RconLabel: string.IsNullOrWhiteSpace(s.RconPassword) ? "Optional" : "Configured"
        );
    }

    public IReadOnlyDictionary<string, string> GetContainerEnv(string? jsonSettings)
    {
        var s = WarforkConfigurationSerializer.Deserialize(jsonSettings);
        var runtimeGametype = UsesCustomClanArenaRuntime(s) ? CustomClanArenaGametype : s.Gametype;
        var env = new Dictionary<string, string>
        {
            ["WARFORK_GAMETYPE"] = runtimeGametype,
            ["WARFORK_BASE_GAMETYPE"] = s.Gametype,
            ["WARFORK_START_MAP"] = s.StartMap,
            ["WARFORK_MAPLIST"] = string.Join(" ", s.MapList),
            ["WARFORK_INSTAGIB"] = s.Instagib ? "1" : "0",
            ["WARFORK_INSTAJUMP"] = s.Instajump ? "1" : "0",
            ["WARFORK_INSTASHIELD"] = s.Instashield ? "1" : "0",
            ["WARFORK_SCORELIMIT"] = s.Scorelimit.ToString(),
            ["WARFORK_TIMELIMIT"] = s.Timelimit.ToString(),
            ["WARFORK_RCON_PASSWORD"] = s.RconPassword,
            ["WARFORK_PASSWORD"] = s.ServerPassword,
            ["WARFORK_CA_LOADOUT_ENABLED"] = "0",
            ["WARFORK_CA_LOADOUT_INVENTORY"] = string.Empty,
            ["WARFORK_CA_STRONG_AMMO"] = string.Empty,
            ["WARFORK_CA_INFINITE_WEAPONS"] = string.Empty,
            ["WARFORK_CA_DAMAGE_OVERRIDES"] = string.Empty,
            ["WARFORK_CA_HEALING_WEAPONS"] = string.Empty,
        };

        if (s.CustomRules is { Enabled: true } rules)
        {
            env["WARFORK_CUSTOM_RULES"] = "1";
            env["WARFORK_ALLOWED_WEAPONS"] = string.Join(" ", rules.AllowedWeapons);
            env["WARFORK_DISABLE_HEALTH"] = rules.DisableHealthItems ? "1" : "0";
            env["WARFORK_DISABLE_ARMOR"] = rules.DisableArmorItems ? "1" : "0";
            env["WARFORK_DISABLE_POWERUPS"] = rules.DisablePowerups ? "1" : "0";
            env["WARFORK_GRAVITY"] = rules.Gravity?.ToString() ?? "";

            if (string.Equals(s.Gametype, "ca", StringComparison.OrdinalIgnoreCase) &&
                rules.ClanArenaLoadoutEnabled &&
                rules.ClanArenaLoadout.Count > 0)
            {
                env["WARFORK_CA_LOADOUT_ENABLED"] = "1";
                env["WARFORK_CA_LOADOUT_INVENTORY"] = WarforkWeaponsCatalog.BuildClanArenaInventory(rules.ClanArenaLoadout);
                env["WARFORK_CA_STRONG_AMMO"] = WarforkWeaponsCatalog.BuildClanArenaStrongAmmoString(rules.ClanArenaLoadout);
                env["WARFORK_CA_DAMAGE_OVERRIDES"] = WarforkWeaponsCatalog.BuildDamageOverrideString(rules.ClanArenaLoadout);
                env["WARFORK_CA_HEALING_WEAPONS"] = WarforkWeaponsCatalog.BuildHealingWeaponsString(rules.ClanArenaLoadout);
                env["WARFORK_CA_INFINITE_WEAPONS"] = string.Join(
                    " ",
                    rules.ClanArenaLoadout
                        .Where(rule => rule.InfiniteAmmo)
                        .Select(rule => rule.WeaponKey)
                        .Distinct(StringComparer.OrdinalIgnoreCase));
            }
        }

        return env;
    }

    public string CreateDefaultJson() => WarforkSeedConfiguration.CreateDefaultJson();

    private static bool UsesCustomClanArenaRuntime(WarforkServerSettings settings) =>
        settings.CustomRules is { Enabled: true } &&
        string.Equals(settings.Gametype, "ca", StringComparison.OrdinalIgnoreCase);
}
