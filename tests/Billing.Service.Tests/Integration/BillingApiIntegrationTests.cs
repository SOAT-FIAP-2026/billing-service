using Billing.Service.Api;
using Billing.Service.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace Billing.Service.Tests.Integration;

public sealed class BillingApiIntegrationTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            EnvironmentName = "Testing"
        });

        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Host=localhost;Port=5432;Database=integration-tests;Username=test;Password=test",
            ["AWS_ACCESS_KEY_ID"] = "test",
            ["AWS_SECRET_ACCESS_KEY"] = "test",
            ["AWS_REGION"] = "sa-east-1"
        });

        var startup = new Startup(builder.Configuration);
        startup.ConfigureServices(builder.Services);
        RemoveSqsWorker(builder.Services);

        _app = builder.Build();
        startup.Configure(_app, _app.Environment);
        await _app.StartAsync();
        _client = _app.GetTestClient();
        _client.BaseAddress = new Uri("https://localhost");
    }

    [Fact]
    public async Task Ping_deve_executar_pipeline_http_e_retornar_ok()
    {
        using var response = await _client.GetAsync("/api/ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("pong");
        response.Headers.TryGetValues("X-Correlation-ID", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Health_live_deve_retornar_ok_sem_consultar_dependencias_externas()
    {
        using var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"status\":\"healthy\"");
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private static void RemoveSqsWorker(IServiceCollection services)
    {
        var workerDescriptors = services
            .Where(descriptor => descriptor.ImplementationType == typeof(BillingSqsBackgroundWorker))
            .ToArray();

        foreach (var descriptor in workerDescriptors)
            services.Remove(descriptor);
    }
}
