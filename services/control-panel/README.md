# control-panel

`control-panel` это ASP.NET Core веб-панель первой итерации проекта.

На Этапе 2 она уже умеет:

- требовать login/password перед доступом к UI;
- показывать базовый dashboard по Warsow;
- хранить конфигурацию в `SQLite`;
- отдавать JSON API для чтения статуса и конфига;
- инициировать `start`, `stop`, `restart` через будущий `docker-agent`.

## Стек

- `ASP.NET Core 9`
- `Razor Pages`
- `Minimal API`
- `EF Core + SQLite`
- `Twitter Bootstrap`

## Локальный запуск

PowerShell:

```powershell
dotnet build services/control-panel/control-panel.csproj
dotnet run --no-build --project services/control-panel/control-panel.csproj --no-launch-profile --urls http://127.0.0.1:5099
```

После первого старта панель создаст:

- SQLite базу в `data/control-panel/control-panel.db`;
- key ring для cookie/DataProtection в `data/control-panel/data-protection/`.

## Запуск в контейнере

Сборка образа:

```powershell
docker build -f services/control-panel/Dockerfile -t control-panel .
```

Пример запуска на Windows через PowerShell:

```powershell
docker run --rm `
  -p 8080:8080 `
  -e PanelAuth__SeedAdminUsername="admin" `
  -e PanelAuth__SeedAdminPassword="change-me" `
  -e DockerAgent__BaseUrl="http://host.docker.internal:5081" `
  -v control-panel-data:/var/lib/control-panel `
  control-panel
```

Пример запуска на Linux/macOS:

```bash
docker run --rm \
  -p 8080:8080 \
  -e PanelAuth__SeedAdminUsername="admin" \
  -e PanelAuth__SeedAdminPassword="change-me" \
  -e DockerAgent__BaseUrl="http://docker-agent:8081" \
  -v control-panel-data:/var/lib/control-panel \
  control-panel
```

После старта панель будет доступна на `http://localhost:8080`.

Если `docker-agent` ещё не поднят, это не мешает открыть UI и войти в панель, но действия `Start`, `Stop`, `Restart` будут возвращать ожидаемое сообщение, что endpoint агента недоступен или не настроен.

## Доступ по умолчанию

- login: `admin`
- password: `change-me`

Для реального использования пароль нужно переопределить через `PanelAuth__SeedAdminPassword`.

## Полезные переменные окружения

- `PanelStorage__RootPath` - путь для SQLite и DataProtection keys;
- `PanelAuth__SeedAdminUsername` - seed login администратора;
- `PanelAuth__SeedAdminPassword` - seed password администратора;
- `DockerAgent__BaseUrl` - base URL локального `docker-agent`.

## Tests

PowerShell:

```powershell
dotnet test tests/control-panel.Tests/control-panel.Tests.csproj
```

## Gametype config priority

При старте Warsow автоматически исполняет `basewsw/configs/server/gametypes/<gametype>.cfg`, который сбрасывает ряд параметров. Панель предотвращает это с помощью двухслойной override-стратегии, реализованной в `docker-entrypoint.sh`.

### Параметры, переопределяемые gametype-конфигами

| Параметр       | Поле панели    | Поведение без защиты           |
|----------------|----------------|-------------------------------|
| `g_maplist`    | Map pool       | Сброс к defaults gametype     |
| `g_maprotation`| (нет в панели) | Сброс к defaults gametype     |
| `g_scorelimit` | Score limit    | Сброс к defaults gametype     |
| `g_timelimit`  | Time limit     | Сброс к defaults gametype     |

Дополнительно gametype-конфиги переопределяют параметры матча (`g_warmup_timelimit`, `g_match_extendedtime`, `g_allow_falldamage`, `g_teams_maxplayers` и другие), которые панель не управляет напрямую.

### Стратегия приоритета

1. `runtime-overrides.cfg` исполняется через `dedicated_autoexec.cfg` и устанавливает все пользовательские значения (`g_gametype`, `g_maplist`, `g_scorelimit`, `g_timelimit`).
2. Когда Warsow применяет gametype-конфиг, он сбросил бы эти значения — но entrypoint предварительно **копирует** gametype-файл и **дописывает** пользовательские значения в конец. Поскольку append идёт после дистрибутивных defaults, пользовательские значения всегда побеждают.

Результат: `g_maplist`, `g_scorelimit` и `g_timelimit` не сбрасываются при любой смене gametype.

### Defaults gametype-конфигов (для справки)

| gametype     | g_scorelimit | g_timelimit (мин) |
|--------------|:------------:|:-----------------:|
| `ca`         | 11           | 0                 |
| `dm`         | 0            | 15                |
| `duel`       | 0            | 10                |
| `ffa`        | 0            | 15                |
| `tdm`        | 0            | 20                |
| `ctf`        | 0            | 20                |
| `ctftactics` | 0            | 20                |
| `bomb`       | 16           | 0                 |
| `da`         | 11           | 0                 |
| `headhunt`   | 0            | 15                |
| `race`       | 0            | 0                 |

Эти значения зафиксированы в `WarsowModuleCatalog` (`DefaultScorelimit`, `DefaultTimelimit`) и верифицированы unit-тестами.

## Соответствие полей панели и Warsow cvar

| Поле панели       | cvar Warsow      | Env var контейнера      |
|-------------------|------------------|------------------------|
| Gametype          | `g_gametype`     | `WARSOW_GAMETYPE`      |
| Start map         | `sv_defaultmap`  | `WARSOW_START_MAP`     |
| Map pool          | `g_maplist`      | `WARSOW_MAPLIST`       |
| Instagib          | `g_instagib`     | `WARSOW_INSTAGIB`      |
| Instajump         | `g_instajump`    | `WARSOW_INSTAJUMP`     |
| Instashield       | `g_instashield`  | `WARSOW_INSTASHIELD`   |
| Score limit       | `g_scorelimit`   | `WARSOW_SCORELIMIT`    |
| Time limit        | `g_timelimit`    | `WARSOW_TIMELIMIT`     |
| RCON password     | `rcon_password`  | `WARSOW_RCON_PASSWORD` |
| Join password     | `password`       | `WARSOW_PASSWORD`      |
