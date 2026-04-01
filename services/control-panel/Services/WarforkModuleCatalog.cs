// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using System.Text.Json;

namespace control_panel.Services;

public static class WarforkModuleCatalog
{
    public static IReadOnlyList<WarforkGametypeOption> Gametypes { get; }
    public static IReadOnlyList<WarforkMapGroup> MapGroups { get; }
    public static IReadOnlyList<string> AllMaps { get; }

    static WarforkModuleCatalog()
    {
        var assembly = typeof(WarforkModuleCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream("control_panel.Data.warfork_catalog.json")
            ?? throw new InvalidOperationException("Embedded resource 'warfork_catalog.json' not found.");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var doc = JsonSerializer.Deserialize<CatalogDoc>(stream, options)!;

        MapGroups = doc.MapGroups
            .Select(g => new WarforkMapGroup(g.Key, g.Label, g.Maps.Select(m => new WarforkMapOption(m)).ToArray()))
            .ToArray();

        AllMaps = MapGroups
            .SelectMany(g => g.Maps)
            .Select(m => m.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Gametypes = doc.Gametypes
            .Select(g => new WarforkGametypeOption(
                g.Key,
                g.Label,
                g.Description,
                g.RecommendedMaps,
                g.DefaultScorelimit,
                g.DefaultTimelimit))
            .ToArray();
    }

    public static bool IsValidGametype(string? gametype) =>
        !string.IsNullOrWhiteSpace(gametype) &&
        Gametypes.Any(option => string.Equals(option.Key, gametype, StringComparison.OrdinalIgnoreCase));

    public static bool IsValidMap(string? mapKey) =>
        !string.IsNullOrWhiteSpace(mapKey) &&
        AllMaps.Contains(mapKey, StringComparer.OrdinalIgnoreCase);

    public static WarforkGametypeOption? FindGametype(string? gametype) =>
        string.IsNullOrWhiteSpace(gametype)
            ? null
            : Gametypes.FirstOrDefault(option => string.Equals(option.Key, gametype, StringComparison.OrdinalIgnoreCase));

    public static string GetGametypeLabel(string? gametype) =>
        FindGametype(gametype)?.Label ?? "Custom";

    public static IReadOnlyList<string> GetRecommendedMaps(string? gametype) =>
        FindGametype(gametype)?.RecommendedMaps ?? [];

    public static IReadOnlyList<string> GetUnsupportedMapsForGametype(
        IEnumerable<string>? selectedMaps,
        string? gametype) => [];

    public static bool IsSupportedMapForGametype(string? mapKey, string? gametype)
        => IsValidMap(mapKey);

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

        return GetRecommendedMaps(gametype).FirstOrDefault() ?? "return";
    }
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

public sealed record WarforkGametypeOption(
    string Key,
    string Label,
    string Description,
    IReadOnlyList<string> RecommendedMaps,
    int DefaultScorelimit = 0,
    int DefaultTimelimit = 0);

public sealed record WarforkMapGroup(
    string Key,
    string Label,
    IReadOnlyList<WarforkMapOption> Maps);

public sealed record WarforkMapOption(string Key)
{
    public string Label => Key.ToUpperInvariant();
}
