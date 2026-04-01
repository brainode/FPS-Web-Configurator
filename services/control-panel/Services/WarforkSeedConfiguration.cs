// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

using control_panel.Models;

namespace control_panel.Services;

public static class WarforkSeedConfiguration
{
    public static string CreateDefaultJson() => WarforkConfigurationSerializer.Serialize(new WarforkServerSettings());
}
