using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ToxiProxy.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Refit;
using Stream = Aspire.Hosting.ToxiProxy.Client.Stream;

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
        var client = RestService.For<IToxiClient>(toxiProxyUrl);

        await client.CreateProxy(new Proxy(
            proxy.Name,
            true,
            $"0.0.0.0:{proxy.Port}",
            $"host.docker.internal:{targetPort}"
        ));
        foreach (var toxicResource in proxy.ToxiResources)
        {
            var toxic = toxicResource.Toxic;
            if (toxic.Type == ToxicType.Latency)
            {
                await client.AddToxic(new Client.Toxic(
                    new Attributes(
                        Latency: toxic.Parameters.Latency, 
                        Jitter: toxic.Parameters.Jitter),
                    toxicResource.Name,
                    ToxicType.Latency,
                    toxic.Direction == Direction.Downstream ? Stream.Downstream : Stream.Upstream,
                    toxic.Toxicity
                ), proxy.Name);
            } else if (toxic.Type == ToxicType.Bandwidth)
            {
                await client.AddToxic(new Client.Toxic(
                    new Attributes(
                        Rate: toxic.Parameters.Bandwidth),
                    toxicResource.Name,
                    ToxicType.Bandwidth,
                    toxic.Direction == Direction.Downstream ? Stream.Downstream : Stream.Upstream,
                    toxic.Toxicity
                ), proxy.Name);
            }
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
            {
                // var toxiProxyUrl = builder.Resource.PrimaryEndpoint.Url;
                // var client = RestService.For<IToxiClient>(toxiProxyUrl);
                // var result = await client.GetProxies();
                return HealthCheckResult.Healthy();
            });
        
        return builder.ApplicationBuilder
            .AddResource(httpEndpoint)
            .WithHealthCheck(healthCheckKey)
            .WithEndpoint(targetPort: port, name: ExternalHttpEndpointResource.PrimaryEndpointName, scheme: "http", isExternal: true, isProxied:false)
            .WithIconName("ArrowCircleDown");
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
                return HealthCheckResult.Healthy();
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
        
        return builder.ApplicationBuilder
            .AddResource(connectionStringResource);
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
                
            connectionStringResource.ConnectionStringExpression = ReferenceExpression.Create($"{connectionString.Replace($"{connectionStringResource.TargetPort};", $"{port};")}");
        };
    }
}