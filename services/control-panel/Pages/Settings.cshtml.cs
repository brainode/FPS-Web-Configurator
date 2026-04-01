using control_panel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace control_panel.Pages;

[Authorize]
public sealed class SettingsModel(
    IModuleVisibilityService moduleVisibilityService,
    PanelGameModuleCatalog moduleCatalog) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IReadOnlyList<GameModuleDescriptor> Modules => moduleCatalog.AllModules;
    public string UpdatedLabel { get; private set; } = "Never";
    public string UpdatedByLabel { get; private set; } = "System";

    [TempData]
    public string? SuccessMessage { get; set; }

    public bool IsEnabled(string gameKey) =>
        Input.EnabledGameKeys.Contains(gameKey, StringComparer.OrdinalIgnoreCase);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        await moduleVisibilityService.SaveAsync(
            Input.EnabledGameKeys,
            User.Identity?.Name ?? "unknown",
            cancellationToken);

        SuccessMessage = "Panel settings saved.";
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var snapshot = await moduleVisibilityService.GetAsync(cancellationToken);
        Input = InputModel.FromEnabledGameKeys(snapshot.Settings.EnabledGameKeys);
        UpdatedLabel = PanelHelpers.FormatUpdatedLabel(snapshot.UpdatedUtc);
        UpdatedByLabel = PanelHelpers.FormatUpdatedByLabel(snapshot.UpdatedBy);
    }

    public sealed class InputModel
    {
        public List<string> EnabledGameKeys { get; set; } = [];

        public static InputModel FromEnabledGameKeys(IEnumerable<string> enabledGameKeys) =>
            new()
            {
                EnabledGameKeys = enabledGameKeys.ToList()
            };
    }
}
