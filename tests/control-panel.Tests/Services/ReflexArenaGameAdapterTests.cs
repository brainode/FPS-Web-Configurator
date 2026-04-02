// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Models;
using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class ReflexArenaGameAdapterTests
{
    private readonly ReflexArenaGameAdapter _adapter = new();

    [Fact]
    public void GameKey_IsReflexArena()
    {
        Assert.Equal("reflex-arena", _adapter.GameKey);
    }

    [Fact]
    public void Adapter_ImplementsIGameAdapter()
    {
        Assert.IsAssignableFrom<IGameAdapter>(_adapter);
    }

    [Fact]
    public void GetSummary_DefaultSettings_AreReadable()
    {
        var summary = _adapter.GetSummary(null);

        Assert.Equal("1v1 Duel", summary.ModeName);
        Assert.Equal("Fusion", summary.StartMap);
        Assert.Equal("Single-map startup", summary.MapCountLabel);
    }

    [Fact]
    public void GetSummary_WithMutator_ShowsMutatorLabel()
    {
        var json = ReflexArenaConfigurationSerializer.Serialize(new ReflexArenaServerSettings
        {
            Mode = "ffa",
            StartMap = "Phobos",
            Mutators = ["instagib", "lowgravity"],
        });

        var summary = _adapter.GetSummary(json);

        Assert.Contains("Instagib", summary.ModeFlags);
        Assert.Contains("Low Gravity", summary.ModeFlags);
    }

    [Fact]
    public void GetContainerEnv_ContainsExpectedKeys()
    {
        var env = _adapter.GetContainerEnv(null);

        Assert.Contains("REFLEX_MODE", env.Keys);
        Assert.Contains("REFLEX_START_MAP", env.Keys);
        Assert.Contains("REFLEX_START_MUTATORS", env.Keys);
        Assert.Contains("REFLEX_MAXCLIENTS", env.Keys);
        Assert.Contains("REFLEX_PASSWORD", env.Keys);
        Assert.Contains("REFLEX_REF_PASSWORD", env.Keys);
    }

    [Fact]
    public void GetContainerEnv_WorkshopMap_AddsWorkshopStartupEnv()
    {
        var json = ReflexArenaConfigurationSerializer.Serialize(new ReflexArenaServerSettings
        {
            Mode = "tdm",
            StartMap = "Aerowalk",
        });

        var env = _adapter.GetContainerEnv(json);

        Assert.Equal("Aerowalk", env["REFLEX_START_MAP"]);
        Assert.Equal("608517732", env["REFLEX_START_WORKSHOP_MAP"]);
    }

    [Fact]
    public void CreateDefaultJson_ProducesDeserializableSettings()
    {
        var json = _adapter.CreateDefaultJson();
        var settings = ReflexArenaConfigurationSerializer.Deserialize(json);

        Assert.NotNull(settings);
        Assert.Equal("1v1", settings.Mode);
    }
}
