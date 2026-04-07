// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using System.Text.Json;
using control_panel.Models;
using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class WarforkConfigurationSerializerTests
{
    [Fact]
    public void Deserialize_ReadsLegacyStringBasedWarforkConfiguration()
    {
        const string json = """
        {
          "sv_defaultmap": "wfca1",
          "g_maplist": "wfca1 wfca2",
          "g_gametype": "ca",
          "g_instagib": "1",
          "g_instajump": "0",
          "g_instashield": "1",
          "g_scorelimit": "11",
          "g_timelimit": "0",
          "rcon_password": "secret",
          "password": "joinme"
        }
        """;

        var settings = WarforkConfigurationSerializer.Deserialize(json);

        Assert.Equal("wfca1", settings.StartMap);
        Assert.Equal("ca", settings.Gametype);
        Assert.Equal(["wfca1", "wfca2"], settings.MapList);
        Assert.True(settings.Instagib);
        Assert.False(settings.Instajump);
        Assert.True(settings.Instashield);
        Assert.Equal(11, settings.Scorelimit);
        Assert.Equal(0, settings.Timelimit);
        Assert.Equal("secret", settings.RconPassword);
        Assert.Equal("joinme", settings.ServerPassword);
    }

    [Fact]
    public void Deserialize_UsesGametypeRecommendations_WhenMapListIsEmpty()
    {
        const string json = """
        {
          "sv_defaultmap": "wfdm1",
          "g_maplist": "",
          "g_gametype": "dm"
        }
        """;

        var settings = WarforkConfigurationSerializer.Deserialize(json);

        Assert.Contains("wfdm1", settings.MapList);
        Assert.Contains("wfdm2", settings.MapList);
    }

    [Fact]
    public void Serialize_WritesWarforkValues_AsExpectedByCurrentConfigStore()
    {
        var settings = new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "wfca1",
            MapList = ["wfca1", "wfca2"],
            Instagib = true,
            Instajump = false,
            Instashield = true,
            Scorelimit = 20,
            Timelimit = 12,
            RconPassword = "admin",
            ServerPassword = "friends"
        };

        var json = WarforkConfigurationSerializer.Serialize(settings);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("wfca1", root.GetProperty("sv_defaultmap").GetString());
        Assert.Equal("wfca1 wfca2", root.GetProperty("g_maplist").GetString());
        Assert.Equal("1", root.GetProperty("g_instagib").GetString());
        Assert.Equal("0", root.GetProperty("g_instajump").GetString());
        Assert.Equal("1", root.GetProperty("g_instashield").GetString());
        Assert.Equal("20", root.GetProperty("g_scorelimit").GetString());
        Assert.Equal("12", root.GetProperty("g_timelimit").GetString());
    }

    [Fact]
    public void Serialize_ResolvesStartMap_ToFirstPoolMap_WhenStartMapIsOutsidePool()
    {
        var settings = new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "wfctf1",
            MapList = ["wfca1", "wfca2"]
        };

        var json = WarforkConfigurationSerializer.Serialize(settings);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("wfca1", root.GetProperty("sv_defaultmap").GetString());
        Assert.Equal("wfca1 wfca2", root.GetProperty("g_maplist").GetString());
    }

    [Fact]
    public void Serialize_PreservesMixedMapPools_ForNonRestrictedGametype()
    {
        var settings = new WarforkServerSettings
        {
            Gametype = "ffa",
            StartMap = "wfda1",
            MapList = ["wfda1", "wfdm1", "wfctf3"]
        };

        var json = WarforkConfigurationSerializer.Serialize(settings);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("wfda1", root.GetProperty("sv_defaultmap").GetString());
        Assert.Equal("wfda1 wfdm1 wfctf3", root.GetProperty("g_maplist").GetString());
    }

    [Fact]
    public void Serialize_AndDeserialize_RoundTripsPasswords()
    {
        var settings = new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "wfca1",
            MapList = ["wfca1", "wfca2"],
            RconPassword = "rcon-secret",
            ServerPassword = "join-secret"
        };

        var json = WarforkConfigurationSerializer.Serialize(settings);
        var restored = WarforkConfigurationSerializer.Deserialize(json);

        Assert.Equal("rcon-secret", restored.RconPassword);
        Assert.Equal("join-secret", restored.ServerPassword);
    }

    [Fact]
    public void Serialize_AndDeserialize_RoundTripsClanArenaLoadout()
    {
        var settings = new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "wfca1",
            MapList = ["wfca1", "wfca2"],
            CustomRules = new WarforkCustomRules
            {
                Enabled = true,
                ClanArenaLoadoutEnabled = true,
                ClanArenaLoadout =
                [
                    new WarforkClanArenaWeaponLoadout
                    {
                        WeaponKey = "electrobolt",
                        Ammo = 99,
                        InfiniteAmmo = true,
                        DamageOverride = 99
                    },
                    new WarforkClanArenaWeaponLoadout
                    {
                        WeaponKey = "rocketlauncher",
                        Ammo = 20,
                        InfiniteAmmo = false,
                        DamageOverride = 45,
                        FireCooldownMs = 3000,
                        HealOnHit = true
                    }
                ]
            }
        };

        var json = WarforkConfigurationSerializer.Serialize(settings);
        var restored = WarforkConfigurationSerializer.Deserialize(json);

        Assert.NotNull(restored.CustomRules);
        Assert.True(restored.CustomRules!.ClanArenaLoadoutEnabled);
        Assert.Collection(
            restored.CustomRules.ClanArenaLoadout,
            rocketlauncher =>
            {
                Assert.Equal("rocketlauncher", rocketlauncher.WeaponKey);
                Assert.Equal(20, rocketlauncher.Ammo);
                Assert.False(rocketlauncher.InfiniteAmmo);
                Assert.Equal(45, rocketlauncher.DamageOverride);
                Assert.Equal(3000, rocketlauncher.FireCooldownMs);
                Assert.True(rocketlauncher.HealOnHit);
            },
            electrobolt =>
            {
                Assert.Equal("electrobolt", electrobolt.WeaponKey);
                Assert.Equal(99, electrobolt.Ammo);
                Assert.True(electrobolt.InfiniteAmmo);
                Assert.Equal(99, electrobolt.DamageOverride);
                Assert.Null(electrobolt.FireCooldownMs);
                Assert.False(electrobolt.HealOnHit);
            });
    }

    [Fact]
    public void Serialize_AndDeserialize_RoundTripsGravity()
    {
        var settings = new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "wfca1",
            MapList = ["wfca1"],
            CustomRules = new WarforkCustomRules
            {
                Enabled = true,
                Gravity = 500
            }
        };

        var json = WarforkConfigurationSerializer.Serialize(settings);
        var restored = WarforkConfigurationSerializer.Deserialize(json);

        Assert.NotNull(restored.CustomRules);
        Assert.Equal(500, restored.CustomRules!.Gravity);
    }

    [Fact]
    public void Serialize_AndDeserialize_RoundTripsSplashDamageOverride()
    {
        var settings = new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "wfca1",
            MapList = ["wfca1"],
            CustomRules = new WarforkCustomRules
            {
                Enabled = true,
                ClanArenaLoadoutEnabled = true,
                ClanArenaLoadout =
                [
                    new WarforkClanArenaWeaponLoadout
                    {
                        WeaponKey = "rocketlauncher",
                        Ammo = 20,
                        DamageOverride = 70,
                        SplashDamageOverride = 30
                    }
                ]
            }
        };

        var json = WarforkConfigurationSerializer.Serialize(settings);
        var restored = WarforkConfigurationSerializer.Deserialize(json);

        Assert.NotNull(restored.CustomRules);
        Assert.Single(restored.CustomRules!.ClanArenaLoadout);
        Assert.Equal(70, restored.CustomRules.ClanArenaLoadout[0].DamageOverride);
        Assert.Equal(30, restored.CustomRules.ClanArenaLoadout[0].SplashDamageOverride);
    }

    [Fact]
    public void Serialize_AndDeserialize_DropsSplashDamageOverride_ForUnsupportedWeapons()
    {
        var settings = new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "wfca1",
            MapList = ["wfca1"],
            CustomRules = new WarforkCustomRules
            {
                Enabled = true,
                ClanArenaLoadoutEnabled = true,
                ClanArenaLoadout =
                [
                    new WarforkClanArenaWeaponLoadout
                    {
                        WeaponKey = "electrobolt",
                        Ammo = 15,
                        SplashDamageOverride = 999
                    }
                ]
            }
        };

        var json = WarforkConfigurationSerializer.Serialize(settings);
        var restored = WarforkConfigurationSerializer.Deserialize(json);

        Assert.Null(restored.CustomRules!.ClanArenaLoadout[0].SplashDamageOverride);
    }

    [Fact]
    public void Serialize_AndDeserialize_RoundTripsFireCooldownOverride()
    {
        var settings = new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "wfca1",
            MapList = ["wfca1"],
            CustomRules = new WarforkCustomRules
            {
                Enabled = true,
                ClanArenaLoadoutEnabled = true,
                ClanArenaLoadout =
                [
                    new WarforkClanArenaWeaponLoadout
                    {
                        WeaponKey = "rocketlauncher",
                        Ammo = 20,
                        FireCooldownMs = 3000
                    }
                ]
            }
        };

        var json = WarforkConfigurationSerializer.Serialize(settings);
        var restored = WarforkConfigurationSerializer.Deserialize(json);

        Assert.NotNull(restored.CustomRules);
        Assert.Single(restored.CustomRules!.ClanArenaLoadout);
        Assert.Equal(3000, restored.CustomRules.ClanArenaLoadout[0].FireCooldownMs);
    }

    [Fact]
    public void Serialize_AndDeserialize_RoundTripsPickupDisableFlags()
    {
        var settings = new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "wfca1",
            MapList = ["wfca1"],
            CustomRules = new WarforkCustomRules
            {
                Enabled = true,
                DisableHealthItems = true,
                DisableArmorItems = false,
                DisablePowerups = true
            }
        };

        var json = WarforkConfigurationSerializer.Serialize(settings);
        var restored = WarforkConfigurationSerializer.Deserialize(json);

        Assert.NotNull(restored.CustomRules);
        Assert.True(restored.CustomRules!.DisableHealthItems);
        Assert.False(restored.CustomRules!.DisableArmorItems);
        Assert.True(restored.CustomRules!.DisablePowerups);
    }
}
