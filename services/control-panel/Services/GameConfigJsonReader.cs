using System.Text.Json;

namespace control_panel.Services;

internal static class GameConfigJsonReader
{
    public static string ReadString(JsonElement root, string propertyName, string fallback)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return fallback;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? fallback,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            _ => fallback
        };
    }

    public static bool ReadBoolean(JsonElement root, string propertyName, bool defaultValue = false)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return defaultValue;
        }

        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.TryGetInt32(out var n) && n != 0,
            JsonValueKind.String => element.GetString() is "1" or "true" or "True",
            _ => defaultValue
        };
    }

    public static int ReadInt(JsonElement root, string propertyName, int fallback)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return fallback;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(element.GetString(), out var p) => p,
            _ => fallback
        };
    }

    public static List<string> ReadStringList(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return [];
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(s => s.Length > 0)
                .ToList();
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return (element.GetString() ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        return [];
    }
}
