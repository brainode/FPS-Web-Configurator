using System.Net;
using control_panel.Options;
using control_panel.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace control_panel.Tests.Services;

public sealed class DockerAgentClientTests
{
    [Fact]
    public async Task GetStatusAsync_ReturnsNotConfigured_WhenBaseUrlIsMissing()
    {
        var client = CreateClient(baseUrl: string.Empty);

        var status = await client.GetStatusAsync("warsow");

        Assert.Equal("agent-not-configured", status.State);
        Assert.Equal("Agent not configured", status.StateLabel);
    }

    [Fact]
    public async Task StartAsync_ReturnsFailure_WhenBaseUrlIsMissing()
    {
        var client = CreateClient(baseUrl: string.Empty);

        var result = await client.StartAsync("warsow");

        Assert.False(result.Success);
        Assert.Equal("start", result.Action);
        Assert.Contains("not configured", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAsync_WithEnv_SendsEnvVarsInRequestBody()
    {
        var handler = new FakeHttpHandler();
        var client = CreateClientWithHandler(handler, "http://localhost:8080");
        var env = new Dictionary<string, string>
        {
            ["WARSOW_GAMETYPE"] = "ca",
            ["WARSOW_START_MAP"] = "wca1"
        };

        await client.StartAsync("warsow", env);

        Assert.NotNull(handler.LastRequest?.Content);
        var body = await handler.LastRequest.Content.ReadAsStringAsync();
        Assert.Contains("WARSOW_GAMETYPE", body);
        Assert.Contains("ca", body);
    }

    [Fact]
    public async Task StartAsync_WithNoEnv_SendsNoRequestBody()
    {
        var handler = new FakeHttpHandler();
        var client = CreateClientWithHandler(handler, "http://localhost:8080");

        await client.StartAsync("warsow");

        Assert.Null(handler.LastRequest?.Content);
    }

    [Fact]
    public async Task RestartAsync_WithEnv_SendsEnvVarsInRequestBody()
    {
        var handler = new FakeHttpHandler();
        var client = CreateClientWithHandler(handler, "http://localhost:8080");
        var env = new Dictionary<string, string>
        {
            ["WARSOW_RCON_PASSWORD"] = "secret",
            ["WARSOW_SCORELIMIT"] = "11"
        };

        await client.RestartAsync("warsow", env);

        Assert.NotNull(handler.LastRequest?.Content);
        var body = await handler.LastRequest.Content.ReadAsStringAsync();
        Assert.Contains("WARSOW_RCON_PASSWORD", body);
        Assert.Contains("secret", body);
    }

    [Fact]
    public async Task StartAsync_WithEnv_PostsToCorrectEndpoint()
    {
        var handler = new FakeHttpHandler();
        var client = CreateClientWithHandler(handler, "http://localhost:8080");

        await client.StartAsync("warsow", new Dictionary<string, string> { ["K"] = "V" });

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.EndsWith("/api/games/warsow/start", handler.LastRequest.RequestUri?.ToString());
    }

    private static DockerAgentClient CreateClient(string baseUrl)
    {
        return new DockerAgentClient(
            new HttpClient(),
            Microsoft.Extensions.Options.Options.Create(new DockerAgentOptions { BaseUrl = baseUrl }),
            NullLogger<DockerAgentClient>.Instance);
    }

    private static DockerAgentClient CreateClientWithHandler(HttpMessageHandler handler, string baseUrl)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
        return new DockerAgentClient(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(new DockerAgentOptions { BaseUrl = baseUrl }),
            NullLogger<DockerAgentClient>.Instance);
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
