using System.Text.Json;
using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class WarsowSeedConfigurationTests
{
    [Fact]
    public void CreateDefaultJson_ContainsExpectedWarsowKeys()
    {
        var json = WarsowSeedConfiguration.CreateDefaultJson();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("wca1", root.GetProperty("sv_defaultmap").GetString());
        Assert.Equal("ca", root.GetProperty("g_gametype").GetString());
        Assert.Equal("0", root.GetProperty("g_instagib").GetString());
        Assert.Equal("11", root.GetProperty("g_scorelimit").GetString());
        Assert.Equal(string.Empty, root.GetProperty("rcon_password").GetString());
    }
}
