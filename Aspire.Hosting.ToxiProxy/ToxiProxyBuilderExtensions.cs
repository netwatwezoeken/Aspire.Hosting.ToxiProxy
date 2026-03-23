using System.Net;
using System.Net.Sockets;
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
            .WithIconName("DatabaseMultiple")
            .WithHttpHealthCheck("/proxies")
            .OnResourceEndpointsAllocated((resource, @event, arg3) =>
            {
                foreach (var proxy in resource.HttpEndPointResources)
                {
                    var endpointAnnotation = resource.Annotations.OfType<EndpointAnnotation>().FirstOrDefault(a => a.Name == proxy.Name);
                    if (endpointAnnotation != null)
                    {
                        endpointAnnotation.AllocatedEndpoint = new AllocatedEndpoint(endpointAnnotation, "localhost", proxy.Port);
                    }
                }
                return Task.FromResult(resource);
            })
            .OnResourceReady(async (toxiproxy, evt, cancellationToken) =>
            {
                foreach (var proxy in toxiproxy.HttpEndPointResources)
                {
                    var toxiProxyUrl = proxy.Parent.PrimaryEndpoint.Url;
                    var client = RestService.For<IToxiClient>(toxiProxyUrl);

                    var portToUse = port;
                    if (port is null)
                    {
                        var listener = new TcpListener(IPAddress.Loopback, 0);
                        listener.Start();
                        portToUse = ((IPEndPoint)listener.LocalEndpoint).Port;
                        listener.Stop();
                    }

                    await client.CreateProxy(new Proxy(
                        proxy.Name,
                        true,
                        $"0.0.0.0:{portToUse}",
                        $"host.docker.internal:{proxy.TargetResource.Resource.GetEndpoint("http").Port}"
                    ));
                    foreach (var toxicResource in proxy.ToxicResources)
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
                    var t = await client.GetProxies();
                    var proxies = t.ToProxies();
                }
            });
    }

    /// <summary>
    /// Adds a http proxy resource for a specific service.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{ToxiProxyResource}"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="port">New port for the proxy to listen on.</param>
    /// <param name="targetPort">Port of the proxied service.</param>
    /// <param name="proxiedService">Name of the service that is proxied.</param>
    /// <param name="targetResource"></param>
    /// <returns>A reference to the <see cref="IResourceBuilder{ToxicHttpEndPointResource}"/>.</returns>
    public static IResourceBuilder<ToxicHttpEndPointResource> AddHttpProxy(
        this IResourceBuilder<ToxiProxyResource> builder, [ResourceName] string name, int port, IResourceBuilder<IResourceWithEndpoints> targetResource)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var httpEndpoint = new ToxicHttpEndPointResource(name, builder.Resource, port, null, targetResource);
        builder.Resource.AddHttpProxy(httpEndpoint);
        
        var healthCheckKey = $"{name}_check";
        builder.ApplicationBuilder.Services.AddHealthChecks()
            .AddAsyncCheck(healthCheckKey, async () =>
            {
                var toxiProxyUrl = builder.Resource.PrimaryEndpoint.Url;
                var client = RestService.For<IToxiClient>(toxiProxyUrl);
                var t = await client.GetProxies();
                var proxies = t.ToProxies();
                return HealthCheckResult.Healthy();
            });
        
        return builder.ApplicationBuilder
            .AddResource(httpEndpoint)
            .WithHealthCheck(healthCheckKey)
            .WithEndpoint(targetPort: port, name: ExternalHttpEndpointResource.PrimaryEndpointName, scheme: "http", isExternal: true, isProxied:false)
            .WithUrlForEndpoint(ExternalHttpEndpointResource.PrimaryEndpointName, (EndpointReference _) => new ResourceUrlAnnotation { Url = "http://gogl.com" })
            .WithUrl($"http://localhost{port.ToString()}")
            .OnInitializeResource( (builder2, @event, arg3) =>
            {
                return Task.FromResult(builder2);
            })
            .OnResourceReady(async (resource, evt, cancellationToken) =>
            {
                var t = resource.Annotations;
                var e = resource.GetEndpoints();
            });
    }
    
    /// <summary>
    /// Adds a latency toxic to a specific proxy.
    /// </summary>
    /// <param name="builder">The <see cref="ToxicHttpEndPointResource"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="latency">time in milliseconds.</param>
    /// <param name="jitter">time in milliseconds.</param>
    /// <param name="toxicity">probability of the toxic being applied to a link (defaults to 1.0, 100%).</param>
    /// <param name="direction">link direction to affect (defaults to downstream).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{ToxicHttpEndPointResource}"/>.</returns>
    public static IResourceBuilder<ToxicHttpEndPointResource> AddLatency(this IResourceBuilder<ToxicHttpEndPointResource> builder, [ResourceName] string name, int latency, int jitter = 0, double toxicity = 1.0, Direction direction = Direction.Downstream)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        
        var toxic = new Toxic(ToxicType.Latency, new Parameters(latency, jitter), direction, toxicity);

        var toxi = new ToxiResource(name, toxic, builder.Resource);
        builder.Resource.AddToxic(toxi);
        
        return builder;
    }

    /// <summary>
    /// Adds a bandwidth toxic to a specific proxy.
    /// </summary>
    /// <param name="builder">The <see cref="ToxicHttpEndPointResource"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="bandwidth">Bandwidth limit in in KB/s.</param>
    /// <param name="toxicity">probability of the toxic being applied to a link (defaults to 1.0, 100%).</param>
    /// <param name="direction">link direction to affect (defaults to downstream)</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{ToxicHttpEndPointResource}"/>.</returns>
    public static IResourceBuilder<ToxicHttpEndPointResource> AddBandwidthLimit(this IResourceBuilder<ToxicHttpEndPointResource> builder, [ResourceName] string name, int bandwidth, double toxicity = 1.0, Direction direction = Direction.Downstream)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        
        var toxic = new Toxic(ToxicType.Bandwidth, new Parameters(Bandwidth: bandwidth), direction, toxicity);

        var toxi = new ToxiResource(name, toxic, builder.Resource);
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
                "toxi-ui", "../toxi-ui")
            .WithHttpEndpoint(targetPort: 3000)
            .WithEnvironment(i =>
            {
                i.EnvironmentVariables.Add("TOXIPROXY_URL", builder.Resource.GetEndpoint("http"));
            });
        return builder;
    }
    
    public static IResourceBuilder<TDestination> WithReference<TDestination>(this IResourceBuilder<TDestination> builder, IResourceBuilder<ToxicHttpEndPointResource> endpointReference)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(endpointReference);

        builder.WithEnvironment(async context =>
        {
            context.EnvironmentVariables[$"services__{endpointReference.Resource.ProxiedService}__http__0"] = "http://localhost:8666";
            context.EnvironmentVariables["discoveryEnvVarName"] = "jos";
        });
        return builder;
    }
}