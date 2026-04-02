// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Models;
using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class ReflexArenaConfigurationSerializerTests
{
    [Fact]
    public void Deserialize_Null_ReturnsDefaultSettings()
    {
        var settings = ReflexArenaConfigurationSerializer.Deserialize(null);

        Assert.Equal("1v1", settings.Mode);
        Assert.Equal("Fusion", settings.StartMap);
        Assert.Equal(8, settings.MaxClients);
        Assert.True(settings.SteamEnabled);
        Assert.Empty(settings.Mutators);
    }

    [Fact]
    public void Deserialize_InvalidJson_ReturnsDefaults()
    {
        var settings = ReflexArenaConfigurationSerializer.Deserialize("{ not valid json }");

        Assert.Equal("1v1", settings.Mode);
        Assert.Equal("Fusion", settings.StartMap);
    }

    [Fact]
    public void Serialize_Deserialize_Roundtrip()
    {
        var original = new ReflexArenaServerSettings
        {
            Hostname = "Reflex Test Server",
            Mode = "tdm",
            StartMap = "SkyTemples",
            Mutators = ["instagib", "lowgravity", "arena"],
            MaxClients = 10,
            SteamEnabled = false,
            Country = "ru",
            TimeLimitOverride = 15,
            ServerPassword = "join-secret",
            RefPassword = "ref-secret",
        };

        var json = ReflexArenaConfigurationSerializer.Serialize(original);
        var restored = ReflexArenaConfigurationSerializer.Deserialize(json);

        Assert.Equal(original.Hostname, restored.Hostname);
        Assert.Equal(original.Mode, restored.Mode);
        Assert.Equal(original.StartMap, restored.StartMap);
        Assert.Equal(original.Mutators, restored.Mutators);
        Assert.Equal(original.MaxClients, restored.MaxClients);
        Assert.Equal(original.SteamEnabled, restored.SteamEnabled);
        Assert.Equal("RU", restored.Country);
        Assert.Equal(original.TimeLimitOverride, restored.TimeLimitOverride);
        Assert.Equal(original.ServerPassword, restored.ServerPassword);
        Assert.Equal(original.RefPassword, restored.RefPassword);
    }

    [Fact]
    public void Serialize_WorkshopStartMap_UsesSvStartwmap()
    {
        var json = ReflexArenaConfigurationSerializer.Serialize(new ReflexArenaServerSettings
        {
            Mode = "tdm",
            StartMap = "Aerowalk",
        });

        Assert.Contains("\"sv_startmap\": \"\"", json);
        Assert.Contains("\"sv_startwmap\": \"608517732\"", json);
    }

    [Fact]
    public void Deserialize_WorkshopStartMap_MapsBackToLogicalMapKey()
    {
        var json = """{"sv_startmode":"tdm","sv_startmap":"","sv_startwmap":"608517732"}""";

        var settings = ReflexArenaConfigurationSerializer.Deserialize(json);

        Assert.Equal("tdm", settings.Mode);
        Assert.Equal("Aerowalk", settings.StartMap);
    }

    [Fact]
    public void Deserialize_UnsupportedMap_FallsBackToRecommendedMap()
    {
        var json = """{"sv_startmode":"ctf","sv_startmap":"Fusion"}""";

        var settings = ReflexArenaConfigurationSerializer.Deserialize(json);

        Assert.Equal("ctf", settings.Mode);
        Assert.Equal("SkyTemples", settings.StartMap);
    }

    [Fact]
    public void Serialize_ProducesExpectedKeys()
    {
        var json = ReflexArenaConfigurationSerializer.Serialize(new ReflexArenaServerSettings());

        Assert.Contains("sv_startmode", json);
        Assert.Contains("sv_startmap", json);
        Assert.Contains("sv_startwmap", json);
        Assert.Contains("sv_startmutators", json);
        Assert.Contains("sv_refpassword", json);
    }

    [Fact]
    public void Serialize_WithCustomRules_ProducesCustomRulesKey()
    {
        var settings = new ReflexArenaServerSettings
        {
            CustomRules = new control_panel.Models.ReflexArenaCustomRules
            {
                Enabled = true,
                RulesetName = "rocketrail",
            },
        };

        var json = ReflexArenaConfigurationSerializer.Serialize(settings);

        Assert.Contains("custom_rules", json);
        Assert.Contains("rocketrail", json);
    }

    [Fact]
    public void Serialize_Deserialize_CustomRulesRoundtrip()
    {
        var original = new ReflexArenaServerSettings
        {
            Mode = "tdm",
            StartMap = "Fusion",
            CustomRules = new control_panel.Models.ReflexArenaCustomRules
            {
                Enabled = true,
                RulesetName = "custom",
                Gravity = 400,
                Weapons =
                [
                    new() { Key = "boltrifle", WeaponEnabled = true, DirectDamage = 99, InfiniteAmmo = true },
                    new() { Key = "rocketlauncher", WeaponEnabled = true, SplashDamage = 1, MaxAmmo = 5 },
                    new() { Key = "shaft", WeaponEnabled = false },
                ],
                Pickups =
                [
                    new() { Key = "armor", Enabled = false },
                    new() { Key = "health", Enabled = true },
                ],
            },
        };

        var json = ReflexArenaConfigurationSerializer.Serialize(original);
        var restored = ReflexArenaConfigurationSerializer.Deserialize(json);

        Assert.NotNull(restored.CustomRules);
        Assert.True(restored.CustomRules.Enabled);
        Assert.Equal("custom", restored.CustomRules.RulesetName);
        Assert.Equal(400, restored.CustomRules.Gravity);

        var bolt = restored.CustomRules.Weapons.First(w => w.Key == "boltrifle");
        Assert.True(bolt.WeaponEnabled);
        Assert.Equal(99, bolt.DirectDamage);
        Assert.True(bolt.InfiniteAmmo);

        var rocket = restored.CustomRules.Weapons.First(w => w.Key == "rocketlauncher");
        Assert.Equal(1, rocket.SplashDamage);
        Assert.Equal(5, rocket.MaxAmmo);

        var shaft = restored.CustomRules.Weapons.First(w => w.Key == "shaft");
        Assert.False(shaft.WeaponEnabled);

        var armor = restored.CustomRules.Pickups.First(p => p.Key == "armor");
        Assert.False(armor.Enabled);
    }

    [Fact]
    public void Deserialize_WithoutCustomRules_ReturnsNullCustomRules()
    {
        var json = """{"sv_startmode":"1v1","sv_startmap":"Fusion","sv_startwmap":""}""";

        var settings = ReflexArenaConfigurationSerializer.Deserialize(json);

        Assert.Null(settings.CustomRules);
    }
}
