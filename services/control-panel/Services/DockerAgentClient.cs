// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using System.Net.Http.Json;
using control_panel.Models;
using control_panel.Options;
using Microsoft.Extensions.Options;

namespace control_panel.Services;

public sealed class DockerAgentClient(
    HttpClient httpClient,
    IOptions<DockerAgentOptions> options,
    ILogger<DockerAgentClient> logger) : IDockerAgentClient
{
    private readonly DockerAgentOptions _options = options.Value;

    public async Task<ServerStatusSnapshot> GetStatusAsync(string gameKey, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            return ServerStatusSnapshot.NotConfigured(gameKey);
        }

        try
        {
            using var response = await httpClient.GetAsync($"/api/games/{gameKey}/status", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ServerStatusSnapshot(
                    gameKey,
                    "agent-error",
                    "Agent error",
                    $"docker-agent returned HTTP {(int)response.StatusCode}.",
                    SafeBaseUrl(),
                    DateTimeOffset.UtcNow);
            }

            var payload = await response.Content.ReadFromJsonAsync<ServerStatusSnapshot>(cancellationToken: cancellationToken);
            return payload ?? ServerStatusSnapshot.NotConfigured(gameKey);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to query docker-agent status for {GameKey}.", gameKey);
            return new ServerStatusSnapshot(
                gameKey,
                "agent-unreachable",
                "Agent unreachable",
                exception.Message,
                SafeBaseUrl(),
                DateTimeOffset.UtcNow);
        }
    }

    public Task<AgentActionResult> StartAsync(string gameKey, CancellationToken cancellationToken = default) =>
        SendActionAsync(gameKey, "start", null, cancellationToken);

    public Task<AgentActionResult> StartAsync(string gameKey, IReadOnlyDictionary<string, string> env, CancellationToken cancellationToken = default) =>
        SendActionAsync(gameKey, "start", env, cancellationToken);

    public Task<AgentActionResult> StopAsync(string gameKey, CancellationToken cancellationToken = default) =>
        SendActionAsync(gameKey, "stop", null, cancellationToken);

    public Task<AgentActionResult> RestartAsync(string gameKey, CancellationToken cancellationToken = default) =>
        SendActionAsync(gameKey, "restart", null, cancellationToken);

    public Task<AgentActionResult> RestartAsync(string gameKey, IReadOnlyDictionary<string, string> env, CancellationToken cancellationToken = default) =>
        SendActionAsync(gameKey, "restart", env, cancellationToken);

    private async Task<AgentActionResult> SendActionAsync(
        string gameKey,
        string action,
        IReadOnlyDictionary<string, string>? env,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured())
        {
            return new AgentActionResult(
                false,
                gameKey,
                action,
                $"docker-agent is not configured. Set DockerAgent:BaseUrl before using '{action}'.",
                DateTimeOffset.UtcNow);
        }

        try
        {
            // StringContent buffers the body upfront, ensuring Content-Length is always set.
            // JsonContent.Create uses lazy serialization which may trigger chunked encoding.
            HttpContent? content = env?.Count > 0
                ? new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(new { env }),
                    System.Text.Encoding.UTF8,
                    "application/json")
                : null;

            using var response = await httpClient.PostAsync($"/api/games/{gameKey}/{action}", content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new AgentActionResult(
                    true,
                    gameKey,
                    action,
                    $"Action '{action}' has been sent to docker-agent for '{gameKey}'.",
                    DateTimeOffset.UtcNow);
            }

            return new AgentActionResult(
                false,
                gameKey,
                action,
                $"docker-agent returned HTTP {(int)response.StatusCode} for '{action}'.",
                DateTimeOffset.UtcNow);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "docker-agent action '{Action}' failed for {GameKey}.", action, gameKey);
            return new AgentActionResult(
                false,
                gameKey,
                action,
                exception.Message,
                DateTimeOffset.UtcNow);
        }
    }

    private bool IsConfigured() => Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _);

    private string SafeBaseUrl() => string.IsNullOrWhiteSpace(_options.BaseUrl) ? "No docker-agent endpoint" : _options.BaseUrl!;
}
