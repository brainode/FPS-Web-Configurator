#!/bin/sh
set -eu

if ! pgrep -f '/opt/warfork/wf_server.x86_64' >/dev/null 2>&1; then
    exit 1
fi

exit 0
