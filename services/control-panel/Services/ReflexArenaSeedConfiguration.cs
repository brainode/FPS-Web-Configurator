// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Models;

namespace control_panel.Services;

public static class ReflexArenaSeedConfiguration
{
    public static string CreateDefaultJson() => ReflexArenaConfigurationSerializer.Serialize(
        new ReflexArenaServerSettings
        {
            Hostname = "Reflex Arena Docker Server",
            Mode = "1v1",
            StartMap = ReflexArenaModuleCatalog.GetRecommendedMap("1v1"),
            Mutators = [],
            MaxClients = 8,
            SteamEnabled = true,
            Country = string.Empty,
            TimeLimitOverride = 0,
            ServerPassword = string.Empty,
            RefPassword = string.Empty,
        });
}
