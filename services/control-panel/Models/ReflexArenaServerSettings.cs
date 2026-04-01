namespace control_panel.Models;

public sealed class ReflexArenaServerSettings
{
    public string Hostname { get; set; } = "Reflex Arena Docker Server";
    public string Mode { get; set; } = "1v1";
    public string StartMap { get; set; } = "Fusion";
    public List<string> Mutators { get; set; } = [];
    public int MaxClients { get; set; } = 8;
    public bool SteamEnabled { get; set; } = true;
    public string Country { get; set; } = string.Empty;
    public int TimeLimitOverride { get; set; }
    public string ServerPassword { get; set; } = string.Empty;
    public string RefPassword { get; set; } = string.Empty;
}
