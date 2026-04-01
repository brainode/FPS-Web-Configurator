// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

namespace control_panel.Services;

public static class PanelStoragePathResolver
{
    public static string ResolveRootPath(string contentRootPath, string? configuredRootPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredRootPath))
        {
            return Path.GetFullPath(configuredRootPath);
        }

        if (File.Exists(Path.Combine(contentRootPath, "TASKS.md")) &&
            Directory.Exists(Path.Combine(contentRootPath, "services")))
        {
            return Path.GetFullPath(Path.Combine(contentRootPath, "data", "control-panel"));
        }

        if (File.Exists(Path.Combine(contentRootPath, "control-panel.csproj")) &&
            Directory.Exists(Path.Combine(contentRootPath, "Pages")))
        {
            return Path.GetFullPath(Path.Combine(contentRootPath, "..", "..", "data", "control-panel"));
        }

        return Path.GetFullPath(Path.Combine(contentRootPath, "data", "control-panel"));
    }
}
