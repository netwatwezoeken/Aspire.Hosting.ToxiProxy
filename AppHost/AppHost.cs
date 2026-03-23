using Aspire.Hosting.ToxiProxy;

var builder = DistributedApplication.CreateBuilder(args);

var weatherapi =  builder.AddProject<Projects.WebApplication>("weatherapi");

var proxy = builder.AddToxiProxyServer("toxiproxy");
    proxy.WithUi()
        .WithNewUi();

var toxicWeatherApi = proxy.AddHttpProxy("apiProxy", 8666, weatherapi)
    .AddLatency("latency",123, 0, 0.45, Direction.Upstream)
    .AddBandwidthLimit("bandwidth-1",321, 0.25, Direction.Downstream)
    .AddBandwidthLimit("bandwidth-2",142, 0.9, Direction.Upstream);

var httpEndpoint2 = proxy.AddHttpProxy("otherProxy", 8667, weatherapi)
    .AddLatency("latency",1000);

builder.AddProject<Projects.DemoApi>("demoapi")
    .WithReference(toxicWeatherApi)
    .WaitFor(toxicWeatherApi)
    .WithUrlForEndpoint("http", ep => new() { Url = $"/forecast", DisplayText = "Forecast" });
    
builder.Build().Run();