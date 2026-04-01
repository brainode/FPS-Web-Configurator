// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

namespace control_panel.Models;

public sealed class PanelSetting
{
    public int Id { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string JsonContent { get; set; } = "{}";
    public DateTimeOffset UpdatedUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
