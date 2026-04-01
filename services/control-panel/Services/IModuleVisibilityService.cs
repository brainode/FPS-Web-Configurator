// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Models;

namespace control_panel.Services;

public interface IModuleVisibilityService
{
    Task<ModuleVisibilitySnapshot> GetAsync(CancellationToken cancellationToken = default);
    Task<ModuleVisibilitySnapshot> SaveAsync(IEnumerable<string>? enabledGameKeys, string updatedBy, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameModuleDescriptor>> GetVisibleModulesAsync(CancellationToken cancellationToken = default);
}

public sealed record ModuleVisibilitySnapshot(
    ModuleVisibilitySettings Settings,
    DateTimeOffset UpdatedUtc,
    string? UpdatedBy);
