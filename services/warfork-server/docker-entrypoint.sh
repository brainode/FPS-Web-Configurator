#!/bin/sh
# SPDX-License-Identifier: AGPL-3.0-or-later
# Copyright (C) 2025 Zeus <admin@brainode.com>

set -eu

read_cfg_value() {
    file="$1"
    key="$2"
    if [ ! -f "$file" ]; then
        return 0
    fi

    line="$(grep -E "^[[:space:]]*set[[:space:]]+${key}[[:space:]]+\"" "$file" | tail -n 1 || true)"
    if [ -z "$line" ]; then
        return 0
    fi

    printf '%s\n' "$line" | sed -E 's/^[[:space:]]*set[[:space:]]+[A-Za-z0-9_]+[[:space:]]+"([^"]*)".*$/\1/'
}

first_word() {
    value="$1"
    set -- $value
    printf '%s\n' "${1:-}"
}

quote_cfg_value() {
    printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'
}

write_set() {
    key="$1"
    value="$2"
    printf 'set %s "%s"\n' "$key" "$(quote_cfg_value "$value")"
}

require_file() {
    file="$1"
    if [ ! -f "$file" ]; then
        printf 'Required file not found: %s\n' "$file" >&2
        exit 1
    fi
}

WARFORK_INSTALL_DIR="${WARFORK_INSTALL_DIR:-/opt/warfork}"
WARFORK_STEAM_RUNTIME_DIR="${WARFORK_STEAM_RUNTIME_DIR:-/opt/steam-runtime}"
WARFORK_HOME_ROOT="${WARFORK_HOME_ROOT:-/var/lib/warfork}"
WARFORK_HOME_DIR="${WARFORK_HOME_DIR:-${WARFORK_HOME_ROOT}/.local/share/warfork-2.1}"
STEAM_HOME_DIR="${WARFORK_HOME_ROOT}/.steam"

DIST_DEDICATED_CFG="${WARFORK_INSTALL_DIR}/basewf/dedicated_autoexec.cfg"

require_file "${WARFORK_INSTALL_DIR}/wf_server.x86_64"
require_file "$DIST_DEDICATED_CFG"

WARFORK_GAMETYPE="${WARFORK_GAMETYPE:-$(read_cfg_value "$DIST_DEDICATED_CFG" "g_gametype")}"
WARFORK_GAMETYPE="${WARFORK_GAMETYPE:-ca}"
DIST_GAMETYPE_CFG="${WARFORK_INSTALL_DIR}/basewf/configs/server/gametypes/${WARFORK_GAMETYPE}.cfg"

require_file "$DIST_GAMETYPE_CFG"

DEFAULT_SERVER_HOSTNAME="$(read_cfg_value "$DIST_DEDICATED_CFG" "sv_hostname")"
DEFAULT_SERVER_PUBLIC="$(read_cfg_value "$DIST_DEDICATED_CFG" "sv_public")"
DEFAULT_SERVER_MAXCLIENTS="$(read_cfg_value "$DIST_DEDICATED_CFG" "sv_maxclients")"
DEFAULT_SERVER_SKILLLEVEL="$(read_cfg_value "$DIST_DEDICATED_CFG" "sv_skilllevel")"
DEFAULT_SERVER_IP="$(read_cfg_value "$DIST_DEDICATED_CFG" "sv_ip")"
DEFAULT_SERVER_PORT="$(read_cfg_value "$DIST_DEDICATED_CFG" "sv_port")"
DEFAULT_SERVER_PORT6="$(read_cfg_value "$DIST_DEDICATED_CFG" "sv_port6")"
DEFAULT_SERVER_HTTP="$(read_cfg_value "$DIST_DEDICATED_CFG" "sv_http")"
DEFAULT_SERVER_HTTP_IP="$(read_cfg_value "$DIST_DEDICATED_CFG" "sv_http_ip")"
DEFAULT_SERVER_HTTP_PORT="$(read_cfg_value "$DIST_DEDICATED_CFG" "sv_http_port")"
DEFAULT_SERVER_SHOW_INFO_QUERIES="$(read_cfg_value "$DIST_DEDICATED_CFG" "sv_showInfoQueries")"
DEFAULT_SERVER_AUTOUPDATE="$(read_cfg_value "$DIST_DEDICATED_CFG" "sv_autoupdate")"
DEFAULT_START_MAP="$(read_cfg_value "$DIST_DEDICATED_CFG" "sv_defaultmap")"
DEFAULT_INSTA_GIB="$(read_cfg_value "$DIST_DEDICATED_CFG" "g_instagib")"
DEFAULT_INSTA_JUMP="$(read_cfg_value "$DIST_DEDICATED_CFG" "g_instajump")"
DEFAULT_INSTA_SHIELD="$(read_cfg_value "$DIST_DEDICATED_CFG" "g_instashield")"
DEFAULT_LOG_FILE="$(read_cfg_value "$DIST_DEDICATED_CFG" "logconsole")"
DEFAULT_LOG_APPEND="$(read_cfg_value "$DIST_DEDICATED_CFG" "logconsole_append")"
DEFAULT_AUTORECORD="$(read_cfg_value "$DIST_DEDICATED_CFG" "g_autorecord")"
DEFAULT_WARMUP_TIMELIMIT="$(read_cfg_value "$DIST_DEDICATED_CFG" "g_warmup_timelimit")"

DEFAULT_MAPLIST="$(read_cfg_value "$DIST_GAMETYPE_CFG" "g_maplist")"
DEFAULT_MAPROTATION="$(read_cfg_value "$DIST_GAMETYPE_CFG" "g_maprotation")"
DEFAULT_SCORELIMIT="$(read_cfg_value "$DIST_GAMETYPE_CFG" "g_scorelimit")"
DEFAULT_TIMELIMIT="$(read_cfg_value "$DIST_GAMETYPE_CFG" "g_timelimit")"

if [ -z "$DEFAULT_START_MAP" ]; then
    DEFAULT_START_MAP="$(first_word "$DEFAULT_MAPLIST")"
fi

WARFORK_SERVER_HOSTNAME="${WARFORK_SERVER_HOSTNAME:-${DEFAULT_SERVER_HOSTNAME:-Warfork Docker Server}}"
WARFORK_PUBLIC="${WARFORK_PUBLIC:-${DEFAULT_SERVER_PUBLIC:-1}}"
WARFORK_MAXCLIENTS="${WARFORK_MAXCLIENTS:-${DEFAULT_SERVER_MAXCLIENTS:-16}}"
WARFORK_SKILLLEVEL="${WARFORK_SKILLLEVEL:-${DEFAULT_SERVER_SKILLLEVEL:-1}}"
WARFORK_SERVER_IP="${WARFORK_SERVER_IP:-${DEFAULT_SERVER_IP:-}}"
WARFORK_SERVER_PORT="${WARFORK_SERVER_PORT:-${DEFAULT_SERVER_PORT:-44400}}"
WARFORK_SERVER_PORT6="${WARFORK_SERVER_PORT6:-${DEFAULT_SERVER_PORT6:-$WARFORK_SERVER_PORT}}"
WARFORK_HTTP_ENABLED="${WARFORK_HTTP_ENABLED:-${DEFAULT_SERVER_HTTP:-1}}"
WARFORK_HTTP_IP="${WARFORK_HTTP_IP:-${DEFAULT_SERVER_HTTP_IP:-}}"
WARFORK_HTTP_PORT="${WARFORK_HTTP_PORT:-${DEFAULT_SERVER_HTTP_PORT:-44444}}"
WARFORK_SHOW_INFO_QUERIES="${WARFORK_SHOW_INFO_QUERIES:-${DEFAULT_SERVER_SHOW_INFO_QUERIES:-0}}"
WARFORK_AUTOUPDATE="${WARFORK_AUTOUPDATE:-${DEFAULT_SERVER_AUTOUPDATE:-0}}"
WARFORK_START_MAP="${WARFORK_START_MAP:-${DEFAULT_START_MAP:-return}}"
WARFORK_MAPLIST="${WARFORK_MAPLIST:-$DEFAULT_MAPLIST}"
WARFORK_MAPROTATION="${WARFORK_MAPROTATION:-${DEFAULT_MAPROTATION:-0}}"
WARFORK_SCORELIMIT="${WARFORK_SCORELIMIT:-${DEFAULT_SCORELIMIT:-0}}"
WARFORK_TIMELIMIT="${WARFORK_TIMELIMIT:-${DEFAULT_TIMELIMIT:-10}}"
WARFORK_INSTAGIB="${WARFORK_INSTAGIB:-${DEFAULT_INSTA_GIB:-0}}"
WARFORK_INSTAJUMP="${WARFORK_INSTAJUMP:-${DEFAULT_INSTA_JUMP:-0}}"
WARFORK_INSTASHIELD="${WARFORK_INSTASHIELD:-${DEFAULT_INSTA_SHIELD:-0}}"
WARFORK_PASSWORD="${WARFORK_PASSWORD:-}"
WARFORK_RCON_PASSWORD="${WARFORK_RCON_PASSWORD:-}"
WARFORK_OPERATOR_PASSWORD="${WARFORK_OPERATOR_PASSWORD:-}"
WARFORK_CUSTOM_RULES="${WARFORK_CUSTOM_RULES:-0}"
WARFORK_ALLOWED_WEAPONS="${WARFORK_ALLOWED_WEAPONS:-}"
WARFORK_CA_LOADOUT_ENABLED="${WARFORK_CA_LOADOUT_ENABLED:-0}"
WARFORK_CA_LOADOUT_INVENTORY="${WARFORK_CA_LOADOUT_INVENTORY:-}"
WARFORK_CA_STRONG_AMMO="${WARFORK_CA_STRONG_AMMO:-}"
WARFORK_DISABLE_HEALTH="${WARFORK_DISABLE_HEALTH:-0}"
WARFORK_DISABLE_ARMOR="${WARFORK_DISABLE_ARMOR:-0}"
WARFORK_DISABLE_POWERUPS="${WARFORK_DISABLE_POWERUPS:-0}"
WARFORK_GRAVITY="${WARFORK_GRAVITY:-}"
WARFORK_LOG_FILE="${WARFORK_LOG_FILE:-logs/${DEFAULT_LOG_FILE:-wfconsole.log}}"
WARFORK_LOG_APPEND="${WARFORK_LOG_APPEND:-${DEFAULT_LOG_APPEND:-1}}"
WARFORK_AUTORECORD="${WARFORK_AUTORECORD:-${DEFAULT_AUTORECORD:-1}}"
WARFORK_WARMUP_TIMELIMIT="${WARFORK_WARMUP_TIMELIMIT:-${DEFAULT_WARMUP_TIMELIMIT:-1}}"
WARFORK_SV_FPS="${WARFORK_SV_FPS:-120}"
WARFORK_SV_PPS="${WARFORK_SV_PPS:-80}"
WARFORK_DEMO_DIR_NAME="${WARFORK_DEMO_DIR_NAME:-demos}"

MANAGED_BASEWF_DIR="${WARFORK_HOME_DIR}/basewf"
MANAGED_DOCKER_DIR="${MANAGED_BASEWF_DIR}/docker"
MANAGED_GAMETYPE_DIR="${MANAGED_BASEWF_DIR}/configs/server/gametypes"
MANAGED_LOG_DIR="${WARFORK_HOME_DIR}/logs"
MANAGED_DEMO_DIR="${WARFORK_HOME_DIR}/${WARFORK_DEMO_DIR_NAME}"

mkdir -p \
    "${STEAM_HOME_DIR}/sdk32" \
    "${STEAM_HOME_DIR}/sdk64" \
    "${STEAM_HOME_DIR}/root/sdk32" \
    "${STEAM_HOME_DIR}/root/sdk64" \
    "$MANAGED_DOCKER_DIR" \
    "$MANAGED_GAMETYPE_DIR" \
    "$MANAGED_LOG_DIR" \
    "$MANAGED_DEMO_DIR"

if [ -f "${WARFORK_STEAM_RUNTIME_DIR}/linux32/steamclient.so" ]; then
    ln -snf "${WARFORK_STEAM_RUNTIME_DIR}/linux32/steamclient.so" "${STEAM_HOME_DIR}/sdk32/steamclient.so"
    ln -snf "${WARFORK_STEAM_RUNTIME_DIR}/linux32/steamclient.so" "${STEAM_HOME_DIR}/root/sdk32/steamclient.so"
fi

if [ -f "${WARFORK_STEAM_RUNTIME_DIR}/linux64/steamclient.so" ]; then
    ln -snf "${WARFORK_STEAM_RUNTIME_DIR}/linux64/steamclient.so" "${STEAM_HOME_DIR}/sdk64/steamclient.so"
    ln -snf "${WARFORK_STEAM_RUNTIME_DIR}/linux64/steamclient.so" "${STEAM_HOME_DIR}/root/sdk64/steamclient.so"
fi

cp "$DIST_DEDICATED_CFG" "${MANAGED_DOCKER_DIR}/original_dedicated_autoexec.cfg"

{
    echo "// Generated by warfork-entrypoint."
    echo "// This file shadows the distribution dedicated_autoexec.cfg from the writable homepath."
    echo "exec docker/original_dedicated_autoexec.cfg"
    echo "exec docker/runtime-overrides.cfg"
} > "${MANAGED_BASEWF_DIR}/dedicated_autoexec.cfg"

{
    echo "// Generated by warfork-entrypoint."
    echo "// Values here are intended to be managed by Docker or the control panel."
    echo "echo \"docker runtime overrides executed\""
    write_set "sv_hostname" "$WARFORK_SERVER_HOSTNAME"
    write_set "sv_ip" "$WARFORK_SERVER_IP"
    write_set "sv_port" "$WARFORK_SERVER_PORT"
    write_set "sv_port6" "$WARFORK_SERVER_PORT6"
    write_set "sv_http" "$WARFORK_HTTP_ENABLED"
    write_set "sv_http_ip" "$WARFORK_HTTP_IP"
    write_set "sv_http_port" "$WARFORK_HTTP_PORT"
    write_set "sv_showInfoQueries" "$WARFORK_SHOW_INFO_QUERIES"
    write_set "sv_autoupdate" "$WARFORK_AUTOUPDATE"
    write_set "sv_public" "$WARFORK_PUBLIC"
    write_set "sv_maxclients" "$WARFORK_MAXCLIENTS"
    write_set "sv_skilllevel" "$WARFORK_SKILLLEVEL"
    write_set "password" "$WARFORK_PASSWORD"
    write_set "rcon_password" "$WARFORK_RCON_PASSWORD"
    write_set "g_operator_password" "$WARFORK_OPERATOR_PASSWORD"
    write_set "logconsole" "$WARFORK_LOG_FILE"
    write_set "logconsole_append" "$WARFORK_LOG_APPEND"
    write_set "g_autorecord" "$WARFORK_AUTORECORD"
    write_set "g_warmup_timelimit" "$WARFORK_WARMUP_TIMELIMIT"
    write_set "sv_fps" "$WARFORK_SV_FPS"
    write_set "sv_pps" "$WARFORK_SV_PPS"
    write_set "sv_demodir" "$WARFORK_DEMO_DIR_NAME"
    write_set "g_gametype" "$WARFORK_GAMETYPE"
    write_set "g_instagib" "$WARFORK_INSTAGIB"
    write_set "g_instajump" "$WARFORK_INSTAJUMP"
    write_set "g_instashield" "$WARFORK_INSTASHIELD"
    write_set "sv_defaultmap" "$WARFORK_START_MAP"
    write_set "g_maplist" "$WARFORK_MAPLIST"
    write_set "g_maprotation" "$WARFORK_MAPROTATION"
    write_set "g_scorelimit" "$WARFORK_SCORELIMIT"
    write_set "g_timelimit" "$WARFORK_TIMELIMIT"
} > "${MANAGED_DOCKER_DIR}/runtime-overrides.cfg"

cp "$DIST_GAMETYPE_CFG" "${MANAGED_GAMETYPE_DIR}/${WARFORK_GAMETYPE}.cfg"
{
    echo
    echo "// Generated by warfork-entrypoint."
    echo "// These lines are appended after the distribution gametype file so user-managed values win."
    printf 'echo "%s"\n' "docker managed ${WARFORK_GAMETYPE}.cfg overlay executed"
    write_set "g_maplist" "$WARFORK_MAPLIST"
    write_set "g_maprotation" "$WARFORK_MAPROTATION"
    write_set "g_scorelimit" "$WARFORK_SCORELIMIT"
    write_set "g_timelimit" "$WARFORK_TIMELIMIT"

    if [ "$WARFORK_CUSTOM_RULES" = "1" ]; then
        echo
        echo "// Custom rules generated by warfork-entrypoint."
        # Weapon arena — restricts which weapons spawn on the map and in players'
        # inventories. g_weaponarena_items uses space-separated lowercase weapon keys
        # as expected by the Warfork gametype scripts (warfork-qfusion).
        if [ -n "$WARFORK_ALLOWED_WEAPONS" ]; then
            write_set "g_weaponarena" "1"
            write_set "g_weaponarena_items" "$WARFORK_ALLOWED_WEAPONS"
        fi
        # Stock Clan Arena reads these two cvars on respawn, which lets the
        # panel build a precise spawn loadout without shipping a custom gametype.
        if [ "$WARFORK_GAMETYPE" = "ca" ] && [ "$WARFORK_CA_LOADOUT_ENABLED" = "1" ]; then
            if [ -n "$WARFORK_CA_LOADOUT_INVENTORY" ]; then
                write_set "g_noclass_inventory" "$WARFORK_CA_LOADOUT_INVENTORY"
            fi
            if [ -n "$WARFORK_CA_STRONG_AMMO" ]; then
                write_set "g_class_strong_ammo" "$WARFORK_CA_STRONG_AMMO"
            fi
        fi
        # Pickup item control. These cvars are read by the stock Warfork gametype
        # scripts; set to 0 to prevent the respective items from spawning.
        if [ "$WARFORK_DISABLE_HEALTH" = "1" ]; then
            write_set "g_pickup_health" "0"
        fi
        if [ "$WARFORK_DISABLE_ARMOR" = "1" ]; then
            write_set "g_pickup_armor" "0"
        fi
        if [ "$WARFORK_DISABLE_POWERUPS" = "1" ]; then
            write_set "g_pickup_powerups" "0"
        fi
        # Server-side gravity override (sv_gravity is the QFusion/Warfork cvar).
        if [ -n "$WARFORK_GRAVITY" ]; then
            write_set "sv_gravity" "$WARFORK_GRAVITY"
        fi
    fi
} >> "${MANAGED_GAMETYPE_DIR}/${WARFORK_GAMETYPE}.cfg"

printf 'Warfork home directory: %s\n' "$WARFORK_HOME_DIR"
printf 'Warfork gametype: %s\n' "$WARFORK_GAMETYPE"
printf 'Warfork start map: %s\n' "$WARFORK_START_MAP"
printf 'Warfork networking: %s fps / %s pps\n' "$WARFORK_SV_FPS" "$WARFORK_SV_PPS"
if [ "$WARFORK_CUSTOM_RULES" = "1" ]; then
    printf 'Warfork custom rules: enabled (weapons: %s)\n' "${WARFORK_ALLOWED_WEAPONS:-all}"
    if [ "$WARFORK_GAMETYPE" = "ca" ] && [ "$WARFORK_CA_LOADOUT_ENABLED" = "1" ]; then
        printf 'Warfork CA loadout inventory: %s\n' "${WARFORK_CA_LOADOUT_INVENTORY:-default}"
        printf 'Warfork CA strong ammo: %s\n' "${WARFORK_CA_STRONG_AMMO:-default}"
    fi
fi
printf 'Warfork server port: %s/udp\n' "$WARFORK_SERVER_PORT"
printf 'Warfork HTTP port: %s/tcp\n' "$WARFORK_HTTP_PORT"

export HOME="$WARFORK_HOME_ROOT"
export LD_LIBRARY_PATH="${WARFORK_INSTALL_DIR}:${WARFORK_INSTALL_DIR}/libs${LD_LIBRARY_PATH:+:${LD_LIBRARY_PATH}}"

cd "$WARFORK_INSTALL_DIR"
exec "${WARFORK_INSTALL_DIR}/wf_server.x86_64" \
    +set fs_basepath "$WARFORK_INSTALL_DIR" \
    +set fs_homepath "$WARFORK_HOME_DIR" \
    "$@"
