using control_panel.Models;

namespace control_panel.Services;

public interface IGameAdapter
{
    string GameKey { get; }
    string DisplayName { get; }
    string ConfigurationPagePath { get; }
    GameSummary GetSummary(string? jsonSettings);
    IReadOnlyDictionary<string, string> GetContainerEnv(string? jsonSettings);
    string CreateDefaultJson();
}
