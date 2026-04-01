// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

namespace control_panel.Models;

public sealed class QuakeLiveServerSettings
{
    public string Hostname { get; set; } = "Quake Live Standalone Test";
    public string Factory { get; set; } = "duel";
    public List<string> MapList { get; set; } = ["asylum", "brimstoneabbey", "campgrounds", "purgatory", "theedge"];
    public int MaxClients { get; set; } = 16;
    public int ServerType { get; set; } = 2;   // 0 = offline, 1 = LAN, 2 = internet
    public bool ZmqRconEnabled { get; set; }
    public int ZmqRconPort { get; set; } = 28960;
    public string ZmqRconPassword { get; set; } = string.Empty;
    public bool ZmqStatsEnabled { get; set; }
    public int ZmqStatsPort { get; set; } = 27960;
    public string ZmqStatsPassword { get; set; } = string.Empty;
    public string ServerPassword { get; set; } = string.Empty;
    public string Tags { get; set; } = "standalone";
}
