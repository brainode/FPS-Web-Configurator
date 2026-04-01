namespace control_panel.Options;

public sealed class DockerAgentOptions
{
    public string? BaseUrl { get; set; }
    public int TimeoutSeconds { get; set; } = 5;
}
