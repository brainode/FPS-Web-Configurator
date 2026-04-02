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
            Mutators = ["instagib"],
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
}
