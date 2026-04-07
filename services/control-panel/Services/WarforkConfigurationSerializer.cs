// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using System.Text.Json;
using System.Text.Json.Serialization;
using control_panel.Models;

namespace control_panel.Services;

public static class WarforkConfigurationSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static WarforkServerSettings Deserialize(string? json)
    {
        var settings = new WarforkServerSettings();

        if (string.IsNullOrWhiteSpace(json))
        {
            settings.MapList = WarforkModuleCatalog.NormalizeMapSelection(settings.MapList, settings.Gametype);
            settings.StartMap = WarforkModuleCatalog.ResolveStartMap(settings.StartMap, settings.MapList, settings.Gametype);
            return settings;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            settings.StartMap = GameConfigJsonReader.ReadString(root, "sv_defaultmap", settings.StartMap);
            settings.Gametype = GameConfigJsonReader.ReadString(root, "g_gametype", settings.Gametype);
            settings.MapList = WarforkModuleCatalog.NormalizeMapSelection(
                GameConfigJsonReader.ReadStringList(root, "g_maplist"), settings.Gametype);
            settings.Instagib = GameConfigJsonReader.ReadBoolean(root, "g_instagib");
            settings.Instajump = GameConfigJsonReader.ReadBoolean(root, "g_instajump");
            settings.Instashield = GameConfigJsonReader.ReadBoolean(root, "g_instashield");
            settings.Scorelimit = GameConfigJsonReader.ReadInt(root, "g_scorelimit", settings.Scorelimit);
            settings.Timelimit = GameConfigJsonReader.ReadInt(root, "g_timelimit", settings.Timelimit);
            settings.RconPassword = GameConfigJsonReader.ReadString(root, "rcon_password", string.Empty);
            settings.ServerPassword = GameConfigJsonReader.ReadString(root, "password", string.Empty);

            if (root.TryGetProperty("custom_rules", out var rulesEl) &&
                rulesEl.ValueKind == JsonValueKind.Object)
            {
                var doc = JsonSerializer.Deserialize<CustomRulesDoc>(rulesEl.GetRawText(), SerializerOptions);
                if (doc is not null)
                {
                    settings.CustomRules = new WarforkCustomRules
                    {
                        Enabled = doc.Enabled,
                        AllowedWeapons = doc.AllowedWeapons
                            .Where(WarforkWeaponsCatalog.IsValidWeapon)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        ClanArenaLoadoutEnabled = doc.ClanArenaLoadoutEnabled,
                        ClanArenaLoadout = WarforkWeaponsCatalog.NormalizeClanArenaLoadout(
                            doc.ClanArenaLoadout?.Select(rule => new WarforkClanArenaWeaponLoadout
                            {
                                WeaponKey = rule.WeaponKey,
                                Ammo = rule.Ammo,
                                InfiniteAmmo = rule.InfiniteAmmo,
                                DamageOverride = rule.DamageOverride is > 0 ? rule.DamageOverride : null,
                                SplashDamageOverride = rule.SplashDamageOverride is > 0 ? rule.SplashDamageOverride : null,
                                FireCooldownMs = rule.FireCooldownMs is > 0 ? rule.FireCooldownMs : null,
                                HealOnHit = rule.HealOnHit
                            })),
                        DisableHealthItems = doc.DisableHealth,
                        DisableArmorItems = doc.DisableArmor,
                        DisablePowerups = doc.DisablePowerups,
                        Gravity = doc.Gravity is > 0 ? doc.Gravity : null,
                    };
                }
            }
        }
        catch (JsonException)
        {
            settings = new WarforkServerSettings();
        }

        settings.MapList = WarforkModuleCatalog.NormalizeMapSelection(settings.MapList, settings.Gametype);
        settings.StartMap = WarforkModuleCatalog.ResolveStartMap(settings.StartMap, settings.MapList, settings.Gametype);
        return settings;
    }

    public static string Serialize(WarforkServerSettings settings)
    {
        var normalizedMaps = WarforkModuleCatalog.NormalizeMapSelection(settings.MapList, settings.Gametype);
        var resolvedStartMap = WarforkModuleCatalog.ResolveStartMap(settings.StartMap, normalizedMaps, settings.Gametype);

        CustomRulesDoc? rulesDoc = null;
        if (settings.CustomRules is { } rules)
        {
            rulesDoc = new CustomRulesDoc
            {
                Enabled = rules.Enabled,
                AllowedWeapons = rules.AllowedWeapons
                    .Where(WarforkWeaponsCatalog.IsValidWeapon)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                ClanArenaLoadoutEnabled = rules.ClanArenaLoadoutEnabled,
                ClanArenaLoadout = WarforkWeaponsCatalog.NormalizeClanArenaLoadout(rules.ClanArenaLoadout)
                    .Select(rule => new ClanArenaWeaponLoadoutDoc
                    {
                        WeaponKey = rule.WeaponKey,
                        Ammo = rule.Ammo,
                        InfiniteAmmo = rule.InfiniteAmmo,
                        DamageOverride = rule.DamageOverride,
                        SplashDamageOverride = rule.SplashDamageOverride,
                        FireCooldownMs = rule.FireCooldownMs,
                        HealOnHit = rule.HealOnHit
                    })
                    .ToList(),
                DisableHealth = rules.DisableHealthItems,
                DisableArmor = rules.DisableArmorItems,
                DisablePowerups = rules.DisablePowerups,
                Gravity = rules.Gravity,
            };
        }

        var doc = new SettingsDoc
        {
            SvDefaultmap = resolvedStartMap,
            GMaplist = string.Join(' ', normalizedMaps),
            GGametype = settings.Gametype,
            GInstagib = settings.Instagib ? "1" : "0",
            GInstajump = settings.Instajump ? "1" : "0",
            GInstashield = settings.Instashield ? "1" : "0",
            GScorelimit = settings.Scorelimit.ToString(),
            GTimelimit = settings.Timelimit.ToString(),
            RconPassword = settings.RconPassword ?? string.Empty,
            Password = settings.ServerPassword ?? string.Empty,
            CustomRules = rulesDoc,
        };

        return JsonSerializer.Serialize(doc, SerializerOptions);
    }

    // ── Private DTOs ──────────────────────────────────────────────────────────

    private sealed class SettingsDoc
    {
        [JsonPropertyName("sv_defaultmap")] public string SvDefaultmap { get; set; } = "";
        [JsonPropertyName("g_maplist")] public string GMaplist { get; set; } = "";
        [JsonPropertyName("g_gametype")] public string GGametype { get; set; } = "";
        [JsonPropertyName("g_instagib")] public string GInstagib { get; set; } = "0";
        [JsonPropertyName("g_instajump")] public string GInstajump { get; set; } = "0";
        [JsonPropertyName("g_instashield")] public string GInstashield { get; set; } = "0";
        [JsonPropertyName("g_scorelimit")] public string GScorelimit { get; set; } = "0";
        [JsonPropertyName("g_timelimit")] public string GTimelimit { get; set; } = "0";
        [JsonPropertyName("rcon_password")] public string RconPassword { get; set; } = "";
        [JsonPropertyName("password")] public string Password { get; set; } = "";
        [JsonPropertyName("custom_rules")] public CustomRulesDoc? CustomRules { get; set; }
    }

    private sealed class CustomRulesDoc
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; }
        [JsonPropertyName("allowed_weapons")] public List<string> AllowedWeapons { get; set; } = [];
        [JsonPropertyName("clan_arena_loadout_enabled")] public bool ClanArenaLoadoutEnabled { get; set; }
        [JsonPropertyName("clan_arena_loadout")] public List<ClanArenaWeaponLoadoutDoc> ClanArenaLoadout { get; set; } = [];
        [JsonPropertyName("disable_health")] public bool DisableHealth { get; set; }
        [JsonPropertyName("disable_armor")] public bool DisableArmor { get; set; }
        [JsonPropertyName("disable_powerups")] public bool DisablePowerups { get; set; }
        [JsonPropertyName("gravity")] public int? Gravity { get; set; }
    }

    private sealed class ClanArenaWeaponLoadoutDoc
    {
        [JsonPropertyName("weapon_key")] public string WeaponKey { get; set; } = "";
        [JsonPropertyName("ammo")] public int Ammo { get; set; }
        [JsonPropertyName("infinite_ammo")] public bool InfiniteAmmo { get; set; }
        [JsonPropertyName("damage_override")] public int? DamageOverride { get; set; }
        [JsonPropertyName("splash_damage_override")] public int? SplashDamageOverride { get; set; }
        [JsonPropertyName("fire_cooldown_ms")] public int? FireCooldownMs { get; set; }
        [JsonPropertyName("heal_on_hit")] public bool HealOnHit { get; set; }
    }
}
