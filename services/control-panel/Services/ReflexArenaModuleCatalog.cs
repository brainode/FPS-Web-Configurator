// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace control_panel.Services;

public static class ReflexArenaModuleCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> SupportedMapsByMode;
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> SupportedModesByMap;
    private static readonly IReadOnlyDictionary<string, ReflexArenaMapOption> MapsByKey;
    private static readonly IReadOnlyDictionary<string, ReflexArenaMutatorOption> MutatorsByKey;

    public static IReadOnlyList<ReflexArenaModeOption> Modes { get; }
    public static IReadOnlyList<ReflexArenaMutatorOption> Mutators { get; }
    public static IReadOnlyList<ReflexArenaMapGroup> MapGroups { get; }
    public static IReadOnlyList<string> AllMaps { get; }

    static ReflexArenaModuleCatalog()
    {
        var assembly = typeof(ReflexArenaModuleCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream("control_panel.Data.reflex_arena_catalog.json")
            ?? throw new InvalidOperationException("Embedded resource 'reflex_arena_catalog.json' not found.");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var doc = JsonSerializer.Deserialize<CatalogDoc>(stream, options)!;

        MapGroups = doc.MapGroups
            .Select(group => new ReflexArenaMapGroup(
                group.Key,
                group.Label,
                group.Maps.Select(mapKey => new ReflexArenaMapOption(mapKey, HumanizeLabel(mapKey))).ToArray()))
            .ToArray();

        MapsByKey = MapGroups
            .SelectMany(group => group.Maps)
            .DistinctBy(map => map.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(map => map.Key, StringComparer.OrdinalIgnoreCase);

        AllMaps = MapsByKey.Keys
            .OrderBy(mapKey => mapKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Mutators = doc.Mutators
            .Select(mutator => new ReflexArenaMutatorOption(mutator.Key, mutator.Label, mutator.Description))
            .ToArray();

        MutatorsByKey = Mutators.ToDictionary(mutator => mutator.Key, StringComparer.OrdinalIgnoreCase);

        Modes = doc.Modes
            .Select(mode => new ReflexArenaModeOption(
                mode.Key,
                mode.Label,
                mode.Description,
                mode.RecommendedMap,
                mode.SupportedMapGroups))
            .ToArray();

        SupportedMapsByMode = BuildSupportedMapsByMode(Modes, MapGroups);
        SupportedModesByMap = BuildSupportedModesByMap(Modes);
    }

    public static bool IsValidMode(string? mode) =>
        !string.IsNullOrWhiteSpace(mode) &&
        Modes.Any(option => string.Equals(option.Key, mode, StringComparison.OrdinalIgnoreCase));

    public static bool IsValidMap(string? mapKey) =>
        !string.IsNullOrWhiteSpace(mapKey) &&
        MapsByKey.ContainsKey(mapKey);

    public static bool IsValidMutator(string? mutatorKey) =>
        !string.IsNullOrWhiteSpace(mutatorKey) &&
        MutatorsByKey.ContainsKey(mutatorKey);

    public static ReflexArenaModeOption? FindMode(string? mode) =>
        string.IsNullOrWhiteSpace(mode)
            ? null
            : Modes.FirstOrDefault(option => string.Equals(option.Key, mode, StringComparison.OrdinalIgnoreCase));

    public static ReflexArenaMapOption? FindMap(string? mapKey) =>
        string.IsNullOrWhiteSpace(mapKey) || !MapsByKey.TryGetValue(mapKey, out var map)
            ? null
            : map;

    public static ReflexArenaMutatorOption? FindMutator(string? mutatorKey) =>
        string.IsNullOrWhiteSpace(mutatorKey) || !MutatorsByKey.TryGetValue(mutatorKey, out var mutator)
            ? null
            : mutator;

    public static string GetModeLabel(string? mode) =>
        FindMode(mode)?.Label ?? "Custom";

    public static string GetMapLabel(string? mapKey) =>
        FindMap(mapKey)?.Label ?? HumanizeLabel(mapKey ?? string.Empty);

    public static string GetRecommendedMap(string? mode) =>
        ResolveStartMap(null, mode);

    public static IReadOnlyList<string> GetSupportedMapsForMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode) ||
            !SupportedMapsByMode.TryGetValue(mode, out var supportedMaps))
        {
            return [];
        }

        return MapGroups
            .SelectMany(group => group.Maps)
            .Select(map => map.Key)
            .Where(supportedMaps.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsSupportedMapForMode(string? mapKey, string? mode)
    {
        if (!IsValidMap(mapKey) || string.IsNullOrWhiteSpace(mode))
        {
            return false;
        }

        return SupportedMapsByMode.TryGetValue(mode, out var supportedMaps) &&
               supportedMaps.Contains(mapKey!);
    }

    public static IReadOnlyList<string> GetSupportedModesForMap(string? mapKey)
    {
        if (string.IsNullOrWhiteSpace(mapKey) ||
            !SupportedModesByMap.TryGetValue(mapKey, out var supportedModes))
        {
            return [];
        }

        return Modes
            .Select(mode => mode.Key)
            .Where(supportedModes.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string ResolveStartMap(string? mapKey, string? mode)
    {
        if (IsSupportedMapForMode(mapKey, mode))
        {
            return FindMap(mapKey)!.Key;
        }

        var recommendedMap = FindMode(mode)?.RecommendedMap;
        if (IsSupportedMapForMode(recommendedMap, mode))
        {
            return FindMap(recommendedMap)!.Key;
        }

        return MapGroups
            .SelectMany(group => group.Maps)
            .Select(map => map.Key)
            .FirstOrDefault(map => IsSupportedMapForMode(map, mode), string.Empty);
    }

    public static List<string> NormalizeMutatorSelection(IEnumerable<string>? selectedMutators)
    {
        return (selectedMutators ?? [])
            .Where(IsValidMutator)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildSupportedMapsByMode(
        IEnumerable<ReflexArenaModeOption> modes,
        IEnumerable<ReflexArenaMapGroup> mapGroups)
    {
        var mapGroupsByKey = mapGroups.ToDictionary(group => group.Key, StringComparer.OrdinalIgnoreCase);
        var supportedMapsByMode = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mode in modes)
        {
            if (mode.SupportedMapGroups.Count == 0)
            {
                continue;
            }

            var missingGroup = mode.SupportedMapGroups
                .FirstOrDefault(groupKey => !mapGroupsByKey.ContainsKey(groupKey));

            if (missingGroup is not null)
            {
                throw new InvalidOperationException(
                    $"Mode '{mode.Key}' references unknown map group '{missingGroup}' in reflex_arena_catalog.json.");
            }

            var supportedMaps = mode.SupportedMapGroups
                .SelectMany(groupKey => mapGroupsByKey[groupKey].Maps)
                .Select(map => map.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            supportedMapsByMode[mode.Key] = supportedMaps;
        }

        return supportedMapsByMode;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildSupportedModesByMap(
        IEnumerable<ReflexArenaModeOption> modes)
    {
        var mapsByMode = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mode in modes)
        {
            IEnumerable<string> supportedMapKeys = SupportedMapsByMode.TryGetValue(mode.Key, out var supportedMaps)
                ? supportedMaps
                : [];

            foreach (var mapKey in supportedMapKeys)
            {
                if (!mapsByMode.TryGetValue(mapKey, out var supportedModes))
                {
                    supportedModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    mapsByMode[mapKey] = supportedModes;
                }

                supportedModes.Add(mode.Key);
            }
        }

        return mapsByMode.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlySet<string>)entry.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static string HumanizeLabel(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        var withSpaces = key.Replace('_', ' ');
        withSpaces = Regex.Replace(withSpaces, "([a-z0-9])([A-Z])", "$1 $2");
        withSpaces = Regex.Replace(withSpaces, "\\s+", " ").Trim();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(withSpaces.ToLowerInvariant());
    }

    private sealed class CatalogDoc
    {
        public List<ModeDoc> Modes { get; set; } = [];
        public List<MutatorDoc> Mutators { get; set; } = [];
        public List<MapGroupDoc> MapGroups { get; set; } = [];
    }

    private sealed class ModeDoc
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RecommendedMap { get; set; } = string.Empty;
        public List<string> SupportedMapGroups { get; set; } = [];
    }

    private sealed class MutatorDoc
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    private sealed class MapGroupDoc
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public List<string> Maps { get; set; } = [];
    }
}

public sealed record ReflexArenaModeOption(
    string Key,
    string Label,
    string Description,
    string RecommendedMap,
    IReadOnlyList<string> SupportedMapGroups);

public sealed record ReflexArenaMutatorOption(
    string Key,
    string Label,
    string Description);

public sealed record ReflexArenaMapGroup(
    string Key,
    string Label,
    IReadOnlyList<ReflexArenaMapOption> Maps);

public sealed record ReflexArenaMapOption(
    string Key,
    string Label);
