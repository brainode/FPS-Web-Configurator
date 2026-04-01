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
          "sv_defaultmap": "return",
          "g_maplist": "return pressure",
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

        Assert.Equal("return", settings.StartMap);
        Assert.Equal("ca", settings.Gametype);
        Assert.Equal(["return", "pressure"], settings.MapList);
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
            StartMap = "return",
            MapList = ["return", "pressure"],
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

        Assert.Equal("return", root.GetProperty("sv_defaultmap").GetString());
        Assert.Equal("return pressure", root.GetProperty("g_maplist").GetString());
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
            MapList = ["return", "pressure"]
        };

        var json = WarforkConfigurationSerializer.Serialize(settings);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("return", root.GetProperty("sv_defaultmap").GetString());
        Assert.Equal("return pressure", root.GetProperty("g_maplist").GetString());
    }

    [Fact]
    public void Serialize_AndDeserialize_RoundTripsPasswords()
    {
        var settings = new WarforkServerSettings
        {
            Gametype = "ca",
            StartMap = "return",
            MapList = ["return", "pressure"],
            RconPassword = "rcon-secret",
            ServerPassword = "join-secret"
        };

        var json = WarforkConfigurationSerializer.Serialize(settings);
        var restored = WarforkConfigurationSerializer.Deserialize(json);

        Assert.Equal("rcon-secret", restored.RconPassword);
        Assert.Equal("join-secret", restored.ServerPassword);
    }
}
