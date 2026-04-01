// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using System.Text.Json;

namespace control_panel.Services;

public static class JsonValidator
{
    public static bool IsValid(string json, out string? error)
    {
        try
        {
            JsonDocument.Parse(json);
            error = null;
            return true;
        }
        catch (JsonException exception)
        {
            error = exception.Message;
            return false;
        }
    }
}
