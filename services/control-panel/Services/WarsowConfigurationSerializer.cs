using System.Text.Json;
using control_panel.Models;

namespace control_panel.Services;

public static class WarsowConfigurationSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static WarsowServerSettings Deserialize(string? json)
    {
        var settings = new WarsowServerSettings();

        if (string.IsNullOrWhiteSpace(json))
        {
            settings.MapList = WarsowModuleCatalog.NormalizeMapSelection(settings.MapList, settings.Gametype);
            settings.StartMap = WarsowModuleCatalog.ResolveStartMap(settings.StartMap, settings.MapList, settings.Gametype);
            return settings;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            settings.StartMap = GameConfigJsonReader.ReadString(root, "sv_defaultmap", settings.StartMap);
            settings.Gametype = GameConfigJsonReader.ReadString(root, "g_gametype", settings.Gametype);
            settings.MapList = WarsowModuleCatalog.NormalizeMapSelection(
                GameConfigJsonReader.ReadStringList(root, "g_maplist"), settings.Gametype);
            settings.Instagib = GameConfigJsonReader.ReadBoolean(root, "g_instagib");
            settings.Instajump = GameConfigJsonReader.ReadBoolean(root, "g_instajump");
            settings.Instashield = GameConfigJsonReader.ReadBoolean(root, "g_instashield");
            settings.Scorelimit = GameConfigJsonReader.ReadInt(root, "g_scorelimit", settings.Scorelimit);
            settings.Timelimit = GameConfigJsonReader.ReadInt(root, "g_timelimit", settings.Timelimit);
            settings.RconPassword = GameConfigJsonReader.ReadString(root, "rcon_password", string.Empty);
            settings.ServerPassword = GameConfigJsonReader.ReadString(root, "password", string.Empty);
        }
        catch (JsonException)
        {
            settings = new WarsowServerSettings();
        }

        settings.MapList = WarsowModuleCatalog.NormalizeMapSelection(settings.MapList, settings.Gametype);
        settings.StartMap = WarsowModuleCatalog.ResolveStartMap(settings.StartMap, settings.MapList, settings.Gametype);
        return settings;
    }

    public static string Serialize(WarsowServerSettings settings)
    {
        var normalizedMaps = WarsowModuleCatalog.NormalizeMapSelection(settings.MapList, settings.Gametype);
        var resolvedStartMap = WarsowModuleCatalog.ResolveStartMap(settings.StartMap, normalizedMaps, settings.Gametype);

        var payload = new Dictionary<string, string>
        {
            ["sv_defaultmap"] = resolvedStartMap,
            ["g_maplist"] = string.Join(' ', normalizedMaps),
            ["g_gametype"] = settings.Gametype,
            ["g_instagib"] = settings.Instagib ? "1" : "0",
            ["g_instajump"] = settings.Instajump ? "1" : "0",
            ["g_instashield"] = settings.Instashield ? "1" : "0",
            ["g_scorelimit"] = settings.Scorelimit.ToString(),
            ["g_timelimit"] = settings.Timelimit.ToString(),
            ["rcon_password"] = settings.RconPassword ?? string.Empty,
            ["password"] = settings.ServerPassword ?? string.Empty
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }
}
