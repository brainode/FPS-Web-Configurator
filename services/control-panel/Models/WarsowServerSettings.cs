// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

namespace control_panel.Models;

public sealed class WarsowServerSettings
{
    public string StartMap { get; set; } = "wca1";
    public List<string> MapList { get; set; } = ["wca1"];
    public string Gametype { get; set; } = "ca";
    public bool Instagib { get; set; }
    public bool Instajump { get; set; }
    public bool Instashield { get; set; }
    public int Scorelimit { get; set; } = 11;
    public int Timelimit { get; set; }
    public string RconPassword { get; set; } = string.Empty;
    public string ServerPassword { get; set; } = string.Empty;
}
