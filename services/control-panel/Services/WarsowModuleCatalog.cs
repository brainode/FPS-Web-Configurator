using System.Text.Json;

namespace control_panel.Services;

public static class WarsowModuleCatalog
{
    public static IReadOnlyList<WarsowGametypeOption> Gametypes { get; }
    public static IReadOnlyList<WarsowMapGroup> MapGroups { get; }
    public static IReadOnlyList<string> AllMaps { get; }

    static WarsowModuleCatalog()
    {
        var assembly = typeof(WarsowModuleCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream("control_panel.Data.warsow_catalog.json")
            ?? throw new InvalidOperationException("Embedded resource 'warsow_catalog.json' not found.");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var doc = JsonSerializer.Deserialize<CatalogDoc>(stream, options)!;

        Gametypes = doc.Gametypes
            .Select(g => new WarsowGametypeOption(g.Key, g.Label, g.Description, g.RecommendedMaps, g.DefaultScorelimit, g.DefaultTimelimit))
            .ToArray();

        MapGroups = doc.MapGroups
            .Select(g => new WarsowMapGroup(g.Key, g.Label, g.Maps.Select(m => new WarsowMapOption(m)).ToArray()))
            .ToArray();

        AllMaps = MapGroups
            .SelectMany(g => g.Maps)
            .Select(m => m.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsValidGametype(string? gametype) =>
        !string.IsNullOrWhiteSpace(gametype) &&
        Gametypes.Any(option => string.Equals(option.Key, gametype, StringComparison.OrdinalIgnoreCase));

    public static bool IsValidMap(string? mapKey) =>
        !string.IsNullOrWhiteSpace(mapKey) &&
        AllMaps.Contains(mapKey, StringComparer.OrdinalIgnoreCase);

    public static WarsowGametypeOption? FindGametype(string? gametype) =>
        string.IsNullOrWhiteSpace(gametype)
            ? null
            : Gametypes.FirstOrDefault(option => string.Equals(option.Key, gametype, StringComparison.OrdinalIgnoreCase));

    public static string GetGametypeLabel(string? gametype) =>
        FindGametype(gametype)?.Label ?? "Custom";

    public static IReadOnlyList<string> GetRecommendedMaps(string? gametype) =>
        FindGametype(gametype)?.RecommendedMaps ?? [];

    public static List<string> NormalizeMapSelection(
        IEnumerable<string>? selectedMaps,
        string? gametype = null,
        bool fillDefaultsWhenEmpty = true)
    {
        var normalized = (selectedMaps ?? [])
            .Where(IsValidMap)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0 && fillDefaultsWhenEmpty)
        {
            normalized.AddRange(GetRecommendedMaps(gametype));
        }

        return normalized;
    }

    public static string ResolveStartMap(
        string? startMap,
        IReadOnlyList<string> selectedMaps,
        string? gametype = null,
        bool allowEmpty = false)
    {
        if (IsValidMap(startMap) && selectedMaps.Contains(startMap!, StringComparer.OrdinalIgnoreCase))
        {
            return startMap!;
        }

        if (selectedMaps.Count > 0)
        {
            return selectedMaps[0];
        }

        if (allowEmpty)
        {
            return string.Empty;
        }

        return GetRecommendedMaps(gametype).FirstOrDefault() ?? "wca1";
    }

    // ── JSON deserialization DTOs ─────────────────────────────────────────────

    private sealed class CatalogDoc
    {
        public List<GametypeDoc> Gametypes { get; set; } = [];
        public List<MapGroupDoc> MapGroups { get; set; } = [];
    }

    private sealed class GametypeDoc
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> RecommendedMaps { get; set; } = [];
        public int DefaultScorelimit { get; set; }
        public int DefaultTimelimit { get; set; }
    }

    private sealed class MapGroupDoc
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public List<string> Maps { get; set; } = [];
    }
}

public sealed record WarsowGametypeOption(
    string Key,
    string Label,
    string Description,
    IReadOnlyList<string> RecommendedMaps,
    int DefaultScorelimit = 0,
    int DefaultTimelimit = 0);

public sealed record WarsowMapGroup(
    string Key,
    string Label,
    IReadOnlyList<WarsowMapOption> Maps);

public sealed record WarsowMapOption(string Key)
{
    public string Label => Key.ToUpperInvariant();
}
