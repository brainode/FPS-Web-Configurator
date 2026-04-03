// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

namespace control_panel.Models;

public sealed class SavedRuleset
{
    public int Id { get; set; }
    public string GameKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string JsonContent { get; set; } = "{}";
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}
