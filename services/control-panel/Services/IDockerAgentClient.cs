// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Models;

namespace control_panel.Services;

public interface IDockerAgentClient
{
    Task<ServerStatusSnapshot> GetStatusAsync(string gameKey, CancellationToken cancellationToken = default);
    Task<AgentActionResult> StartAsync(string gameKey, CancellationToken cancellationToken = default);
    Task<AgentActionResult> StartAsync(string gameKey, IReadOnlyDictionary<string, string> env, CancellationToken cancellationToken = default);
    Task<AgentActionResult> StopAsync(string gameKey, CancellationToken cancellationToken = default);
    Task<AgentActionResult> RestartAsync(string gameKey, CancellationToken cancellationToken = default);
    Task<AgentActionResult> RestartAsync(string gameKey, IReadOnlyDictionary<string, string> env, CancellationToken cancellationToken = default);
}
