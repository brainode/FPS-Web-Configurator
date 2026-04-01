#!/bin/sh
# SPDX-License-Identifier: AGPL-3.0-or-later
# Copyright (C) 2025 Zeus <admin@brainode.com>

# One-command project startup.
# Builds all images (including warsow-server) and starts panel services.
set -eu

if [ ! -f .env ]; then
    echo "No .env file found. Copying .env.example to .env — edit it before proceeding."
    cp .env.example .env
fi

# Build all images, including the warsow-server image used by docker-agent at runtime.
docker compose --profile game build

# Start panel services (caddy, control-panel, docker-agent).
# warsow-server is NOT started here; use the panel UI to start it on demand.
docker compose up -d

PANEL_PORT="${PANEL_PORT:-5099}"
echo ""
echo "Panel is starting. Open http://localhost:${PANEL_PORT} in your browser."
echo "Default login: admin / change-me  (set PANEL_ADMIN_PASSWORD in .env)"
