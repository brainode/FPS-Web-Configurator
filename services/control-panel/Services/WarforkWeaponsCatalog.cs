// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Models;

namespace control_panel.Services;

// Weapon arena in Warfork is driven by the g_weaponarena and g_weaponarena_items
// server cvars, which the stock gametype scripts read at match start.
// Gunblade (melee) is always available and therefore not listed here.
public static class WarforkWeaponsCatalog
{
    public const int PracticalInfiniteAmmoReserve = 9999;

    public static IReadOnlyList<WarforkWeaponEntry> Weapons { get; } =
    [
        new("machinegun",      "Machinegun",       "Rapid-fire hitscan starter weapon.",                 "mg", "bullets", 1,  75),
        new("riotgun",         "Riotgun",          "Short-range spread weapon.",                         "rg", "shells",  2,  20),
        new("grenadelauncher", "Grenade Launcher", "Bouncing arc projectile with area damage.",         "gl", "grens",   3,  20),
        new("rocketlauncher",  "Rocket Launcher",  "Direct hit and area splash — primary movement tool.","rl", "rockets", 4,  40),
        new("plasmagun",       "Plasmagun",        "Rapid-fire plasma bolts.",                           "pg", "plasma",  5, 125),
        new("lasergun",        "Lasergun",         "Continuous beam for precise tracking shots.",        "lg", "lasers",  6, 180),
        new("electrobolt",     "Electrobolt",      "Hitscan rail-style one-shot weapon.",               "eb", "bolts",   7,  15),
    ];

    public static IReadOnlyList<WarforkPickupEntry> Pickups { get; } =
    [
        new("health",   "Health items",  "g_pickup_health",   "Health packs and mega-health that spawn on the map."),
        new("armor",    "Armor items",   "g_pickup_armor",    "All armor shards, combat armor and body armor."),
        new("powerups", "Powerups",      "g_pickup_powerups", "Quad Damage, Warshell and similar powerup items."),
    ];

    public static bool IsValidWeapon(string? key) =>
        !string.IsNullOrWhiteSpace(key) &&
        Weapons.Any(w => string.Equals(w.Key, key, StringComparison.OrdinalIgnoreCase));

    public static WarforkWeaponEntry? FindWeapon(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : Weapons.FirstOrDefault(w => string.Equals(w.Key, key, StringComparison.OrdinalIgnoreCase));

    public static List<WarforkClanArenaWeaponLoadout> NormalizeClanArenaLoadout(
        IEnumerable<WarforkClanArenaWeaponLoadout>? loadout)
    {
        var map = (loadout ?? [])
            .Where(rule => IsValidWeapon(rule.WeaponKey))
            .GroupBy(rule => rule.WeaponKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        return Weapons
            .Where(weapon => map.ContainsKey(weapon.Key))
            .Select(weapon =>
            {
                var configured = map[weapon.Key];
                return new WarforkClanArenaWeaponLoadout
                {
                    WeaponKey = weapon.Key,
                    Ammo = configured.Ammo > 0 ? configured.Ammo : weapon.ClanArenaDefaultAmmo,
                    InfiniteAmmo = configured.InfiniteAmmo
                };
            })
            .ToList();
    }

    public static string BuildClanArenaInventory(IEnumerable<WarforkClanArenaWeaponLoadout>? loadout)
    {
        var configured = NormalizeClanArenaLoadout(loadout);
        var tokens = new List<string> { "gb", "cells" };

        foreach (var weapon in Weapons)
        {
            var rule = configured.FirstOrDefault(item => string.Equals(item.WeaponKey, weapon.Key, StringComparison.OrdinalIgnoreCase));
            if (rule is null)
            {
                continue;
            }

            tokens.Add(weapon.ClanArenaWeaponToken);
            tokens.Add(weapon.ClanArenaAmmoToken);
        }

        return string.Join(' ', tokens.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    public static string BuildClanArenaStrongAmmoString(IEnumerable<WarforkClanArenaWeaponLoadout>? loadout)
    {
        var configured = NormalizeClanArenaLoadout(loadout);
        var ammo = new[] { 1, 0, 0, 0, 0, 0, 0, 0 };

        foreach (var rule in configured)
        {
            var weapon = FindWeapon(rule.WeaponKey);
            if (weapon is null)
            {
                continue;
            }

            ammo[weapon.ClanArenaAmmoSlot] = rule.InfiniteAmmo
                ? PracticalInfiniteAmmoReserve
                : Math.Clamp(rule.Ammo, 1, PracticalInfiniteAmmoReserve);
        }

        return string.Join(' ', ammo);
    }
}

public sealed record WarforkWeaponEntry(
    string Key,
    string Label,
    string Description,
    string ClanArenaWeaponToken,
    string ClanArenaAmmoToken,
    int ClanArenaAmmoSlot,
    int ClanArenaDefaultAmmo);
public sealed record WarforkPickupEntry(string Key, string Label, string CVar, string Description);
