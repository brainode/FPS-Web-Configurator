// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

namespace control_panel.Services;

internal static class PanelHelpers
{
    public static string MaskSecret(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return string.Empty;
        }

        return new string('\u2022', Math.Clamp(secret.Length, 8, 12));
    }

    public static string FormatUpdatedLabel(DateTimeOffset updatedUtc) =>
        updatedUtc == default
            ? "Never"
            : updatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public static string FormatUpdatedByLabel(string? updatedBy) =>
        string.IsNullOrWhiteSpace(updatedBy) ? "System" : updatedBy;
}
