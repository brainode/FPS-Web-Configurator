// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using System.Text.Json;
using control_panel.Models;

namespace control_panel.Services;

public static class QuakeLiveConfigurationSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static QuakeLiveServerSettings Deserialize(string? json)
    {
        var settings = new QuakeLiveServerSettings();

        if (string.IsNullOrWhiteSpace(json))
        {
            settings.MapList = QuakeLiveModuleCatalog.NormalizeMapSelection(settings.MapList, settings.Factory);
            return settings;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            settings.Hostname = GameConfigJsonReader.ReadString(root, "sv_hostname", settings.Hostname);
            settings.Factory = GameConfigJsonReader.ReadString(root, "g_factory", settings.Factory);
            settings.MapList = QuakeLiveModuleCatalog.NormalizeMapSelection(
                GameConfigJsonReader.ReadStringList(root, "g_maplist"), settings.Factory);
            settings.MaxClients = GameConfigJsonReader.ReadInt(root, "sv_maxclients", settings.MaxClients);
            settings.ServerType = GameConfigJsonReader.ReadInt(root, "sv_serverType", settings.ServerType);
            settings.ZmqRconEnabled = GameConfigJsonReader.ReadBoolean(root, "zmq_rcon_enable", settings.ZmqRconEnabled);
            settings.ZmqRconPort = GameConfigJsonReader.ReadInt(root, "zmq_rcon_port", settings.ZmqRconPort);
            settings.ZmqRconPassword = GameConfigJsonReader.ReadString(root, "zmq_rcon_password", string.Empty);
            settings.ZmqStatsEnabled = GameConfigJsonReader.ReadBoolean(root, "zmq_stats_enable");
            settings.ZmqStatsPort = GameConfigJsonReader.ReadInt(root, "zmq_stats_port", settings.ZmqStatsPort);
            settings.ZmqStatsPassword = GameConfigJsonReader.ReadString(root, "zmq_stats_password", string.Empty);
            settings.ServerPassword = GameConfigJsonReader.ReadString(root, "g_password", string.Empty);
            settings.Tags = GameConfigJsonReader.ReadString(root, "sv_tags", string.Empty);
        }
        catch (JsonException)
        {
            settings = new QuakeLiveServerSettings();
        }

        settings.MapList = QuakeLiveModuleCatalog.NormalizeMapSelection(settings.MapList, settings.Factory);
        return settings;
    }

    public static string Serialize(QuakeLiveServerSettings settings)
    {
        var normalizedMaps = QuakeLiveModuleCatalog.NormalizeMapSelection(settings.MapList, settings.Factory);

        var payload = new Dictionary<string, string>
        {
            ["sv_hostname"] = settings.Hostname,
            ["g_factory"] = settings.Factory,
            ["g_maplist"] = string.Join(' ', normalizedMaps),
            ["sv_maxclients"] = settings.MaxClients.ToString(),
            ["sv_serverType"] = settings.ServerType.ToString(),
            ["zmq_rcon_enable"] = settings.ZmqRconEnabled ? "1" : "0",
            ["zmq_rcon_port"] = settings.ZmqRconPort.ToString(),
            ["zmq_rcon_password"] = settings.ZmqRconPassword ?? string.Empty,
            ["zmq_stats_enable"] = settings.ZmqStatsEnabled ? "1" : "0",
            ["zmq_stats_port"] = settings.ZmqStatsPort.ToString(),
            ["zmq_stats_password"] = settings.ZmqStatsPassword ?? string.Empty,
            ["g_password"] = settings.ServerPassword ?? string.Empty,
            ["sv_tags"] = settings.Tags ?? string.Empty,
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }
}
