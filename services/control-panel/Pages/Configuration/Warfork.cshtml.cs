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
    public IReadOnlyList<WarforkWeaponEntry> WeaponOptions => WarforkWeaponsCatalog.Weapons;
    public IReadOnlyList<WarforkPickupEntry> PickupOptions => WarforkWeaponsCatalog.Pickups;
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

    public bool IsWeaponAllowed(string weaponKey) =>
        Input.CustomRules.AllowedWeapons.Contains(weaponKey, StringComparer.OrdinalIgnoreCase);

    public WarforkWeaponEntry? FindWeapon(string? weaponKey) =>
        WarforkWeaponsCatalog.FindWeapon(weaponKey);

    public bool SupportsDamageOverride(string? weaponKey) =>
        WarforkWeaponsCatalog.SupportsDamageOverride(weaponKey);

    public bool SupportsHealingMode(string? weaponKey) =>
        WarforkWeaponsCatalog.SupportsHealingMode(weaponKey);

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
        Input.CustomRules.Normalize();
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

        if (Input.CustomRules.Enabled &&
            Input.CustomRules.ClanArenaLoadoutEnabled &&
            !string.Equals(Input.Gametype, "ca", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                "Input.CustomRules.ClanArenaLoadoutEnabled",
                "Clan Arena loadout currently works only when Match mode is Clan Arena.");
        }

        if (Input.CustomRules.Enabled &&
            Input.CustomRules.AllowedWeapons.Count > 0 &&
            !string.Equals(Input.Gametype, "ca", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                "Input.CustomRules.AllowedWeapons",
                "Map weapon filtering currently works only in the custom Clan Arena runtime.");
        }

        if (Input.CustomRules.Enabled &&
            (Input.CustomRules.DisableHealthItems ||
             Input.CustomRules.DisableArmorItems ||
             Input.CustomRules.DisablePowerups) &&
            !string.Equals(Input.Gametype, "ca", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                "Input.CustomRules.Enabled",
                "Item-category spawn toggles currently work only in the custom Clan Arena runtime.");
        }

        if (Input.CustomRules.Enabled &&
            Input.CustomRules.ClanArenaLoadoutEnabled &&
            !Input.CustomRules.ClanArenaLoadout.Any(rule => rule.Enabled))
        {
            ModelState.AddModelError(
                "Input.CustomRules.ClanArenaLoadoutEnabled",
                "Select at least one weapon for the Clan Arena spawn loadout.");
        }

        for (var i = 0; i < Input.CustomRules.ClanArenaLoadout.Count; i++)
        {
            var rule = Input.CustomRules.ClanArenaLoadout[i];
            if (!Input.CustomRules.Enabled || !Input.CustomRules.ClanArenaLoadoutEnabled || !rule.Enabled)
            {
                continue;
            }

            if (!WarforkWeaponsCatalog.IsValidWeapon(rule.Key))
            {
                ModelState.AddModelError(
                    $"Input.CustomRules.ClanArenaLoadout[{i}].Key",
                    "Choose a supported Warfork weapon.");
            }

            if (rule.Ammo is < 1 or > WarforkWeaponsCatalog.PracticalInfiniteAmmoReserve)
            {
                ModelState.AddModelError(
                    $"Input.CustomRules.ClanArenaLoadout[{i}].Ammo",
                    $"Ammo must be between 1 and {WarforkWeaponsCatalog.PracticalInfiniteAmmoReserve}.");
            }

            if (rule.DamageOverride is not null &&
                !WarforkWeaponsCatalog.SupportsDamageOverride(rule.Key))
            {
                ModelState.AddModelError(
                    $"Input.CustomRules.ClanArenaLoadout[{i}].DamageOverride",
                    "Damage override is currently supported only for Electrobolt and projectile weapons in custom Clan Arena.");
            }

            if (rule.HealOnHit &&
                !WarforkWeaponsCatalog.SupportsHealingMode(rule.Key))
            {
                ModelState.AddModelError(
                    $"Input.CustomRules.ClanArenaLoadout[{i}].HealOnHit",
                    "Heal-on-hit is currently supported only for Rocket Launcher in custom Clan Arena.");
            }
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

        public CustomRulesInputModel CustomRules { get; set; } = new();

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
                        : ServerPassword,
                CustomRules = CustomRules.ToModel(),
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
                ClearServerPassword = false,
                CustomRules = CustomRulesInputModel.FromModel(settings.CustomRules),
            };
        }
    }

    public sealed class CustomRulesInputModel
    {
        [Display(Name = "Enable custom rules")]
        public bool Enabled { get; set; }

        public List<string> AllowedWeapons { get; set; } = [];

        [Display(Name = "Enable Clan Arena spawn loadout")]
        public bool ClanArenaLoadoutEnabled { get; set; }

        public List<ClanArenaLoadoutWeaponInputModel> ClanArenaLoadout { get; set; } = [];

        [Display(Name = "Disable health items")]
        public bool DisableHealthItems { get; set; }

        [Display(Name = "Disable armor items")]
        public bool DisableArmorItems { get; set; }

        [Display(Name = "Disable powerups")]
        public bool DisablePowerups { get; set; }

        [Range(50, 3000)]
        [Display(Name = "Gravity override")]
        public int? Gravity { get; set; }

        public void Normalize()
        {
            AllowedWeapons = AllowedWeapons
                .Where(WarforkWeaponsCatalog.IsValidWeapon)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var configured = (ClanArenaLoadout ?? [])
                .Where(rule => WarforkWeaponsCatalog.IsValidWeapon(rule.Key))
                .GroupBy(rule => rule.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

            ClanArenaLoadout = WarforkWeaponsCatalog.Weapons
                .Select(weapon =>
                {
                    if (configured.TryGetValue(weapon.Key, out var rule))
                    {
                        rule.Key = weapon.Key;
                        if (rule.Ammo <= 0)
                        {
                            rule.Ammo = weapon.ClanArenaDefaultAmmo;
                        }

                        if (rule.DamageOverride <= 0 || !weapon.SupportsDamageOverride)
                        {
                            rule.DamageOverride = null;
                        }

                        if (!weapon.SupportsHealingMode)
                        {
                            rule.HealOnHit = false;
                        }

                        return rule;
                    }

                    return ClanArenaLoadoutWeaponInputModel.ForWeapon(weapon);
                })
                .ToList();
        }

        public WarforkCustomRules ToModel() => new()
        {
            Enabled = Enabled,
            AllowedWeapons = AllowedWeapons
                .Where(WarforkWeaponsCatalog.IsValidWeapon)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ClanArenaLoadoutEnabled = ClanArenaLoadoutEnabled,
            ClanArenaLoadout = WarforkWeaponsCatalog.NormalizeClanArenaLoadout(
                ClanArenaLoadout
                    .Where(rule => rule.Enabled && WarforkWeaponsCatalog.IsValidWeapon(rule.Key))
                    .Select(rule => new WarforkClanArenaWeaponLoadout
                    {
                        WeaponKey = rule.Key,
                        Ammo = rule.Ammo,
                        InfiniteAmmo = rule.InfiniteAmmo,
                        DamageOverride = WarforkWeaponsCatalog.SupportsDamageOverride(rule.Key) && rule.DamageOverride is > 0
                            ? rule.DamageOverride
                            : null,
                        HealOnHit = WarforkWeaponsCatalog.SupportsHealingMode(rule.Key) && rule.HealOnHit,
                    })),
            DisableHealthItems = DisableHealthItems,
            DisableArmorItems = DisableArmorItems,
            DisablePowerups = DisablePowerups,
            Gravity = Gravity is > 0 ? Gravity : null,
        };

        public static CustomRulesInputModel FromModel(WarforkCustomRules? rules) =>
            rules is null
                ? new()
                : new()
                {
                    Enabled = rules.Enabled,
                    AllowedWeapons = rules.AllowedWeapons.ToList(),
                    ClanArenaLoadoutEnabled = rules.ClanArenaLoadoutEnabled,
                    ClanArenaLoadout = rules.ClanArenaLoadout
                        .Select(rule =>
                        {
                            var weapon = WarforkWeaponsCatalog.FindWeapon(rule.WeaponKey);
                            return new ClanArenaLoadoutWeaponInputModel
                            {
                                Key = rule.WeaponKey,
                                Enabled = weapon is not null,
                                Ammo = rule.Ammo > 0 ? rule.Ammo : weapon?.ClanArenaDefaultAmmo ?? 1,
                                InfiniteAmmo = rule.InfiniteAmmo,
                                DamageOverride = weapon?.SupportsDamageOverride == true && rule.DamageOverride is > 0
                                    ? rule.DamageOverride
                                    : null,
                                HealOnHit = weapon?.SupportsHealingMode == true && rule.HealOnHit,
                            };
                        })
                        .ToList(),
                    DisableHealthItems = rules.DisableHealthItems,
                    DisableArmorItems = rules.DisableArmorItems,
                    DisablePowerups = rules.DisablePowerups,
                    Gravity = rules.Gravity,
                };
    }

    public sealed class ClanArenaLoadoutWeaponInputModel
    {
        public string Key { get; set; } = string.Empty;

        [Display(Name = "Enable weapon")]
        public bool Enabled { get; set; }

        [Range(1, WarforkWeaponsCatalog.PracticalInfiniteAmmoReserve)]
        [Display(Name = "Ammo reserve")]
        public int Ammo { get; set; }

        [Display(Name = "Use practical infinite ammo reserve")]
        public bool InfiniteAmmo { get; set; }

        [Range(1, 9999)]
        [Display(Name = "Damage override")]
        public int? DamageOverride { get; set; }

        [Display(Name = "Heal on hit")]
        public bool HealOnHit { get; set; }

        public static ClanArenaLoadoutWeaponInputModel ForWeapon(WarforkWeaponEntry weapon) => new()
        {
            Key = weapon.Key,
            Ammo = weapon.ClanArenaDefaultAmmo,
        };
    }
}
