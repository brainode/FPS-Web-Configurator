# warsow-server

Модуль 1 поднимает выделенный сервер Warsow в отдельном Docker-образе и готовит writable homepath для конфигов, логов и демо.

С учётом решений Этапа 0 этот модуль теперь живёт в сервисной структуре проекта:

- сервисный код: `services/warsow-server/`
- игровые файлы Warsow: `games/warsow/distribution/`
- общий runtime-volume игры: `/var/lib/warsow`

Для обратной совместимости в корне проекта оставлены junction-пути `warsow-server/` и `warsow-2.1.2/`.

## Что уже реализовано

- Dockerfile для Linux-сервера Warsow;
- entrypoint, который генерирует управляемые конфиги при старте контейнера;
- отдельный writable homepath в volume `/var/lib/warsow`;
- healthcheck на живой процесс `wsw_server.x86_64`;
- подготовка к будущей веб-панели через runtime-overrides и управляемый gametype-конфиг.

## Структура данных в volume

После первого запуска контейнер пишет данные в одном общем runtime-volume игры:

- `/var/lib/warsow/.local/share/warsow-2.1/basewsw/dedicated_autoexec.cfg`
- `/var/lib/warsow/.local/share/warsow-2.1/basewsw/docker/runtime-overrides.cfg`
- `/var/lib/warsow/.local/share/warsow-2.1/basewsw/configs/server/gametypes/<gametype>.cfg`
- `/var/lib/warsow/.local/share/warsow-2.1/logs/`
- `/var/lib/warsow/.local/share/warsow-2.1/demos/`

Несмотря на наличие каталогов `logs/` и `demos/`, отдельные volumes под них не требуются. Для выбранного пользовательского сценария всё хранится в одном volume на игру.

## Сборка

```bash
docker build -f services/warsow-server/Dockerfile -t warsow-server .
```

## Запуск

Linux/macOS:

```bash
docker run --rm \
  -p 44400:44400/udp \
  -p 44444:44444/tcp \
  -e WARSOW_SERVER_HOSTNAME="Warsow Insta DM Server" \
  -e WARSOW_GAMETYPE="dm" \
  -e WARSOW_START_MAP="wdm1" \
  -e WARSOW_MAPLIST="wdm1 wdm2 wdm4" \
  -e WARSOW_INSTAGIB="1" \
  -e WARSOW_INSTAJUMP="1" \
  -e WARSOW_INSTASHIELD="1" \
  -e WARSOW_SCORELIMIT="50" \
  -e WARSOW_TIMELIMIT="15" \
  -e WARSOW_RCON_PASSWORD="change-me" \
  -v warsow-data:/var/lib/warsow \
  warsow-server
```

Windows PowerShell:

```powershell
docker run --rm `
  -p 44400:44400/udp `
  -p 44444:44444/tcp `
  -e WARSOW_SERVER_HOSTNAME="Warsow Insta DM Server" `
  -e WARSOW_GAMETYPE="dm" `
  -e WARSOW_START_MAP="wdm1" `
  -e WARSOW_MAPLIST="wdm1 wdm2 wdm4" `
  -e WARSOW_INSTAGIB="1" `
  -e WARSOW_INSTAJUMP="1" `
  -e WARSOW_INSTASHIELD="1" `
  -e WARSOW_SCORELIMIT="50" `
  -e WARSOW_TIMELIMIT="15" `
  -e WARSOW_RCON_PASSWORD="change-me" `
  -v warsow-data:/var/lib/warsow `
  warsow-server
```

## Основные переменные окружения

- `WARSOW_SERVER_HOSTNAME`
- `WARSOW_GAMETYPE`
- `WARSOW_START_MAP`
- `WARSOW_MAPLIST`
- `WARSOW_MAPROTATION`
- `WARSOW_SCORELIMIT`
- `WARSOW_TIMELIMIT`
- `WARSOW_INSTAGIB`
- `WARSOW_INSTAJUMP`
- `WARSOW_INSTASHIELD`
- `WARSOW_PASSWORD`
- `WARSOW_RCON_PASSWORD`
- `WARSOW_OPERATOR_PASSWORD`
- `WARSOW_SERVER_PORT`
- `WARSOW_HTTP_PORT`

## Почему так устроено

`dedicated_autoexec.cfg` в Warsow исполняется при старте выделенного сервера, но часть матчевых значений затем может быть переопределена файлом конкретного гейммода из `basewsw/configs/server/gametypes/*.cfg`.

Поэтому entrypoint делает две вещи:

1. Генерирует управляемый `dedicated_autoexec.cfg` в writable homepath.
2. Создаёт overlay-файл для выбранного gametype и дописывает в него `g_maplist`, `g_maprotation`, `g_scorelimit` и `g_timelimit`.

Это снижает риск того, что значения матча будут потеряны уже на первом старте контейнера.

## Совместимость с будущим docker-agent

По принятой архитектуре этим контейнером будет управлять отдельный локальный agent/service, а не сама веб-панель. Поэтому `warsow-server` сознательно остаётся "тупым" runtime-модулем:

- принимает конфигурацию через environment variables;
- генерирует итоговый runtime-конфиг локально;
- не зависит от панели напрямую;
- может быть запущен, остановлен и перезапущен внешним оркестратором или агентом.
