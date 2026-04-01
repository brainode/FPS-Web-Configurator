// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class WarforkModuleCatalogTests
{
    [Fact]
    public void ResolveStartMap_ReturnsFirstSelectedMap_WhenCurrentStartMapIsOutsidePool()
    {
        var result = WarforkModuleCatalog.ResolveStartMap(
            "wfctf1",
            ["return", "pressure"],
            gametype: "ca");

        Assert.Equal("return", result);
    }

    [Fact]
    public void ResolveStartMap_ReturnsEmpty_WhenPoolIsEmptyAndAllowEmptyIsTrue()
    {
        var result = WarforkModuleCatalog.ResolveStartMap(
            startMap: "return",
            selectedMaps: [],
            gametype: "ca",
            allowEmpty: true);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void FindGametype_ReturnsNull_ForUnknownKey()
    {
        Assert.Null(WarforkModuleCatalog.FindGametype("unknown"));
    }

    [Fact]
    public void NormalizeMapSelection_PreservesAnyValidStockMaps_ForSelectedGametype()
    {
        var maps = WarforkModuleCatalog.NormalizeMapSelection(["wfda1", "wfdm1"], "ffa", fillDefaultsWhenEmpty: false);

        Assert.Equal(["wfda1", "wfdm1"], maps);
    }

    [Theory]
    [InlineData("ca", "return", true)]
    [InlineData("ca", "wfdm1", true)]
    [InlineData("ffa", "wfda1", true)]
    [InlineData("ctf", "wfda5", true)]
    [InlineData("rekt", "wfctf3", true)]
    [InlineData("rekt", "missing-map", false)]
    public void IsSupportedMapForGametype_AllowsAnyKnownStockMap(string gametype, string mapKey, bool expected)
    {
        Assert.Equal(expected, WarforkModuleCatalog.IsSupportedMapForGametype(mapKey, gametype));
    }

    [Fact]
    public void GetUnsupportedMapsForGametype_ReturnsEmpty_ForMixedStockPools()
    {
        var unsupportedMaps = WarforkModuleCatalog.GetUnsupportedMapsForGametype(["wfda1", "wfdm1", "wfctf3"], "ffa");

        Assert.Empty(unsupportedMaps);
    }

    [Theory]
    [InlineData("ca", 11, 0)]
    [InlineData("dm", 0, 15)]
    [InlineData("duel", 0, 10)]
    [InlineData("ffa", 0, 15)]
    [InlineData("tdm", 0, 20)]
    [InlineData("ctf", 0, 20)]
    [InlineData("ctftactics", 0, 20)]
    [InlineData("bomb", 16, 0)]
    [InlineData("da", 11, 0)]
    [InlineData("headhunt", 0, 15)]
    [InlineData("race", 0, 0)]
    [InlineData("rekt", 0, 0)]
    [InlineData("tutorial", 5, 0)]
    public void FindGametype_ReturnsDistributionDefaults_MatchingStockCfgs(
        string key, int expectedScorelimit, int expectedTimelimit)
    {
        var option = WarforkModuleCatalog.FindGametype(key);

        Assert.NotNull(option);
        Assert.Equal(expectedScorelimit, option.DefaultScorelimit);
        Assert.Equal(expectedTimelimit, option.DefaultTimelimit);
    }

    [Fact]
    public void AllGametypes_HaveNonEmptyLabels()
    {
        Assert.All(WarforkModuleCatalog.Gametypes, g => Assert.NotEmpty(g.Label));
    }

    [Fact]
    public void AllGametypes_HaveAtLeastOneRecommendedMap()
    {
        Assert.All(WarforkModuleCatalog.Gametypes, g => Assert.NotEmpty(g.RecommendedMaps));
    }
}
