namespace control_panel.Models;

public sealed record ServerStatusSnapshot(
    string GameKey,
    string State,
    string StateLabel,
    string Message,
    string SourceLabel,
    DateTimeOffset CheckedAtUtc)
{
    public string StatusToneClass => State switch
    {
        "running"    => "status-running",
        "stopped"    => "status-stopped",
        "restarting" => "status-restarting",
        _            => "status-neutral"
    };

    public bool CanStart    => State == "stopped";
    public bool CanRestart  => State == "running";
    public bool CanStop     => State is "running" or "restarting";
    public bool ShowUnavailableActions => !CanStart && !CanRestart && !CanStop;

    public static ServerStatusSnapshot NotConfigured(string gameKey) =>
        new(
            gameKey,
            "agent-not-configured",
            "Agent not configured",
            "Configure DockerAgent:BaseUrl to enable server actions.",
            "No docker-agent endpoint",
            DateTimeOffset.UtcNow);
}
