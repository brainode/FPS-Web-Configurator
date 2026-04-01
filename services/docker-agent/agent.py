#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-or-later
# Copyright (C) 2025 Zeus <admin@brainode.com>

"""
docker-agent — manages game containers via Docker CLI.

REST API:
  GET  /api/games/{gameKey}/status
  POST /api/games/{gameKey}/start    (optional body: {"env": {"KEY": "value", ...}})
  POST /api/games/{gameKey}/stop
  POST /api/games/{gameKey}/restart  (optional body: {"env": {"KEY": "value", ...}})
  GET  /health/live
"""
import json
import os
import re
import subprocess
import sys
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

AGENT_PORT = int(os.environ.get("DOCKER_AGENT_PORT", "8081"))
GAME_SERVER_PLATFORM = os.environ.get("GAME_SERVER_PLATFORM", "linux/amd64")

# Per-game container configuration driven by environment variables.
GAME_CONFIGS = {
    "quake-live": {
        "container_name": "quake-live-server",
        "image": os.environ.get("QL_IMAGE", "quake-live-server:latest"),
        "platform": GAME_SERVER_PLATFORM,
        "ports": [
            f"{os.environ.get('QL_SERVER_PORT', '27970')}:27960/udp",
            f"{os.environ.get('QL_SERVER_PORT', '27970')}:27960/tcp",
            f"{os.environ.get('QL_RCON_PORT', '28970')}:28960/tcp",
        ],
        "volumes": [
            f"{os.environ.get('QL_DATA_VOLUME', 'quake-live-standalone-data')}:/var/lib/quake-live",
        ],
        "env": {
            "QL_RUNTIME_ARCH": "x86",
            "QL_HOSTNAME": "Quake Live Standalone Test",
            "QL_FACTORY": "duel",
            "QL_MAPLIST": "asylum brimstoneabbey campgrounds purgatory theedge",
            "QL_MAXCLIENTS": "16",
            "QL_SERVER_TYPE": "2",
            "QL_TAGS": "standalone",
            "QL_IDLE_EXIT": "120",
            "QL_ZMQ_RCON_ENABLE": "0",
            "QL_ZMQ_STATS_ENABLE": "0",
            "QL_NET_PORT": "27960",
        },
        "command": ["+set", "com_crashreport", "0"],
    },
    "reflex-arena": {
        "container_name": "reflex-arena-server",
        "image": os.environ.get("REFLEX_IMAGE", "reflex-arena-server:latest"),
        "platform": GAME_SERVER_PLATFORM,
        "ports": [
            f"{os.environ.get('REFLEX_SERVER_PORT', '25787')}:25787/udp",
            f"{os.environ.get('REFLEX_SERVER_PORT', '25787')}:25787/tcp",
        ],
        "volumes": [
            f"{os.environ.get('REFLEX_DATA_VOLUME', 'reflex-arena-data')}:/var/lib/reflex-arena",
        ],
        "env": {
            "REFLEX_HOSTNAME": "Reflex Arena Docker Server",
            "REFLEX_MODE": "1v1",
            "REFLEX_START_MAP": "Fusion",
            "REFLEX_START_MUTATORS": "",
            "REFLEX_MAXCLIENTS": "8",
            "REFLEX_STEAM": "1",
            "REFLEX_ALLOW_EDIT": "0",
            "REFLEX_GAME_PORT": "25787",
            "REFLEX_COUNTRY": "",
            "REFLEX_TIMELIMIT_OVERRIDE": "0",
            "REFLEX_PASSWORD": "",
            "REFLEX_REF_PASSWORD": "",
        },
    },
    "warsow": {
        "container_name": "warsow-server",
        "image": os.environ.get("WARSOW_IMAGE", "warsow-server:latest"),
        "platform": GAME_SERVER_PLATFORM,
        "ports": [
            f"{os.environ.get('WARSOW_SERVER_PORT', '44400')}:44400/udp",
            f"{os.environ.get('WARSOW_HTTP_PORT', '44444')}:44444/tcp",
        ],
        "volumes": [
            f"{os.environ.get('WARSOW_DATA_VOLUME', 'warsow-data')}:/var/lib/warsow",
        ],
    },
    "warfork": {
        "container_name": "warfork-server",
        "image": os.environ.get("WARFORK_IMAGE", "warfork-server:latest"),
        "platform": GAME_SERVER_PLATFORM,
        "ports": [
            f"{os.environ.get('WARFORK_SERVER_PORT', '44500')}:44400/udp",
            f"{os.environ.get('WARFORK_HTTP_PORT', '44544')}:44444/tcp",
        ],
        "volumes": [
            f"{os.environ.get('WARFORK_DATA_VOLUME', 'warfork-data')}:/var/lib/warfork",
        ],
        "env": {
            "WARFORK_SERVER_HOSTNAME": "Warfork Docker Server",
            "WARFORK_GAMETYPE": "ca",
            "WARFORK_START_MAP": "return",
            "WARFORK_MAPLIST": "return pressure",
            "WARFORK_SCORELIMIT": "11",
            "WARFORK_TIMELIMIT": "0",
            "WARFORK_INSTAGIB": "0",
            "WARFORK_INSTAJUMP": "0",
            "WARFORK_INSTASHIELD": "0",
            "WARFORK_SERVER_PORT": "44400",
            "WARFORK_HTTP_PORT": "44444",
            "WARFORK_PUBLIC": "1",
            "WARFORK_MAXCLIENTS": "16",
            "WARFORK_HTTP_ENABLED": "1",
        },
    },
}

# Normalize Docker-native states to the panel's state vocabulary.
# Panel buttons: CanStart("stopped"), CanRestart("running"), CanStop("running"|"restarting")
_NORMALIZED_STATE = {
    "running":    "running",
    "restarting": "restarting",
    "paused":     "paused",
    "exited":     "stopped",   # Container exists but is not running
    "created":    "stopped",   # Created but never started
    "dead":       "stopped",   # Docker marked it dead
    "not-found":  "stopped",   # Container doesn't exist yet — can be started
}

_STATE_LABELS = {
    "running":    "Running",
    "restarting": "Restarting",
    "paused":     "Paused",
    "stopped":    "Stopped",
}


def _docker(*args):
    return subprocess.run(["docker"] + list(args), capture_output=True, text=True)


def _container_state(container_name):
    r = _docker("inspect", "--format", "{{.State.Status}}", container_name)
    if r.returncode != 0:
        return "not-found"
    return r.stdout.strip()


def _status_payload(game_key, config):
    raw_state = _container_state(config["container_name"])
    state = _NORMALIZED_STATE.get(raw_state, "stopped")
    return {
        "gameKey":      game_key,
        "state":        state,
        "stateLabel":   _STATE_LABELS.get(state, state.capitalize()),
        "message":      "",
        "sourceLabel":  "docker-agent",
        "checkedAtUtc": datetime.now(timezone.utc).isoformat(),
    }


def _stop(config):
    r = _docker("stop", config["container_name"])
    if r.returncode == 0:
        return True, "Container stopped."
    if "No such container" in r.stderr:
        return True, "Container was not running."
    return False, r.stderr.strip()


def _start(config, env=None):
    name = config["container_name"]
    _docker("rm", "-f", name)  # Remove any stopped or dead container.

    args = ["run", "-d", "--name", name, "--restart", "unless-stopped"]
    platform = config.get("platform")
    if platform:
        args += ["--platform", platform]
    for port in config.get("ports", []):
        args += ["-p", port]
    for vol in config.get("volumes", []):
        args += ["-v", vol]

    merged_env = dict(config.get("env", {}))
    if env:
        merged_env.update(env)

    if merged_env:
        for k, v in merged_env.items():
            args += ["-e", f"{k}={v}"]

    args.append(config["image"])
    args += config.get("command", [])

    r = _docker(*args)
    if r.returncode == 0:
        return True, "Container started."
    return False, (r.stderr.strip() or r.stdout.strip())


def _restart(config, env=None):
    _stop(config)
    return _start(config, env)


class _AgentHandler(BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        print(f"[agent] {fmt % args}", file=sys.stderr, flush=True)

    def _send_json(self, status_code, data):
        body = json.dumps(data).encode()
        self.send_response(status_code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", len(body))
        self.end_headers()
        self.wfile.write(body)

    def _read_env_from_body(self):
        encoding = self.headers.get("Transfer-Encoding", "")
        if "chunked" in encoding.lower():
            raw = bytearray()
            while True:
                size_line = self.rfile.readline().strip()
                if not size_line:
                    break
                chunk_size = int(size_line, 16)
                if chunk_size == 0:
                    break
                raw.extend(self.rfile.read(chunk_size))
                self.rfile.read(2)  # trailing CRLF after each chunk
            raw = bytes(raw)
        else:
            length = int(self.headers.get("Content-Length", 0))
            if length <= 0:
                return None
            raw = self.rfile.read(length)

        if not raw:
            return None
        body = json.loads(raw)
        return body.get("env") or None

    def do_GET(self):
        m = re.match(r"^/api/games/([^/]+)/status$", self.path)
        if m:
            key = m.group(1)
            cfg = GAME_CONFIGS.get(key)
            if not cfg:
                self._send_json(404, {"error": f"Unknown game: {key}"})
                return
            self._send_json(200, _status_payload(key, cfg))
            return
        if self.path in ("/health", "/health/live"):
            self._send_json(200, {"status": "ok"})
            return
        self._send_json(404, {"error": "Not found"})

    def do_POST(self):
        m = re.match(r"^/api/games/([^/]+)/(start|stop|restart)$", self.path)
        if not m:
            self._send_json(404, {"error": "Not found"})
            return

        key, action = m.group(1), m.group(2)
        cfg = GAME_CONFIGS.get(key)
        if not cfg:
            self._send_json(404, {"error": f"Unknown game: {key}"})
            return

        env = self._read_env_from_body()

        if action == "start":
            ok, msg = _start(cfg, env)
        elif action == "stop":
            ok, msg = _stop(cfg)
        else:  # restart
            ok, msg = _restart(cfg, env)

        print(f"[agent] {action} {key}: {'OK' if ok else 'FAIL'} — {msg}", flush=True)
        self._send_json(200 if ok else 500, {"success": ok, "message": msg})


if __name__ == "__main__":
    print(f"[agent] Listening on :{AGENT_PORT}", flush=True)
    server = ThreadingHTTPServer(("0.0.0.0", AGENT_PORT), _AgentHandler)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
