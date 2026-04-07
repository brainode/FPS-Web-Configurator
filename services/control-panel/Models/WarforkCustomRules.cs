// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

namespace control_panel.Models;

public sealed class WarforkCustomRules
{
    public bool Enabled { get; set; }
    public List<string> AllowedWeapons { get; set; } = [];
    public bool ClanArenaLoadoutEnabled { get; set; }
    public List<WarforkClanArenaWeaponLoadout> ClanArenaLoadout { get; set; } = [];
    public bool DisableHealthItems { get; set; }
    public bool DisableArmorItems { get; set; }
    public bool DisablePowerups { get; set; }
    public int? Gravity { get; set; }
}
