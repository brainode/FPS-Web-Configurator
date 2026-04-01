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

WARSOW_INSTALL_DIR="${WARSOW_INSTALL_DIR:-/opt/warsow}"
WARSOW_HOME_ROOT="${WARSOW_HOME_ROOT:-/var/lib/warsow}"
WARSOW_HOME_DIR="${WARSOW_HOME_DIR:-${WARSOW_HOME_ROOT}/.local/share/warsow-2.1}"

DIST_DEDICATED_CFG="${WARSOW_INSTALL_DIR}/basewsw/dedicated_autoexec.cfg"

require_file "${WARSOW_INSTALL_DIR}/wsw_server"
require_file "$DIST_DEDICATED_CFG"

WARSOW_GAMETYPE="${WARSOW_GAMETYPE:-$(read_cfg_value "$DIST_DEDICATED_CFG" "g_gametype")}"
WARSOW_GAMETYPE="${WARSOW_GAMETYPE:-ca}"
DIST_GAMETYPE_CFG="${WARSOW_INSTALL_DIR}/basewsw/configs/server/gametypes/${WARSOW_GAMETYPE}.cfg"

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
DEFAULT_SERVER_SHOW_CHALLENGE="$(read_cfg_value "$DIST_DEDICATED_CFG" "sv_showChallenge")"
DEFAULT_SERVER_SHOW_RCON="$(read_cfg_value "$DIST_DEDICATED_CFG" "sv_showRcon")"
DEFAULT_SERVER_AUTOUPDATE="$(read_cfg_value "$DIST_DEDICATED_CFG" "sv_autoupdate")"
DEFAULT_START_MAP="$(read_cfg_value "$DIST_DEDICATED_CFG" "sv_defaultmap")"
DEFAULT_INSTA_GIB="$(read_cfg_value "$DIST_DEDICATED_CFG" "g_instagib")"
DEFAULT_INSTA_JUMP="$(read_cfg_value "$DIST_DEDICATED_CFG" "g_instajump")"
DEFAULT_INSTA_SHIELD="$(read_cfg_value "$DIST_DEDICATED_CFG" "g_instashield")"

DEFAULT_MAPLIST="$(read_cfg_value "$DIST_GAMETYPE_CFG" "g_maplist")"
DEFAULT_MAPROTATION="$(read_cfg_value "$DIST_GAMETYPE_CFG" "g_maprotation")"
DEFAULT_SCORELIMIT="$(read_cfg_value "$DIST_GAMETYPE_CFG" "g_scorelimit")"
DEFAULT_TIMELIMIT="$(read_cfg_value "$DIST_GAMETYPE_CFG" "g_timelimit")"

if [ -z "$DEFAULT_START_MAP" ]; then
    DEFAULT_START_MAP="$(first_word "$DEFAULT_MAPLIST")"
fi

WARSOW_SERVER_HOSTNAME="${WARSOW_SERVER_HOSTNAME:-${DEFAULT_SERVER_HOSTNAME:-Warsow Docker Server}}"
WARSOW_PUBLIC="${WARSOW_PUBLIC:-${DEFAULT_SERVER_PUBLIC:-1}}"
WARSOW_MAXCLIENTS="${WARSOW_MAXCLIENTS:-${DEFAULT_SERVER_MAXCLIENTS:-16}}"
WARSOW_SKILLLEVEL="${WARSOW_SKILLLEVEL:-${DEFAULT_SERVER_SKILLLEVEL:-1}}"
WARSOW_SERVER_IP="${WARSOW_SERVER_IP:-${DEFAULT_SERVER_IP:-}}"
WARSOW_SERVER_PORT="${WARSOW_SERVER_PORT:-${DEFAULT_SERVER_PORT:-44400}}"
WARSOW_SERVER_PORT6="${WARSOW_SERVER_PORT6:-${DEFAULT_SERVER_PORT6:-$WARSOW_SERVER_PORT}}"
WARSOW_HTTP_ENABLED="${WARSOW_HTTP_ENABLED:-${DEFAULT_SERVER_HTTP:-1}}"
WARSOW_HTTP_IP="${WARSOW_HTTP_IP:-${DEFAULT_SERVER_HTTP_IP:-}}"
WARSOW_HTTP_PORT="${WARSOW_HTTP_PORT:-${DEFAULT_SERVER_HTTP_PORT:-44444}}"
WARSOW_SHOW_INFO_QUERIES="${WARSOW_SHOW_INFO_QUERIES:-${DEFAULT_SERVER_SHOW_INFO_QUERIES:-0}}"
WARSOW_SHOW_CHALLENGE="${WARSOW_SHOW_CHALLENGE:-${DEFAULT_SERVER_SHOW_CHALLENGE:-0}}"
WARSOW_SHOW_RCON="${WARSOW_SHOW_RCON:-${DEFAULT_SERVER_SHOW_RCON:-0}}"
WARSOW_AUTOUPDATE="${WARSOW_AUTOUPDATE:-0}"
WARSOW_START_MAP="${WARSOW_START_MAP:-${DEFAULT_START_MAP:-wca1}}"
WARSOW_MAPLIST="${WARSOW_MAPLIST:-$DEFAULT_MAPLIST}"
WARSOW_MAPROTATION="${WARSOW_MAPROTATION:-${DEFAULT_MAPROTATION:-0}}"
WARSOW_SCORELIMIT="${WARSOW_SCORELIMIT:-${DEFAULT_SCORELIMIT:-0}}"
WARSOW_TIMELIMIT="${WARSOW_TIMELIMIT:-${DEFAULT_TIMELIMIT:-20}}"
WARSOW_INSTAGIB="${WARSOW_INSTAGIB:-${DEFAULT_INSTA_GIB:-0}}"
WARSOW_INSTAJUMP="${WARSOW_INSTAJUMP:-${DEFAULT_INSTA_JUMP:-0}}"
WARSOW_INSTASHIELD="${WARSOW_INSTASHIELD:-${DEFAULT_INSTA_SHIELD:-0}}"
WARSOW_PASSWORD="${WARSOW_PASSWORD:-}"
WARSOW_RCON_PASSWORD="${WARSOW_RCON_PASSWORD:-}"
WARSOW_OPERATOR_PASSWORD="${WARSOW_OPERATOR_PASSWORD:-}"
WARSOW_LOG_FILE="${WARSOW_LOG_FILE:-logs/wswconsole.log}"
WARSOW_LOG_APPEND="${WARSOW_LOG_APPEND:-1}"
WARSOW_LOG_FLUSH="${WARSOW_LOG_FLUSH:-1}"
WARSOW_LOG_TIMESTAMPS="${WARSOW_LOG_TIMESTAMPS:-1}"
WARSOW_AUTORECORD="${WARSOW_AUTORECORD:-0}"
WARSOW_WARMUP_TIMELIMIT="${WARSOW_WARMUP_TIMELIMIT:-0}"
WARSOW_SV_PPS="${WARSOW_SV_PPS:-62}"
WARSOW_DEMO_DIR_NAME="${WARSOW_DEMO_DIR_NAME:-demos}"

MANAGED_BASEWSW_DIR="${WARSOW_HOME_DIR}/basewsw"
MANAGED_DOCKER_DIR="${MANAGED_BASEWSW_DIR}/docker"
MANAGED_GAMETYPE_DIR="${MANAGED_BASEWSW_DIR}/configs/server/gametypes"
MANAGED_LOG_DIR="${WARSOW_HOME_DIR}/logs"
MANAGED_DEMO_DIR="${WARSOW_HOME_DIR}/${WARSOW_DEMO_DIR_NAME}"

mkdir -p \
    "$MANAGED_DOCKER_DIR" \
    "$MANAGED_GAMETYPE_DIR" \
    "$MANAGED_LOG_DIR" \
    "$MANAGED_DEMO_DIR"

cp "$DIST_DEDICATED_CFG" "${MANAGED_DOCKER_DIR}/original_dedicated_autoexec.cfg"

{
    echo "// Generated by warsow-entrypoint."
    echo "// This file shadows the distribution dedicated_autoexec.cfg from the writable homepath."
    echo "exec docker/original_dedicated_autoexec.cfg"
    echo "exec docker/runtime-overrides.cfg"
} > "${MANAGED_BASEWSW_DIR}/dedicated_autoexec.cfg"

{
    echo "// Generated by warsow-entrypoint."
    echo "// Values here are intended to be managed by Docker or the future control panel."
    echo "echo \"docker runtime overrides executed\""
    write_set "sv_hostname" "$WARSOW_SERVER_HOSTNAME"
    write_set "sv_ip" "$WARSOW_SERVER_IP"
    write_set "sv_port" "$WARSOW_SERVER_PORT"
    write_set "sv_port6" "$WARSOW_SERVER_PORT6"
    write_set "sv_http" "$WARSOW_HTTP_ENABLED"
    write_set "sv_http_ip" "$WARSOW_HTTP_IP"
    write_set "sv_http_port" "$WARSOW_HTTP_PORT"
    write_set "sv_showInfoQueries" "$WARSOW_SHOW_INFO_QUERIES"
    write_set "sv_showChallenge" "$WARSOW_SHOW_CHALLENGE"
    write_set "sv_showRcon" "$WARSOW_SHOW_RCON"
    write_set "sv_autoupdate" "$WARSOW_AUTOUPDATE"
    write_set "sv_public" "$WARSOW_PUBLIC"
    write_set "sv_maxclients" "$WARSOW_MAXCLIENTS"
    write_set "sv_skilllevel" "$WARSOW_SKILLLEVEL"
    write_set "password" "$WARSOW_PASSWORD"
    write_set "rcon_password" "$WARSOW_RCON_PASSWORD"
    write_set "g_operator_password" "$WARSOW_OPERATOR_PASSWORD"
    write_set "logconsole" "$WARSOW_LOG_FILE"
    write_set "logconsole_append" "$WARSOW_LOG_APPEND"
    write_set "logconsole_flush" "$WARSOW_LOG_FLUSH"
    write_set "logconsole_timestamp" "$WARSOW_LOG_TIMESTAMPS"
    write_set "g_autorecord" "$WARSOW_AUTORECORD"
    write_set "g_warmup_timelimit" "$WARSOW_WARMUP_TIMELIMIT"
    write_set "sv_pps" "$WARSOW_SV_PPS"
    write_set "sv_demodir" "$WARSOW_DEMO_DIR_NAME"
    write_set "g_gametype" "$WARSOW_GAMETYPE"
    write_set "g_instagib" "$WARSOW_INSTAGIB"
    write_set "g_instajump" "$WARSOW_INSTAJUMP"
    write_set "g_instashield" "$WARSOW_INSTASHIELD"
    write_set "sv_defaultmap" "$WARSOW_START_MAP"
    write_set "g_maplist" "$WARSOW_MAPLIST"
    write_set "g_maprotation" "$WARSOW_MAPROTATION"
    write_set "g_scorelimit" "$WARSOW_SCORELIMIT"
    write_set "g_timelimit" "$WARSOW_TIMELIMIT"
} > "${MANAGED_DOCKER_DIR}/runtime-overrides.cfg"

cp "$DIST_GAMETYPE_CFG" "${MANAGED_GAMETYPE_DIR}/${WARSOW_GAMETYPE}.cfg"
{
    echo
    echo "// Generated by warsow-entrypoint."
    echo "// These lines are appended after the distribution gametype file so user-managed values win."
    printf 'echo "%s"\n' "docker managed ${WARSOW_GAMETYPE}.cfg overlay executed"
    write_set "g_maplist" "$WARSOW_MAPLIST"
    write_set "g_maprotation" "$WARSOW_MAPROTATION"
    write_set "g_scorelimit" "$WARSOW_SCORELIMIT"
    write_set "g_timelimit" "$WARSOW_TIMELIMIT"
} >> "${MANAGED_GAMETYPE_DIR}/${WARSOW_GAMETYPE}.cfg"

printf 'Warsow home directory: %s\n' "$WARSOW_HOME_DIR"
printf 'Warsow gametype: %s\n' "$WARSOW_GAMETYPE"
printf 'Warsow start map: %s\n' "$WARSOW_START_MAP"
printf 'Warsow server port: %s/udp\n' "$WARSOW_SERVER_PORT"
printf 'Warsow HTTP port: %s/tcp\n' "$WARSOW_HTTP_PORT"

export HOME="$WARSOW_HOME_ROOT"

cd "$WARSOW_INSTALL_DIR"
exec "${WARSOW_INSTALL_DIR}/wsw_server" "$@"
