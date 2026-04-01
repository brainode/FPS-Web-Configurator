using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class WarsowModuleCatalogTests
{
    [Fact]
    public void ResolveStartMap_ReturnsFirstSelectedMap_WhenCurrentStartMapIsOutsidePool()
    {
        var result = WarsowModuleCatalog.ResolveStartMap(
            "wctf1",
            ["wdm4", "wdm7", "wdm9"],
            gametype: "dm");

        Assert.Equal("wdm4", result);
    }

    [Fact]
    public void ResolveStartMap_ReturnsEmpty_WhenPoolIsEmptyAndAllowEmptyIsTrue()
    {
        var result = WarsowModuleCatalog.ResolveStartMap(
            startMap: "wca1",
            selectedMaps: [],
            gametype: "ca",
            allowEmpty: true);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void FindGametype_ReturnsNull_ForUnknownKey()
    {
        Assert.Null(WarsowModuleCatalog.FindGametype("unknown"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void FindGametype_ReturnsNull_ForNullOrWhitespace(string? key)
    {
        Assert.Null(WarsowModuleCatalog.FindGametype(key));
    }

    [Theory]
    [InlineData("ca",         11,  0)]
    [InlineData("dm",          0, 15)]
    [InlineData("duel",        0, 10)]
    [InlineData("ffa",         0, 15)]
    [InlineData("tdm",         0, 20)]
    [InlineData("ctf",         0, 20)]
    [InlineData("ctftactics",  0, 20)]
    [InlineData("bomb",       16,  0)]
    [InlineData("da",         11,  0)]
    [InlineData("headhunt",    0, 15)]
    [InlineData("race",        0,  0)]
    public void FindGametype_ReturnsDistributionDefaults_MatchingGametypeCfg(
        string key, int expectedScorelimit, int expectedTimelimit)
    {
        var option = WarsowModuleCatalog.FindGametype(key);

        Assert.NotNull(option);
        Assert.Equal(expectedScorelimit, option.DefaultScorelimit);
        Assert.Equal(expectedTimelimit, option.DefaultTimelimit);
    }

    [Fact]
    public void FindGametype_IsCaseInsensitive()
    {
        var option = WarsowModuleCatalog.FindGametype("CA");

        Assert.NotNull(option);
        Assert.Equal("ca", option.Key);
    }

    [Fact]
    public void AllGametypes_HaveNonEmptyLabels()
    {
        Assert.All(WarsowModuleCatalog.Gametypes, g => Assert.NotEmpty(g.Label));
    }

    [Fact]
    public void AllGametypes_HaveAtLeastOneRecommendedMap()
    {
        // race has wrace1, all others have a non-empty list too
        Assert.All(WarsowModuleCatalog.Gametypes, g => Assert.NotEmpty(g.RecommendedMaps));
    }
}
