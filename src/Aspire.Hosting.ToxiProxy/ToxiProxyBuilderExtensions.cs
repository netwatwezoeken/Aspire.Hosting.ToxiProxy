using System.Text.Json;
using System.Text.Json.Serialization;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ToxiProxy.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Refit;

namespace Aspire.Hosting.ToxiProxy;

public static class ToxiProxyBuilderExtensions
{
    /// <summary>
    /// Adds a toxiproxy container resource to the application model.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="ToxyProxyContainerImageTags.Tag"/> tag of the <inheritdoc cref="ToxyProxyContainerImageTags.Registry"/>/<inheritdoc cref="ToxyProxyContainerImageTags.Image"/> container image.
    /// </remarks>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="port">The host port for ToxiProxy.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{ToxiProxyResource}"/>.</returns>
    public static IResourceBuilder<ToxiProxyResource> AddToxiProxyServer(this IDistributedApplicationBuilder builder, [ResourceName] string name, int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var toxiProxy = new ToxiProxyResource(name);
        
        return builder.AddResource(toxiProxy)
            .WithHttpEndpoint(port, 8474)
            .WithArgs("-host", "0.0.0.0")
            .WithImage(ToxyProxyContainerImageTags.Image, ToxyProxyContainerImageTags.Tag)
            .WithImageRegistry(ToxyProxyContainerImageTags.Registry)
            .WithIconName("ArrowCircleDownUp")
            .WithHttpHealthCheck("/proxies")
            .OnResourceReady(async (toxiproxy, _, _) =>
            {
                foreach (var proxy in toxiproxy.ConnectionStringResources)
                {
                    await ConfigureProxy(proxy, proxy.TargetPort);
                }

                foreach (var proxy in toxiproxy.HttpEndPointResources)
                {
                    await ConfigureProxy(proxy, proxy.TargetResource.Resource.GetEndpoint("http").Port);
                }
            });
    }

    private static async Task ConfigureProxy(ToxicEndpointResource proxy, int targetPort)
    {
        var toxiProxyUrl = proxy.Parent.PrimaryEndpoint.Url;
        var client = RestService.For<IToxiClient>(toxiProxyUrl, ToxiClientSettings.Refit);

        var upstream = $"host.docker.internal:{targetPort}";
        await client.CreateProxy(ToxicMapper.BuildProxy(proxy.Name, proxy.Port, upstream));

        foreach (var toxic in ToxicMapper.MapToxics(proxy.ToxiResources))
        {
            await client.AddToxic(toxic, proxy.Name);
        }
    }

    /// <summary>
    /// Adds a http proxy resource for a specific service.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{ToxiProxyResource}"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="port">New port for the proxy to listen on.</param>
    /// <param name="proxiedService">Name of the service that is proxied.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{ToxicHttpEndPointResource}"/>.</returns>
    public static IResourceBuilder<ToxicHttpEndpointResource> AddHttpProxy(this IResourceBuilder<ToxiProxyResource> builder, [ResourceName] string name, int port, IResourceBuilder<IResourceWithEndpoints> proxiedService)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var httpEndpoint = new ToxicHttpEndpointResource(name, builder.Resource, port, proxiedService);
        builder.Resource.AddHttpProxy(httpEndpoint);
        
        var healthCheckKey = $"{name}_check";
        builder.ApplicationBuilder.Services.AddHealthChecks()
            .AddAsyncCheck(healthCheckKey, async () => 
                await CheckProxyHealth(builder, name));
        
        return builder.ApplicationBuilder
            .AddResource(httpEndpoint)
            .WithHealthCheck(healthCheckKey)
            .WithEndpoint(targetPort: port, name: ExternalHttpEndpointResource.PrimaryEndpointName, scheme: "http", isExternal: true, isProxied:false)
            .WithIconName("ArrowCircleDown");
    }

    private static async Task<HealthCheckResult> CheckProxyHealth(IResourceBuilder<ToxiProxyResource> builder, string name)
    {
        var toxiProxyUrl = builder.Resource.PrimaryEndpoint.Url;
        var client = RestService.For<IToxiClient>(toxiProxyUrl, ToxiClientSettings.Refit);
        var result = await client.GetProxies();
        return result.ToProxies().Any(p => p.Key == name) ?
            HealthCheckResult.Healthy() :
            HealthCheckResult.Unhealthy("Proxy not (yet) known in ToxiProxy");
    }

    /// <summary>
    /// Add Toxicity to a ConnectionString Resource
    /// </summary>
    /// <param name="proxiedResourceBuilder">The <see cref="IResourceBuilder{IResourceWithConnectionString}"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="port">New port for the proxy to listen on.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{ToxicConnectionStringResource}"/>.</returns>
    public static IResourceBuilder<ToxicConnectionStringResource> WithToxicity(this IResourceBuilder<IResourceWithConnectionString> proxiedResourceBuilder, [ResourceName] string name, int port)
    {
        ArgumentNullException.ThrowIfNull(proxiedResourceBuilder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        
        var connectionStringResource = new ToxicConnectionStringResource(name, port, proxiedResourceBuilder);

        proxiedResourceBuilder.OnConnectionStringAvailable(
            BuildConnectionString(name, port, proxiedResourceBuilder, connectionStringResource));
        
        return proxiedResourceBuilder.ApplicationBuilder
            .AddResource(connectionStringResource);
    }
    /// <summary>
    /// Add Toxicity to a Endpoint Resource
    /// </summary>
    /// <param name="proxiedResourceBuilder">The <see cref="IResourceBuilder{IResourceWithEndpoints}"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="port">New port for the proxy to listen on.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{ToxicHttpEndpointResource}"/>.</returns>
    public static IResourceBuilder<ToxicHttpEndpointResource> WithToxicity(this IResourceBuilder<IResourceWithEndpoints> proxiedResourceBuilder, [ResourceName] string name, int port)
    {
        ArgumentNullException.ThrowIfNull(proxiedResourceBuilder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        
        var httpEndpoint = new ToxicHttpEndpointResource(name, port, proxiedResourceBuilder);
        
        var healthCheckKey = $"{name}_check";
        proxiedResourceBuilder.ApplicationBuilder.Services.AddHealthChecks()
            .AddAsyncCheck(healthCheckKey, async () =>
            {
                var toxiProxyUrl = httpEndpoint.Parent.PrimaryEndpoint.Url;
                var client = RestService.For<IToxiClient>(toxiProxyUrl, ToxiClientSettings.Refit);
                var result = await client.GetProxies();
                return result.ToProxies().Any(p => p.Key == name) ?
                    HealthCheckResult.Healthy() :
                    HealthCheckResult.Unhealthy("Proxy not (yet) known in ToxiProxy");
            });
        
        return proxiedResourceBuilder.ApplicationBuilder
            .AddResource(httpEndpoint)
            .WithHealthCheck(healthCheckKey)
            .WithEndpoint(targetPort: port, name: ExternalHttpEndpointResource.PrimaryEndpointName, scheme: "http", isExternal: true, isProxied:false)
            .WithIconName("ArrowCircleDown");
    }

    /// <summary>
    /// Attach a Toxic resource to the ToxiProxy server. Use this with the "low impact API"
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="connectionStringResource"></param>
    /// <returns></returns>
    public static IResourceBuilder<ToxiProxyResource> With(
        this IResourceBuilder<ToxiProxyResource> builder,
        IResourceBuilder<ToxicConnectionStringResource> connectionStringResource)
    {
        builder.Resource.AddConnectionStringProxy(connectionStringResource.Resource);
        connectionStringResource.Resource.Parent = builder.Resource;
        return builder;
    }
    
    /// <summary>
    /// Attach a Toxic resource to the ToxiProxy server. Use this with the "low impact API"
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="httpEndpointResource"></param>
    /// <returns></returns>
    public static IResourceBuilder<ToxiProxyResource> With(
        this IResourceBuilder<ToxiProxyResource> builder,
        IResourceBuilder<ToxicHttpEndpointResource> httpEndpointResource)
    {
        builder.Resource.AddHttpProxy(httpEndpointResource.Resource);
        httpEndpointResource.Resource.Parent = builder.Resource;
        return builder;
    }

    /// <summary>
    /// Adds a http proxy resource for a specific service.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{ToxiProxyResource}"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="port">New port for the proxy to listen on.</param>
    /// <param name="proxiedResourceBuilder">Name of the service that is proxied.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{ToxicConnectionStringResource}"/>.</returns>
    public static IResourceBuilder<ToxicConnectionStringResource> AddConnectionStringProxy(this IResourceBuilder<ToxiProxyResource> builder, [ResourceName] string name, int port, IResourceBuilder<IResourceWithConnectionString> proxiedResourceBuilder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        
        var connectionStringResource = new ToxicConnectionStringResource(name, builder.Resource, port, proxiedResourceBuilder);
        builder.Resource.AddConnectionStringProxy(connectionStringResource);
        builder.WaitFor(proxiedResourceBuilder);
        
        proxiedResourceBuilder.OnConnectionStringAvailable(
            BuildConnectionString(name, port, proxiedResourceBuilder, connectionStringResource));
        
        var healthCheckKey = $"{name}_check";
        builder.ApplicationBuilder.Services.AddHealthChecks()
            .AddAsyncCheck(healthCheckKey, async () => 
                await CheckProxyHealth(builder, name));
        
        return builder.ApplicationBuilder
            .AddResource(connectionStringResource)
            .WithHealthCheck(healthCheckKey);
    }
    
    /// <summary>
    /// Adds a latency toxic to a specific proxy.
    /// </summary>
    /// <param name="builder">The <see cref="ToxicHttpEndpointResource"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="latency">time in milliseconds.</param>
    /// <param name="jitter">time in milliseconds.</param>
    /// <param name="toxicity">probability of the toxic being applied to a link (defaults to 1.0, 100%).</param>
    /// <param name="direction">link direction to affect (defaults to downstream).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{ToxicHttpEndPointResource}"/>.</returns>
    public static IResourceBuilder<TResource> AddLatency<TResource>(
        this IResourceBuilder<TResource> builder,
        [ResourceName] string name,
        int latency,
        int jitter = 0,
        double toxicity = 1.0,
        Direction direction = Direction.Downstream)
        where TResource : ToxicEndpointResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        
        var toxic = new Toxic(ToxicType.Latency, new Parameters(latency, jitter), direction, toxicity);

        var toxi = new ToxicResource(name, toxic, builder.Resource);
        builder.Resource.AddToxic(toxi);
        
        return builder;
    }

    /// <summary>
    /// Adds a bandwidth toxic to a specific proxy.
    /// </summary>
    /// <param name="builder">The <see cref="ToxicHttpEndpointResource"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="bandwidth">Bandwidth limit in in KB/s.</param>
    /// <param name="toxicity">probability of the toxic being applied to a link (defaults to 1.0, 100%).</param>
    /// <param name="direction">link direction to affect (defaults to downstream)</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{ToxicHttpEndPointResource}"/>.</returns>
    public static IResourceBuilder<TResource> AddBandwidthLimit<TResource>(
        this IResourceBuilder<TResource> builder,
        [ResourceName] string name,
        int bandwidth,
        double toxicity = 1.0,
        Direction direction = Direction.Downstream)
        where TResource : ToxicEndpointResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        
        var toxic = new Toxic(ToxicType.Bandwidth, new Parameters(Bandwidth: bandwidth), direction, toxicity);

        var toxi = new ToxicResource(name, toxic, builder.Resource);
        builder.Resource.AddToxic(toxi);
        
        return builder;
    }

    /// <summary>
    /// Adds a slow close toxic to a specific proxy. The toxic delays the TCP socket
    /// from closing until <paramref name="delay"/> has elapsed.
    /// </summary>
    /// <param name="builder">The <see cref="ToxicHttpEndpointResource"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="delay">time in milliseconds to delay the socket close by.</param>
    /// <param name="toxicity">probability of the toxic being applied to a link (defaults to 1.0, 100%).</param>
    /// <param name="direction">link direction to affect (defaults to downstream).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TResource}"/>.</returns>
    public static IResourceBuilder<TResource> AddSlowClose<TResource>(
        this IResourceBuilder<TResource> builder,
        [ResourceName] string name,
        int delay,
        double toxicity = 1.0,
        Direction direction = Direction.Downstream)
        where TResource : ToxicEndpointResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        
        var toxic = new Toxic(ToxicType.SlowClose, new Parameters(Delay: delay), direction, toxicity);

        var toxi = new ToxicResource(name, toxic, builder.Resource);
        builder.Resource.AddToxic(toxi);
        
        return builder;
    }

    /// <summary>
    /// Adds a timeout toxic to a specific proxy. Data is stopped in the given direction, and
    /// after <paramref name="timeout"/> the connection is closed. A <paramref name="timeout"/> of
    /// <c>0</c> means the connection is held open (data is dropped) until the client or upstream
    /// closes it.
    /// </summary>
    /// <param name="builder">The <see cref="ToxicHttpEndpointResource"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="timeout">time in milliseconds to wait before the connection is closed (0 keeps it open indefinitely).</param>
    /// <param name="toxicity">probability of the toxic being applied to a link (defaults to 1.0, 100%).</param>
    /// <param name="direction">link direction to affect (defaults to downstream).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TResource}"/>.</returns>
    public static IResourceBuilder<TResource> AddTimeout<TResource>(
        this IResourceBuilder<TResource> builder,
        [ResourceName] string name,
        int timeout,
        double toxicity = 1.0,
        Direction direction = Direction.Downstream)
        where TResource : ToxicEndpointResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        
        var toxic = new Toxic(ToxicType.Timeout, new Parameters(Timeout: timeout), direction, toxicity);

        var toxi = new ToxicResource(name, toxic, builder.Resource);
        builder.Resource.AddToxic(toxi);
        
        return builder;
    }

    /// <summary>
    /// Adds a reset peer toxic to a specific proxy. Simulates a TCP RST ("connection reset by peer")
    /// by closing the connection immediately, or after <paramref name="timeout"/> milliseconds have
    /// elapsed. A <paramref name="timeout"/> of <c>0</c> (the default) resets the connection immediately.
    /// </summary>
    /// <param name="builder">The <see cref="ToxicHttpEndpointResource"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="timeout">time in milliseconds before the connection is reset (0 resets immediately).</param>
    /// <param name="toxicity">probability of the toxic being applied to a link (defaults to 1.0, 100%).</param>
    /// <param name="direction">link direction to affect (defaults to downstream).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TResource}"/>.</returns>
    public static IResourceBuilder<TResource> AddResetPeer<TResource>(
        this IResourceBuilder<TResource> builder,
        [ResourceName] string name,
        int timeout = 0,
        double toxicity = 1.0,
        Direction direction = Direction.Downstream)
        where TResource : ToxicEndpointResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        
        var toxic = new Toxic(ToxicType.ResetPeer, new Parameters(Timeout: timeout), direction, toxicity);

        var toxi = new ToxicResource(name, toxic, builder.Resource);
        builder.Resource.AddToxic(toxi);
        
        return builder;
    }

    /// <summary>
    /// Adds a slicer toxic to a specific proxy. Slices TCP data up into small bits, optionally
    /// adding a <paramref name="delay"/> between each sliced "packet". Packet sizes are
    /// <paramref name="averageSize"/> +/- <paramref name="sizeVariation"/> bytes.
    /// </summary>
    /// <param name="builder">The <see cref="ToxicHttpEndpointResource"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="averageSize">size in bytes of an average packet.</param>
    /// <param name="sizeVariation">variation in bytes of an average packet (should be smaller than <paramref name="averageSize"/>).</param>
    /// <param name="delay">time in microseconds to delay each packet by.</param>
    /// <param name="toxicity">probability of the toxic being applied to a link (defaults to 1.0, 100%).</param>
    /// <param name="direction">link direction to affect (defaults to downstream).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TResource}"/>.</returns>
    public static IResourceBuilder<TResource> AddSlicer<TResource>(
        this IResourceBuilder<TResource> builder,
        [ResourceName] string name,
        int averageSize,
        int sizeVariation = 0,
        int delay = 0,
        double toxicity = 1.0,
        Direction direction = Direction.Downstream)
        where TResource : ToxicEndpointResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        
        var toxic = new Toxic(ToxicType.Slicer, new Parameters(Delay: delay, AverageSize: averageSize, SizeVariation: sizeVariation), direction, toxicity);

        var toxi = new ToxicResource(name, toxic, builder.Resource);
        builder.Resource.AddToxic(toxi);
        
        return builder;
    }

    /// <summary>
    /// Adds a limit data toxic to a specific proxy. Closes the connection once <paramref name="bytes"/>
    /// bytes have been transmitted through it.
    /// </summary>
    /// <param name="builder">The <see cref="ToxicHttpEndpointResource"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="bytes">number of bytes to transmit before the connection is closed.</param>
    /// <param name="toxicity">probability of the toxic being applied to a link (defaults to 1.0, 100%).</param>
    /// <param name="direction">link direction to affect (defaults to downstream).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TResource}"/>.</returns>
    public static IResourceBuilder<TResource> AddLimitData<TResource>(
        this IResourceBuilder<TResource> builder,
        [ResourceName] string name,
        long bytes,
        double toxicity = 1.0,
        Direction direction = Direction.Downstream)
        where TResource : ToxicEndpointResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        
        var toxic = new Toxic(ToxicType.LimitData, new Parameters(Bytes: bytes), direction, toxicity);

        var toxi = new ToxicResource(name, toxic, builder.Resource);
        builder.Resource.AddToxic(toxi);
        
        return builder;
    }

    /// <summary>
    /// Adds a packet loss toxic to a specific proxy. Randomly drops chunks flowing through the proxy,
    /// simulating flaky Wi-Fi, mobile, or satellite network conditions.
    /// </summary>
    /// <param name="builder">The <see cref="ToxicHttpEndpointResource"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="lossRate">probability [0.0-1.0] that a chunk is dropped.</param>
    /// <param name="correlation">extra drop probability [0.0-1.0] when the previous chunk was dropped, modeling burst loss (defaults to 0.0).</param>
    /// <param name="toxicity">probability of the toxic being applied to a link (defaults to 1.0, 100%).</param>
    /// <param name="direction">link direction to affect (defaults to downstream).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TResource}"/>.</returns>
    public static IResourceBuilder<TResource> AddPacketLoss<TResource>(
        this IResourceBuilder<TResource> builder,
        [ResourceName] string name,
        double lossRate,
        double correlation = 0.0,
        double toxicity = 1.0,
        Direction direction = Direction.Downstream)
        where TResource : ToxicEndpointResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        
        var toxic = new Toxic(ToxicType.PacketLoss, new Parameters(LossRate: lossRate, Correlation: correlation), direction, toxicity);

        var toxi = new ToxicResource(name, toxic, builder.Resource);
        builder.Resource.AddToxic(toxi);
        
        return builder;
    }

    public static IResourceBuilder<T> WithUi<T>(this IResourceBuilder<T> builder)
        where T : ToxiProxyResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ApplicationBuilder.AddContainer(builder.Resource.Name + "-ui", "buckle/toxiproxy-frontend")
            .WithHttpEndpoint(targetPort: 8080)
            .WithEnvironment(i =>
            {
                i.EnvironmentVariables.Add("TOXIPROXY_URL", builder.Resource.GetEndpoint("http"));
            })
            .WithHttpHealthCheck("/api/proxies");
        return builder;
    }
    
    public static IResourceBuilder<T> WithNewUi<T>(this IResourceBuilder<T> builder)
        where T : ToxiProxyResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ApplicationBuilder.AddDockerfile(
                "toxi-ui", "../../toxi-ui")
            .WithHttpEndpoint(targetPort: 3000)
            .WithEnvironment(i =>
            {
                i.EnvironmentVariables.Add("TOXIPROXY_URL", builder.Resource.GetEndpoint("http"));
            });
        return builder;
    }
    
    public static IResourceBuilder<TDestination> WithReference<TDestination>(this IResourceBuilder<TDestination> builder, IResourceBuilder<ToxicHttpEndpointResource> endpointReference)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(endpointReference);

        builder.WithEnvironment(async context =>
        {
            var proxiedServiceName = endpointReference.Resource.ProxiedService;
            var port = endpointReference.Resource.Port;
            context.EnvironmentVariables[$"services__{proxiedServiceName}__http__0"] = $"http://localhost:{port}";
        });
        return builder;
    }
    
    public static IResourceBuilder<TDestination> WithReference<TDestination>(this IResourceBuilder<TDestination> builder, IResourceBuilder<ToxicConnectionStringResource> source, string? connectionName = null, bool optional = false)
        where TDestination : IResourceWithEnvironment
    {
        return ResourceBuilderExtensions.WithReference(
            builder, 
            source, 
            connectionName ?? source.Resource.TargetResource.Resource.Name,
            optional);
    }
    
    private static Func<IResourceWithConnectionString, ConnectionStringAvailableEvent, CancellationToken, Task> BuildConnectionString(string name, int port, IResourceBuilder<IResourceWithConnectionString> targetResourceBuilder, ToxicConnectionStringResource connectionStringResource)
    {
        return async (targetConnectionString, _, ct) =>
        {
            if (targetResourceBuilder.Resource is IResourceWithParent resource)
            {
                if (resource.Parent.TryGetEndpoints(out var endpoints))
                {
                    var targetPort = endpoints.FirstOrDefault()?.AllocatedEndpoint?.Port;
                    if(targetPort == null)
                        throw new DistributedApplicationException($"Could not get target port.");

                    connectionStringResource.TargetPort = (int)targetPort;
                }
            }
            
            var connectionString = await targetConnectionString.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);

            if (connectionString == null)
            {
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{name}' resource but the connection string was null.");
            }
                
            connectionStringResource.ConnectionStringExpression = ReferenceExpression.Create($"{ConnectionStringRewriter.Rewrite(connectionString, connectionStringResource.TargetPort, port)}");
        };
    }
}