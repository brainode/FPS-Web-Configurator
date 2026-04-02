# Adding a New Game Service

This guide walks you through adding a new game module to the platform.
Every game follows the same pattern — when you're done, the new game will appear on the dashboard, have its own configuration page, and start/stop via docker-agent.

The guide uses **Quake Live** as the reference example. Replace `QuakeLive` / `quake-live` / `QL` with your game's name throughout.

---

## Architecture overview

```
User ──► Razor Page ──► IConfigurationStore (SQLite)
              │                    │
              ▼                    ▼
         IGameAdapter ──► GetContainerEnv(json) ──► docker-agent ──► docker run
```

Each game module consists of these layers:

| # | Layer | File | Purpose |
|---|---|---|---|
| 1 | Settings model | `Models/{Game}ServerSettings.cs` | Typed C# class for all game-specific settings |
| 2 | Serializer | `Services/{Game}ConfigurationSerializer.cs` | JSON ↔ typed model conversion |
| 3 | Module catalog | `Services/{Game}ModuleCatalog.cs` | Maps, game modes, validation rules |
| 4 | Catalog data | `Data/{game}_catalog.json` | Embedded JSON resource with maps and modes |
| 5 | Seed config | `Services/{Game}SeedConfiguration.cs` | Default JSON for first-time setup |
| 6 | Game adapter | `Services/{Game}GameAdapter.cs` | `IGameAdapter` implementation |
| 7 | Config page | `Pages/Configuration/{Game}.cshtml` + `.cs` | Razor Page with form, validation, server control |
| 8 | Unit tests | `tests/control-panel.Tests/Services/` | Serializer, catalog, and adapter tests |
| 9 | Container | `services/{game}-server/` | Dockerfile + entrypoint reading env vars |
| 10 | Compose entry | `docker-compose.yml` | Service definition under `game` profile |
| 11 | Agent config | `services/docker-agent/agent.py` | Image, ports, volumes for `docker run` |
| 12 | Env vars | `.env.example` | Host-level port/image overrides |

Below is each step in detail with code.

---

## Step 1. Settings model

Create `services/control-panel/Models/QuakeLiveServerSettings.cs`:

```csharp
namespace control_panel.Models;

public sealed class QuakeLiveServerSettings
{
    public string Hostname { get; set; } = "Quake Live Standalone Test";
    public string Factory { get; set; } = "duel";
    public List<string> MapList { get; set; } = ["asylum", "brimstoneabbey", "campgrounds", "purgatory", "theedge"];
    public int MaxClients { get; set; } = 16;
    public int ServerType { get; set; } = 2;   // 0 = offline, 1 = LAN, 2 = internet
    public bool ZmqRconEnabled { get; set; }
    public int ZmqRconPort { get; set; } = 28960;
    public string ZmqRconPassword { get; set; } = string.Empty;
    public bool ZmqStatsEnabled { get; set; }
    public int ZmqStatsPort { get; set; } = 27960;
    public string ZmqStatsPassword { get; set; } = string.Empty;
    public string ServerPassword { get; set; } = string.Empty;
    public string Tags { get; set; } = "standalone";
}
```

**Rules:**
- Class must be `sealed` with a parameterless constructor.
- Every property must have a sensible default — this is what the user sees on first launch.
- Use `List<string>` for map lists (serializer and catalog normalize them).

---

## Step 2. Configuration serializer

Create `services/control-panel/Services/QuakeLiveConfigurationSerializer.cs`:

```csharp
using System.Text.Json;
using control_panel.Models;

namespace control_panel.Services;

public static class QuakeLiveConfigurationSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static QuakeLiveServerSettings Deserialize(string? json)
    {
        var settings = new QuakeLiveServerSettings();

        if (string.IsNullOrWhiteSpace(json))
        {
            settings.MapList = QuakeLiveModuleCatalog.NormalizeMapSelection(
                settings.MapList, settings.Factory);
            return settings;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            settings.Hostname = GameConfigJsonReader.ReadString(root, "sv_hostname", settings.Hostname);
            settings.Factory = GameConfigJsonReader.ReadString(root, "g_factory", settings.Factory);
            settings.MapList = QuakeLiveModuleCatalog.NormalizeMapSelection(
                GameConfigJsonReader.ReadStringList(root, "g_maplist"), settings.Factory);
            settings.MaxClients = GameConfigJsonReader.ReadInt(root, "sv_maxclients", settings.MaxClients);
            settings.ServerType = GameConfigJsonReader.ReadInt(root, "sv_serverType", settings.ServerType);
            settings.ZmqRconEnabled = GameConfigJsonReader.ReadBoolean(root, "zmq_rcon_enable");
            settings.ZmqRconPort = GameConfigJsonReader.ReadInt(root, "zmq_rcon_port", settings.ZmqRconPort);
            settings.ZmqRconPassword = GameConfigJsonReader.ReadString(root, "zmq_rcon_password", string.Empty);
            settings.ZmqStatsEnabled = GameConfigJsonReader.ReadBoolean(root, "zmq_stats_enable");
            settings.ZmqStatsPort = GameConfigJsonReader.ReadInt(root, "zmq_stats_port", settings.ZmqStatsPort);
            settings.ZmqStatsPassword = GameConfigJsonReader.ReadString(root, "zmq_stats_password", string.Empty);
            settings.ServerPassword = GameConfigJsonReader.ReadString(root, "g_password", string.Empty);
            settings.Tags = GameConfigJsonReader.ReadString(root, "sv_tags", string.Empty);
        }
        catch (JsonException)
        {
            settings = new QuakeLiveServerSettings();
        }

        settings.MapList = QuakeLiveModuleCatalog.NormalizeMapSelection(
            settings.MapList, settings.Factory);
        return settings;
    }

    public static string Serialize(QuakeLiveServerSettings settings)
    {
        var normalizedMaps = QuakeLiveModuleCatalog.NormalizeMapSelection(
            settings.MapList, settings.Factory);

        var payload = new Dictionary<string, string>
        {
            ["sv_hostname"] = settings.Hostname,
            ["g_factory"] = settings.Factory,
            ["g_maplist"] = string.Join(' ', normalizedMaps),
            ["sv_maxclients"] = settings.MaxClients.ToString(),
            ["sv_serverType"] = settings.ServerType.ToString(),
            ["zmq_rcon_enable"] = settings.ZmqRconEnabled ? "1" : "0",
            ["zmq_rcon_port"] = settings.ZmqRconPort.ToString(),
            ["zmq_rcon_password"] = settings.ZmqRconPassword ?? string.Empty,
            ["zmq_stats_enable"] = settings.ZmqStatsEnabled ? "1" : "0",
            ["zmq_stats_port"] = settings.ZmqStatsPort.ToString(),
            ["zmq_stats_password"] = settings.ZmqStatsPassword ?? string.Empty,
            ["g_password"] = settings.ServerPassword ?? string.Empty,
            ["sv_tags"] = settings.Tags ?? string.Empty,
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }
}
```

**Rules:**
- Must be `public static` class with `Deserialize(string? json)` and `Serialize(settings)`.
- Use the shared `GameConfigJsonReader` helper (`ReadString`, `ReadInt`, `ReadBoolean`, `ReadStringList`) instead of writing your own JSON parsing. It handles type coercion (string `"1"` ↔ bool `true`, space-separated strings ↔ arrays, etc.).
- `Deserialize` must handle `null`, empty string, and invalid JSON gracefully — always return a valid settings object.
- Normalize maps through the module catalog on both read and write.

---

## Step 3. Module catalog

The catalog defines available maps and game modes. It loads once at startup from an embedded JSON resource.

### 3a. Catalog JSON file

Create `services/control-panel/Data/quake_live_catalog.json`:

```json
{
  "factories": [
    {
      "key": "ffa",
      "label": "Free For All",
      "description": "Classic deathmatch — every player for themselves.",
      "supportedMapGroups": ["ffa"],
      "recommendedMaps": ["almostlost", "campgrounds", "overkill", "tornado", "wargrounds"]
    },
    {
      "key": "duel",
      "label": "Duel",
      "description": "1-on-1 competitive with item control.",
      "supportedMapGroups": ["duel"],
      "recommendedMaps": ["aerowalk", "bloodrun", "campgrounds", "furiousheights", "toxicity"]
    }
  ],
  "mapGroups": [
    {
      "key": "ffa",
      "label": "Free For All Maps",
      "maps": ["almostlost", "asylum", "brimstoneabbey", "campgrounds", "overkill"]
    },
    {
      "key": "duel",
      "label": "Duel Maps",
      "maps": ["aerowalk", "bloodrun", "campgrounds", "furiousheights", "toxicity"]
    }
  ]
}
```

**Structure:**
- `factories` — game modes. Each has a `key` (used in code), `label` (shown in UI), `description`, optional `supportedMapGroups` (restricts which maps are valid), and `recommendedMaps` (default selection when user hasn't picked any).
- `mapGroups` — logical groupings of maps. Each has a `key`, `label`, and `maps` array.

### 3b. Register as embedded resource

Add to `services/control-panel/control-panel.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="Data\quake_live_catalog.json" />
</ItemGroup>
```

### 3c. Catalog class

Create `services/control-panel/Services/QuakeLiveModuleCatalog.cs`:

```csharp
using System.Text.Json;

namespace control_panel.Services;

public static class QuakeLiveModuleCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> SupportedMapsByFactory;

    public static IReadOnlyList<QuakeLiveFactoryOption> Factories { get; }
    public static IReadOnlyList<QuakeLiveMapGroup> MapGroups { get; }
    public static IReadOnlyList<string> AllMaps { get; }

    static QuakeLiveModuleCatalog()
    {
        // Load embedded resource. The name is: {RootNamespace}.{folder}.{filename}
        // In this project RootNamespace = "control_panel", folder = "Data".
        var assembly = typeof(QuakeLiveModuleCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream("control_panel.Data.quake_live_catalog.json")
            ?? throw new InvalidOperationException(
                "Embedded resource 'quake_live_catalog.json' not found.");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var doc = JsonSerializer.Deserialize<CatalogDoc>(stream, options)!;

        // Parse map groups
        MapGroups = doc.MapGroups
            .Select(g => new QuakeLiveMapGroup(
                g.Key, g.Label,
                g.Maps.Select(m => new QuakeLiveMapOption(m)).ToArray()))
            .ToArray();

        // Flatten all maps into a single list
        AllMaps = MapGroups
            .SelectMany(g => g.Maps)
            .Select(m => m.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Parse factories (game modes)
        Factories = doc.Factories
            .Select(f => new QuakeLiveFactoryOption(
                f.Key, f.Label, f.Description,
                f.RecommendedMaps, f.SupportedMapGroups))
            .ToArray();

        SupportedMapsByFactory = BuildSupportedMapsByFactory(Factories, MapGroups);
    }

    public static bool IsValidFactory(string? factory) =>
        !string.IsNullOrWhiteSpace(factory) &&
        Factories.Any(f => string.Equals(f.Key, factory, StringComparison.OrdinalIgnoreCase));

    public static bool IsValidMap(string? mapKey) =>
        !string.IsNullOrWhiteSpace(mapKey) &&
        AllMaps.Contains(mapKey, StringComparer.OrdinalIgnoreCase);

    public static QuakeLiveFactoryOption? FindFactory(string? factory) =>
        string.IsNullOrWhiteSpace(factory)
            ? null
            : Factories.FirstOrDefault(f =>
                string.Equals(f.Key, factory, StringComparison.OrdinalIgnoreCase));

    public static string GetFactoryLabel(string? factory) =>
        FindFactory(factory)?.Label ?? "Custom";

    public static IReadOnlyList<string> GetRecommendedMaps(string? factory) =>
        FindFactory(factory)?.RecommendedMaps ?? [];

    public static bool IsSupportedMapForFactory(string? mapKey, string? factory)
    {
        if (!IsValidMap(mapKey))
            return false;

        return string.IsNullOrWhiteSpace(factory) ||
               !SupportedMapsByFactory.TryGetValue(factory, out var supportedMaps) ||
               supportedMaps.Contains(mapKey!);
    }

    public static IReadOnlyList<string> GetUnsupportedMapsForFactory(
        IEnumerable<string>? selectedMaps, string? factory)
    {
        if (selectedMaps is null ||
            string.IsNullOrWhiteSpace(factory) ||
            !SupportedMapsByFactory.TryGetValue(factory, out var supportedMaps))
            return [];

        return selectedMaps
            .Where(IsValidMap)
            .Where(map => !supportedMaps.Contains(map))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Filters the map selection to valid maps, optionally filling defaults.
    /// Used by both the serializer and the page model.
    /// </summary>
    public static List<string> NormalizeMapSelection(
        IEnumerable<string>? selectedMaps,
        string? factory = null,
        bool fillDefaultsWhenEmpty = true)
    {
        var normalized = (selectedMaps ?? [])
            .Where(IsValidMap)
            .Where(map => IsSupportedMapForFactory(map, factory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0 && fillDefaultsWhenEmpty)
        {
            normalized.AddRange(
                GetRecommendedMaps(factory)
                    .Where(map => IsSupportedMapForFactory(map, factory)));
        }

        return normalized;
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, IReadOnlySet<string>>
        BuildSupportedMapsByFactory(
            IEnumerable<QuakeLiveFactoryOption> factories,
            IEnumerable<QuakeLiveMapGroup> mapGroups)
    {
        var mapGroupsByKey = mapGroups.ToDictionary(
            group => group.Key, StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, IReadOnlySet<string>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var factory in factories)
        {
            if (factory.SupportedMapGroups.Count == 0)
                continue;

            var supportedMaps = factory.SupportedMapGroups
                .SelectMany(groupKey => mapGroupsByKey[groupKey].Maps)
                .Select(map => map.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            result[factory.Key] = supportedMaps;
        }

        return result;
    }

    // ── JSON deserialization DTOs ─────────────────────────────────────────────

    private sealed class CatalogDoc
    {
        public List<FactoryDoc> Factories { get; set; } = [];
        public List<MapGroupDoc> MapGroups { get; set; } = [];
    }

    private sealed class FactoryDoc
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> RecommendedMaps { get; set; } = [];
        public List<string> SupportedMapGroups { get; set; } = [];
    }

    private sealed class MapGroupDoc
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public List<string> Maps { get; set; } = [];
    }
}

// ── Public record types used by the page model and UI ────────────────────

public sealed record QuakeLiveFactoryOption(
    string Key,
    string Label,
    string Description,
    IReadOnlyList<string> RecommendedMaps,
    IReadOnlyList<string> SupportedMapGroups);

public sealed record QuakeLiveMapGroup(
    string Key,
    string Label,
    IReadOnlyList<QuakeLiveMapOption> Maps);

public sealed record QuakeLiveMapOption(string Key)
{
    public string Label => Key.ToUpperInvariant();
}
```

**Rules:**
- The class is `public static` with a `static` constructor.
- Embedded resource name is `{RootNamespace}.Data.{filename}` — in this project `control_panel.Data.quake_live_catalog.json`.
- The public record types (`*FactoryOption`, `*MapGroup`, `*MapOption`) live in the same file below the catalog class.
- `NormalizeMapSelection` is used by both the serializer and the page model to ensure only valid maps are stored.

---

## Step 4. Seed configuration

Create `services/control-panel/Services/QuakeLiveSeedConfiguration.cs`:

```csharp
using control_panel.Models;

namespace control_panel.Services;

public static class QuakeLiveSeedConfiguration
{
    public static string CreateDefaultJson() =>
        QuakeLiveConfigurationSerializer.Serialize(
            new QuakeLiveServerSettings
            {
                Hostname = "Quake Live Standalone Test",
                Factory = "duel",
                MapList = QuakeLiveModuleCatalog.GetRecommendedMaps("duel").ToList(),
                MaxClients = 16,
                ServerType = 2,
                ZmqRconEnabled = false,
                ZmqRconPort = 28960,
                ZmqRconPassword = string.Empty,
                ZmqStatsEnabled = false,
                ZmqStatsPort = 27960,
                ZmqStatsPassword = string.Empty,
                ServerPassword = string.Empty,
                Tags = "standalone",
            });
}
```

This JSON is written to SQLite the first time a user opens the configuration page. Use the recommended maps from the catalog as the default selection.

---

## Step 5. Game adapter

Create `services/control-panel/Services/QuakeLiveGameAdapter.cs`:

```csharp
using control_panel.Models;

namespace control_panel.Services;

public sealed class QuakeLiveGameAdapter : IGameAdapter
{
    public string GameKey => "quake-live";
    public string DisplayName => "Quake Live";
    public string ConfigurationPagePath => "/Configuration/QuakeLive";

    public GameSummary GetSummary(string? jsonSettings)
    {
        var s = QuakeLiveConfigurationSerializer.Deserialize(jsonSettings);

        var serverTypeLabel = s.ServerType switch
        {
            1 => "LAN",
            2 => "Internet",
            _ => "Offline"
        };

        return new GameSummary(
            ModeName: QuakeLiveModuleCatalog.GetFactoryLabel(s.Factory),
            ModeFlags: serverTypeLabel,
            StartMap: s.MapList.Count > 0
                ? s.MapList[0].ToUpperInvariant()
                : "\u2014",
            MapCountLabel: $"{s.MapList.Count} map(s) selected",
            RotationPreview: s.MapList.Count == 0
                ? "No maps selected"
                : string.Join(", ", s.MapList.Take(6).Select(m => m.ToUpperInvariant())),
            LimitsSummary: $"Max {s.MaxClients} players",
            AccessLabel: string.IsNullOrWhiteSpace(s.ServerPassword)
                ? "Open lobby"
                : "Password protected",
            RconLabel: !s.ZmqRconEnabled
                ? "Disabled"
                : string.IsNullOrWhiteSpace(s.ZmqRconPassword)
                    ? "Required"
                    : "Configured"
        );
    }

    public IReadOnlyDictionary<string, string> GetContainerEnv(string? jsonSettings)
    {
        var s = QuakeLiveConfigurationSerializer.Deserialize(jsonSettings);
        return new Dictionary<string, string>
        {
            ["QL_HOSTNAME"] = s.Hostname,
            ["QL_FACTORY"] = s.Factory,
            ["QL_MAPLIST"] = string.Join(" ", s.MapList),
            ["QL_MAXCLIENTS"] = s.MaxClients.ToString(),
            ["QL_SERVER_TYPE"] = s.ServerType.ToString(),
            ["QL_ZMQ_RCON_ENABLE"] = s.ZmqRconEnabled ? "1" : "0",
            ["QL_ZMQ_RCON_PORT"] = s.ZmqRconPort.ToString(),
            ["QL_ZMQ_RCON_PASSWORD"] = s.ZmqRconPassword,
            ["QL_ZMQ_STATS_ENABLE"] = s.ZmqStatsEnabled ? "1" : "0",
            ["QL_ZMQ_STATS_PORT"] = s.ZmqStatsPort.ToString(),
            ["QL_ZMQ_STATS_PASSWORD"] = s.ZmqStatsPassword,
            ["QL_PASSWORD"] = s.ServerPassword,
            ["QL_TAGS"] = s.Tags,
        };
    }

    public string CreateDefaultJson() => QuakeLiveSeedConfiguration.CreateDefaultJson();
}
```

**The `IGameAdapter` interface:**

```csharp
public interface IGameAdapter
{
    string GameKey { get; }                    // unique key, e.g. "quake-live"
    string DisplayName { get; }               // shown in UI, e.g. "Quake Live"
    string ConfigurationPagePath { get; }     // Razor Page path, e.g. "/Configuration/QuakeLive"
    GameSummary GetSummary(string? jsonSettings);
    IReadOnlyDictionary<string, string> GetContainerEnv(string? jsonSettings);
    string CreateDefaultJson();
}
```

**Rules:**
- Class must be `sealed`.
- `GameKey` must match the key used in docker-agent's `GAME_CONFIGS` dict and the SQLite `GameConfiguration.GameKey` column.
- `ConfigurationPagePath` must match the Razor Page path (Step 7).
- `GetContainerEnv` converts the settings model into `KEY=VALUE` pairs that docker-agent passes as `-e` flags to `docker run`. These env var names must match what the container's entrypoint expects.
- `GetSummary` produces the dashboard card content. The `GameSummary` record has fixed fields — fill them with game-appropriate values.

---

## Step 6. Register the adapter in DI

Add one line to `services/control-panel/Program.cs`:

```csharp
builder.Services.AddSingleton<IGameAdapter, QuakeLiveGameAdapter>();
```

Place it next to the other adapter registrations. That's it — the rest of the system discovers your adapter via `IEnumerable<IGameAdapter>`.

The dashboard (`IndexModel`) automatically picks up all registered adapters. The configuration store (`SqliteConfigurationStore`) calls `CreateDefaultJson()` to seed the database on first access. No other changes to `Program.cs` are needed.

### Dashboard module ordering

If you want your module to appear in a specific position on the dashboard, add the game key to the `ModuleOrder` array in `Services/PanelGameModuleCatalog.cs`:

```csharp
private static readonly string[] ModuleOrder = ["warsow", "warfork", "quake-live", "reflex-arena"];
//                                               ↑ add your game key here
```

`PanelGameModuleCatalog` is the central module registry. It builds the ordered list of all game modules from `IEnumerable<IGameAdapter>` at startup. Modules not listed in `ModuleOrder` appear last (sorted by registration order).

### Navigation and visibility

Navigation links in `_Layout.cshtml` are generated **automatically** from registered adapters via `IModuleVisibilityService.GetVisibleModulesAsync()`. No manual edits to `_Layout.cshtml` are needed — your module appears in the sidebar as soon as it's registered in DI.

Users can hide/show individual modules on the Settings page. The visibility state is stored in SQLite via `SqliteModuleVisibilityService`. By default all registered modules are visible.

---

## Step 7. Configuration Razor Page

Create two files: `Pages/Configuration/QuakeLive.cshtml.cs` (page model) and `Pages/Configuration/QuakeLive.cshtml` (view).

### 7a. Page model (`QuakeLive.cshtml.cs`)

The page model follows a strict pattern. Here's the complete structure:

```csharp
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
    // Select this game's adapter from the injected collection.
    private readonly IGameAdapter _gameAdapter =
        gameAdapters.First(a => a.GameKey == "quake-live");
    private string GameKey => _gameAdapter.GameKey;

    // ── Form binding ─────────────────────────────────────────────────────
    [BindProperty]
    public InputModel Input { get; set; } =
        InputModel.FromSettings(new QuakeLiveServerSettings());

    // ── Catalog data for the view ────────────────────────────────────────
    public IReadOnlyList<QuakeLiveFactoryOption> FactoryOptions =>
        QuakeLiveModuleCatalog.Factories;
    public IReadOnlyList<QuakeLiveMapGroup> MapGroups =>
        QuakeLiveModuleCatalog.MapGroups;

    // ── Server status (delegates to ServerStatusSnapshot) ────────────────
    public ServerStatusSnapshot Status { get; private set; } =
        ServerStatusSnapshot.NotConfigured("quake-live");
    public string StatusToneClass => Status.StatusToneClass;
    public bool CanStart => Status.CanStart;
    public bool CanRestart => Status.CanRestart;
    public bool CanStop => Status.CanStop;
    public bool ShowUnavailableActions => Status.ShowUnavailableActions;

    // ── Password display helpers (delegate to PanelHelpers) ──────────────
    public string CurrentRconPassword { get; private set; } = string.Empty;
    public string CurrentServerPassword { get; private set; } = string.Empty;
    public bool HasConfiguredRconPassword =>
        !string.IsNullOrWhiteSpace(CurrentRconPassword);
    public string RconPasswordStateLabel =>
        HasConfiguredRconPassword ? "Configured" : "Required";
    public string RconPasswordStateClass =>
        HasConfiguredRconPassword ? "status-running" : "status-stopped";
    public bool HasJoinPassword =>
        !string.IsNullOrWhiteSpace(CurrentServerPassword);
    public string JoinPasswordStateLabel =>
        HasJoinPassword ? "Protected lobby" : "Open lobby";
    public string JoinPasswordStateClass =>
        HasJoinPassword ? "status-running" : "status-neutral";
    public string MaskedRconPassword =>
        PanelHelpers.MaskSecret(CurrentRconPassword);
    public string MaskedJoinPassword =>
        PanelHelpers.MaskSecret(CurrentServerPassword);

    // ── Metadata ─────────────────────────────────────────────────────────
    public string UpdatedLabel { get; private set; } = "Never";
    public string UpdatedByLabel { get; private set; } = "System";

    // ── Toast messages ───────────────────────────────────────────────────
    [TempData] public string? SuccessMessage { get; set; }
    [TempData] public string? ErrorMessage { get; set; }

    // ── View helpers ─────────────────────────────────────────────────────
    public bool IsMapSelected(string mapKey) =>
        Input.SelectedMaps.Contains(mapKey, StringComparer.OrdinalIgnoreCase);

    // ── GET ──────────────────────────────────────────────────────────────
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    // ── Server control POST handlers ─────────────────────────────────────
    public async Task<IActionResult> OnPostStartAsync(CancellationToken ct)
    {
        var config = await configurationStore.GetOrCreateAsync(GameKey, ct);
        var env = _gameAdapter.GetContainerEnv(config.JsonContent);
        var result = await dockerAgentClient.StartAsync(GameKey, env, ct);
        StoreResult(result);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRestartAsync(CancellationToken ct)
    {
        var config = await configurationStore.GetOrCreateAsync(GameKey, ct);
        var env = _gameAdapter.GetContainerEnv(config.JsonContent);
        var result = await dockerAgentClient.RestartAsync(GameKey, env, ct);
        StoreResult(result);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostStopAsync(CancellationToken ct)
    {
        var result = await dockerAgentClient.StopAsync(GameKey, ct);
        StoreResult(result);
        return RedirectToPage();
    }

    // ── Save / Apply handlers ────────────────────────────────────────────
    public Task<IActionResult> OnPostSaveAsync(CancellationToken ct) =>
        HandleSaveAsync(restartServer: false, ct);

    public Task<IActionResult> OnPostApplyAsync(CancellationToken ct) =>
        HandleSaveAsync(restartServer: true, ct);

    private async Task<IActionResult> HandleSaveAsync(
        bool restartServer, CancellationToken ct)
    {
        var existing = await configurationStore.GetOrCreateAsync(GameKey, ct);
        var existingSettings = QuakeLiveConfigurationSerializer
            .Deserialize(existing.JsonContent);

        NormalizeInput(fillDefaultsWhenEmpty: false);
        var effectiveSettings = Input.ToSettings(existingSettings);

        // Re-validate after normalization
        ModelState.ClearValidationState(nameof(Input));
        TryValidateModel(Input, nameof(Input));
        ValidateInput(effectiveSettings);

        if (!ModelState.IsValid)
        {
            await LoadAsync(ct, preserveInput: true);
            return Page();
        }

        var json = QuakeLiveConfigurationSerializer.Serialize(effectiveSettings);
        await configurationStore.SaveAsync(
            GameKey, json, User.Identity?.Name ?? "unknown", ct);

        if (!restartServer)
        {
            SuccessMessage = "Quake Live settings saved.";
            return RedirectToPage();
        }

        var env = _gameAdapter.GetContainerEnv(json);
        var result = await dockerAgentClient.RestartAsync(GameKey, env, ct);
        SuccessMessage = result.Success
            ? "Settings saved and restart requested."
            : null;
        ErrorMessage = result.Success
            ? null
            : $"Settings saved, but restart failed: {result.Message}";
        return RedirectToPage();
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private async Task LoadAsync(
        CancellationToken ct, bool preserveInput = false)
    {
        Status = await dockerAgentClient.GetStatusAsync(GameKey, ct);

        var config = await configurationStore.GetOrCreateAsync(GameKey, ct);
        var current = QuakeLiveConfigurationSerializer
            .Deserialize(config.JsonContent);
        CurrentRconPassword = current.ZmqRconPassword;
        CurrentServerPassword = current.ServerPassword;

        if (!preserveInput)
            Input = InputModel.FromSettings(current);

        NormalizeInput(fillDefaultsWhenEmpty: !preserveInput);

        UpdatedLabel = PanelHelpers.FormatUpdatedLabel(config.UpdatedUtc);
        UpdatedByLabel = PanelHelpers.FormatUpdatedByLabel(config.UpdatedBy);
    }

    private void NormalizeInput(bool fillDefaultsWhenEmpty)
    {
        Input.SelectedMaps = QuakeLiveModuleCatalog.NormalizeMapSelection(
            Input.SelectedMaps, Input.Factory, fillDefaultsWhenEmpty);
    }

    private void ValidateInput(QuakeLiveServerSettings effectiveSettings)
    {
        if (!QuakeLiveModuleCatalog.IsValidFactory(Input.Factory))
            ModelState.AddModelError("Input.Factory",
                "Choose a supported Quake Live factory.");

        var invalidMaps = Input.SelectedMaps
            .Where(m => !QuakeLiveModuleCatalog.IsValidMap(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (invalidMaps.Length > 0)
            ModelState.AddModelError("Input.SelectedMaps",
                $"Unsupported maps: {string.Join(", ", invalidMaps)}");

        if (Input.SelectedMaps.Count == 0)
            ModelState.AddModelError("Input.SelectedMaps",
                "Select at least one map for the rotation.");

        if (effectiveSettings.ZmqRconEnabled &&
            string.IsNullOrWhiteSpace(effectiveSettings.ZmqRconPassword))
            ModelState.AddModelError("Input.ZmqRconPassword",
                "RCON password is required when RCON is enabled.");
    }

    private void StoreResult(AgentActionResult result)
    {
        if (result.Success) { SuccessMessage = result.Message; return; }
        ErrorMessage = result.Message;
    }

    // ── InputModel (form binding) ────────────────────────────────────────

    public sealed class InputModel
    {
        [Required]
        [StringLength(128)]
        [Display(Name = "Server hostname")]
        public string Hostname { get; set; } = "Quake Live Standalone Test";

        [Required]
        [Display(Name = "Game factory")]
        public string Factory { get; set; } = "duel";

        public List<string> SelectedMaps { get; set; } = [];

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

        /// <summary>
        /// Merges form input with existing settings.
        /// Empty password fields = keep existing password (not overwrite).
        /// </summary>
        public QuakeLiveServerSettings ToSettings(
            QuakeLiveServerSettings? existing = null)
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
                // Never pre-fill password fields with the current secret
                ZmqStatsPassword = string.Empty,
                ServerPassword = string.Empty,
                ClearServerPassword = false,
                Tags = settings.Tags,
            };
        }
    }
}
```

**Key patterns in the page model:**
- Inject `IEnumerable<IGameAdapter>` and select by `GameKey` — never inject the concrete adapter type.
- Status properties delegate to `ServerStatusSnapshot` (no duplicated switch statements).
- Password display delegates to `PanelHelpers.MaskSecret()`.
- Date display delegates to `PanelHelpers.FormatUpdatedLabel()` / `FormatUpdatedByLabel()`.
- `ToSettings()` merges form input with existing settings — empty password fields keep the saved password.
- `FromSettings()` never pre-fills password fields with the current secret for security.

### 7b. View (`QuakeLive.cshtml`)

The view structure:

```html
@page
@model control_panel.Pages.Configuration.QuakeLiveModel
@{
    ViewData["Title"] = "Quake Live Module";
}

<section class="page-shell py-4 py-lg-5">
    <div class="container-xxl">
        <!-- Page header with status chip -->
        <div class="page-header ...">
            <div>
                <span class="section-kicker">Quake Live Module</span>
                <h1 class="page-title mb-2">Server parameters without touching cfg files</h1>
            </div>
            <div class="d-flex gap-2 align-items-center">
                <div class="status-chip @Model.StatusToneClass">
                    <span class="status-dot"></span>
                    <span>@Model.Status.StateLabel</span>
                </div>
                <a class="btn btn-outline-light" asp-page="/Index">Back to dashboard</a>
            </div>
        </div>

        <!-- Toast messages -->
        @if (!string.IsNullOrWhiteSpace(Model.SuccessMessage))
        {
            <div class="alert alert-success ..." role="alert">@Model.SuccessMessage</div>
        }
        @if (!string.IsNullOrWhiteSpace(Model.ErrorMessage))
        {
            <div class="alert alert-danger ..." role="alert">@Model.ErrorMessage</div>
        }

        <!-- Server control card (Start / Restart / Stop) -->
        <div class="panel-card mb-4">
            <div class="panel-card-header">
                <span class="section-kicker">Container</span>
                <h2 class="panel-title">Server control</h2>
            </div>
            <div class="panel-card-body">
                <div class="d-flex flex-wrap gap-2">
                    @if (Model.CanStart)
                    {
                        <form method="post" asp-page-handler="Start">
                            <button type="submit" class="btn btn-primary btn-lg">
                                Start server</button>
                        </form>
                    }
                    @if (Model.CanRestart)
                    {
                        <form method="post" asp-page-handler="Restart">
                            <button type="submit" class="btn btn-outline-warning">
                                Restart server</button>
                        </form>
                    }
                    @if (Model.CanStop)
                    {
                        <form method="post" asp-page-handler="Stop">
                            <button type="submit" class="btn btn-outline-light">
                                Stop server</button>
                        </form>
                    }
                    @if (Model.ShowUnavailableActions)
                    {
                        <button type="button" class="btn btn-outline-secondary" disabled>
                            Server actions unavailable</button>
                    }
                </div>
            </div>
        </div>

        <!-- Settings form -->
        <form method="post" class="vstack gap-4">
            <div asp-validation-summary="ModelOnly" class="..."></div>

            <!-- Your game-specific form fields go here -->
            <!-- Use asp-for="Input.PropertyName" for binding -->
            <!-- Use asp-validation-for="Input.PropertyName" for errors -->

            <!-- Map selection grid -->
            <div class="panel-card">
                <div class="panel-card-body">
                    @foreach (var group in Model.MapGroups)
                    {
                        <div class="map-group-card">
                            <h3>@group.Label</h3>
                            <div class="map-grid">
                                @foreach (var map in group.Maps)
                                {
                                    <label class="map-check" for="map-@map.Key">
                                        <input id="map-@map.Key"
                                               class="form-check-input"
                                               type="checkbox"
                                               name="Input.SelectedMaps"
                                               value="@map.Key"
                                               @(Model.IsMapSelected(map.Key) ? "checked" : null) />
                                        <span>@map.Label</span>
                                    </label>
                                }
                            </div>
                        </div>
                    }
                </div>
            </div>

            <!-- Save / Apply buttons -->
            <div class="panel-card sticky-actions">
                <div class="panel-card-body ...">
                    <button type="submit" asp-page-handler="Save"
                            class="btn btn-primary btn-lg">Save settings</button>
                    <button type="submit" asp-page-handler="Apply"
                            class="btn btn-outline-warning btn-lg">Save and restart</button>
                </div>
            </div>
        </form>
    </div>
</section>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

**View patterns:**
- `asp-page-handler="Start"` / `"Restart"` / `"Stop"` maps to `OnPostStartAsync`, `OnPostRestartAsync`, `OnPostStopAsync`.
- `asp-page-handler="Save"` / `"Apply"` maps to `OnPostSaveAsync`, `OnPostApplyAsync`.
- Map checkboxes use `name="Input.SelectedMaps"` — ASP.NET Core model binding collects all checked values into `List<string>`.
- Anti-forgery tokens are automatic in Razor Pages forms.

### 7c. Dynamic mode/factory -> map filtering (shared UI contract)

If your game has a dependency such as:
- selected mode limits valid opening maps;
- selected factory limits valid rotation maps;
- selected ruleset limits valid map pool entries;

then **do not add game-specific JavaScript functions to `wwwroot/js/site.js`**.
The shared script must stay generic and be driven entirely by neutral `data-*` attributes.

Use this contract:

- Add `data-choice-filter-scope="true"` to the form or container that owns the filter interaction.
- Add `data-choice-filter-source="true"` to the controlling `<select>` (`Mode`, `Factory`, etc.).
- On each source `<option>`, set `data-choice-filter-preferred-value="..."` if the source has a preferred default target choice.
- Add `data-choice-filter-target-select="true"` to a dependent `<select>` such as `Opening map`.
- On each dependent `<option>` or map checkbox `<input>`, set `data-choice-filter-values="..."` to the space-separated list of source keys that allow it.

Example:

```html
<form method="post" data-choice-filter-scope="true">
  <select asp-for="Input.Factory" data-choice-filter-source="true">
    <option value="duel" data-choice-filter-preferred-value="aerowalk">Duel</option>
    <option value="ctf" data-choice-filter-preferred-value="courtyard">CTF</option>
  </select>

  <select asp-for="Input.StartMap" data-choice-filter-target-select="true">
    <option value="aerowalk" data-choice-filter-values="duel ca tdm">Aerowalk</option>
    <option value="courtyard" data-choice-filter-values="ctf oneflag race">Courtyard</option>
  </select>
</form>
```

For map grids:

```html
<input type="checkbox"
       name="Input.SelectedMaps"
       value="@map.Key"
       data-choice-filter-values="@string.Join(' ', Model.GetSupportedFactoriesForMap(map.Key))"
       disabled="@(!Model.IsMapSupportedForSelectedFactory(map.Key))" />
```

Required server-side support:

- The catalog should expose the normal forward lookup (`IsSupportedMapForFactory`, `IsSupportedMapForMode`) **and** a reverse lookup for the UI (`GetSupportedFactoriesForMap`, `GetSupportedModesForMap`).
- The page model should expose small helpers that the Razor view can call when rendering `data-choice-filter-values`.
- Keep server-side normalization and validation even if the UI filters choices live. The browser is only a convenience layer; the catalog remains the source of truth.

UI behavior handled centrally by the shared script:

- invalid choices are disabled immediately when the source select changes;
- dependent selects fall back to the current valid choice, then preferred value, then first valid value;
- disabled map cards receive the shared `is-disabled` visual state;
- "All" group buttons skip disabled checkboxes automatically.

If a new module needs source->choice filtering, extend the shared generic contract rather than adding a `{GameName}Something()` function to `site.js`.

### 7d. Navigation (automatic)

Navigation links are generated dynamically from registered adapters via `IModuleVisibilityService` in `_Layout.cshtml`. **No manual edits needed** — your module appears in the sidebar automatically after DI registration (Step 6).

---

## Step 8. Unit tests

Create tests for the serializer, catalog, and adapter. Tests live in `tests/control-panel.Tests/Services/`.

### 8a. Serializer tests

`QuakeLiveConfigurationSerializerTests.cs`:

```csharp
using control_panel.Models;
using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class QuakeLiveConfigurationSerializerTests
{
    [Fact]
    public void Deserialize_Null_ReturnsDefaultSettings()
    {
        var settings = QuakeLiveConfigurationSerializer.Deserialize(null);

        Assert.Equal("duel", settings.Factory);
        Assert.NotEmpty(settings.MapList);
        Assert.Equal(16, settings.MaxClients);
    }

    [Fact]
    public void Deserialize_EmptyString_ReturnsDefaultSettings()
    {
        var settings = QuakeLiveConfigurationSerializer.Deserialize(string.Empty);
        Assert.Equal("duel", settings.Factory);
    }

    [Fact]
    public void Serialize_Deserialize_Roundtrip()
    {
        var original = new QuakeLiveServerSettings
        {
            Hostname = "Test Server",
            Factory = "ffa",
            MapList = ["campgrounds", "almostlost"],
            MaxClients = 12,
            ServerType = 1,
            ZmqRconEnabled = true,
            ZmqRconPort = 28961,
            ZmqRconPassword = "secret123",
        };

        var json = QuakeLiveConfigurationSerializer.Serialize(original);
        var restored = QuakeLiveConfigurationSerializer.Deserialize(json);

        Assert.Equal(original.Hostname, restored.Hostname);
        Assert.Equal(original.Factory, restored.Factory);
        Assert.Equal(original.MapList, restored.MapList);
        Assert.Equal(original.MaxClients, restored.MaxClients);
        Assert.Equal(original.ZmqRconEnabled, restored.ZmqRconEnabled);
        Assert.Equal(original.ZmqRconPassword, restored.ZmqRconPassword);
    }

    [Fact]
    public void Deserialize_InvalidJson_ReturnsDefaultSettings()
    {
        var settings = QuakeLiveConfigurationSerializer.Deserialize("{not valid}");
        Assert.Equal("duel", settings.Factory);
    }

    [Fact]
    public void Serialize_ProducesValidJson()
    {
        var json = QuakeLiveConfigurationSerializer.Serialize(new QuakeLiveServerSettings());

        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.Contains("g_factory", json);
        Assert.Contains("g_maplist", json);
    }
}
```

### 8b. Catalog tests

`QuakeLiveModuleCatalogTests.cs`:

```csharp
using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class QuakeLiveModuleCatalogTests
{
    [Fact]
    public void AllFactories_HaveNonEmptyLabels()
    {
        Assert.All(QuakeLiveModuleCatalog.Factories,
            f => Assert.NotEmpty(f.Label));
    }

    [Fact]
    public void AllFactories_RecommendedMaps_AreValidMaps()
    {
        foreach (var factory in QuakeLiveModuleCatalog.Factories)
        {
            foreach (var map in factory.RecommendedMaps)
            {
                Assert.True(
                    QuakeLiveModuleCatalog.IsValidMap(map),
                    $"Factory '{factory.Key}' recommends invalid map '{map}'.");
            }
        }
    }

    [Fact]
    public void IsValidFactory_ReturnsTrue_ForKnownKey()
    {
        Assert.True(QuakeLiveModuleCatalog.IsValidFactory("ca"));
    }

    [Fact]
    public void IsValidMap_ReturnsTrue_ForKnownMap()
    {
        Assert.True(QuakeLiveModuleCatalog.IsValidMap("campgrounds"));
    }

    [Fact]
    public void NormalizeMapSelection_FillsDefaults_WhenEmpty()
    {
        var result = QuakeLiveModuleCatalog.NormalizeMapSelection(
            [], "ca", fillDefaultsWhenEmpty: true);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void NormalizeMapSelection_FiltersInvalidMaps()
    {
        var result = QuakeLiveModuleCatalog.NormalizeMapSelection(
            ["campgrounds", "not_a_real_map"], "ca",
            fillDefaultsWhenEmpty: false);
        Assert.Equal(["campgrounds"], result);
    }
}
```

### 8c. Adapter tests

`QuakeLiveGameAdapterTests.cs`:

```csharp
using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class QuakeLiveGameAdapterTests
{
    private readonly QuakeLiveGameAdapter _adapter = new();

    [Fact]
    public void GameKey_IsQuakeLive()
    {
        Assert.Equal("quake-live", _adapter.GameKey);
    }

    [Fact]
    public void Adapter_ImplementsIGameAdapter()
    {
        Assert.IsAssignableFrom<IGameAdapter>(_adapter);
    }

    [Fact]
    public void GetSummary_ReturnsNonNullSummary()
    {
        Assert.NotNull(_adapter.GetSummary(null));
    }

    [Fact]
    public void CreateDefaultJson_ReturnsNonEmptyString()
    {
        Assert.NotEmpty(_adapter.CreateDefaultJson());
    }

    [Fact]
    public void GetContainerEnv_ReturnsNonEmptyDictionary()
    {
        Assert.NotEmpty(_adapter.GetContainerEnv(null));
    }
}
```

Run tests:

```bash
dotnet test tests/control-panel.Tests/control-panel.Tests.csproj
```

---

## Step 9. Game server container

Create `services/quake-live-server/` with a `Dockerfile` and `docker-entrypoint.sh`.

### 9a. Dockerfile

```dockerfile
FROM ubuntu:22.04

ARG DEBIAN_FRONTEND=noninteractive

# Install dependencies for your game
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
    ca-certificates curl tini netcat-openbsd \
 && rm -rf /var/lib/apt/lists/*

# Download / install the game server binary
RUN mkdir -p /opt/quake-live \
 && curl -fsSL https://example.com/game-server.tar.gz | tar -xz -C /opt/quake-live

COPY services/quake-live-server/docker-entrypoint.sh /usr/local/bin/entrypoint
RUN chmod +x /usr/local/bin/entrypoint

# Create a non-root user
RUN groupadd --system gameuser \
 && useradd --system --gid gameuser --home-dir /var/lib/quake-live --create-home gameuser

VOLUME ["/var/lib/quake-live"]

EXPOSE 27960/udp 27960/tcp

HEALTHCHECK --interval=30s --timeout=5s --start-period=90s --retries=3 \
    CMD nc -zu 127.0.0.1 27960 || exit 1

ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/entrypoint"]
```

### 9b. Entrypoint

The entrypoint reads env vars (set by docker-agent from `GetContainerEnv()`) and writes config files.

```bash
#!/bin/sh
set -eu

# Read env vars with defaults (must match GetContainerEnv keys)
QL_HOSTNAME="${QL_HOSTNAME:-Quake Live Standalone Test}"
QL_FACTORY="${QL_FACTORY:-duel}"
QL_MAPLIST="${QL_MAPLIST:-asylum brimstoneabbey campgrounds purgatory theedge}"
QL_MAXCLIENTS="${QL_MAXCLIENTS:-16}"
QL_SERVER_TYPE="${QL_SERVER_TYPE:-2}"
QL_PASSWORD="${QL_PASSWORD:-}"

# Write runtime config from env vars
cat > /var/lib/quake-live/baseq3/server.cfg << CFG
set sv_hostname "${QL_HOSTNAME}"
set sv_maxclients "${QL_MAXCLIENTS}"
set g_password "${QL_PASSWORD}"
CFG

# Write map rotation
for map in $QL_MAPLIST; do
    printf '%s|%s\n' "$map" "$QL_FACTORY"
done > /var/lib/quake-live/baseq3/mappool.txt

printf '[entrypoint] Maps: %s\n' "$QL_MAPLIST"

# Start the game server
exec /opt/quake-live/run_server.sh "$@"
```

**Rules:**
- Start with `set -eu` — fail-fast on errors.
- Use `tini` as PID 1 for proper signal handling.
- Run as non-root user when possible.
- Env var names in the entrypoint must match the keys from `GetContainerEnv()` in the adapter.

---

## Step 10. Docker Compose entry

Add to `docker-compose.yml`:

```yaml
  # ── Quake Live server (image build target; started on demand via agent) ──
  quake-live-server:
    build:
      context: .
      dockerfile: services/quake-live-server/Dockerfile
    image: quake-live-server:latest
    profiles:
      - game      # Not started by `docker compose up` — built only
    ports:
      - "${QL_SERVER_PORT:-27970}:27960/udp"
      - "${QL_SERVER_PORT:-27970}:27960/tcp"
      - "${QL_RCON_PORT:-28970}:28960/tcp"
    volumes:
      - quake-live-standalone-data:/var/lib/quake-live
    cpus: 1.0
    mem_limit: 512m
    restart: "no"
```

Add the volume:

```yaml
volumes:
  quake-live-standalone-data:
    name: quake-live-standalone-data
```

**Rules:**
- Use `profiles: [game]` so the container is only built, never started by `docker compose up`.
- Use `restart: "no"` — docker-agent manages the lifecycle with `--restart unless-stopped`.
- Add `cpus` and `mem_limit` resource limits to prevent a runaway game server from saturating the host.

---

## Step 11. Docker agent config

Add to the `GAME_CONFIGS` dict in `services/docker-agent/agent.py`:

```python
GAME_CONFIGS = {
    # ... existing games ...
    "quake-live": {
        "container_name": "quake-live-server",
        "image": os.environ.get("QL_IMAGE", "quake-live-server:latest"),
        "ports": [
            f"{os.environ.get('QL_SERVER_PORT', '27970')}:27960/udp",
            f"{os.environ.get('QL_SERVER_PORT', '27970')}:27960/tcp",
            f"{os.environ.get('QL_RCON_PORT', '28970')}:28960/tcp",
        ],
        "volumes": [
            f"{os.environ.get('QL_DATA_VOLUME', 'quake-live-standalone-data')}:/var/lib/quake-live",
        ],
        "env": {
            # Default env vars (overridden by control-panel at runtime)
            "QL_HOSTNAME": "Quake Live Standalone Test",
            "QL_FACTORY": "duel",
            "QL_MAPLIST": "asylum brimstoneabbey campgrounds purgatory theedge",
            "QL_MAXCLIENTS": "16",
        },
        "command": ["+set", "com_crashreport", "0"],  # optional extra args
    },
}
```

**Key in the dict must match `GameKey` from the adapter** (`"quake-live"`).

Pass the agent's env vars in `docker-compose.yml` under the `docker-agent` service:

```yaml
  docker-agent:
    environment:
      QL_IMAGE: "${QL_IMAGE:-quake-live-server:latest}"
      QL_SERVER_PORT: "${QL_SERVER_PORT:-27970}"
      QL_RCON_PORT: "${QL_RCON_PORT:-28970}"
      QL_DATA_VOLUME: "quake-live-standalone-data"
```

**How it works:**
1. Control panel calls `POST /api/games/quake-live/start` with `{"env": {"QL_HOSTNAME": "...", ...}}`.
2. Agent looks up `GAME_CONFIGS["quake-live"]`.
3. Agent merges the request `env` over the default `env` in the config.
4. Agent runs `docker run -d --name quake-live-server -p ... -v ... -e QL_HOSTNAME=... image:tag`.
5. Container entrypoint reads env vars and writes config files.

---

## Step 12. Environment variables

Add to `.env.example`:

```bash
# ── Quake Live server ─────────────────────────────────────────────────────────
QL_IMAGE=quake-live-server:latest
QL_SERVER_PORT=27970
QL_RCON_PORT=28970
```

---

## Build and verify

```bash
# Build the game server image
docker compose --profile game build quake-live-server

# Build and start the platform
docker compose up --build

# Run all tests
dotnet test tests/control-panel.Tests/control-panel.Tests.csproj
```

Then open the panel at `http://localhost:5099`, log in, and verify:
1. Dashboard shows the new game module card with correct summary.
2. Configuration page loads with default settings.
3. Saving settings persists to SQLite (check "Last updated" timestamp).
4. Start/Stop/Restart buttons work when docker-agent is running.
5. "Save and restart" saves config and restarts the container.

---

## Checklist

| # | Step | Files |
|---|---|---|
| 1 | Settings model | `Models/{Game}ServerSettings.cs` |
| 2 | Serializer | `Services/{Game}ConfigurationSerializer.cs` |
| 3 | Module catalog + JSON | `Services/{Game}ModuleCatalog.cs` + `Data/{game}_catalog.json` |
| 4 | Embed resource | `control-panel.csproj` |
| 5 | Seed config | `Services/{Game}SeedConfiguration.cs` |
| 6 | Game adapter | `Services/{Game}GameAdapter.cs` |
| 7 | Register in DI | `Program.cs` (one line) |
| 8 | Dashboard order | `Services/PanelGameModuleCatalog.cs` `ModuleOrder` array |
| 9 | Config page | `Pages/Configuration/{Game}.cshtml` + `.cshtml.cs` |
| 10 | Dynamic UI filter | If mode/factory changes valid maps, use the shared `data-choice-filter-*` contract in Razor + catalog helpers |
| 11 | Nav link | Automatic (via `IModuleVisibilityService` in `_Layout.cshtml`) |
| 12 | Unit tests | `tests/control-panel.Tests/Services/{Game}*.cs` |
| 13 | Container | `services/{game}-server/Dockerfile` + `docker-entrypoint.sh` |
| 14 | Compose entry | `docker-compose.yml` (service + volume) |
| 15 | Agent config | `services/docker-agent/agent.py` `GAME_CONFIGS` |
| 16 | Agent env vars | `docker-compose.yml` under `docker-agent` |
| 17 | Env example | `.env.example` |
| 18 | Build & test | `dotnet test` + `docker compose --profile game build` |

---

## Shared utilities reference

These are the shared classes you should use rather than reimplementing:

| Class | Purpose | Used by |
|---|---|---|
| `GameConfigJsonReader` | JSON parsing helpers (`ReadString`, `ReadInt`, `ReadBoolean`, `ReadStringList`) | Serializers |
| `PanelHelpers` | `MaskSecret()`, `FormatUpdatedLabel()`, `FormatUpdatedByLabel()` | Page models |
| `ServerStatusSnapshot` | `StatusToneClass`, `CanStart`, `CanRestart`, `CanStop`, `ShowUnavailableActions` | Page models, dashboard |
| `GameSummary` | Dashboard card content record | Adapters |
| `IConfigurationStore` | `GetOrCreateAsync()`, `SaveAsync()` — reads/writes JSON to SQLite | Page models, API |
| `IDockerAgentClient` | `GetStatusAsync()`, `StartAsync()`, `StopAsync()`, `RestartAsync()` | Page models, API |
| `PanelGameModuleCatalog` | Central module registry — ordered list of all adapters, `IsKnownGameKey()` | Dashboard, settings, API |
| `IModuleVisibilityService` | `GetVisibleModulesAsync()`, `GetAsync()`, `SaveAsync()` — controls which modules are shown | `_Layout.cshtml`, settings page |
