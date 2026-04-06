using Aspire.Hosting.ToxiProxy;

var builder = DistributedApplication.CreateBuilder(args);
var testRun = GetBoolArg(args, "TEST_RUN");

var weatherapi =  builder.AddProject<Projects.WeatherApi>("weatherapi");

var sqlbuilder = builder.AddSqlServer(
        "sql-server",
        builder.AddParameter("sql-server-password", secret: true))
    .WithLifetime(ContainerLifetime.Persistent);
var sqlServer = sqlbuilder;
// set port for deterministic test behavior
if (testRun)
{
    sqlServer = sqlbuilder.WithHostPort(65432);
}
var sql = sqlServer.AddDatabase("SqlDatabase");

var proxy = builder.AddToxiProxyServer("toxiproxy", 8474);
// no UI improves test performance
if (!testRun)
{
    proxy.WithNewUi();
    proxy.WithUi();
}

var toxicSql = proxy.AddConnectionStringProxy("sqlProxy", 8668, sql);

var toxicWeatherApi = proxy.AddHttpProxy("apiProxy", 8666, weatherapi)
    .AddLatency("latency",123, 0, 0.8, Direction.Upstream)
    .AddBandwidthLimit("bandwidth",142, 0.9, Direction.Downstream);

var toxicWeatherApi2 = proxy.AddHttpProxy("otherProxy", 8667, weatherapi)
    .AddLatency("latency",1000);

builder.AddProject<Projects.DemoApi>("demoapi")
    .WithReference(toxicWeatherApi)
    .WithReference(toxicSql)
    .WaitFor(toxicWeatherApi)
    .WithUrlForEndpoint("http", ep => new() { Url = $"/forecast", DisplayText = "Forecast" });
    
builder.Build().Run();

static bool GetBoolArg(string[] args, string name, bool defaultValue = false)
{
    foreach (var arg in args)
    {
        var parts = arg.Split('=', 2);
        if (parts.Length == 2 && parts[0].Equals(name, StringComparison.OrdinalIgnoreCase) &&
            bool.TryParse(parts[1], out var value))
        {
            return value;
        }
    }

    return defaultValue;
}