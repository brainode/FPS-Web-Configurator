// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

namespace control_panel.Services;

// Weapon arena in Warfork is driven by the g_weaponarena and g_weaponarena_items
// server cvars, which the stock gametype scripts read at match start.
// Gunblade (melee) is always available and therefore not listed here.
public static class WarforkWeaponsCatalog
{
    public static IReadOnlyList<WarforkWeaponEntry> Weapons { get; } =
    [
        new("machinegun",      "Machinegun",       "Rapid-fire hitscan starter weapon."),
        new("riotgun",         "Riotgun",           "Short-range spread weapon."),
        new("grenadelauncher", "Grenade Launcher",  "Bouncing arc projectile with area damage."),
        new("rocketlauncher",  "Rocket Launcher",   "Direct hit and area splash — primary movement tool."),
        new("plasmagun",       "Plasmagun",         "Rapid-fire plasma bolts."),
        new("lasergun",        "Lasergun",          "Continuous beam for precise tracking shots."),
        new("electrobolt",     "Electrobolt",       "Hitscan rail-style one-shot weapon."),
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
}

public sealed record WarforkWeaponEntry(string Key, string Label, string Description);
public sealed record WarforkPickupEntry(string Key, string Label, string CVar, string Description);
