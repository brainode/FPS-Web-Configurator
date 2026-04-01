# docker-agent

`docker-agent` — минимальный HTTP-сервис на Python, который управляет жизненным циклом игровых контейнеров через Docker CLI.

Панель `control-panel` не имеет прямого доступа к `docker.sock`. Вместо этого она отправляет команды агенту, а агент исполняет их.

## REST API

| Метод  | Путь                              | Описание                                   |
|--------|-----------------------------------|--------------------------------------------|
| GET    | `/api/games/{gameKey}/status`     | Статус контейнера                          |
| POST   | `/api/games/{gameKey}/start`      | Запустить контейнер (тело: `{"env": {}}`)  |
| POST   | `/api/games/{gameKey}/stop`       | Остановить контейнер                       |
| POST   | `/api/games/{gameKey}/restart`    | Перезапустить контейнер (тело: `{"env": {}}`) |
| GET    | `/health/live`                    | Healthcheck                                |

### Тело запроса start/restart

Опциональное JSON-тело позволяет передать переменные окружения в контейнер:

```json
{
  "env": {
    "WARSOW_GAMETYPE": "ca",
    "WARSOW_RCON_PASSWORD": "secret"
  }
}
```

### Формат ответа status

```json
{
  "gameKey": "warsow",
  "state": "running",
  "stateLabel": "Running",
  "message": "",
  "sourceLabel": "docker-agent",
  "checkedAtUtc": "2024-01-01T00:00:00+00:00"
}
```

Возможные значения `state`: `running`, `exited`, `restarting`, `paused`, `created`, `dead`, `not-found`.

## Конфигурация через переменные окружения

| Переменная           | Значение по умолчанию     | Описание                               |
|----------------------|---------------------------|----------------------------------------|
| `DOCKER_AGENT_PORT`  | `8081`                    | Порт агента                            |
| `WARSOW_IMAGE`       | `warsow-server:latest`    | Docker-образ для Warsow-контейнера     |
| `WARSOW_SERVER_PORT` | `44400`                   | Хостовый UDP-порт игрового трафика     |
| `WARSOW_HTTP_PORT`   | `44444`                   | Хостовый TCP-порт HTTP Warsow          |
| `WARSOW_DATA_VOLUME` | `warsow-data`             | Named volume для `/var/lib/warsow`     |

## Запуск как часть docker-compose

Агент поднимается автоматически при `docker compose up`. Полный запуск системы:

```bash
./start.sh
```

## Архитектурные ограничения

- Агент знает только об игровых контейнерах, перечисленных в `agent.py` (`GAME_CONFIGS`).
- Агент требует доступа к `/var/run/docker.sock` на хосте.
- На Windows (Docker Desktop) сокет пробрасывается корректно при использовании Linux-контейнеров.
- Warsow-контейнер запускается с `--restart unless-stopped`, то есть перезапускается автоматически после сбоя или перезагрузки хоста.
