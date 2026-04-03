// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
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
    IEnumerable<IGameAdapter> gameAdapters,
    IRulesetLibrary rulesetLibrary) : PageModel
{
    private static readonly Regex CountryCodePattern = new("^[A-Za-z]{2,3}$", RegexOptions.Compiled);
    private static readonly Regex RulesetNamePattern = new("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);
    private readonly IGameAdapter _gameAdapter = gameAdapters.First(adapter => adapter.GameKey == "reflex-arena");
    private string GameKey => _gameAdapter.GameKey;

    [BindProperty]
    public InputModel Input { get; set; } = InputModel.FromSettings(new ReflexArenaServerSettings());

    public IReadOnlyList<ReflexArenaModeOption> ModeOptions => ReflexArenaModuleCatalog.Modes;
    public IReadOnlyList<ReflexArenaMutatorOption> MutatorOptions => ReflexArenaModuleCatalog.Mutators;
    public IReadOnlyList<ReflexArenaMapGroup> MapGroups => ReflexArenaModuleCatalog.MapGroups;
    public IReadOnlyList<ReflexArenaWeaponEntry> WeaponEntries => ReflexArenaWeaponsCatalog.Weapons;
    public IReadOnlyList<ReflexArenaPickupEntry> PickupEntries => ReflexArenaWeaponsCatalog.Pickups;
    public IReadOnlyList<SavedRuleset> SavedRulesets { get; private set; } = [];

    public ServerStatusSnapshot Status { get; private set; } = ServerStatusSnapshot.NotConfigured("reflex-arena");
    public string StatusToneClass => Status.StatusToneClass;
    public bool CanStart => Status.CanStart;
    public bool CanRestart => Status.CanRestart;
    public bool CanStop => Status.CanStop;
    public bool ShowUnavailableActions => Status.ShowUnavailableActions;

    public string CurrentRefPassword { get; private set; } = string.Empty;
    public string CurrentServerPassword { get; private set; } = string.Empty;
    public bool HasConfiguredRefPassword => !string.IsNullOrWhiteSpace(CurrentRefPassword);
    public string RefPasswordStateLabel => HasConfiguredRefPassword ? "Configured" : "Not set — required";
    public string RefPasswordStateClass => HasConfiguredRefPassword ? "status-running" : "status-stopped";
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

    public async Task<IActionResult> OnPostStopAsync(CancellationToken cancellationToken)
    {
        var result = await dockerAgentClient.StopAsync(GameKey, cancellationToken);
        StoreResult(result);
        return RedirectToPage();
    }

    public Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken) =>
        HandleSaveAsync(serverAction: ServerAction.None, cancellationToken);

    public Task<IActionResult> OnPostStartAsync(CancellationToken cancellationToken) =>
        HandleSaveAsync(serverAction: ServerAction.Start, cancellationToken);

    public Task<IActionResult> OnPostApplyAsync(CancellationToken cancellationToken) =>
        HandleSaveAsync(serverAction: ServerAction.Restart, cancellationToken);

    public async Task<IActionResult> OnPostSaveRulesetAsync(CancellationToken cancellationToken)
    {
        NormalizeInput();

        if (!Input.CustomRulesEnabled)
        {
            await LoadAsync(cancellationToken, preserveInput: true);
            return Page();
        }

        var existingConfig = await configurationStore.GetOrCreateAsync(GameKey, cancellationToken);
        var existingSettings = ReflexArenaConfigurationSerializer.Deserialize(existingConfig.JsonContent);
        var settings = Input.ToSettings(existingSettings);

        if (settings.CustomRules is { } rules)
        {
            var json = JsonSerializer.Serialize(rules, new JsonSerializerOptions { WriteIndented = true });
            await rulesetLibrary.SaveAsync(GameKey, Input.CustomRulesetName, json, cancellationToken);
            SuccessMessage = $"Ruleset '{Input.CustomRulesetName}' saved to library.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLoadRulesetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken, preserveInput: true);

        if (!string.IsNullOrWhiteSpace(Input.LoadRulesetName))
        {
            var saved = await rulesetLibrary.GetByNameAsync(GameKey, Input.LoadRulesetName, cancellationToken);
            if (saved is not null)
            {
                var rules = JsonSerializer.Deserialize<ReflexArenaCustomRules>(saved.JsonContent);
                if (rules is not null)
                {
                    Input.ApplyRuleset(rules);
                    NormalizeInput();
                }
            }
        }

        return Page();
    }

    private enum ServerAction { None, Start, Restart }

    private async Task<IActionResult> HandleSaveAsync(ServerAction serverAction, CancellationToken cancellationToken)
    {
        var existingConfiguration = await configurationStore.GetOrCreateAsync(GameKey, cancellationToken);
        var existingSettings = ReflexArenaConfigurationSerializer.Deserialize(existingConfiguration.JsonContent);

        NormalizeInput();
        var effectiveSettings = Input.ToSettings(existingSettings);

        ModelState.ClearValidationState(nameof(Input));
        TryValidateModel(Input, nameof(Input));
        ValidateInput();

        if (string.IsNullOrWhiteSpace(effectiveSettings.RefPassword))
        {
            ModelState.AddModelError("Input.RefPassword", "Referee / rcon password is required.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken, preserveInput: true);
            return Page();
        }

        var jsonContent = ReflexArenaConfigurationSerializer.Serialize(effectiveSettings);
        await configurationStore.SaveAsync(GameKey, jsonContent, User.Identity?.Name ?? "unknown", cancellationToken);

        if (serverAction == ServerAction.None)
        {
            SuccessMessage = "Reflex Arena settings saved.";
            return RedirectToPage();
        }

        var env = _gameAdapter.GetContainerEnv(jsonContent);
        var result = serverAction == ServerAction.Start
            ? await dockerAgentClient.StartAsync(GameKey, env, cancellationToken)
            : await dockerAgentClient.RestartAsync(GameKey, env, cancellationToken);

        if (result.Success)
            SuccessMessage = serverAction == ServerAction.Start
                ? "Settings saved and server started."
                : "Settings saved and restart requested.";
        else
            ErrorMessage = $"Settings saved, but {(serverAction == ServerAction.Start ? "start" : "restart")} failed: {result.Message}";

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
            Input = InputModel.FromSettings(currentSettings);

        NormalizeInput();
        SavedRulesets = await rulesetLibrary.GetAllAsync(GameKey, cancellationToken);

        UpdatedLabel = PanelHelpers.FormatUpdatedLabel(configuration.UpdatedUtc);
        UpdatedByLabel = PanelHelpers.FormatUpdatedByLabel(configuration.UpdatedBy);
    }

    private void NormalizeInput()
    {
        Input.StartMap = ReflexArenaModuleCatalog.ResolveStartMap(Input.StartMap, Input.Mode);
        Input.SelectedMutators = ReflexArenaModuleCatalog.NormalizeMutatorSelection(Input.SelectedMutators);
        Input.Country = Input.Country.Trim().ToUpperInvariant();
        Input.CustomRulesetName = Input.CustomRulesetName.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(Input.CustomRulesetName))
            Input.CustomRulesetName = "custom";
    }

    private void ValidateInput()
    {
        if (!ReflexArenaModuleCatalog.IsValidMode(Input.Mode))
            ModelState.AddModelError("Input.Mode", "Choose a supported Reflex Arena mode.");

        if (!ReflexArenaModuleCatalog.IsValidMap(Input.StartMap))
            ModelState.AddModelError("Input.StartMap", "Choose a valid Reflex Arena map.");
        else if (!ReflexArenaModuleCatalog.IsSupportedMapForMode(Input.StartMap, Input.Mode))
            ModelState.AddModelError("Input.StartMap",
                $"Map '{ReflexArenaModuleCatalog.GetMapLabel(Input.StartMap)}' does not support the selected mode.");

        var invalidMutators = Input.SelectedMutators
            .Where(mutator => !ReflexArenaModuleCatalog.IsValidMutator(mutator))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (invalidMutators.Length > 0)
            ModelState.AddModelError("Input.SelectedMutators",
                $"Unsupported mutators: {string.Join(", ", invalidMutators)}");

        if (!string.IsNullOrWhiteSpace(Input.Country) && !CountryCodePattern.IsMatch(Input.Country))
            ModelState.AddModelError("Input.Country", "Country must be a 2-3 letter code such as RU or USA.");

        if (Input.CustomRulesEnabled && !RulesetNamePattern.IsMatch(Input.CustomRulesetName))
            ModelState.AddModelError("Input.CustomRulesetName",
                "Ruleset name must start with a letter and contain only lowercase letters, digits, and underscores.");
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
        [Display(Name = "Referee / rcon password")]
        public string? RefPassword { get; set; }

        [Display(Name = "Use custom rules")]
        public bool CustomRulesEnabled { get; set; }

        [StringLength(64)]
        [Display(Name = "Ruleset name")]
        public string CustomRulesetName { get; set; } = "custom";

        public string? LoadRulesetName { get; set; }

        public Dictionary<string, bool> WeaponEnabled { get; set; } = [];
        public Dictionary<string, int?> WeaponDirectDamage { get; set; } = [];
        public Dictionary<string, int?> WeaponSplashDamage { get; set; } = [];
        public Dictionary<string, bool> WeaponInfiniteAmmo { get; set; } = [];
        public Dictionary<string, int?> WeaponMaxAmmo { get; set; } = [];
        public Dictionary<string, bool> PickupEnabled { get; set; } = [];

        [Range(-3000, 3000)]
        [Display(Name = "Gravity override")]
        public int? GravityOverride { get; set; }

        public void ApplyRuleset(ReflexArenaCustomRules rules)
        {
            CustomRulesEnabled = true;
            CustomRulesetName = string.IsNullOrWhiteSpace(rules.RulesetName) ? "custom" : rules.RulesetName;
            GravityOverride = rules.Gravity;

            WeaponEnabled.Clear();
            WeaponDirectDamage.Clear();
            WeaponSplashDamage.Clear();
            WeaponInfiniteAmmo.Clear();
            WeaponMaxAmmo.Clear();
            PickupEnabled.Clear();

            var weaponDict = rules.Weapons.ToDictionary(w => w.Key, StringComparer.OrdinalIgnoreCase);
            foreach (var entry in ReflexArenaWeaponsCatalog.Weapons)
            {
                if (weaponDict.TryGetValue(entry.Key, out var wo))
                {
                    WeaponEnabled[entry.Key] = wo.WeaponEnabled;
                    if (wo.DirectDamage.HasValue) WeaponDirectDamage[entry.Key] = wo.DirectDamage;
                    if (wo.SplashDamage.HasValue) WeaponSplashDamage[entry.Key] = wo.SplashDamage;
                    WeaponInfiniteAmmo[entry.Key] = wo.InfiniteAmmo;
                    if (wo.MaxAmmo.HasValue) WeaponMaxAmmo[entry.Key] = wo.MaxAmmo;
                }
                else
                {
                    WeaponEnabled[entry.Key] = true;
                }
            }

            var pickupDict = rules.Pickups.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);
            foreach (var entry in ReflexArenaWeaponsCatalog.Pickups)
            {
                PickupEnabled[entry.Key] = pickupDict.TryGetValue(entry.Key, out var po) ? po.Enabled : true;
            }
        }

        public ReflexArenaServerSettings ToSettings(ReflexArenaServerSettings? existingSettings = null)
        {
            ReflexArenaCustomRules? customRules = null;
            if (CustomRulesEnabled)
            {
                var weapons = ReflexArenaWeaponsCatalog.Weapons.Select(entry => new ReflexArenaWeaponOverride
                {
                    Key = entry.Key,
                    WeaponEnabled = WeaponEnabled.GetValueOrDefault(entry.Key, true),
                    DirectDamage = WeaponDirectDamage.TryGetValue(entry.Key, out var dd) ? dd : null,
                    SplashDamage = entry.HasSplashDamage
                        ? (WeaponSplashDamage.TryGetValue(entry.Key, out var sd) ? sd : null)
                        : null,
                    InfiniteAmmo = entry.HasAmmo && WeaponInfiniteAmmo.GetValueOrDefault(entry.Key, false),
                    MaxAmmo = entry.HasAmmo && !WeaponInfiniteAmmo.GetValueOrDefault(entry.Key, false)
                        ? (WeaponMaxAmmo.TryGetValue(entry.Key, out var ma) ? ma : null)
                        : null,
                }).ToList();

                var pickups = ReflexArenaWeaponsCatalog.Pickups.Select(entry => new ReflexArenaPickupOverride
                {
                    Key = entry.Key,
                    Enabled = PickupEnabled.GetValueOrDefault(entry.Key, true),
                }).ToList();

                customRules = new ReflexArenaCustomRules
                {
                    Enabled = true,
                    RulesetName = CustomRulesetName,
                    Weapons = weapons,
                    Pickups = pickups,
                    Gravity = GravityOverride,
                };
            }

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
                RefPassword = string.IsNullOrWhiteSpace(RefPassword)
                    ? existingSettings?.RefPassword ?? string.Empty
                    : RefPassword,
                CustomRules = customRules,
            };
        }

        public static InputModel FromSettings(ReflexArenaServerSettings settings)
        {
            var model = new InputModel
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
                CustomRulesEnabled = settings.CustomRules?.Enabled ?? false,
                CustomRulesetName = settings.CustomRules?.RulesetName ?? "custom",
                GravityOverride = settings.CustomRules?.Gravity,
            };

            var savedWeapons = settings.CustomRules?.Weapons
                .ToDictionary(w => w.Key, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, ReflexArenaWeaponOverride>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in ReflexArenaWeaponsCatalog.Weapons)
            {
                if (savedWeapons.TryGetValue(entry.Key, out var wo))
                {
                    model.WeaponEnabled[entry.Key] = wo.WeaponEnabled;
                    if (wo.DirectDamage.HasValue) model.WeaponDirectDamage[entry.Key] = wo.DirectDamage;
                    if (wo.SplashDamage.HasValue) model.WeaponSplashDamage[entry.Key] = wo.SplashDamage;
                    model.WeaponInfiniteAmmo[entry.Key] = wo.InfiniteAmmo;
                    if (wo.MaxAmmo.HasValue) model.WeaponMaxAmmo[entry.Key] = wo.MaxAmmo;
                }
                else
                {
                    model.WeaponEnabled[entry.Key] = true;
                }
            }

            var savedPickups = settings.CustomRules?.Pickups
                .ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, ReflexArenaPickupOverride>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in ReflexArenaWeaponsCatalog.Pickups)
            {
                model.PickupEnabled[entry.Key] = savedPickups.TryGetValue(entry.Key, out var po)
                    ? po.Enabled : true;
            }

            return model;
        }
    }
}
