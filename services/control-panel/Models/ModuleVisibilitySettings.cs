// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

namespace control_panel.Models;

public sealed class ModuleVisibilitySettings
{
    public List<string> EnabledGameKeys { get; set; } = [];
}
