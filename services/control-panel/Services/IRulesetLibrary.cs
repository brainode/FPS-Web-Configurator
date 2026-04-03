// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Models;

namespace control_panel.Services;

public interface IRulesetLibrary
{
    Task<IReadOnlyList<SavedRuleset>> GetAllAsync(string gameKey, CancellationToken cancellationToken = default);
    Task<SavedRuleset?> GetByNameAsync(string gameKey, string name, CancellationToken cancellationToken = default);
    Task<SavedRuleset> SaveAsync(string gameKey, string name, string jsonContent, CancellationToken cancellationToken = default);
}
