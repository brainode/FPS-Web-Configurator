namespace control_panel.Models;

public sealed record GameSummary(
    string ModeName,
    string ModeFlags,
    string StartMap,
    string MapCountLabel,
    string RotationPreview,
    string LimitsSummary,
    string AccessLabel,
    string RconLabel);
