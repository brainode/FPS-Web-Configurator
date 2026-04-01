// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

namespace control_panel.Options;

public sealed class PanelAuthOptions
{
    public string SeedAdminUsername { get; set; } = "admin";
    public string SeedAdminPassword { get; set; } = "change-me";
}
