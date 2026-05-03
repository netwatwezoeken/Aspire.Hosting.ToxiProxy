using Aspire.Hosting.ToxiProxy;

var builder = DistributedApplication.CreateBuilder(args);
var isTestRun = GetBoolArg(args, "TEST_RUN");

var weatherapi =  builder.AddProject<Projects.WeatherApi>("weatherapi");

var mssql = BuildMsSql(builder, "SqlDatabase");

// You can add toxicity to a ConnectionsStringResource
var pgsql = BuildPgSql(builder, "postgresdb")
    .WithToxicity("pgsqlProxy", 8669)
    .AddLatency("latency", 123, 0, 0.75, Direction.Upstream);

var proxy = builder.AddToxiProxyServer("toxiproxy", 8474)
    .With(pgsql);

// no UI improves test performance
if (!isTestRun)
{
    proxy.WithNewUi();
    proxy.WithUi();
}

var toxicMsSql = proxy.AddConnectionStringProxy("mssqlProxy", 8668, mssql)
    .AddLatency("latency",150, 0, 0.95, Direction.Downstream)
    .AddBandwidthLimit("bandwidth",102, 0.85, Direction.Downstream);

var toxicWeather = proxy.AddHttpProxy("weatherapiProxy", 8666, weatherapi)
    .WaitFor(weatherapi)
    .AddLatency("latency",150, 0, 0.95, Direction.Downstream)
    .AddBandwidthLimit("bandwidth",102, 0.85, Direction.Downstream);

builder.AddProject<Projects.DemoApi>("demoapi")
    .WithReference(toxicWeather)
    .WithReference(toxicMsSql)
    .WithReference(pgsql)
    .WaitFor(toxicWeather)
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

IResourceBuilder<SqlServerDatabaseResource> BuildMsSql(IDistributedApplicationBuilder distributedApplicationBuilder, string dbname)
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
    var resourceBuilder = sqlServer.AddDatabase(dbname);
    return resourceBuilder;
}

IResourceBuilder<PostgresDatabaseResource> BuildPgSql(IDistributedApplicationBuilder distributedApplicationBuilder, string dbname)
{
    var postgres = distributedApplicationBuilder.AddPostgres("postgres");
    
    var sqlServer = postgres;
    // set port for deterministic test behavior
    if (isTestRun)
    {
        sqlServer = postgres.WithHostPort(65433);
    }
    return sqlServer.AddDatabase(dbname);
}