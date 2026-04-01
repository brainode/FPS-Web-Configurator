namespace control_panel.Models;

public sealed record AgentActionResult(
    bool Success,
    string GameKey,
    string Action,
    string Message,
    DateTimeOffset PerformedAtUtc);
