// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class ReflexArenaModuleCatalogTests
{
    [Fact]
    public void AllModes_HaveNonEmptyLabelsAndDescriptions()
    {
        Assert.All(ReflexArenaModuleCatalog.Modes, mode =>
        {
            Assert.NotEmpty(mode.Label);
            Assert.NotEmpty(mode.Description);
        });
    }

    [Theory]
    [InlineData("1v1", "Fusion")]
    [InlineData("2v2", "Fusion")]
    [InlineData("ffa", "Phobos")]
    [InlineData("tdm", "Fusion")]
    [InlineData("ctf", "SkyTemples")]
    [InlineData("race", "SkyTemples")]
    [InlineData("training", "training_combat_stage_1")]
    public void Modes_UseValidRecommendedMaps(string modeKey, string recommendedMap)
    {
        Assert.True(ReflexArenaModuleCatalog.IsValidMode(modeKey));
        Assert.True(ReflexArenaModuleCatalog.IsValidMap(recommendedMap));
        Assert.Equal(recommendedMap, ReflexArenaModuleCatalog.GetRecommendedMap(modeKey));
    }

    [Theory]
    [InlineData("Fusion", "1v1", true)]
    [InlineData("SkyTemples", "ctf", true)]
    [InlineData("SkyTemples", "race", true)]
    [InlineData("Phobos", "ffa", true)]
    [InlineData("training_combat_stage_1", "training", true)]
    [InlineData("Aerowalk", "ctf", false)]
    [InlineData("Hieratic", "1v1", false)]
    public void IsSupportedMapForMode_UsesCatalogRules(string mapKey, string modeKey, bool expected)
    {
        Assert.Equal(expected, ReflexArenaModuleCatalog.IsSupportedMapForMode(mapKey, modeKey));
    }

    [Fact]
    public void NormalizeMutatorSelection_FiltersInvalidEntries()
    {
        var normalized = ReflexArenaModuleCatalog.NormalizeMutatorSelection(
            ["instagib", "lowgravity", "unknown", "instagib", "arena"]);

        Assert.Equal(["instagib", "lowgravity", "arena"], normalized);
    }

    [Fact]
    public void Catalog_ExposesExpandedStockMutators()
    {
        Assert.NotNull(ReflexArenaModuleCatalog.FindMutator("arena"));
        Assert.NotNull(ReflexArenaModuleCatalog.FindMutator("bighead"));
        Assert.NotNull(ReflexArenaModuleCatalog.FindMutator("handicap"));
        Assert.NotNull(ReflexArenaModuleCatalog.FindMutator("instagib"));
        Assert.NotNull(ReflexArenaModuleCatalog.FindMutator("lowgravity"));
        Assert.NotNull(ReflexArenaModuleCatalog.FindMutator("meleeonly"));
        Assert.NotNull(ReflexArenaModuleCatalog.FindMutator("vampire"));
        Assert.NotNull(ReflexArenaModuleCatalog.FindMutator("warmup"));
    }

    [Fact]
    public void GetMapLabel_HumanizesStockMapNames()
    {
        Assert.Equal("Sky Temples", ReflexArenaModuleCatalog.GetMapLabel("SkyTemples"));
        Assert.Equal("Training Combat Stage 1", ReflexArenaModuleCatalog.GetMapLabel("training_combat_stage_1"));
    }

    [Fact]
    public void WorkshopMapMetadata_UsesCatalogAsSingleSourceOfTruth()
    {
        var aerowalk = ReflexArenaModuleCatalog.FindMap("Aerowalk");
        var workshopMap = ReflexArenaModuleCatalog.FindMapByWorkshopId("608517732");

        Assert.NotNull(aerowalk);
        Assert.Equal("608517732", aerowalk!.WorkshopId);
        Assert.False(aerowalk.BuiltIn);
        Assert.NotNull(workshopMap);
        Assert.Equal("Aerowalk", workshopMap!.Key);
        Assert.True(ReflexArenaModuleCatalog.UsesWorkshopStartup("Aerowalk"));
        Assert.False(ReflexArenaModuleCatalog.UsesWorkshopStartup("Fusion"));
    }

    [Fact]
    public void GetSupportedMapsForMode_ReturnsCatalogOrderedMaps()
    {
        var maps = ReflexArenaModuleCatalog.GetSupportedMapsForMode("tdm");

        Assert.Contains("Fusion", maps);
        Assert.Contains("SkyTemples", maps);
        Assert.DoesNotContain("AbandonedShelter", maps);
    }

    [Fact]
    public void GetSupportedModesForMap_ReturnsMatchingStockModes()
    {
        var modes = ReflexArenaModuleCatalog.GetSupportedModesForMap("Aerowalk");

        Assert.Contains("1v1", modes);
        Assert.Contains("2v2", modes);
        Assert.Contains("tdm", modes);
        Assert.DoesNotContain("ctf", modes);
    }
}
