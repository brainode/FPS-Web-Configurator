// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using System.Text.Json;

namespace control_panel.Services;

public static class ReflexArenaWeaponsCatalog
{
    public static IReadOnlyList<ReflexArenaWeaponEntry> Weapons { get; }
    public static IReadOnlyList<ReflexArenaPickupEntry> Pickups { get; }
    public static IReadOnlyList<ReflexArenaGlobalConstEntry> GlobalConstants { get; }

    private static readonly IReadOnlyDictionary<string, ReflexArenaWeaponEntry> WeaponsByKey;
    private static readonly IReadOnlyDictionary<string, ReflexArenaPickupEntry> PickupsByKey;

    static ReflexArenaWeaponsCatalog()
    {
        var assembly = typeof(ReflexArenaWeaponsCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            "control_panel.Data.reflex_arena_weapons_catalog.json")
            ?? throw new InvalidOperationException(
                "Embedded resource 'reflex_arena_weapons_catalog.json' not found.");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var doc = JsonSerializer.Deserialize<WeaponsCatalogDoc>(stream, options)!;

        Weapons = doc.Weapons
            .Select(w => new ReflexArenaWeaponEntry(
                w.Key,
                w.Label,
                w.Description,
                w.GconstPrefix,
                w.HasSplashDamage,
                w.HasAmmo,
                w.DefaultDirectDamage,
                w.DefaultSplashDamage,
                w.DefaultAmmo,
                w.DamageMin,
                w.DamageMax))
            .ToArray();

        WeaponsByKey = Weapons.ToDictionary(w => w.Key, StringComparer.OrdinalIgnoreCase);

        Pickups = doc.Pickups
            .Select(p => new ReflexArenaPickupEntry(p.Key, p.Label, p.Description, p.Gconst))
            .ToArray();

        PickupsByKey = Pickups.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);

        GlobalConstants = doc.GlobalConstants
            .Select(g => new ReflexArenaGlobalConstEntry(
                g.Key, g.Gconst, g.Label, g.Hint, g.DefaultValue, g.Min, g.Max))
            .ToArray();
    }

    public static ReflexArenaWeaponEntry? FindWeapon(string? key) =>
        !string.IsNullOrWhiteSpace(key) && WeaponsByKey.TryGetValue(key, out var entry)
            ? entry
            : null;

    public static ReflexArenaPickupEntry? FindPickup(string? key) =>
        !string.IsNullOrWhiteSpace(key) && PickupsByKey.TryGetValue(key, out var entry)
            ? entry
            : null;

    private sealed class WeaponsCatalogDoc
    {
        public List<WeaponDoc> Weapons { get; set; } = [];
        public List<PickupDoc> Pickups { get; set; } = [];
        public List<GlobalConstDoc> GlobalConstants { get; set; } = [];
    }

    private sealed class WeaponDoc
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string GconstPrefix { get; set; } = string.Empty;
        public bool HasSplashDamage { get; set; }
        public bool HasAmmo { get; set; }
        public int DefaultDirectDamage { get; set; }
        public int? DefaultSplashDamage { get; set; }
        public int DefaultAmmo { get; set; }
        public int DamageMin { get; set; } = -999;
        public int DamageMax { get; set; } = 999;
    }

    private sealed class PickupDoc
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Gconst { get; set; } = string.Empty;
    }

    private sealed class GlobalConstDoc
    {
        public string Key { get; set; } = string.Empty;
        public string Gconst { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Hint { get; set; } = string.Empty;
        public int DefaultValue { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
    }
}

public sealed record ReflexArenaWeaponEntry(
    string Key,
    string Label,
    string Description,
    string GconstPrefix,
    bool HasSplashDamage,
    bool HasAmmo,
    int DefaultDirectDamage,
    int? DefaultSplashDamage,
    int DefaultAmmo,
    int DamageMin,
    int DamageMax);

public sealed record ReflexArenaPickupEntry(
    string Key,
    string Label,
    string Description,
    string Gconst);

public sealed record ReflexArenaGlobalConstEntry(
    string Key,
    string Gconst,
    string Label,
    string Hint,
    int DefaultValue,
    int Min,
    int Max);
