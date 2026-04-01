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
public sealed class WarforkModel(
    IConfigurationStore configurationStore,
    IDockerAgentClient dockerAgentClient,
    IEnumerable<IGameAdapter> gameAdapters) : PageModel
{
    private readonly IGameAdapter _gameAdapter = gameAdapters.First(a => a.GameKey == "warfork");
    private string GameKey => _gameAdapter.GameKey;

    [BindProperty]
    public InputModel Input { get; set; } = InputModel.FromSettings(new WarforkServerSettings());

    public IReadOnlyList<WarforkGametypeOption> GametypeOptions => WarforkModuleCatalog.Gametypes;
    public IReadOnlyList<WarforkMapGroup> MapGroups => WarforkModuleCatalog.MapGroups;
    public IReadOnlyList<WarforkMapOption> StartMapOptions => Input.SelectedMaps
        .Where(WarforkModuleCatalog.IsValidMap)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(mapKey => new WarforkMapOption(mapKey))
        .ToArray();

    public ServerStatusSnapshot Status { get; private set; } = ServerStatusSnapshot.NotConfigured("warfork");
    public string StatusToneClass => Status.StatusToneClass;
    public bool CanStart => Status.CanStart;
    public bool CanRestart => Status.CanRestart;
    public bool CanStop => Status.CanStop;
    public bool ShowUnavailableActions => Status.ShowUnavailableActions;

    public string CurrentRconPassword { get; private set; } = string.Empty;
    public string CurrentServerPassword { get; private set; } = string.Empty;
    public bool HasConfiguredRconPassword => !string.IsNullOrWhiteSpace(CurrentRconPassword);
    public string RconPasswordStateLabel => HasConfiguredRconPassword ? "Configured" : "Optional";
    public string RconPasswordStateClass => HasConfiguredRconPassword ? "status-running" : "status-neutral";
    public string RconPasswordStateHint => HasConfiguredRconPassword
        ? "A remote-control password is already saved. Type a new one to replace it or leave the field empty to keep it."
        : "Remote-control password is optional. Set one if you want RCON access outside the panel.";
    public string MaskedRconPassword => PanelHelpers.MaskSecret(CurrentRconPassword);
    public bool HasJoinPassword => !string.IsNullOrWhiteSpace(CurrentServerPassword);
    public string JoinPasswordStateLabel => HasJoinPassword ? "Protected lobby" : "Open lobby";
    public string JoinPasswordStateClass => HasJoinPassword ? "status-running" : "status-neutral";
    public string JoinPasswordStateHint => HasJoinPassword
        ? "A join password is already saved. Leave the field empty to keep it, type a new one to replace it, or clear it explicitly."
        : "No join password is saved. Players can connect without a lobby password.";
    public string MaskedJoinPassword => PanelHelpers.MaskSecret(CurrentServerPassword);

    public string UpdatedLabel { get; private set; } = "Never";
    public string UpdatedByLabel { get; private set; } = "System";

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public bool IsMapSelected(string mapKey) =>
        Input.SelectedMaps.Contains(mapKey, StringComparer.OrdinalIgnoreCase);

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
        var existingSettings = WarforkConfigurationSerializer.Deserialize(existingConfiguration.JsonContent);

        NormalizeInput(fillDefaultsWhenEmpty: false);
        var effectiveSettings = Input.ToSettings(existingSettings);

        ModelState.ClearValidationState(nameof(Input));
        TryValidateModel(Input, nameof(Input));
        ValidateInput();

        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken, preserveInput: true);
            return Page();
        }

        var jsonContent = WarforkConfigurationSerializer.Serialize(effectiveSettings);
        await configurationStore.SaveAsync(GameKey, jsonContent, User.Identity?.Name ?? "unknown", cancellationToken);

        if (!restartServer)
        {
            SuccessMessage = "Warfork settings saved.";
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
        var currentSettings = WarforkConfigurationSerializer.Deserialize(configuration.JsonContent);
        CurrentRconPassword = currentSettings.RconPassword;
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
        Input.SelectedMaps = WarforkModuleCatalog.NormalizeMapSelection(Input.SelectedMaps, Input.Gametype, fillDefaultsWhenEmpty);
        Input.StartMap = WarforkModuleCatalog.ResolveStartMap(
            Input.StartMap,
            Input.SelectedMaps,
            Input.Gametype,
            allowEmpty: !fillDefaultsWhenEmpty && Input.SelectedMaps.Count == 0);
    }

    private void ValidateInput()
    {
        if (!WarforkModuleCatalog.IsValidGametype(Input.Gametype))
        {
            ModelState.AddModelError("Input.Gametype", "Choose a supported Warfork game mode.");
        }

        if (!WarforkModuleCatalog.IsValidMap(Input.StartMap))
        {
            ModelState.AddModelError("Input.StartMap", "Choose a valid start map.");
        }

        if (Input.SelectedMaps.Count > 0 &&
            !Input.SelectedMaps.Contains(Input.StartMap, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("Input.StartMap", "Start map must be one of the selected pool maps.");
        }

        var invalidMaps = Input.SelectedMaps
            .Where(mapKey => !WarforkModuleCatalog.IsValidMap(mapKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (invalidMaps.Length > 0)
        {
            ModelState.AddModelError("Input.SelectedMaps", $"Unsupported maps: {string.Join(", ", invalidMaps)}");
        }

        if (Input.SelectedMaps.Count == 0)
        {
            ModelState.AddModelError("Input.SelectedMaps", "Select at least one map for the rotation.");
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
        [Display(Name = "Match mode")]
        public string Gametype { get; set; } = "ca";

        [Required]
        [Display(Name = "Start map")]
        public string StartMap { get; set; } = "return";

        public List<string> SelectedMaps { get; set; } = ["return", "pressure"];

        [Display(Name = "Instagib")]
        public bool Instagib { get; set; }

        [Display(Name = "Instajump")]
        public bool Instajump { get; set; }

        [Display(Name = "Instashield")]
        public bool Instashield { get; set; }

        [Range(0, 999)]
        [Display(Name = "Score limit")]
        public int Scorelimit { get; set; } = 11;

        [Range(0, 240)]
        [Display(Name = "Time limit, min")]
        public int Timelimit { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "RCON password")]
        public string? RconPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Join password")]
        public string? ServerPassword { get; set; }

        [Display(Name = "Clear saved join password")]
        public bool ClearServerPassword { get; set; }

        public WarforkServerSettings ToSettings(WarforkServerSettings? existingSettings = null)
        {
            return new WarforkServerSettings
            {
                Gametype = Gametype,
                StartMap = StartMap,
                MapList = SelectedMaps,
                Instagib = Instagib,
                Instajump = Instajump,
                Instashield = Instashield,
                Scorelimit = Scorelimit,
                Timelimit = Timelimit,
                RconPassword = string.IsNullOrWhiteSpace(RconPassword)
                    ? existingSettings?.RconPassword ?? string.Empty
                    : RconPassword,
                ServerPassword = ClearServerPassword
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(ServerPassword)
                        ? existingSettings?.ServerPassword ?? string.Empty
                        : ServerPassword
            };
        }

        public static InputModel FromSettings(WarforkServerSettings settings)
        {
            return new InputModel
            {
                Gametype = settings.Gametype,
                StartMap = settings.StartMap,
                SelectedMaps = settings.MapList.ToList(),
                Instagib = settings.Instagib,
                Instajump = settings.Instajump,
                Instashield = settings.Instashield,
                Scorelimit = settings.Scorelimit,
                Timelimit = settings.Timelimit,
                RconPassword = settings.RconPassword,
                ServerPassword = string.Empty,
                ClearServerPassword = false
            };
        }
    }
}
