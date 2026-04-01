using control_panel.Models;

namespace control_panel.Services;

public static class WarsowSeedConfiguration
{
    public static string CreateDefaultJson() => WarsowConfigurationSerializer.Serialize(new WarsowServerSettings());
}
