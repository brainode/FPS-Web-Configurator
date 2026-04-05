// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Models;
using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class WarforkWeaponsCatalogTests
{
    // ── Weapon capability flags ───────────────────────────────────────────────

    [Theory]
    [InlineData("grenadelauncher", true)]
    [InlineData("rocketlauncher",  true)]
    [InlineData("plasmagun",       true)]
    [InlineData("electrobolt",     true)]
    [InlineData("machinegun",      false)]
    [InlineData("riotgun",         false)]
    [InlineData("lasergun",        false)]
    public void SupportsDamageOverride_MatchesCatalog(string key, bool expected)
    {
        Assert.Equal(expected, WarforkWeaponsCatalog.SupportsDamageOverride(key));
    }

    [Theory]
    [InlineData("rocketlauncher", true)]
    [InlineData("electrobolt",    false)]
    [InlineData("machinegun",     false)]
    [InlineData("riotgun",        false)]
    [InlineData("grenadelauncher", false)]
    [InlineData("plasmagun",      false)]
    [InlineData("lasergun",       false)]
    public void SupportsHealingMode_MatchesCatalog(string key, bool expected)
    {
        Assert.Equal(expected, WarforkWeaponsCatalog.SupportsHealingMode(key));
    }

    // ── BuildDamageOverrideString ─────────────────────────────────────────────

    [Fact]
    public void BuildDamageOverrideString_EmptyLoadout_ReturnsEmpty()
    {
        var result = WarforkWeaponsCatalog.BuildDamageOverrideString([]);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BuildDamageOverrideString_ExcludesUnsupportedWeapons()
    {
        // riotgun and lasergun have SupportsDamageOverride = false.
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "riotgun",    Ammo = 10, DamageOverride = 999 },
            new WarforkClanArenaWeaponLoadout { WeaponKey = "lasergun",   Ammo = 60, DamageOverride = 999 },
            new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt", Ammo = 15, DamageOverride = 75 },
        };

        var result = WarforkWeaponsCatalog.BuildDamageOverrideString(loadout);

        Assert.Equal("electrobolt=75", result);
    }

    [Fact]
    public void BuildDamageOverrideString_ExcludesNullAndZeroOverrides()
    {
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "rocketlauncher", Ammo = 20, DamageOverride = null },
            new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt",    Ammo = 15, DamageOverride = 60  },
        };

        var result = WarforkWeaponsCatalog.BuildDamageOverrideString(loadout);

        Assert.Equal("electrobolt=60", result);
    }

    [Fact]
    public void BuildDamageOverrideString_MultipleWeapons_UseCatalogOrder()
    {
        // Catalog order: machinegun(1) riotgun(2) grenadelauncher(3) rocketlauncher(4) plasmagun(5) lasergun(6) electrobolt(7)
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt",    Ammo = 15, DamageOverride = 90  },
            new WarforkClanArenaWeaponLoadout { WeaponKey = "rocketlauncher", Ammo = 20, DamageOverride = 45  },
            new WarforkClanArenaWeaponLoadout { WeaponKey = "grenadelauncher", Ammo = 15, DamageOverride = 70 },
        };

        var result = WarforkWeaponsCatalog.BuildDamageOverrideString(loadout);

        Assert.Equal("grenadelauncher=70 rocketlauncher=45 electrobolt=90", result);
    }

    // ── BuildHealingWeaponsString ─────────────────────────────────────────────

    [Fact]
    public void BuildHealingWeaponsString_EmptyLoadout_ReturnsEmpty()
    {
        var result = WarforkWeaponsCatalog.BuildHealingWeaponsString([]);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BuildHealingWeaponsString_ExcludesUnsupportedWeapons()
    {
        // Only rocketlauncher supports healing mode.
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt",    Ammo = 15, HealOnHit = true },
            new WarforkClanArenaWeaponLoadout { WeaponKey = "rocketlauncher", Ammo = 20, HealOnHit = true },
        };

        var result = WarforkWeaponsCatalog.BuildHealingWeaponsString(loadout);

        Assert.Equal("rocketlauncher", result);
    }

    [Fact]
    public void BuildHealingWeaponsString_HealOnHitFalse_IsExcluded()
    {
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "rocketlauncher", Ammo = 20, HealOnHit = false },
        };

        var result = WarforkWeaponsCatalog.BuildHealingWeaponsString(loadout);

        Assert.Equal(string.Empty, result);
    }

    // ── BuildClanArenaInventory ───────────────────────────────────────────────

    [Fact]
    public void BuildClanArenaInventory_AlwaysIncludesGunbladeAndCells()
    {
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt", Ammo = 15 }
        };

        var tokens = WarforkWeaponsCatalog.BuildClanArenaInventory(loadout).Split(' ');

        Assert.Contains("gb", tokens);
        Assert.Contains("cells", tokens);
    }

    [Fact]
    public void BuildClanArenaInventory_IncludesWeaponAndAmmoTokensForEachLoadoutEntry()
    {
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "riotgun",      Ammo = 10 },
            new WarforkClanArenaWeaponLoadout { WeaponKey = "rocketlauncher", Ammo = 20 },
            new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt",  Ammo = 15 },
        };

        var tokens = WarforkWeaponsCatalog.BuildClanArenaInventory(loadout).Split(' ');

        Assert.Contains("rg",      tokens);
        Assert.Contains("shells",  tokens);
        Assert.Contains("rl",      tokens);
        Assert.Contains("rockets", tokens);
        Assert.Contains("eb",      tokens);
        Assert.Contains("bolts",   tokens);
    }

    [Fact]
    public void BuildClanArenaInventory_DoesNotIncludeTokensForWeaponsNotInLoadout()
    {
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt", Ammo = 15 }
        };

        var tokens = WarforkWeaponsCatalog.BuildClanArenaInventory(loadout).Split(' ');

        Assert.DoesNotContain("rl",      tokens);
        Assert.DoesNotContain("rockets", tokens);
        Assert.DoesNotContain("rg",      tokens);
        Assert.DoesNotContain("shells",  tokens);
    }

    // ── BuildClanArenaStrongAmmoString ────────────────────────────────────────

    [Fact]
    public void BuildClanArenaStrongAmmoString_HasEightSlots()
    {
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt", Ammo = 15 }
        };

        var parts = WarforkWeaponsCatalog.BuildClanArenaStrongAmmoString(loadout).Split(' ');

        Assert.Equal(8, parts.Length);
    }

    [Fact]
    public void BuildClanArenaStrongAmmoString_SlotOneIsAlways1_ForGunblade()
    {
        // Slot 0 (GB) is always 1.
        var result = WarforkWeaponsCatalog.BuildClanArenaStrongAmmoString([]);
        Assert.Equal("1", result.Split(' ')[0]);
    }

    [Fact]
    public void BuildClanArenaStrongAmmoString_InfiniteAmmo_Is9999()
    {
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt", Ammo = 15, InfiniteAmmo = true },
        };

        var parts = WarforkWeaponsCatalog.BuildClanArenaStrongAmmoString(loadout).Split(' ');

        // Electrobolt is slot 7 (index 7).
        Assert.Equal("9999", parts[7]);
    }

    [Fact]
    public void BuildClanArenaStrongAmmoString_CorrectSlotMappingForMultipleWeapons()
    {
        // riotgun=slot 2, rocketlauncher=slot 4, electrobolt=slot 7
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "riotgun",       Ammo = 12 },
            new WarforkClanArenaWeaponLoadout { WeaponKey = "rocketlauncher", Ammo = 20 },
            new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt",   Ammo = 15, InfiniteAmmo = true },
        };

        var parts = WarforkWeaponsCatalog.BuildClanArenaStrongAmmoString(loadout).Split(' ');

        Assert.Equal("12",   parts[2]); // riotgun shells
        Assert.Equal("20",   parts[4]); // rocketlauncher rockets
        Assert.Equal("9999", parts[7]); // electrobolt bolts (infinite)
    }

    // ── SupportsSplashDamageOverride ──────────────────────────────────────────

    [Theory]
    [InlineData("grenadelauncher", true)]
    [InlineData("rocketlauncher",  true)]
    [InlineData("plasmagun",       true)]
    [InlineData("electrobolt",     false)]
    [InlineData("machinegun",      false)]
    [InlineData("riotgun",         false)]
    [InlineData("lasergun",        false)]
    public void SupportsSplashDamageOverride_MatchesCatalog(string key, bool expected)
    {
        Assert.Equal(expected, WarforkWeaponsCatalog.SupportsSplashDamageOverride(key));
    }

    // ── BuildSplashDamageOverrideString ───────────────────────────────────────

    [Fact]
    public void BuildSplashDamageOverrideString_EmptyLoadout_ReturnsEmpty()
    {
        var result = WarforkWeaponsCatalog.BuildSplashDamageOverrideString([]);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BuildSplashDamageOverrideString_ExcludesUnsupportedWeapons()
    {
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt",    Ammo = 15, SplashDamageOverride = 999 },
            new WarforkClanArenaWeaponLoadout { WeaponKey = "rocketlauncher", Ammo = 20, SplashDamageOverride = 30  },
        };

        var result = WarforkWeaponsCatalog.BuildSplashDamageOverrideString(loadout);

        Assert.Equal("rocketlauncher=30", result);
    }

    [Fact]
    public void BuildSplashDamageOverrideString_MultipleProjectiles_UseCatalogOrder()
    {
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "rocketlauncher",  Ammo = 20, SplashDamageOverride = 40 },
            new WarforkClanArenaWeaponLoadout { WeaponKey = "grenadelauncher", Ammo = 15, SplashDamageOverride = 60 },
        };

        var result = WarforkWeaponsCatalog.BuildSplashDamageOverrideString(loadout);

        Assert.Equal("grenadelauncher=60 rocketlauncher=40", result);
    }

    // ── NormalizeClanArenaLoadout ─────────────────────────────────────────────

    [Fact]
    public void NormalizeClanArenaLoadout_SortsWeaponsByCatalogOrder()
    {
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt",    Ammo = 15 },
            new WarforkClanArenaWeaponLoadout { WeaponKey = "rocketlauncher", Ammo = 20 },
            new WarforkClanArenaWeaponLoadout { WeaponKey = "riotgun",        Ammo = 10 },
        };

        var normalized = WarforkWeaponsCatalog.NormalizeClanArenaLoadout(loadout);

        Assert.Equal(["riotgun", "rocketlauncher", "electrobolt"],
            normalized.Select(r => r.WeaponKey));
    }

    [Fact]
    public void NormalizeClanArenaLoadout_DeduplicatesKeepingLast()
    {
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt", Ammo = 10 },
            new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt", Ammo = 25 },
        };

        var normalized = WarforkWeaponsCatalog.NormalizeClanArenaLoadout(loadout);

        Assert.Single(normalized);
        Assert.Equal(25, normalized[0].Ammo);
    }

    [Fact]
    public void NormalizeClanArenaLoadout_DamageOverride_ClearedForUnsupportedWeapons()
    {
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "riotgun", Ammo = 10, DamageOverride = 999 },
        };

        var normalized = WarforkWeaponsCatalog.NormalizeClanArenaLoadout(loadout);

        Assert.Null(normalized[0].DamageOverride);
    }

    [Fact]
    public void NormalizeClanArenaLoadout_HealOnHit_ClearedForUnsupportedWeapons()
    {
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt", Ammo = 15, HealOnHit = true },
        };

        var normalized = WarforkWeaponsCatalog.NormalizeClanArenaLoadout(loadout);

        Assert.False(normalized[0].HealOnHit);
    }

    [Fact]
    public void NormalizeClanArenaLoadout_SplashDamageOverride_ClearedForUnsupportedWeapons()
    {
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt", Ammo = 15, SplashDamageOverride = 999 },
        };

        var normalized = WarforkWeaponsCatalog.NormalizeClanArenaLoadout(loadout);

        Assert.Null(normalized[0].SplashDamageOverride);
    }

    [Fact]
    public void NormalizeClanArenaLoadout_SplashDamageOverride_PreservedForProjectileWeapons()
    {
        var loadout = new[]
        {
            new WarforkClanArenaWeaponLoadout { WeaponKey = "rocketlauncher", Ammo = 20, SplashDamageOverride = 30 },
        };

        var normalized = WarforkWeaponsCatalog.NormalizeClanArenaLoadout(loadout);

        Assert.Equal(30, normalized[0].SplashDamageOverride);
    }
}
