// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using System.ComponentModel.DataAnnotations;
using control_panel.Models;
using control_panel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace control_panel.Pages.Configuration;

[Authorize]
public sealed class QuakeLiveModel(
    IConfigurationStore configurationStore,
    IDockerAgentClient dockerAgentClient,
    IEnumerable<IGameAdapter> gameAdapters) : PageModel
{
    private readonly IGameAdapter _gameAdapter = gameAdapters.First(a => a.GameKey == "quake-live");
    private string GameKey => _gameAdapter.GameKey;

    [BindProperty]
    public InputModel Input { get; set; } = InputModel.FromSettings(new QuakeLiveServerSettings());

    public IReadOnlyList<QuakeLiveFactoryOption> FactoryOptions => QuakeLiveModuleCatalog.Factories;
    public IReadOnlyList<QuakeLiveMapGroup> MapGroups => QuakeLiveModuleCatalog.MapGroups;

    public ServerStatusSnapshot Status { get; private set; } = ServerStatusSnapshot.NotConfigured("quake-live");
    public string StatusToneClass => Status.StatusToneClass;
    public bool CanStart => Status.CanStart;
    public bool CanRestart => Status.CanRestart;
    public bool CanStop => Status.CanStop;
    public bool ShowUnavailableActions => Status.ShowUnavailableActions;

    public string CurrentRconPassword { get; private set; } = string.Empty;
    public string CurrentServerPassword { get; private set; } = string.Empty;
    public bool HasConfiguredRconPassword => !string.IsNullOrWhiteSpace(CurrentRconPassword);
    public string RconPasswordStateLabel => HasConfiguredRconPassword ? "Configured" : "Required";
    public string RconPasswordStateClass => HasConfiguredRconPassword ? "status-running" : "status-stopped";
    public bool HasJoinPassword => !string.IsNullOrWhiteSpace(CurrentServerPassword);
    public string JoinPasswordStateLabel => HasJoinPassword ? "Protected lobby" : "Open lobby";
    public string JoinPasswordStateClass => HasJoinPassword ? "status-running" : "status-neutral";
    public string MaskedRconPassword => PanelHelpers.MaskSecret(CurrentRconPassword);
    public string MaskedJoinPassword => PanelHelpers.MaskSecret(CurrentServerPassword);

    public string UpdatedLabel { get; private set; } = "Never";
    public string UpdatedByLabel { get; private set; } = "System";

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public bool IsMapSelected(string mapKey) =>
        Input.SelectedMaps.Contains(mapKey, StringComparer.OrdinalIgnoreCase);

    public bool IsMapSupportedForSelectedFactory(string mapKey) =>
        QuakeLiveModuleCatalog.IsSupportedMapForFactory(mapKey, Input.Factory);

    public IReadOnlyList<string> GetSupportedFactoriesForMap(string mapKey) =>
        QuakeLiveModuleCatalog.GetSupportedFactoriesForMap(mapKey);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostStartAsync(CancellationToken cancellationToken)
    {
        var configuration = await configurationStore.GetOrCreateAsync(GameKey, cancellationToken);
        var env = _gameAdapter.GetContainerEnv(configuration.JsonContent);
        var result = await dockerAgentClient.StartAsync(GameKey, env, cancellationToken);
        StoreResult(result);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRestartAsync(CancellationToken cancellationToken)
    {
        var configuration = await configurationStore.GetOrCreateAsync(GameKey, cancellationToken);
        var env = _gameAdapter.GetContainerEnv(configuration.JsonContent);
        var result = await dockerAgentClient.RestartAsync(GameKey, env, cancellationToken);
        StoreResult(result);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostStopAsync(CancellationToken cancellationToken)
    {
        var result = await dockerAgentClient.StopAsync(GameKey, cancellationToken);
        StoreResult(result);
        return RedirectToPage();
    }

    public Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken) =>
        HandleSaveAsync(restartServer: false, cancellationToken);

    public Task<IActionResult> OnPostApplyAsync(CancellationToken cancellationToken) =>
        HandleSaveAsync(restartServer: true, cancellationToken);

    private async Task<IActionResult> HandleSaveAsync(bool restartServer, CancellationToken cancellationToken)
    {
        var existingConfiguration = await configurationStore.GetOrCreateAsync(GameKey, cancellationToken);
        var existingSettings = QuakeLiveConfigurationSerializer.Deserialize(existingConfiguration.JsonContent);
        var unsupportedMaps = QuakeLiveModuleCatalog.GetUnsupportedMapsForFactory(Input.SelectedMaps, Input.Factory);

        NormalizeInput(fillDefaultsWhenEmpty: false);
        var effectiveSettings = Input.ToSettings(existingSettings);

        ModelState.ClearValidationState(nameof(Input));
        TryValidateModel(Input, nameof(Input));
        ValidateInput(effectiveSettings, unsupportedMaps);

        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken, preserveInput: true);
            return Page();
        }

        var jsonContent = QuakeLiveConfigurationSerializer.Serialize(effectiveSettings);
        await configurationStore.SaveAsync(GameKey, jsonContent, User.Identity?.Name ?? "unknown", cancellationToken);

        if (!restartServer)
        {
            SuccessMessage = "Quake Live settings saved.";
            return RedirectToPage();
        }

        var env = _gameAdapter.GetContainerEnv(jsonContent);
        var result = await dockerAgentClient.RestartAsync(GameKey, env, cancellationToken);

        if (result.Success)
        {
            SuccessMessage = "Settings saved and restart requested.";
        }
        else
        {
            ErrorMessage = $"Settings saved, but restart failed: {result.Message}";
        }

        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken, bool preserveInput = false)
    {
        Status = await dockerAgentClient.GetStatusAsync(GameKey, cancellationToken);

        var configuration = await configurationStore.GetOrCreateAsync(GameKey, cancellationToken);
        var currentSettings = QuakeLiveConfigurationSerializer.Deserialize(configuration.JsonContent);
        CurrentRconPassword = currentSettings.ZmqRconPassword;
        CurrentServerPassword = currentSettings.ServerPassword;

        if (!preserveInput)
        {
            Input = InputModel.FromSettings(currentSettings);
        }

        NormalizeInput(fillDefaultsWhenEmpty: !preserveInput);

        UpdatedLabel = PanelHelpers.FormatUpdatedLabel(configuration.UpdatedUtc);
        UpdatedByLabel = PanelHelpers.FormatUpdatedByLabel(configuration.UpdatedBy);
    }

    private void NormalizeInput(bool fillDefaultsWhenEmpty)
    {
        Input.SelectedMaps = QuakeLiveModuleCatalog.NormalizeMapSelection(
            Input.SelectedMaps, Input.Factory, fillDefaultsWhenEmpty);
    }

    private void ValidateInput(QuakeLiveServerSettings effectiveSettings, IReadOnlyList<string> unsupportedMaps)
    {
        if (!QuakeLiveModuleCatalog.IsValidFactory(Input.Factory))
        {
            ModelState.AddModelError("Input.Factory", "Choose a supported Quake Live factory.");
        }

        var invalidMaps = Input.SelectedMaps
            .Where(m => !QuakeLiveModuleCatalog.IsValidMap(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (invalidMaps.Length > 0)
        {
            ModelState.AddModelError("Input.SelectedMaps", $"Unsupported maps: {string.Join(", ", invalidMaps)}");
        }

        if (unsupportedMaps.Count > 0)
        {
            ModelState.AddModelError(
                "Input.SelectedMaps",
                $"These maps do not support the '{Input.Factory}' factory in stock Quake Live: {string.Join(", ", unsupportedMaps)}");
        }

        if (Input.SelectedMaps.Count == 0)
        {
            ModelState.AddModelError("Input.SelectedMaps", "Select at least one map for the rotation.");
        }

        if (effectiveSettings.ZmqRconEnabled && string.IsNullOrWhiteSpace(effectiveSettings.ZmqRconPassword))
        {
            ModelState.AddModelError("Input.ZmqRconPassword", "RCON password is required when RCON is enabled.");
        }
    }

    private void StoreResult(AgentActionResult result)
    {
        if (result.Success)
        {
            SuccessMessage = result.Message;
            return;
        }

        ErrorMessage = result.Message;
    }

    public sealed class InputModel
    {
        [Required]
        [StringLength(128)]
        [Display(Name = "Server hostname")]
        public string Hostname { get; set; } = "Quake Live Standalone Test";

        [Required]
        [Display(Name = "Game factory")]
        public string Factory { get; set; } = "duel";

        public List<string> SelectedMaps { get; set; } = ["asylum", "brimstoneabbey", "campgrounds", "purgatory", "theedge"];

        [Range(1, 64)]
        [Display(Name = "Max players")]
        public int MaxClients { get; set; } = 16;

        [Display(Name = "Server visibility")]
        public int ServerType { get; set; } = 2;

        [Display(Name = "Enable ZMQ RCON")]
        public bool ZmqRconEnabled { get; set; }

        [Range(1, 65535)]
        [Display(Name = "RCON port")]
        public int ZmqRconPort { get; set; } = 28960;

        [DataType(DataType.Password)]
        [Display(Name = "RCON password")]
        public string? ZmqRconPassword { get; set; }

        [Display(Name = "Enable ZMQ stats")]
        public bool ZmqStatsEnabled { get; set; }

        [Range(1, 65535)]
        [Display(Name = "Stats port")]
        public int ZmqStatsPort { get; set; } = 27960;

        [DataType(DataType.Password)]
        [Display(Name = "Stats password")]
        public string? ZmqStatsPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Join password")]
        public string? ServerPassword { get; set; }

        [Display(Name = "Clear saved join password")]
        public bool ClearServerPassword { get; set; }

        [StringLength(256)]
        [Display(Name = "Server tags")]
        public string Tags { get; set; } = "standalone";

        public QuakeLiveServerSettings ToSettings(QuakeLiveServerSettings? existing = null)
        {
            return new QuakeLiveServerSettings
            {
                Hostname = Hostname,
                Factory = Factory,
                MapList = SelectedMaps,
                MaxClients = MaxClients,
                ServerType = ServerType,
                ZmqRconEnabled = ZmqRconEnabled,
                ZmqRconPort = ZmqRconPort,
                ZmqRconPassword = string.IsNullOrWhiteSpace(ZmqRconPassword)
                    ? existing?.ZmqRconPassword ?? string.Empty
                    : ZmqRconPassword,
                ZmqStatsEnabled = ZmqStatsEnabled,
                ZmqStatsPort = ZmqStatsPort,
                ZmqStatsPassword = string.IsNullOrWhiteSpace(ZmqStatsPassword)
                    ? existing?.ZmqStatsPassword ?? string.Empty
                    : ZmqStatsPassword,
                ServerPassword = ClearServerPassword
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(ServerPassword)
                        ? existing?.ServerPassword ?? string.Empty
                        : ServerPassword,
                Tags = Tags,
            };
        }

        public static InputModel FromSettings(QuakeLiveServerSettings settings)
        {
            return new InputModel
            {
                Hostname = settings.Hostname,
                Factory = settings.Factory,
                SelectedMaps = settings.MapList.ToList(),
                MaxClients = settings.MaxClients,
                ServerType = settings.ServerType,
                ZmqRconEnabled = settings.ZmqRconEnabled,
                ZmqRconPort = settings.ZmqRconPort,
                ZmqRconPassword = settings.ZmqRconPassword,
                ZmqStatsEnabled = settings.ZmqStatsEnabled,
                ZmqStatsPort = settings.ZmqStatsPort,
                ZmqStatsPassword = string.Empty,
                ServerPassword = string.Empty,
                ClearServerPassword = false,
                Tags = settings.Tags,
            };
        }
    }
}
