using System.Security.Claims;
using control_panel.Data;
using control_panel.Options;
using control_panel.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

var panelStorageRoot = PanelStoragePathResolver.ResolveRootPath(
    builder.Environment.ContentRootPath,
    builder.Configuration["PanelStorage:RootPath"]);
var databasePath = Path.Combine(panelStorageRoot, "control-panel.db");
var dataProtectionPath = Path.Combine(panelStorageRoot, "data-protection");

Directory.CreateDirectory(panelStorageRoot);
Directory.CreateDirectory(dataProtectionPath);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
builder.Logging.AddDebug();

builder.Services.Configure<PanelAuthOptions>(builder.Configuration.GetSection("PanelAuth"));
builder.Services.Configure<DockerAgentOptions>(builder.Configuration.GetSection("DockerAgent"));
builder.Services.Configure<PanelStorageOptions>(options => options.RootPath = panelStorageRoot);

builder.Services.AddDbContext<ControlPanelDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "pvp.control-panel.auth";
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("pvp-control-panel");
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
});

builder.Services.AddSingleton<IGameAdapter, WarsowGameAdapter>();
builder.Services.AddSingleton<IGameAdapter, WarforkGameAdapter>();
builder.Services.AddSingleton<IGameAdapter, QuakeLiveGameAdapter>();
builder.Services.AddSingleton<IGameAdapter, ReflexArenaGameAdapter>();
builder.Services.AddSingleton<PanelGameModuleCatalog>();

builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IConfigurationStore, SqliteConfigurationStore>();
builder.Services.AddScoped<IModuleVisibilityService, SqliteModuleVisibilityService>();
builder.Services.AddScoped<DatabaseInitializer>();

builder.Services.AddHttpClient<IDockerAgentClient, DockerAgentClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IConfiguration>().GetSection("DockerAgent").Get<DockerAgentOptions>()
        ?? new DockerAgentOptions();

    if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
    {
        client.BaseAddress = baseUri;
    }

    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));

var api = app.MapGroup("/api").RequireAuthorization();

api.MapGet("/server/{gameKey}/status", async (string gameKey, IDockerAgentClient dockerAgentClient, CancellationToken cancellationToken) =>
{
    var status = await dockerAgentClient.GetStatusAsync(gameKey, cancellationToken);
    return Results.Ok(status);
});

api.MapPost("/server/{gameKey}/start", async (
    string gameKey,
    IDockerAgentClient dockerAgentClient,
    IConfigurationStore configurationStore,
    IEnumerable<IGameAdapter> gameAdapters,
    CancellationToken cancellationToken) =>
{
    var configuration = await configurationStore.GetOrCreateAsync(gameKey, cancellationToken);
    var adapter = gameAdapters.FirstOrDefault(a => string.Equals(a.GameKey, gameKey, StringComparison.OrdinalIgnoreCase));
    var result = adapter is null
        ? await dockerAgentClient.StartAsync(gameKey, cancellationToken)
        : await dockerAgentClient.StartAsync(gameKey, adapter.GetContainerEnv(configuration.JsonContent), cancellationToken);
    return Results.Ok(result);
});

api.MapPost("/server/{gameKey}/stop", async (string gameKey, IDockerAgentClient dockerAgentClient, CancellationToken cancellationToken) =>
{
    var result = await dockerAgentClient.StopAsync(gameKey, cancellationToken);
    return Results.Ok(result);
});

api.MapPost("/server/{gameKey}/restart", async (
    string gameKey,
    IDockerAgentClient dockerAgentClient,
    IConfigurationStore configurationStore,
    IEnumerable<IGameAdapter> gameAdapters,
    CancellationToken cancellationToken) =>
{
    var configuration = await configurationStore.GetOrCreateAsync(gameKey, cancellationToken);
    var adapter = gameAdapters.FirstOrDefault(a => string.Equals(a.GameKey, gameKey, StringComparison.OrdinalIgnoreCase));
    var result = adapter is null
        ? await dockerAgentClient.RestartAsync(gameKey, cancellationToken)
        : await dockerAgentClient.RestartAsync(gameKey, adapter.GetContainerEnv(configuration.JsonContent), cancellationToken);
    return Results.Ok(result);
});

api.MapGet("/config/{gameKey}", async (string gameKey, IConfigurationStore configurationStore, CancellationToken cancellationToken) =>
{
    var configuration = await configurationStore.GetOrCreateAsync(gameKey, cancellationToken);
    return Results.Ok(new ConfigurationResponse(
        configuration.GameKey,
        configuration.DisplayName,
        configuration.JsonContent,
        configuration.UpdatedUtc,
        configuration.UpdatedBy));
});

api.MapPut("/config/{gameKey}", async (
    string gameKey,
    ConfigurationUpdateRequest request,
    ClaimsPrincipal user,
    IConfigurationStore configurationStore,
    CancellationToken cancellationToken) =>
{
    if (!JsonValidator.IsValid(request.JsonContent, out var validationError))
    {
        return Results.BadRequest(new { error = validationError });
    }

    var updatedBy = user.Identity?.Name ?? "unknown";
    var configuration = await configurationStore.SaveAsync(gameKey, request.JsonContent, updatedBy, cancellationToken);

    return Results.Ok(new ConfigurationResponse(
        configuration.GameKey,
        configuration.DisplayName,
        configuration.JsonContent,
        configuration.UpdatedUtc,
        configuration.UpdatedBy));
});

api.MapPost("/config/{gameKey}/apply", async (
    string gameKey,
    ConfigurationApplyRequest request,
    ClaimsPrincipal user,
    IConfigurationStore configurationStore,
    IDockerAgentClient dockerAgentClient,
    IEnumerable<IGameAdapter> gameAdapters,
    CancellationToken cancellationToken) =>
{
    if (!JsonValidator.IsValid(request.JsonContent, out var validationError))
    {
        return Results.BadRequest(new { error = validationError });
    }

    var updatedBy = user.Identity?.Name ?? "unknown";
    var configuration = await configurationStore.SaveAsync(gameKey, request.JsonContent, updatedBy, cancellationToken);
    var adapter = gameAdapters.FirstOrDefault(a => string.Equals(a.GameKey, gameKey, StringComparison.OrdinalIgnoreCase));
    var actionResult = adapter is null
        ? await dockerAgentClient.RestartAsync(gameKey, cancellationToken)
        : await dockerAgentClient.RestartAsync(gameKey, adapter.GetContainerEnv(configuration.JsonContent), cancellationToken);

    return Results.Ok(actionResult);
});

app.MapRazorPages();

app.Run();

internal sealed record ConfigurationUpdateRequest(string JsonContent);
internal sealed record ConfigurationApplyRequest(string JsonContent);
internal sealed record ConfigurationResponse(
    string GameKey,
    string DisplayName,
    string JsonContent,
    DateTimeOffset UpdatedUtc,
    string? UpdatedBy);
