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
        var assembly = typeof(QuakeLiveModuleCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream("control_panel.Data.quake_live_catalog.json")
            ?? throw new InvalidOperationException("Embedded resource 'quake_live_catalog.json' not found.");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var doc = JsonSerializer.Deserialize<CatalogDoc>(stream, options)!;

        MapGroups = doc.MapGroups
            .Select(g => new QuakeLiveMapGroup(g.Key, g.Label, g.Maps.Select(m => new QuakeLiveMapOption(m)).ToArray()))
            .ToArray();

        AllMaps = MapGroups
            .SelectMany(g => g.Maps)
            .Select(m => m.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Factories = doc.Factories
            .Select(f => new QuakeLiveFactoryOption(
                f.Key,
                f.Label,
                f.Description,
                f.RecommendedMaps,
                f.SupportedMapGroups))
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
            : Factories.FirstOrDefault(f => string.Equals(f.Key, factory, StringComparison.OrdinalIgnoreCase));

    public static string GetFactoryLabel(string? factory) =>
        FindFactory(factory)?.Label ?? "Custom";

    public static IReadOnlyList<string> GetRecommendedMaps(string? factory) =>
        FindFactory(factory)?.RecommendedMaps ?? [];

    public static IReadOnlyList<string> GetUnsupportedMapsForFactory(
        IEnumerable<string>? selectedMaps,
        string? factory)
    {
        if (selectedMaps is null ||
            string.IsNullOrWhiteSpace(factory) ||
            !SupportedMapsByFactory.TryGetValue(factory, out var supportedMaps))
        {
            return [];
        }

        return selectedMaps
            .Where(IsValidMap)
            .Where(map => !supportedMaps.Contains(map))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsSupportedMapForFactory(string? mapKey, string? factory)
    {
        if (!IsValidMap(mapKey))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(factory) ||
               !SupportedMapsByFactory.TryGetValue(factory, out var supportedMaps) ||
               supportedMaps.Contains(mapKey!);
    }

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
            normalized.AddRange(GetRecommendedMaps(factory).Where(map => IsSupportedMapForFactory(map, factory)));
        }

        return normalized;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildSupportedMapsByFactory(
        IEnumerable<QuakeLiveFactoryOption> factories,
        IEnumerable<QuakeLiveMapGroup> mapGroups)
    {
        var mapGroupsByKey = mapGroups.ToDictionary(group => group.Key, StringComparer.OrdinalIgnoreCase);
        var supportedMapsByFactory = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var factory in factories)
        {
            if (factory.SupportedMapGroups.Count == 0)
            {
                continue;
            }

            var missingGroup = factory.SupportedMapGroups
                .FirstOrDefault(groupKey => !mapGroupsByKey.ContainsKey(groupKey));

            if (missingGroup is not null)
            {
                throw new InvalidOperationException(
                    $"Factory '{factory.Key}' references unknown map group '{missingGroup}' in quake_live_catalog.json.");
            }

            var supportedMaps = factory.SupportedMapGroups
                .SelectMany(groupKey => mapGroupsByKey[groupKey].Maps)
                .Select(map => map.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            supportedMapsByFactory[factory.Key] = supportedMaps;
        }

        return supportedMapsByFactory;
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
