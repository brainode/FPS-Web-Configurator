using System.Text.Json;
using control_panel.Models;

namespace control_panel.Services;

public static class ReflexArenaConfigurationSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static ReflexArenaServerSettings Deserialize(string? json)
    {
        var settings = new ReflexArenaServerSettings();

        if (string.IsNullOrWhiteSpace(json))
        {
            settings.StartMap = ReflexArenaModuleCatalog.ResolveStartMap(settings.StartMap, settings.Mode);
            settings.Mutators = ReflexArenaModuleCatalog.NormalizeMutatorSelection(settings.Mutators);
            return settings;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            settings.Hostname = GameConfigJsonReader.ReadString(root, "sv_hostname", settings.Hostname);
            settings.Mode = GameConfigJsonReader.ReadString(root, "sv_startmode", settings.Mode);
            settings.StartMap = GameConfigJsonReader.ReadString(root, "sv_startmap", settings.StartMap);
            settings.Mutators = ReflexArenaModuleCatalog.NormalizeMutatorSelection(
                GameConfigJsonReader.ReadStringList(root, "sv_startmutators"));
            settings.MaxClients = GameConfigJsonReader.ReadInt(root, "sv_maxclients", settings.MaxClients);
            settings.SteamEnabled = GameConfigJsonReader.ReadBoolean(root, "sv_steam", settings.SteamEnabled);
            settings.Country = GameConfigJsonReader.ReadString(root, "sv_country", string.Empty);
            settings.TimeLimitOverride = GameConfigJsonReader.ReadInt(root, "sv_timelimit_override", settings.TimeLimitOverride);
            settings.ServerPassword = GameConfigJsonReader.ReadString(root, "sv_password", string.Empty);
            settings.RefPassword = GameConfigJsonReader.ReadString(root, "sv_refpassword", string.Empty);
        }
        catch (JsonException)
        {
            settings = new ReflexArenaServerSettings();
        }

        settings.Mutators = ReflexArenaModuleCatalog.NormalizeMutatorSelection(settings.Mutators);
        settings.StartMap = ReflexArenaModuleCatalog.ResolveStartMap(settings.StartMap, settings.Mode);
        settings.Country = settings.Country.Trim().ToUpperInvariant();
        return settings;
    }

    public static string Serialize(ReflexArenaServerSettings settings)
    {
        var normalizedMutators = ReflexArenaModuleCatalog.NormalizeMutatorSelection(settings.Mutators);
        var resolvedStartMap = ReflexArenaModuleCatalog.ResolveStartMap(settings.StartMap, settings.Mode);
        var payload = new Dictionary<string, string>
        {
            ["sv_hostname"] = settings.Hostname,
            ["sv_startmode"] = settings.Mode,
            ["sv_startmap"] = resolvedStartMap,
            ["sv_startmutators"] = string.Join(' ', normalizedMutators),
            ["sv_maxclients"] = settings.MaxClients.ToString(),
            ["sv_steam"] = settings.SteamEnabled ? "1" : "0",
            ["sv_country"] = settings.Country.Trim().ToUpperInvariant(),
            ["sv_timelimit_override"] = settings.TimeLimitOverride.ToString(),
            ["sv_password"] = settings.ServerPassword ?? string.Empty,
            ["sv_refpassword"] = settings.RefPassword ?? string.Empty,
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }
}
