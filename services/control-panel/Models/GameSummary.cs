// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2025 Zeus <admin@brainode.com>

namespace control_panel.Models;

public sealed record GameSummary(
    string ModeName,
    string ModeFlags,
    string StartMap,
    string MapCountLabel,
    string RotationPreview,
    string LimitsSummary,
    string AccessLabel,
    string RconLabel);
