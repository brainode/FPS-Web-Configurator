// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

namespace control_panel.Options;

public sealed class DockerAgentOptions
{
    public string? BaseUrl { get; set; }
    public int TimeoutSeconds { get; set; } = 5;
}
