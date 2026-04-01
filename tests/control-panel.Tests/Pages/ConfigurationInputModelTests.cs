using control_panel.Models;
using control_panel.Pages.Configuration;

namespace control_panel.Tests.Pages;

public sealed class WarsowInputModelTests
{
    [Fact]
    public void ToSettings_KeepsExistingRconPassword_WhenFieldIsBlank()
    {
        var model = new WarsowModel.InputModel
        {
            Gametype = "ca",
            StartMap = "wca1",
            SelectedMaps = ["wca1"],
            RconPassword = string.Empty
        };

        var settings = model.ToSettings(new WarsowServerSettings
        {
            RconPassword = "saved-rcon"
        });

        Assert.Equal("saved-rcon", settings.RconPassword);
    }

    [Fact]
    public void ToSettings_ClearsJoinPassword_OnlyWhenExplicitlyRequested()
    {
        var model = new WarsowModel.InputModel
        {
            Gametype = "ca",
            StartMap = "wca1",
            SelectedMaps = ["wca1"],
            ClearServerPassword = true
        };

        var settings = model.ToSettings(new WarsowServerSettings
        {
            RconPassword = "saved-rcon",
            ServerPassword = "saved-join"
        });

        Assert.Equal(string.Empty, settings.ServerPassword);
    }

    [Fact]
    public void FromSettings_PrefillsRconPassword()
    {
        var model = WarsowModel.InputModel.FromSettings(new WarsowServerSettings
        {
            RconPassword = "super-secret"
        });

        Assert.Equal("super-secret", model.RconPassword);
    }

    [Fact]
    public void FromSettings_DoesNotPrefillServerPassword()
    {
        var model = WarsowModel.InputModel.FromSettings(new WarsowServerSettings
        {
            RconPassword = "rcon",
            ServerPassword = "join-secret"
        });

        Assert.True(string.IsNullOrEmpty(model.ServerPassword));
        Assert.False(model.ClearServerPassword);
    }
}
