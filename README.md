# PVP Control Panel

[![Docker](https://img.shields.io/badge/Docker-2496ED?logo=docker&logoColor=white)](https://www.docker.com/)
[![Docker Compose](https://img.shields.io/badge/Docker_Compose-2496ED?logo=docker&logoColor=white)](https://docs.docker.com/compose/)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-512BD4?logo=dotnet&logoColor=white)](https://learn.microsoft.com/aspnet/core/)
[![SQLite](https://img.shields.io/badge/SQLite-003B57?logo=sqlite&logoColor=white)](https://www.sqlite.org/)
[![SteamCMD](https://img.shields.io/badge/SteamCMD-171A21?logo=steam&logoColor=white)](https://developer.valvesoftware.com/wiki/SteamCMD)
[![AI Assisted](https://img.shields.io/badge/AI-assisted-2F6FED)](#)
[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](LICENSE)

Web control panel for self-hosted PvP game servers running in Docker.

The project currently includes:

- `Warsow`
- `Warfork`
- `Quake Live`
- `Reflex Arena`

The panel stores each game's configuration in SQLite, shows server status on the overview page, and starts or stops game containers through `docker-agent`.

License: `GNU Affero General Public License v3.0` — see [LICENSE](LICENSE).

## Features

- One web UI for multiple game servers
- Per-game configuration pages
- Persistent settings stored in SQLite
- Start, restart, and stop actions from the browser
- Toggle which modules are visible in `Overview` and the top menu
- Docker-based deployment for local machines and VPS hosts

## Architecture

Main components:

- `services/control-panel` — ASP.NET Core web UI
- `services/docker-agent` — thin Docker runner used by the panel
- `services/*-server` — per-game runtime images
- `docker-compose.yml` — main orchestration file

At runtime the flow is:

`Browser -> control-panel -> docker-agent -> Docker -> game container`

## Requirements

- Docker Engine 24+ or Docker Desktop
- Docker Compose plugin
- Linux VPS or local machine with Docker
- Current game server images run as `linux/amd64` by default, because the upstream dedicated binaries are x86/x86_64-only
- Open outbound internet access during build, because some game images are downloaded from Steam or upstream archives

## Quick Start

1. Copy environment variables:

```bash
cp .env.example .env
```

2. Edit `.env` and set at least:

- `PANEL_ADMIN_USERNAME`
- `PANEL_ADMIN_PASSWORD`
- `PANEL_PORT`

3. Build all images, including game images:

```bash
docker compose --profile game build
```

4. Start the panel stack:

```bash
docker compose up -d
```

5. Open the panel:

`http://YOUR_HOST:PANEL_PORT`

Example with defaults:

`http://localhost:5099`

There is also a helper script:

```bash
./start.sh
```

It copies `.env.example` to `.env` if needed, builds all game images, and starts the stack.

## Default Ports

Panel:

- `5099/tcp` — web UI

Games:

- `44400/udp` — Warsow game port
- `44444/tcp` — Warsow HTTP downloads
- `44500/udp` — Warfork game port
- `44544/tcp` — Warfork HTTP downloads
- `27970/udp` and `27970/tcp` — Quake Live game traffic
- `28970/tcp` — Quake Live ZMQ RCON
- `25787/udp` and `25787/tcp` — Reflex Arena

All of these can be changed in `.env`.

## Running on a VPS

These steps assume a Linux VPS with Docker installed.

### 1. Install Docker

Use the official Docker instructions for your distribution, then verify:

```bash
docker --version
docker compose version
```

### 2. Upload the project

Clone or copy the repository to the server:

```bash
git clone <your-repo-url>
cd warsow-2.1.2
```

### 3. Configure environment

```bash
cp .env.example .env
```

Recommended minimum changes:

- set a strong `PANEL_ADMIN_PASSWORD`
- choose the public `PANEL_PORT`
- change game ports if they conflict with anything already running

On a normal Linux VPS, leave `DOCKER_HOST` empty. The stack uses `/var/run/docker.sock` directly.
On rootless Docker, set `DOCKER_SOCKET_PATH=/run/user/<uid>/docker.sock` in `.env` and still leave `DOCKER_HOST` empty.
On ARM64 VPS hosts, keep `GAME_SERVER_PLATFORM=linux/amd64` as-is unless you know a specific game image supports ARM natively.

### 4. Build and start

```bash
docker compose --profile game build
docker compose up -d
```

### 5. Open firewall ports

Open the panel port and only the game ports you actually plan to use.

Example with `ufw`:

```bash
sudo ufw allow 5099/tcp
sudo ufw allow 44400/udp
sudo ufw allow 44444/tcp
sudo ufw allow 44500/udp
sudo ufw allow 44544/tcp
sudo ufw allow 27970/tcp
sudo ufw allow 27970/udp
sudo ufw allow 28970/tcp
sudo ufw allow 25787/tcp
sudo ufw allow 25787/udp
```

If you only use some games, only open those ports.

### 6. Sign in

Open:

`http://YOUR_SERVER_IP:PANEL_PORT`

Then sign in with:

- username from `PANEL_ADMIN_USERNAME`
- password from `PANEL_ADMIN_PASSWORD`

### 7. Start servers from the panel

Game containers are built by Compose, but they are started on demand from the web UI.

Typical flow:

1. Open a game page
2. Save the configuration
3. Click `Start server`

## Reverse Proxy

If you want to publish the panel behind a domain, place a reverse proxy in front of `control-panel`.

Typical options:

- Caddy
- Nginx
- Traefik

In that setup you usually:

- keep `PANEL_PORT` on an internal port such as `5099`
- proxy external `80/443` to `control-panel`
- optionally restrict access by IP or VPN

The repository already includes a sample [Caddyfile](Caddyfile), but Compose does not start Caddy automatically.

## Persistence

Persistent data is stored in Docker volumes:

- `control-panel-data`
- `warsow-data`
- `warfork-data`
- `quake-live-standalone-data`
- `reflex-arena-data`

This means panel settings and game data survive container recreation.

## Useful Commands

Build all game images:

```bash
docker compose --profile game build
```

Start stack:

```bash
docker compose up -d
```

Rebuild only the panel:

```bash
docker compose up -d --build control-panel
```

Show status:

```bash
docker compose ps
```

Watch logs:

```bash
docker compose logs -f control-panel docker-agent
```

Stop stack:

```bash
docker compose down
```

## Updating

When you update the repository:

```bash
git pull
docker compose --profile game build
docker compose up -d
```

If you only changed the panel UI or backend:

```bash
docker compose up -d --build control-panel docker-agent
```

## Troubleshooting

If the panel does not open:

- check `docker compose ps`
- check `docker compose logs -f control-panel`
- verify that `PANEL_PORT` is not occupied

If a game does not start:

- check `docker compose logs -f docker-agent`
- inspect the game container logs, for example:

```bash
docker logs --tail 120 warsow-server
docker logs --tail 120 warfork-server
docker logs --tail 120 quake-live-server
docker logs --tail 120 reflex-arena-server
```

If Docker access fails on Linux:

- verify `/var/run/docker.sock` is available
- make sure Docker Engine is running
- keep `DOCKER_HOST` empty unless you explicitly use a TCP Docker endpoint
- if you use rootless Docker, set `DOCKER_SOCKET_PATH=/run/user/<uid>/docker.sock` in `.env` and recreate `docker-agent`
- on SELinux-enabled hosts, `docker-agent` runs with `security_opt: label=disable` so it can talk to the mounted Docker socket

## Development

Run tests:

```bash
dotnet test tests/control-panel.Tests/control-panel.Tests.csproj
```

The control panel project lives in:

- [services/control-panel](services/control-panel)

## Adding Your Own Game Service

If you want to integrate another game, use:

- [ADD_NEW_SERVICE.md](ADD_NEW_SERVICE.md)

That document explains the full pattern for:

- adding a new settings model
- serializer and catalog
- Razor configuration page
- tests
- Docker runtime image
- `docker-agent` wiring
- `docker-compose.yml` integration

## License

This project is licensed under the `GNU Affero General Public License v3.0`.

See:

- [LICENSE](LICENSE)

If you deploy a modified network-facing version of this software, AGPL obligations may apply.
