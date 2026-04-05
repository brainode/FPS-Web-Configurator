// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Models;
using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class WarforkGameAdapterTests
{
    private readonly WarforkGameAdapter _adapter = new();

    [Fact]
    public void GameKey_IsWarfork()
    {
        Assert.Equal("warfork", _adapter.GameKey);
    }

    [Fact]
    public void GetSummary_NullJson_ReturnsDefaultSummary()
    {
        var summary = _adapter.GetSummary(null);

        Assert.NotEmpty(summary.ModeName);
        Assert.NotEmpty(summary.StartMap);
    }

    [Fact]
    public void GetSummary_WithInstagibAndInstajump_ShowsBothInModeFlags()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"],
            Instagib = true,
            Instajump = true
        });

        var summary = _adapter.GetSummary(json);

        Assert.Contains("Instagib", summary.ModeFlags);
        Assert.Contains("Instajump", summary.ModeFlags);
    }

    [Fact]
    public void GetSummary_NoFlags_ReturnsStandardRuleset()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"]
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("Standard ruleset", summary.ModeFlags);
    }

    [Fact]
    public void GetSummary_WithServerPassword_ShowsPasswordProtected()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"],
            ServerPassword = "secret"
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("Password protected", summary.AccessLabel);
    }

    [Fact]
    public void GetSummary_NoServerPassword_ShowsOpenLobby()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"]
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("Open lobby", summary.AccessLabel);
    }

    [Fact]
    public void GetSummary_RconConfigured_ShowsConfigured()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"],
            RconPassword = "secret"
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("Configured", summary.RconLabel);
    }

    [Fact]
    public void GetSummary_RconEmpty_ShowsOptional()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"],
            RconPassword = string.Empty
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("Optional", summary.RconLabel);
    }

    [Fact]
    public void GetSummary_StartMap_IsUpperCase()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            StartMap = "return",
            MapList = ["return"]
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("RETURN", summary.StartMap);
    }

    [Fact]
    public void GetSummary_MapCountLabel_IncludesCount()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return", "pressure"]
        });

        var summary = _adapter.GetSummary(json);

        Assert.Equal("2 map(s) selected", summary.MapCountLabel);
    }

    [Fact]
    public void CreateDefaultJson_ProducesDeserializableSettings()
    {
        var json = _adapter.CreateDefaultJson();
        var settings = WarforkConfigurationSerializer.Deserialize(json);

        Assert.NotNull(settings);
        Assert.NotEmpty(settings.Gametype);
    }

    [Fact]
    public void Adapter_ImplementsIGameAdapter()
    {
        Assert.IsAssignableFrom<IGameAdapter>(_adapter);
    }

    [Fact]
    public void GetContainerEnv_ContainsAllExpectedKeys()
    {
        var env = _adapter.GetContainerEnv(null);

        Assert.Contains("WARFORK_GAMETYPE", env.Keys);
        Assert.Contains("WARFORK_BASE_GAMETYPE", env.Keys);
        Assert.Contains("WARFORK_START_MAP", env.Keys);
        Assert.Contains("WARFORK_MAPLIST", env.Keys);
        Assert.Contains("WARFORK_INSTAGIB", env.Keys);
        Assert.Contains("WARFORK_INSTAJUMP", env.Keys);
        Assert.Contains("WARFORK_INSTASHIELD", env.Keys);
        Assert.Contains("WARFORK_SCORELIMIT", env.Keys);
        Assert.Contains("WARFORK_TIMELIMIT", env.Keys);
        Assert.Contains("WARFORK_RCON_PASSWORD", env.Keys);
        Assert.Contains("WARFORK_PASSWORD", env.Keys);
        Assert.Contains("WARFORK_CA_LOADOUT_ENABLED", env.Keys);
        Assert.Contains("WARFORK_CA_LOADOUT_INVENTORY", env.Keys);
        Assert.Contains("WARFORK_CA_STRONG_AMMO", env.Keys);
        Assert.Contains("WARFORK_CA_INFINITE_WEAPONS", env.Keys);
        Assert.Contains("WARFORK_CA_DAMAGE_OVERRIDES", env.Keys);
        Assert.Contains("WARFORK_CA_SPLASH_OVERRIDES", env.Keys);
        Assert.Contains("WARFORK_CA_HEALING_WEAPONS", env.Keys);
    }

    [Fact]
    public void GetContainerEnv_MapsGametypeCorrectly()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ctf",
            StartMap = "wfctf1",
            MapList = ["wfctf1"]
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("ctf", env["WARFORK_GAMETYPE"]);
        Assert.Equal("wfctf1", env["WARFORK_START_MAP"]);
    }

    [Fact]
    public void GetContainerEnv_MapList_IsSpaceSeparated()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return", "pressure"]
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("return pressure", env["WARFORK_MAPLIST"]);
    }

    [Fact]
    public void GetContainerEnv_WithClanArenaLoadout_MapsInventoryAndAmmo()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"],
            CustomRules = new WarforkCustomRules
            {
                Enabled = true,
                ClanArenaLoadoutEnabled = true,
                ClanArenaLoadout =
                [
                    new WarforkClanArenaWeaponLoadout
                    {
                        WeaponKey = "riotgun",
                        Ammo = 12,
                        InfiniteAmmo = false,
                        DamageOverride = 200
                    },
                    new WarforkClanArenaWeaponLoadout
                    {
                        WeaponKey = "electrobolt",
                        Ammo = 25,
                        InfiniteAmmo = true,
                        DamageOverride = 99
                    },
                    new WarforkClanArenaWeaponLoadout
                    {
                        WeaponKey = "rocketlauncher",
                        Ammo = 20,
                        InfiniteAmmo = false,
                        DamageOverride = 45,
                        HealOnHit = true
                    }
                ]
            }
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("panelca", env["WARFORK_GAMETYPE"]);
        Assert.Equal("ca", env["WARFORK_BASE_GAMETYPE"]);
        Assert.Equal("1", env["WARFORK_CA_LOADOUT_ENABLED"]);
        Assert.Equal("gb cells rg shells rl rockets eb bolts", env["WARFORK_CA_LOADOUT_INVENTORY"]);
        Assert.Equal("1 0 12 0 20 0 0 9999", env["WARFORK_CA_STRONG_AMMO"]);
        Assert.Equal("electrobolt", env["WARFORK_CA_INFINITE_WEAPONS"]);
        Assert.Equal("rocketlauncher=45 electrobolt=99", env["WARFORK_CA_DAMAGE_OVERRIDES"]);
        Assert.Equal("rocketlauncher", env["WARFORK_CA_HEALING_WEAPONS"]);
    }

    [Fact]
    public void GetContainerEnv_WithGravity_SetsGravityEnvVar()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"],
            CustomRules = new WarforkCustomRules { Enabled = true, Gravity = 500 }
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("500", env["WARFORK_GRAVITY"]);
    }

    [Fact]
    public void GetContainerEnv_WithoutGravity_GravityEnvIsEmpty()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"],
            CustomRules = new WarforkCustomRules { Enabled = true }
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal(string.Empty, env["WARFORK_GRAVITY"]);
    }

    [Fact]
    public void GetContainerEnv_WithDisabledHealthItems_SetsDisableFlag()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"],
            CustomRules = new WarforkCustomRules
            {
                Enabled = true,
                DisableHealthItems = true,
                DisableArmorItems = false,
                DisablePowerups = false
            }
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("1", env["WARFORK_DISABLE_HEALTH"]);
        Assert.Equal("0", env["WARFORK_DISABLE_ARMOR"]);
        Assert.Equal("0", env["WARFORK_DISABLE_POWERUPS"]);
    }

    [Fact]
    public void GetContainerEnv_WithAllPickupsDisabled_SetsAllDisableFlags()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"],
            CustomRules = new WarforkCustomRules
            {
                Enabled = true,
                DisableHealthItems = true,
                DisableArmorItems = true,
                DisablePowerups = true
            }
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("1", env["WARFORK_DISABLE_HEALTH"]);
        Assert.Equal("1", env["WARFORK_DISABLE_ARMOR"]);
        Assert.Equal("1", env["WARFORK_DISABLE_POWERUPS"]);
    }

    [Fact]
    public void GetContainerEnv_ContainsExpectedSplashOverridesKey()
    {
        var env = _adapter.GetContainerEnv(null);
        Assert.Contains("WARFORK_CA_SPLASH_OVERRIDES", env.Keys);
    }

    [Fact]
    public void GetContainerEnv_WithSplashOverride_SetsSplashEnvVar()
    {
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"],
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
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("rocketlauncher=70", env["WARFORK_CA_DAMAGE_OVERRIDES"]);
        Assert.Equal("rocketlauncher=30", env["WARFORK_CA_SPLASH_OVERRIDES"]);
    }

    [Fact]
    public void GetContainerEnv_SplashOverride_ExcludesUnsupportedWeapons()
    {
        // electrobolt has SupportsSplashDamageOverride = false
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"],
            CustomRules = new WarforkCustomRules
            {
                Enabled = true,
                ClanArenaLoadoutEnabled = true,
                ClanArenaLoadout =
                [
                    new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt",    Ammo = 15, SplashDamageOverride = 999 },
                    new WarforkClanArenaWeaponLoadout { WeaponKey = "rocketlauncher", Ammo = 20, SplashDamageOverride = 30  },
                ]
            }
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("rocketlauncher=30", env["WARFORK_CA_SPLASH_OVERRIDES"]);
    }

    [Fact]
    public void GetContainerEnv_DamageOverrides_ExcludesUnsupportedWeapons()
    {
        // riotgun and lasergun have SupportsDamageOverride = false, so their
        // DamageOverride values must NOT appear in WARFORK_CA_DAMAGE_OVERRIDES.
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"],
            CustomRules = new WarforkCustomRules
            {
                Enabled = true,
                ClanArenaLoadoutEnabled = true,
                ClanArenaLoadout =
                [
                    new WarforkClanArenaWeaponLoadout { WeaponKey = "riotgun",       Ammo = 10, DamageOverride = 999 },
                    new WarforkClanArenaWeaponLoadout { WeaponKey = "lasergun",      Ammo = 60, DamageOverride = 999 },
                    new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt",   Ammo = 15, DamageOverride = 75  },
                ]
            }
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("electrobolt=75", env["WARFORK_CA_DAMAGE_OVERRIDES"]);
    }

    [Fact]
    public void GetContainerEnv_HealingWeapons_ExcludesUnsupportedWeapons()
    {
        // Only rocketlauncher has SupportsHealingMode = true.
        var json = WarforkConfigurationSerializer.Serialize(new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return"],
            CustomRules = new WarforkCustomRules
            {
                Enabled = true,
                ClanArenaLoadoutEnabled = true,
                ClanArenaLoadout =
                [
                    new WarforkClanArenaWeaponLoadout { WeaponKey = "electrobolt",   Ammo = 15, HealOnHit = true },
                    new WarforkClanArenaWeaponLoadout { WeaponKey = "rocketlauncher", Ammo = 20, HealOnHit = true },
                ]
            }
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("rocketlauncher", env["WARFORK_CA_HEALING_WEAPONS"]);
    }
}
