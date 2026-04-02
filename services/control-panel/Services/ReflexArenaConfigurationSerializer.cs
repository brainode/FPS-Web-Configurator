// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using System.Text.Json;
using System.Text.Json.Nodes;
using control_panel.Models;

namespace control_panel.Services;

public static class ReflexArenaConfigurationSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static ReflexArenaServerSettings Deserialize(string? json)
    {
        var settings = new ReflexArenaServerSettings();

        if (string.IsNullOrWhiteSpace(json))
        {
            settings.StartMap = ReflexArenaModuleCatalog.ResolveStartMap(settings.StartMap, settings.Mode);
            settings.Mutators = ReflexArenaModuleCatalog.NormalizeMutatorSelection(settings.Mutators);
            return settings;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var workshopStartMapId = GameConfigJsonReader.ReadString(root, "sv_startwmap", string.Empty);
            var mappedWorkshopStartMap = ReflexArenaModuleCatalog.FindMapByWorkshopId(workshopStartMapId)?.Key;

            settings.Hostname = GameConfigJsonReader.ReadString(root, "sv_hostname", settings.Hostname);
            settings.Mode = GameConfigJsonReader.ReadString(root, "sv_startmode", settings.Mode);
            settings.StartMap = mappedWorkshopStartMap
                ?? GameConfigJsonReader.ReadString(root, "sv_startmap", settings.StartMap);
            settings.Mutators = ReflexArenaModuleCatalog.NormalizeMutatorSelection(
                GameConfigJsonReader.ReadStringList(root, "sv_startmutators"));
            settings.MaxClients = GameConfigJsonReader.ReadInt(root, "sv_maxclients", settings.MaxClients);
            settings.SteamEnabled = GameConfigJsonReader.ReadBoolean(root, "sv_steam", settings.SteamEnabled);
            settings.Country = GameConfigJsonReader.ReadString(root, "sv_country", string.Empty);
            settings.TimeLimitOverride = GameConfigJsonReader.ReadInt(root, "sv_timelimit_override", settings.TimeLimitOverride);
            settings.ServerPassword = GameConfigJsonReader.ReadString(root, "sv_password", string.Empty);
            settings.RefPassword = GameConfigJsonReader.ReadString(root, "sv_refpassword", string.Empty);

            if (root.TryGetProperty("custom_rules", out var rulesEl))
                settings.CustomRules = DeserializeCustomRules(rulesEl);
        }
        catch (JsonException)
        {
            settings = new ReflexArenaServerSettings();
        }

        settings.Mutators = ReflexArenaModuleCatalog.NormalizeMutatorSelection(settings.Mutators);
        settings.StartMap = ReflexArenaModuleCatalog.ResolveStartMap(settings.StartMap, settings.Mode);
        settings.Country = settings.Country.Trim().ToUpperInvariant();
        return settings;
    }

    public static string Serialize(ReflexArenaServerSettings settings)
    {
        var normalizedMutators = ReflexArenaModuleCatalog.NormalizeMutatorSelection(settings.Mutators);
        var resolvedStartMap = ReflexArenaModuleCatalog.ResolveStartMap(settings.StartMap, settings.Mode);
        var workshopStartMapId = ReflexArenaModuleCatalog.GetWorkshopMapId(resolvedStartMap);

        var obj = new JsonObject
        {
            ["sv_hostname"] = settings.Hostname,
            ["sv_startmode"] = settings.Mode,
            ["sv_startmap"] = workshopStartMapId is null ? resolvedStartMap : string.Empty,
            ["sv_startwmap"] = workshopStartMapId ?? string.Empty,
            ["sv_startmutators"] = string.Join(' ', normalizedMutators),
            ["sv_maxclients"] = settings.MaxClients.ToString(),
            ["sv_steam"] = settings.SteamEnabled ? "1" : "0",
            ["sv_country"] = settings.Country.Trim().ToUpperInvariant(),
            ["sv_timelimit_override"] = settings.TimeLimitOverride.ToString(),
            ["sv_password"] = settings.ServerPassword ?? string.Empty,
            ["sv_refpassword"] = settings.RefPassword ?? string.Empty,
        };

        if (settings.CustomRules is { } rules)
            obj["custom_rules"] = SerializeCustomRules(rules);

        return obj.ToJsonString(SerializerOptions);
    }

    private static JsonObject SerializeCustomRules(ReflexArenaCustomRules rules)
    {
        var weaponsArray = new JsonArray();
        foreach (var w in rules.Weapons)
        {
            var wNode = new JsonObject
            {
                ["key"] = w.Key,
                ["weapon_enabled"] = w.WeaponEnabled,
                ["infinite_ammo"] = w.InfiniteAmmo,
            };
            if (w.DirectDamage.HasValue) wNode["direct_damage"] = w.DirectDamage.Value;
            if (w.SplashDamage.HasValue) wNode["splash_damage"] = w.SplashDamage.Value;
            if (w.MaxAmmo.HasValue) wNode["max_ammo"] = w.MaxAmmo.Value;
            weaponsArray.Add(wNode);
        }

        var pickupsArray = new JsonArray();
        foreach (var p in rules.Pickups)
        {
            pickupsArray.Add(new JsonObject
            {
                ["key"] = p.Key,
                ["enabled"] = p.Enabled,
            });
        }

        var node = new JsonObject
        {
            ["enabled"] = rules.Enabled,
            ["ruleset_name"] = rules.RulesetName,
            ["weapons"] = weaponsArray,
            ["pickups"] = pickupsArray,
        };
        if (rules.Gravity.HasValue) node["gravity"] = rules.Gravity.Value;
        return node;
    }

    private static ReflexArenaCustomRules DeserializeCustomRules(JsonElement el)
    {
        var rules = new ReflexArenaCustomRules
        {
            Enabled = el.TryGetProperty("enabled", out var en) && en.GetBoolean(),
            RulesetName = el.TryGetProperty("ruleset_name", out var rn)
                ? rn.GetString() ?? "custom" : "custom",
        };

        if (el.TryGetProperty("gravity", out var grav) && grav.TryGetInt32(out var gravVal))
            rules.Gravity = gravVal;

        if (el.TryGetProperty("weapons", out var weapons) &&
            weapons.ValueKind == JsonValueKind.Array)
        {
            foreach (var w in weapons.EnumerateArray())
            {
                var key = w.TryGetProperty("key", out var k) ? k.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(key)) continue;

                var wo = new ReflexArenaWeaponOverride
                {
                    Key = key,
                    WeaponEnabled = !w.TryGetProperty("weapon_enabled", out var we) || we.GetBoolean(),
                    InfiniteAmmo = w.TryGetProperty("infinite_ammo", out var ia) && ia.GetBoolean(),
                };
                if (w.TryGetProperty("direct_damage", out var dd) && dd.TryGetInt32(out var ddVal))
                    wo.DirectDamage = ddVal;
                if (w.TryGetProperty("splash_damage", out var sd) && sd.TryGetInt32(out var sdVal))
                    wo.SplashDamage = sdVal;
                if (w.TryGetProperty("max_ammo", out var ma) && ma.TryGetInt32(out var maVal))
                    wo.MaxAmmo = maVal;

                rules.Weapons.Add(wo);
            }
        }

        if (el.TryGetProperty("pickups", out var pickups) &&
            pickups.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in pickups.EnumerateArray())
            {
                var key = p.TryGetProperty("key", out var k) ? k.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(key)) continue;

                rules.Pickups.Add(new ReflexArenaPickupOverride
                {
                    Key = key,
                    Enabled = !p.TryGetProperty("enabled", out var en2) || en2.GetBoolean(),
                });
            }
        }

        return rules;
    }
}
