// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class ReflexArenaWeaponsCatalogTests
{
    [Fact]
    public void Catalog_LoadsWithoutException()
    {
        Assert.NotEmpty(ReflexArenaWeaponsCatalog.Weapons);
        Assert.NotEmpty(ReflexArenaWeaponsCatalog.Pickups);
        Assert.NotEmpty(ReflexArenaWeaponsCatalog.GlobalConstants);
    }

    [Fact]
    public void AllWeapons_HaveNonEmptyKeys()
    {
        foreach (var weapon in ReflexArenaWeaponsCatalog.Weapons)
        {
            Assert.False(string.IsNullOrWhiteSpace(weapon.Key));
            Assert.False(string.IsNullOrWhiteSpace(weapon.GconstPrefix));
        }
    }

    [Fact]
    public void FindWeapon_KnownKey_ReturnsEntry()
    {
        var entry = ReflexArenaWeaponsCatalog.FindWeapon("boltrifle");

        Assert.NotNull(entry);
        Assert.Equal("boltrifle", entry.Key);
        Assert.False(entry.HasSplashDamage);
        Assert.True(entry.HasAmmo);
    }

    [Fact]
    public void FindWeapon_RocketLauncher_HasSplashDamage()
    {
        var entry = ReflexArenaWeaponsCatalog.FindWeapon("rocketlauncher");

        Assert.NotNull(entry);
        Assert.True(entry.HasSplashDamage);
        Assert.NotNull(entry.DefaultSplashDamage);
    }

    [Fact]
    public void FindWeapon_UnknownKey_ReturnsNull()
    {
        Assert.Null(ReflexArenaWeaponsCatalog.FindWeapon("nonexistent_weapon"));
        Assert.Null(ReflexArenaWeaponsCatalog.FindWeapon(null));
        Assert.Null(ReflexArenaWeaponsCatalog.FindWeapon(string.Empty));
    }

    [Fact]
    public void FindPickup_KnownKey_ReturnsEntry()
    {
        var entry = ReflexArenaWeaponsCatalog.FindPickup("armor");

        Assert.NotNull(entry);
        Assert.False(string.IsNullOrWhiteSpace(entry.Gconst));
    }

    [Fact]
    public void FindPickup_UnknownKey_ReturnsNull()
    {
        Assert.Null(ReflexArenaWeaponsCatalog.FindPickup("nonexistent_pickup"));
    }
}
