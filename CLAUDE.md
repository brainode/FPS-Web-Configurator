# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A Docker-based game server management platform. The current scope covers Warsow and Quake Live dedicated servers, controlled via a web panel. The architecture is designed so future game adapters (e.g. UE5 custom game) can be added without modifying the core panel.

Three runtime services:
- **`control-panel`** — ASP.NET Core 9 web app (Razor Pages + Minimal API), stores settings in SQLite
- **`docker-agent`** — minimal Python HTTP server (stdlib only) that wraps Docker CLI to manage game containers
- **`warsow-server`** / **`quake-live-server`** — game server containers, started on-demand by docker-agent (not via `docker compose up`)

## Commands

### Build and run the full stack
```bash
cp .env.example .env          # first time only
docker compose up --build     # starts control-panel, docker-agent (NOT game servers)
```

### Build game server images (required before starting them from the panel)
```bash
docker compose --profile game build
```

### Build and run control-panel only (local dev)
```bash
cd services/control-panel
dotnet run
```

### Run tests
```bash
dotnet test tests/control-panel.Tests/control-panel.Tests.csproj
# Single test class:
dotnet test --filter "FullyQualifiedName~WarsowConfigurationSerializerTests"
```

### Build the solution
```bash
dotnet build warsow-2.1.2.sln
```

### Warsow server image smoke test
```bash
docker build -f services/warsow-server/Dockerfile -t warsow-server:test .
```

## Architecture

### Game Adapter Pattern

The central extensibility mechanism is `IGameAdapter` (`services/control-panel/Services/IGameAdapter.cs`):

```csharp
public interface IGameAdapter
{
    string GameKey { get; }           // "warsow" or "quake-live"
    string DisplayName { get; }
    string ConfigurationPagePath { get; }
    GameSummary GetSummary(string? jsonSettings);
    IReadOnlyDictionary<string, string> GetContainerEnv(string? jsonSettings);
    string CreateDefaultJson();
}
```

Each game adapter is registered as a singleton in `Program.cs`. Settings for each game are stored as a JSON blob in SQLite keyed by `gameKey`. When a game server is (re)started, `GetContainerEnv()` converts that JSON into Docker `-e KEY=VALUE` flags passed to docker-agent.

**Existing adapters:**
- `WarsowGameAdapter` — full implementation
- `QuakeLiveGameAdapter` — in progress (see untracked files in git status)
- `QuakeLiveStubAdapter` — stub for testing the adapter contract

**Supporting classes per game (Warsow pattern to follow):**
- `*ServerSettings` model — typed settings class
- `*ConfigurationSerializer` — JSON ↔ typed model conversion
- `*ModuleCatalog` — map catalog, gametype labels, validation rules (loaded from embedded `Data/*.json`)
- `*SeedConfiguration` — produces the default JSON for a new installation

### Control Panel API

All API routes are in `Program.cs` under `/api` (require auth):
- `GET /api/server/{gameKey}/status` → docker-agent status
- `POST /api/server/{gameKey}/start|stop|restart` → docker-agent action
- `GET /api/config/{gameKey}` → read stored JSON config from SQLite
- `PUT /api/config/{gameKey}` → save JSON config
- `POST /api/config/{gameKey}/apply` → save + restart server

### Docker Agent

`services/docker-agent/agent.py` — pure Python 3 stdlib HTTP server. Game container configurations (image, ports, volumes) are driven by environment variables set in `docker-compose.yml`. The agent starts game containers with `docker run -d` (removing any prior stopped container first), passing env vars from the request body `{"env": {...}}`.

### Configuration Flow

1. User submits form on Razor Page
2. Page handler calls `PUT /api/config/{gameKey}` to persist JSON to SQLite
3. On apply/restart: `POST /api/config/{gameKey}/apply` → control-panel reads config, calls `IGameAdapter.GetContainerEnv()`, sends `POST /api/games/{gameKey}/restart` to docker-agent with the env dict
4. docker-agent stops the old container and runs a new one with those env vars
5. Game server container reads env vars in its entrypoint and writes a `runtime-overrides.cfg`

### Warsow Config Override Strategy

Warsow gametype configs (`basewsw/configs/server/gametypes/*.cfg`) override settings like `g_maplist`, `g_scorelimit`, `g_timelimit` at startup. The solution: the entrypoint generates a separate `docker/runtime-overrides.cfg` that is executed *after* gametype configs, preserving user values. Verified in container logs by the line `docker runtime overrides executed`.

### Storage

- Panel database: `/var/lib/control-panel/control-panel.db` (SQLite, EF Core, volume `control-panel-data`)
- Warsow runtime data: `/var/lib/warsow` (volume `warsow-data`)
- Quake Live runtime data: `/var/lib/quake-live` (volume `quake-live-data`)
- Map/gametype catalogs: embedded JSON resources in `services/control-panel/Data/`

### Testing Approach

- Unit tests use xUnit, target `net9.0`, live in `tests/control-panel.Tests/`
- Tests cover serializers, module catalogs, and adapter contracts
- No database or HTTP mocks needed for unit tests — the serializers and catalogs are pure functions
- Integration and smoke tests are manual (see TASKS.md verification checklists)

## Key Environment Variables

| Variable | Default | Purpose |
|---|---|---|
| `PANEL_ADMIN_USERNAME` | `admin` | Initial admin login |
| `PANEL_ADMIN_PASSWORD` | `change-me` | Initial admin password |
| `PANEL_PORT` | `5099` | Host port for control-panel |
| `DOCKER_HOST` | (unix socket) | Set to `tcp://host.docker.internal:2375` on Windows |
| `WARSOW_IMAGE` | `warsow-server:latest` | Image used by docker-agent to start Warsow |
| `QL_IMAGE` | `quake-live-server:latest` | Image used by docker-agent to start Quake Live |

## Adding a New Game Adapter

1. Create `*ServerSettings`, `*ConfigurationSerializer`, `*ModuleCatalog`, `*SeedConfiguration` in `services/control-panel/Services/`
2. Implement `IGameAdapter` and register it in `Program.cs` with `AddSingleton<IGameAdapter, YourAdapter>()`
3. Add map/gametype catalog JSON to `services/control-panel/Data/` and include as `<EmbeddedResource>` in the `.csproj`
4. Add a Razor Page under `services/control-panel/Pages/Configuration/` for the game's settings form
5. Add the game container config to `docker-agent/agent.py` `GAME_CONFIGS` dict and matching env vars in `docker-compose.yml`
6. Add unit tests in `tests/control-panel.Tests/Services/`
