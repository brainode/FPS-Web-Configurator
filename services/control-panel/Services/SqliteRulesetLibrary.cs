// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Data;
using control_panel.Models;
using Microsoft.EntityFrameworkCore;

namespace control_panel.Services;

public sealed class SqliteRulesetLibrary(ControlPanelDbContext dbContext) : IRulesetLibrary
{
    public async Task<IReadOnlyList<SavedRuleset>> GetAllAsync(string gameKey, CancellationToken cancellationToken = default)
        => await dbContext.SavedRulesets
            .Where(r => r.GameKey == gameKey)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

    public Task<SavedRuleset?> GetByNameAsync(string gameKey, string name, CancellationToken cancellationToken = default)
        => dbContext.SavedRulesets
            .SingleOrDefaultAsync(r => r.GameKey == gameKey && r.Name == name, cancellationToken);

    public async Task<SavedRuleset> SaveAsync(string gameKey, string name, string jsonContent, CancellationToken cancellationToken = default)
    {
        var existing = await GetByNameAsync(gameKey, name, cancellationToken);
        if (existing is not null)
        {
            existing.JsonContent = jsonContent;
            existing.UpdatedUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            existing = new SavedRuleset
            {
                GameKey = gameKey,
                Name = name,
                JsonContent = jsonContent,
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow,
            };
            dbContext.SavedRulesets.Add(existing);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }
}
