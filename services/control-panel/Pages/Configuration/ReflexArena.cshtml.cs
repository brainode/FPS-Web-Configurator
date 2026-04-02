// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using control_panel.Models;
using control_panel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace control_panel.Pages.Configuration;

[Authorize]
public sealed class ReflexArenaModel(
    IConfigurationStore configurationStore,
    IDockerAgentClient dockerAgentClient,
    IEnumerable<IGameAdapter> gameAdapters) : PageModel
{
    private static readonly Regex CountryCodePattern = new("^[A-Za-z]{2,3}$", RegexOptions.Compiled);
    private readonly IGameAdapter _gameAdapter = gameAdapters.First(adapter => adapter.GameKey == "reflex-arena");
    private string GameKey => _gameAdapter.GameKey;

    [BindProperty]
    public InputModel Input { get; set; } = InputModel.FromSettings(new ReflexArenaServerSettings());

    public IReadOnlyList<ReflexArenaModeOption> ModeOptions => ReflexArenaModuleCatalog.Modes;
    public IReadOnlyList<ReflexArenaMutatorOption> MutatorOptions => ReflexArenaModuleCatalog.Mutators;
    public IReadOnlyList<ReflexArenaMapGroup> MapGroups => ReflexArenaModuleCatalog.MapGroups;

    public ServerStatusSnapshot Status { get; private set; } = ServerStatusSnapshot.NotConfigured("reflex-arena");
    public string StatusToneClass => Status.StatusToneClass;
    public bool CanStart => Status.CanStart;
    public bool CanRestart => Status.CanRestart;
    public bool CanStop => Status.CanStop;
    public bool ShowUnavailableActions => Status.ShowUnavailableActions;

    public string CurrentRefPassword { get; private set; } = string.Empty;
    public string CurrentServerPassword { get; private set; } = string.Empty;
    public bool HasConfiguredRefPassword => !string.IsNullOrWhiteSpace(CurrentRefPassword);
    public string RefPasswordStateLabel => HasConfiguredRefPassword ? "Configured" : "Optional";
    public string RefPasswordStateClass => HasConfiguredRefPassword ? "status-running" : "status-neutral";
    public bool HasJoinPassword => !string.IsNullOrWhiteSpace(CurrentServerPassword);
    public string JoinPasswordStateLabel => HasJoinPassword ? "Protected lobby" : "Open lobby";
    public string JoinPasswordStateClass => HasJoinPassword ? "status-running" : "status-neutral";

    public string UpdatedLabel { get; private set; } = "Never";
    public string UpdatedByLabel { get; private set; } = "System";

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public bool IsMutatorSelected(string mutatorKey) =>
        Input.SelectedMutators.Contains(mutatorKey, StringComparer.OrdinalIgnoreCase);

    public bool IsMapSupportedForSelectedMode(string mapKey) =>
        ReflexArenaModuleCatalog.IsSupportedMapForMode(mapKey, Input.Mode);

    public IReadOnlyList<string> GetSupportedModesForMap(string mapKey) =>
        ReflexArenaModuleCatalog.GetSupportedModesForMap(mapKey);

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
        var existingSettings = ReflexArenaConfigurationSerializer.Deserialize(existingConfiguration.JsonContent);

        NormalizeInput();
        var effectiveSettings = Input.ToSettings(existingSettings);

        ModelState.ClearValidationState(nameof(Input));
        TryValidateModel(Input, nameof(Input));
        ValidateInput();

        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken, preserveInput: true);
            return Page();
        }

        var jsonContent = ReflexArenaConfigurationSerializer.Serialize(effectiveSettings);
        await configurationStore.SaveAsync(GameKey, jsonContent, User.Identity?.Name ?? "unknown", cancellationToken);

        if (!restartServer)
        {
            SuccessMessage = "Reflex Arena settings saved.";
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
        var currentSettings = ReflexArenaConfigurationSerializer.Deserialize(configuration.JsonContent);
        CurrentRefPassword = currentSettings.RefPassword;
        CurrentServerPassword = currentSettings.ServerPassword;

        if (!preserveInput)
        {
            Input = InputModel.FromSettings(currentSettings);
        }

        NormalizeInput();

        UpdatedLabel = PanelHelpers.FormatUpdatedLabel(configuration.UpdatedUtc);
        UpdatedByLabel = PanelHelpers.FormatUpdatedByLabel(configuration.UpdatedBy);
    }

    private void NormalizeInput()
    {
        Input.StartMap = ReflexArenaModuleCatalog.ResolveStartMap(Input.StartMap, Input.Mode);
        Input.SelectedMutators = ReflexArenaModuleCatalog.NormalizeMutatorSelection(Input.SelectedMutators);
        Input.Country = Input.Country.Trim().ToUpperInvariant();
    }

    private void ValidateInput()
    {
        if (!ReflexArenaModuleCatalog.IsValidMode(Input.Mode))
        {
            ModelState.AddModelError("Input.Mode", "Choose a supported Reflex Arena mode.");
        }

        if (!ReflexArenaModuleCatalog.IsValidMap(Input.StartMap))
        {
            ModelState.AddModelError("Input.StartMap", "Choose a valid Reflex Arena map.");
        }
        else if (!ReflexArenaModuleCatalog.IsSupportedMapForMode(Input.StartMap, Input.Mode))
        {
            ModelState.AddModelError(
                "Input.StartMap",
                $"Map '{ReflexArenaModuleCatalog.GetMapLabel(Input.StartMap)}' does not support the selected mode.");
        }

        var invalidMutators = Input.SelectedMutators
            .Where(mutator => !ReflexArenaModuleCatalog.IsValidMutator(mutator))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (invalidMutators.Length > 0)
        {
            ModelState.AddModelError("Input.SelectedMutators", $"Unsupported mutators: {string.Join(", ", invalidMutators)}");
        }

        if (!string.IsNullOrWhiteSpace(Input.Country) &&
            !CountryCodePattern.IsMatch(Input.Country))
        {
            ModelState.AddModelError("Input.Country", "Country must be a 2-3 letter code such as RU or USA.");
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
        public string Hostname { get; set; } = "Reflex Arena Docker Server";

        [Required]
        [Display(Name = "Game mode")]
        public string Mode { get; set; } = "1v1";

        [Required]
        [Display(Name = "Opening map")]
        public string StartMap { get; set; } = "Fusion";

        public List<string> SelectedMutators { get; set; } = [];

        [Range(1, 64)]
        [Display(Name = "Max players")]
        public int MaxClients { get; set; } = 8;

        [Display(Name = "Publish to Steam browser")]
        public bool SteamEnabled { get; set; } = true;

        [StringLength(3)]
        [Display(Name = "Country code")]
        public string Country { get; set; } = string.Empty;

        [Range(0, 6000)]
        [Display(Name = "Time limit override, min")]
        public int TimeLimitOverride { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Join password")]
        public string? ServerPassword { get; set; }

        [Display(Name = "Clear saved join password")]
        public bool ClearServerPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Referee password")]
        public string? RefPassword { get; set; }

        [Display(Name = "Clear saved referee password")]
        public bool ClearRefPassword { get; set; }

        public ReflexArenaServerSettings ToSettings(ReflexArenaServerSettings? existingSettings = null)
        {
            return new ReflexArenaServerSettings
            {
                Hostname = Hostname,
                Mode = Mode,
                StartMap = StartMap,
                Mutators = SelectedMutators,
                MaxClients = MaxClients,
                SteamEnabled = SteamEnabled,
                Country = Country.Trim().ToUpperInvariant(),
                TimeLimitOverride = TimeLimitOverride,
                ServerPassword = ClearServerPassword
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(ServerPassword)
                        ? existingSettings?.ServerPassword ?? string.Empty
                        : ServerPassword,
                RefPassword = ClearRefPassword
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(RefPassword)
                        ? existingSettings?.RefPassword ?? string.Empty
                        : RefPassword,
            };
        }

        public static InputModel FromSettings(ReflexArenaServerSettings settings)
        {
            return new InputModel
            {
                Hostname = settings.Hostname,
                Mode = settings.Mode,
                StartMap = settings.StartMap,
                SelectedMutators = settings.Mutators.ToList(),
                MaxClients = settings.MaxClients,
                SteamEnabled = settings.SteamEnabled,
                Country = settings.Country,
                TimeLimitOverride = settings.TimeLimitOverride,
                ServerPassword = string.Empty,
                ClearServerPassword = false,
                RefPassword = string.Empty,
                ClearRefPassword = false,
            };
        }
    }
}
