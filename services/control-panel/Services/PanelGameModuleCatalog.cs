// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

namespace control_panel.Services;

public sealed class PanelGameModuleCatalog
{
    private static readonly string[] ModuleOrder = ["warsow", "warfork", "quake-live", "reflex-arena"];

    private readonly IReadOnlyList<GameModuleDescriptor> _allModules;
    private readonly IReadOnlyList<string> _allGameKeys;
    private readonly IReadOnlySet<string> _knownGameKeys;

    public PanelGameModuleCatalog(IEnumerable<IGameAdapter> gameAdapters)
    {
        var adapters = gameAdapters.ToArray();

        _allModules = adapters
            .Select(adapter => new GameModuleDescriptor(
                adapter.GameKey,
                adapter.DisplayName,
                adapter.ConfigurationPagePath))
            .OrderBy(module =>
            {
                var index = Array.IndexOf(ModuleOrder, module.GameKey);
                return index >= 0 ? index : int.MaxValue;
            })
            .ToArray();

        _allGameKeys = _allModules
            .Select(module => module.GameKey)
            .ToArray();

        _knownGameKeys = _allGameKeys
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<GameModuleDescriptor> AllModules => _allModules;

    public IReadOnlyList<string> AllGameKeys => _allGameKeys;

    public bool IsKnownGameKey(string? gameKey) =>
        !string.IsNullOrWhiteSpace(gameKey) &&
        _knownGameKeys.Contains(gameKey);
}

public sealed record GameModuleDescriptor(
    string GameKey,
    string DisplayName,
    string ConfigurationPagePath);
