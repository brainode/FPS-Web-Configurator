// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Data;
using control_panel.Models;
using Microsoft.EntityFrameworkCore;

namespace control_panel.Services;

public sealed class SqliteConfigurationStore(
    ControlPanelDbContext dbContext,
    IEnumerable<IGameAdapter> gameAdapters) : IConfigurationStore
{
    public async Task<GameConfiguration> GetOrCreateAsync(string gameKey, CancellationToken cancellationToken = default)
    {
        var configuration = await dbContext.GameConfigurations
            .SingleOrDefaultAsync(x => x.GameKey == gameKey, cancellationToken);

        if (configuration is not null)
        {
            return configuration;
        }

        configuration = CreateSeedConfiguration(gameKey);
        dbContext.GameConfigurations.Add(configuration);
        await dbContext.SaveChangesAsync(cancellationToken);
        return configuration;
    }

    public async Task<GameConfiguration> SaveAsync(
        string gameKey,
        string jsonContent,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var configuration = await GetOrCreateAsync(gameKey, cancellationToken);
        configuration.JsonContent = jsonContent;
        configuration.UpdatedUtc = DateTimeOffset.UtcNow;
        configuration.UpdatedBy = updatedBy;

        await dbContext.SaveChangesAsync(cancellationToken);
        return configuration;
    }

    private GameConfiguration CreateSeedConfiguration(string gameKey)
    {
        var adapter = gameAdapters.FirstOrDefault(a =>
            string.Equals(a.GameKey, gameKey, StringComparison.OrdinalIgnoreCase));

        return new GameConfiguration
        {
            GameKey = gameKey,
            DisplayName = adapter?.DisplayName ?? gameKey,
            JsonContent = adapter?.CreateDefaultJson() ?? "{\n  \"notes\": \"Game-specific configuration goes here.\"\n}",
            UpdatedUtc = DateTimeOffset.UtcNow,
            UpdatedBy = "system"
        };
    }
}
