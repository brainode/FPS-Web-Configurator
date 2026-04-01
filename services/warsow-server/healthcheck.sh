#!/bin/sh
set -eu

# Linux truncates the comm field to 15 bytes, so exact-name matching misses
# "wsw_server.x86_64" even while the server is running.
if ! pgrep -f '/opt/warsow/wsw_server.x86_64' >/dev/null 2>&1; then
    exit 1
fi

exit 0
