// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

namespace control_panel.Models;

public sealed class WarforkClanArenaWeaponLoadout
{
    public string WeaponKey { get; set; } = string.Empty;
    public int Ammo { get; set; }
    public bool InfiniteAmmo { get; set; }
}
