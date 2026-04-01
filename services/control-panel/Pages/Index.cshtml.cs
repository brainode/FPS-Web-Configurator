using control_panel.Models;
using control_panel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace control_panel.Pages;

[Authorize]
public sealed class IndexModel(
    ILogger<IndexModel> logger,
    IConfigurationStore configurationStore,
    IDockerAgentClient dockerAgentClient,
    IModuleVisibilityService moduleVisibilityService,
    IEnumerable<IGameAdapter> gameAdapters) : PageModel
{
    private readonly IReadOnlyDictionary<string, IGameAdapter> _gameAdapters = gameAdapters
        .ToDictionary(adapter => adapter.GameKey, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<DashboardModule> Modules { get; private set; } = [];
    public int RunningModuleCount => Modules.Count(module => module.Status.State == "running");
    public int TotalModuleCount => Modules.Count;
    public string HeaderStatusLabel => TotalModuleCount == 0
        ? "No modules enabled"
        : $"{RunningModuleCount} of {TotalModuleCount} server modules running";

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostStartAsync(string gameKey, CancellationToken cancellationToken)
    {
        var result = await ExecuteActionAsync(gameKey, "start", cancellationToken);
        StoreResult(result);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRestartAsync(string gameKey, CancellationToken cancellationToken)
    {
        var result = await ExecuteActionAsync(gameKey, "restart", cancellationToken);
        StoreResult(result);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostStopAsync(string gameKey, CancellationToken cancellationToken)
    {
        var result = await ExecuteActionAsync(gameKey, "stop", cancellationToken);
        StoreResult(result);
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var modules = new List<DashboardModule>();
        var visibleModules = await moduleVisibilityService.GetVisibleModulesAsync(cancellationToken);

        foreach (var module in visibleModules)
        {
            if (!_gameAdapters.TryGetValue(module.GameKey, out var adapter))
            {
                continue;
            }

            var configurationTask = configurationStore.GetOrCreateAsync(module.GameKey, cancellationToken);
            var statusTask = dockerAgentClient.GetStatusAsync(module.GameKey, cancellationToken);
            await Task.WhenAll(configurationTask, statusTask);

            var configuration = await configurationTask;
            var status = await statusTask;

            modules.Add(new DashboardModule(
                module.GameKey,
                module.DisplayName,
                module.ConfigurationPagePath,
                status,
                adapter.GetSummary(configuration.JsonContent),
                PanelHelpers.FormatUpdatedLabel(configuration.UpdatedUtc),
                PanelHelpers.FormatUpdatedByLabel(configuration.UpdatedBy)));
        }

        Modules = modules;
    }

    private async Task<AgentActionResult> ExecuteActionAsync(string gameKey, string action, CancellationToken cancellationToken)
    {
        if (!_gameAdapters.TryGetValue(gameKey, out var adapter))
        {
            return new AgentActionResult(
                false,
                gameKey,
                action,
                $"Unknown game module '{gameKey}'.",
                DateTimeOffset.UtcNow);
        }

        if (string.Equals(action, "stop", StringComparison.OrdinalIgnoreCase))
        {
            return await dockerAgentClient.StopAsync(gameKey, cancellationToken);
        }

        var configuration = await configurationStore.GetOrCreateAsync(gameKey, cancellationToken);
        var env = adapter.GetContainerEnv(configuration.JsonContent);

        return string.Equals(action, "restart", StringComparison.OrdinalIgnoreCase)
            ? await dockerAgentClient.RestartAsync(gameKey, env, cancellationToken)
            : await dockerAgentClient.StartAsync(gameKey, env, cancellationToken);
    }

    private void StoreResult(AgentActionResult result)
    {
        if (result.Success)
        {
            SuccessMessage = result.Message;
            return;
        }

        ErrorMessage = result.Message;
        logger.LogWarning("Agent action {Action} failed for {GameKey}: {Message}", result.Action, result.GameKey, result.Message);
    }

    public sealed record DashboardModule(
        string GameKey,
        string DisplayName,
        string ConfigurationPagePath,
        ServerStatusSnapshot Status,
        GameSummary Summary,
        string ConfigurationUpdatedLabel,
        string ConfigurationUpdatedByLabel)
    {
        public string StatusToneClass => Status.StatusToneClass;
        public bool CanStart => Status.CanStart;
        public bool CanRestart => Status.CanRestart;
        public bool CanStop => Status.CanStop;
        public bool ShowUnavailableActions => Status.ShowUnavailableActions;
        public string ActionHint => Status.State switch
        {
            "agent-not-configured" => "Server actions will appear here after docker-agent is configured.",
            "agent-unreachable" => "docker-agent is unreachable right now, so server actions are temporarily unavailable.",
            "agent-error" => "docker-agent returned an error. Check the agent before sending a new command.",
            "restarting" => "The server is restarting. You can stop it if you need to interrupt the restart.",
            _ => "Actions adapt to the current server state."
        };
    }
}
