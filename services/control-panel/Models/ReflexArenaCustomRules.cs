// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

namespace control_panel.Models;

public sealed class ReflexArenaCustomRules
{
    public bool Enabled { get; set; }
    public string RulesetName { get; set; } = "custom";
    public List<ReflexArenaWeaponOverride> Weapons { get; set; } = [];
    public List<ReflexArenaPickupOverride> Pickups { get; set; } = [];
    public int? Gravity { get; set; }
}

public sealed class ReflexArenaWeaponOverride
{
    public string Key { get; set; } = string.Empty;
    public bool WeaponEnabled { get; set; } = true;
    public int? DirectDamage { get; set; }
    public int? SplashDamage { get; set; }
    public bool InfiniteAmmo { get; set; }
    public int? MaxAmmo { get; set; }
}

public sealed class ReflexArenaPickupOverride
{
    public string Key { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
