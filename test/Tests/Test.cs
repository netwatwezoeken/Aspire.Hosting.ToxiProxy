using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Projects;

namespace Tests;

public class Test : IAsyncLifetime
{
    private static DistributedApplication? _app;
    
    [Fact]
    public async Task Check_wiring_between_demoApi_and_weatherApi()
    {
        if(_app == null)
            Assert.Fail("Application not initialized");
        var demoApiClient = _app.CreateHttpClient("demoapi");
        var forecast = await demoApiClient
            .GetFromJsonAsync<WeatherApi.WeatherForecast[]>("forecast", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(5, forecast?.Length);
    }
    
    [Fact]
    public async Task Check_toxiproxy_config()
    {
        if(_app == null)
            Assert.Fail("Application not initialized");
        var toxiproxyClient = _app.CreateHttpClient("toxiproxy");
        var toxiproxyConfig = await toxiproxyClient.GetStreamAsync("proxies", TestContext.Current.CancellationToken);
        await VerifyJson(toxiproxyConfig);
    }

    public async ValueTask InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<AppHost>(
                [
                    "DcpPublisher:RandomizePorts=false",
                    "ASPNETCORE_URLS=http://localhost:18888",
                    "ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true"
                ],
                configureBuilder: (appOptions, hostSettings) =>
                {
                    appOptions.DisableDashboard = false;
                    appOptions.AllowUnsecuredTransport = true;
                },
                CancellationToken.None);
        _app = await builder.BuildAsync();
        await _app.StartAsync();
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await _app.ResourceNotifications.WaitForResourceHealthyAsync(
            "demoapi",
            cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
