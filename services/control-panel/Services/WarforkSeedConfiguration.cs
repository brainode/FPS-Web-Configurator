using control_panel.Models;

namespace control_panel.Services;

public static class WarforkSeedConfiguration
{
    public static string CreateDefaultJson() => WarforkConfigurationSerializer.Serialize(new WarforkServerSettings());
}
