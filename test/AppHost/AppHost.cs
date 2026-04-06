using Aspire.Hosting.ToxiProxy;

var builder = DistributedApplication.CreateBuilder(args);
var isTestRun = GetBoolArg(args, "TEST_RUN");

var weatherapi =  builder.AddProject<Projects.WeatherApi>("weatherapi");

var mssql = BuildMsSql(builder);

var pgsql = BuildPgSql(builder);

var proxy = builder.AddToxiProxyServer("toxiproxy", 8474);
// no UI improves test performance
if (!isTestRun)
{
    proxy.WithNewUi();
    proxy.WithUi();
}

var toxicMsSql = proxy.AddConnectionStringProxy("mssqlProxy", 8668, mssql);

var toxicPgSql = proxy.AddConnectionStringProxy("pgsqlProxy", 8669, pgsql);

var toxicWeatherApi = proxy.AddHttpProxy("apiProxy", 8666, weatherapi)
    .AddLatency("latency",123, 0, 0.8, Direction.Upstream)
    .AddBandwidthLimit("bandwidth",142, 0.9, Direction.Downstream);

var toxicWeatherApi2 = proxy.AddHttpProxy("otherProxy", 8667, weatherapi)
    .AddLatency("latency",1000);

builder.AddProject<Projects.DemoApi>("demoapi")
    .WithReference(toxicWeatherApi)
    .WithReference(toxicMsSql)
    .WithReference(toxicPgSql)
    .WaitFor(toxicWeatherApi)
    .WithUrlForEndpoint("http", ep => new() { Url = $"/forecast", DisplayText = "Forecast" });
    
builder.Build().Run();
return;

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

IResourceBuilder<SqlServerDatabaseResource> BuildMsSql(IDistributedApplicationBuilder distributedApplicationBuilder)
{
    var sqlbuilder = distributedApplicationBuilder.AddSqlServer(
            "sql-server",
            distributedApplicationBuilder.AddParameter("sql-server-password", secret: true))
        .WithLifetime(ContainerLifetime.Persistent);
    var sqlServer = sqlbuilder;
    // set port for deterministic test behavior
    if (isTestRun)
    {
        sqlServer = sqlbuilder.WithHostPort(65432);
    }
    var resourceBuilder = sqlServer.AddDatabase("SqlDatabase");
    return resourceBuilder;
}

IResourceBuilder<PostgresDatabaseResource> BuildPgSql(IDistributedApplicationBuilder distributedApplicationBuilder)
{
    var postgres = distributedApplicationBuilder.AddPostgres("postgres");
    
    var sqlServer = postgres;
    // set port for deterministic test behavior
    if (isTestRun)
    {
        sqlServer = postgres.WithHostPort(65433);
    }
    return sqlServer.AddDatabase("postgresdb");
}