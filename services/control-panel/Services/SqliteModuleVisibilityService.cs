// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using System.Text.Json;
using control_panel.Data;
using control_panel.Models;
using Microsoft.EntityFrameworkCore;

namespace control_panel.Services;

public sealed class SqliteModuleVisibilityService(
    ControlPanelDbContext dbContext,
    PanelGameModuleCatalog moduleCatalog) : IModuleVisibilityService
{
    private const string SettingKey = "module-visibility";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<ModuleVisibilitySnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var setting = await GetOrCreateSettingAsync(cancellationToken);
        return ToSnapshot(setting);
    }

    public async Task<ModuleVisibilitySnapshot> SaveAsync(
        IEnumerable<string>? enabledGameKeys,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var setting = await GetOrCreateSettingAsync(cancellationToken);
        setting.JsonContent = Serialize(enabledGameKeys);
        setting.UpdatedUtc = DateTimeOffset.UtcNow;
        setting.UpdatedBy = updatedBy;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToSnapshot(setting);
    }

    public async Task<IReadOnlyList<GameModuleDescriptor>> GetVisibleModulesAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetAsync(cancellationToken);
        var enabledGameKeys = snapshot.Settings.EnabledGameKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return moduleCatalog.AllModules
            .Where(module => enabledGameKeys.Contains(module.GameKey))
            .ToArray();
    }

    private async Task<PanelSetting> GetOrCreateSettingAsync(CancellationToken cancellationToken)
    {
        var setting = await dbContext.PanelSettings
            .SingleOrDefaultAsync(x => x.SettingKey == SettingKey, cancellationToken);

        if (setting is not null)
        {
            return setting;
        }

        setting = new PanelSetting
        {
            SettingKey = SettingKey,
            JsonContent = Serialize(moduleCatalog.AllGameKeys),
            UpdatedUtc = DateTimeOffset.UtcNow,
            UpdatedBy = "system"
        };

        dbContext.PanelSettings.Add(setting);
        await dbContext.SaveChangesAsync(cancellationToken);
        return setting;
    }

    private ModuleVisibilitySnapshot ToSnapshot(PanelSetting setting)
    {
        return new ModuleVisibilitySnapshot(
            Deserialize(setting.JsonContent),
            setting.UpdatedUtc,
            setting.UpdatedBy);
    }

    private ModuleVisibilitySettings Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CreateDefaultSettings();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var enabledGameKeys = GameConfigJsonReader.ReadStringList(root, "enabledGameKeys");
            if (enabledGameKeys.Count == 0)
            {
                enabledGameKeys = GameConfigJsonReader.ReadStringList(root, "EnabledGameKeys");
            }

            return new ModuleVisibilitySettings
            {
                EnabledGameKeys = Normalize(enabledGameKeys)
            };
        }
        catch (JsonException)
        {
            return CreateDefaultSettings();
        }
    }

    private string Serialize(IEnumerable<string>? enabledGameKeys)
    {
        var settings = new ModuleVisibilitySettings
        {
            EnabledGameKeys = Normalize(enabledGameKeys)
        };

        return JsonSerializer.Serialize(settings, SerializerOptions);
    }

    private ModuleVisibilitySettings CreateDefaultSettings() =>
        new()
        {
            EnabledGameKeys = moduleCatalog.AllGameKeys.ToList()
        };

    private List<string> Normalize(IEnumerable<string>? enabledGameKeys)
    {
        var selectedKeys = (enabledGameKeys ?? [])
            .Where(moduleCatalog.IsKnownGameKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return moduleCatalog.AllGameKeys
            .Where(selectedKeys.Contains)
            .ToList();
    }
}
