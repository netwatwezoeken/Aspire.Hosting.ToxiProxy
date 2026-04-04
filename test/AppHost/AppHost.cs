using Aspire.Hosting.ToxiProxy;

var builder = DistributedApplication.CreateBuilder(args);

var weatherapi =  builder.AddProject<Projects.WeatherApi>("weatherapi");

var proxy = builder.AddToxiProxyServer("toxiproxy", 8474);
    proxy.WithUi()
        .WithNewUi();

var toxicWeatherApi = proxy.AddHttpProxy("apiProxy", 8666, weatherapi)
    .AddLatency("latency",123, 0, 0.8, Direction.Upstream)
    .AddBandwidthLimit("bandwidth",142, 0.9, Direction.Downstream);

var toxicWeatherApi2 = proxy.AddHttpProxy("otherProxy", 8667, weatherapi)
    .AddLatency("latency",1000);

builder.AddProject<Projects.DemoApi>("demoapi")
    .WithReference(toxicWeatherApi)
    .WaitFor(toxicWeatherApi)
    .WithUrlForEndpoint("http", ep => new() { Url = $"/forecast", DisplayText = "Forecast" });
    
builder.Build().Run();