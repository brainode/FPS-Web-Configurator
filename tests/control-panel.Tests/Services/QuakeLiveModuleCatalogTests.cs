// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class QuakeLiveModuleCatalogTests
{
    [Fact]
    public void AllFactories_HaveNonEmptyLabels()
    {
        Assert.All(QuakeLiveModuleCatalog.Factories, f => Assert.NotEmpty(f.Label));
    }

    [Fact]
    public void AllFactories_HaveNonEmptyDescriptions()
    {
        Assert.All(QuakeLiveModuleCatalog.Factories, f => Assert.NotEmpty(f.Description));
    }

    [Fact]
    public void AllFactories_HaveAtLeastOneRecommendedMap()
    {
        Assert.All(QuakeLiveModuleCatalog.Factories, f => Assert.NotEmpty(f.RecommendedMaps));
    }

    [Fact]
    public void AllFactories_RecommendedMaps_AreValidMaps()
    {
        foreach (var factory in QuakeLiveModuleCatalog.Factories)
        {
            foreach (var map in factory.RecommendedMaps)
            {
                Assert.True(
                    QuakeLiveModuleCatalog.IsValidMap(map),
                    $"Factory '{factory.Key}' recommends map '{map}' which is not in AllMaps.");
            }
        }
    }

    [Fact]
    public void Factories_WithSupportedMapGroups_OnlyRecommendSupportedMaps()
    {
        foreach (var factory in QuakeLiveModuleCatalog.Factories.Where(factory => factory.SupportedMapGroups.Count > 0))
        {
            foreach (var map in factory.RecommendedMaps)
            {
                Assert.True(
                    QuakeLiveModuleCatalog.IsSupportedMapForFactory(map, factory.Key),
                    $"Factory '{factory.Key}' recommends unsupported map '{map}'.");
            }
        }
    }

    [Theory]
    [InlineData("ffa")]
    [InlineData("duel")]
    [InlineData("race")]
    [InlineData("tdm")]
    [InlineData("ca")]
    [InlineData("ctf")]
    [InlineData("oneflag")]
    [InlineData("har")]
    [InlineData("ft")]
    [InlineData("dom")]
    [InlineData("ad")]
    [InlineData("rr")]
    [InlineData("iffa")]
    public void FindFactory_ReturnsOption_ForAllKnownKeys(string key)
    {
        var option = QuakeLiveModuleCatalog.FindFactory(key);

        Assert.NotNull(option);
        Assert.Equal(key, option.Key);
    }

    [Fact]
    public void FindFactory_IsCaseInsensitive()
    {
        var option = QuakeLiveModuleCatalog.FindFactory("CA");

        Assert.NotNull(option);
        Assert.Equal("ca", option.Key);
    }

    [Fact]
    public void FindFactory_ReturnsNull_ForUnknownKey()
    {
        Assert.Null(QuakeLiveModuleCatalog.FindFactory("unknown"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void FindFactory_ReturnsNull_ForNullOrWhitespace(string? key)
    {
        Assert.Null(QuakeLiveModuleCatalog.FindFactory(key));
    }

    [Fact]
    public void IsValidFactory_ReturnsFalse_ForNull()
    {
        Assert.False(QuakeLiveModuleCatalog.IsValidFactory(null));
    }

    [Fact]
    public void IsValidFactory_ReturnsTrue_ForKnownKey()
    {
        Assert.True(QuakeLiveModuleCatalog.IsValidFactory("ca"));
    }

    [Fact]
    public void IsValidMap_ReturnsFalse_ForNull()
    {
        Assert.False(QuakeLiveModuleCatalog.IsValidMap(null));
    }

    [Fact]
    public void IsValidMap_ReturnsTrue_ForKnownMap()
    {
        Assert.True(QuakeLiveModuleCatalog.IsValidMap("campgrounds"));
    }

    [Theory]
    [InlineData("aerowalk")]
    [InlineData("shiningforces")]
    [InlineData("qzpractice1")]
    public void IsValidMap_ReturnsTrue_ForStockServerMaps(string mapKey)
    {
        Assert.True(QuakeLiveModuleCatalog.IsValidMap(mapKey));
    }

    [Fact]
    public void NormalizeMapSelection_FillsDefaults_WhenEmptyAndFillEnabled()
    {
        var result = QuakeLiveModuleCatalog.NormalizeMapSelection([], "ca", fillDefaultsWhenEmpty: true);

        Assert.NotEmpty(result);
    }

    [Fact]
    public void NormalizeMapSelection_DoesNotFillDefaults_WhenFillDisabled()
    {
        var result = QuakeLiveModuleCatalog.NormalizeMapSelection([], "ca", fillDefaultsWhenEmpty: false);

        Assert.Empty(result);
    }

    [Fact]
    public void NormalizeMapSelection_FiltersInvalidMaps()
    {
        var result = QuakeLiveModuleCatalog.NormalizeMapSelection(
            ["campgrounds", "not_a_real_ql_map"],
            "ca",
            fillDefaultsWhenEmpty: false);

        Assert.Equal(["campgrounds"], result);
    }

    [Fact]
    public void IsSupportedMapForFactory_ReturnsFalse_ForUnsupportedCtfMap()
    {
        Assert.False(QuakeLiveModuleCatalog.IsSupportedMapForFactory("overkill", "ctf"));
    }

    [Fact]
    public void IsSupportedMapForFactory_ReturnsTrue_ForSupportedCtfMap()
    {
        Assert.True(QuakeLiveModuleCatalog.IsSupportedMapForFactory("courtyard", "ctf"));
    }

    [Fact]
    public void IsSupportedMapForFactory_ReturnsTrue_ForSupportedDuelMap()
    {
        Assert.True(QuakeLiveModuleCatalog.IsSupportedMapForFactory("aerowalk", "duel"));
    }

    [Fact]
    public void IsSupportedMapForFactory_ReturnsTrue_ForSupportedRaceMap()
    {
        Assert.True(QuakeLiveModuleCatalog.IsSupportedMapForFactory("qzpractice1", "race"));
    }

    [Fact]
    public void GetSupportedFactoriesForMap_ReturnsMatchingFactories()
    {
        var factories = QuakeLiveModuleCatalog.GetSupportedFactoriesForMap("courtyard");

        Assert.Contains("ctf", factories);
        Assert.Contains("race", factories);
        Assert.DoesNotContain("duel", factories);
    }

    [Fact]
    public void NormalizeMapSelection_FiltersMapsUnsupportedByFactory()
    {
        var result = QuakeLiveModuleCatalog.NormalizeMapSelection(
            ["overkill"],
            "ctf",
            fillDefaultsWhenEmpty: false);

        Assert.Empty(result);
    }

    [Fact]
    public void NormalizeMapSelection_FallsBackToRecommendedMaps_WhenOnlyUnsupportedMapsRemain()
    {
        var result = QuakeLiveModuleCatalog.NormalizeMapSelection(
            ["overkill"],
            "ctf",
            fillDefaultsWhenEmpty: true);

        Assert.NotEmpty(result);
        Assert.DoesNotContain("overkill", result);
        Assert.All(result, map => Assert.True(QuakeLiveModuleCatalog.IsSupportedMapForFactory(map, "ctf")));
    }

    [Fact]
    public void GetUnsupportedMapsForFactory_ReturnsExplicitUnsupportedSelection()
    {
        var result = QuakeLiveModuleCatalog.GetUnsupportedMapsForFactory(["overkill", "courtyard"], "ctf");

        Assert.Equal(["overkill"], result);
    }

    [Fact]
    public void AllMaps_ContainsUnionOfAllGroupMaps()
    {
        var groupMaps = QuakeLiveModuleCatalog.MapGroups
            .SelectMany(g => g.Maps)
            .Select(m => m.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(m => m)
            .ToList();

        var allMaps = QuakeLiveModuleCatalog.AllMaps
            .OrderBy(m => m)
            .ToList();

        Assert.Equal(groupMaps, allMaps);
    }
}
