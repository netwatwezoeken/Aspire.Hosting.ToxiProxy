using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ToxiProxy;

public class ToxiProxyResource : ContainerResource
{
    private const string PrimaryEndpointName = "http";

    public ToxiProxyResource(string name) : base(name)
    {
        PrimaryEndpoint = new(this, PrimaryEndpointName);
    }

    public EndpointReference PrimaryEndpoint { get; }

    private readonly List<ToxicHttpEndpointResource> _httpEndPointResources = [];
    private readonly List<ToxicConnectionStringResource> _connectionsStringResources = [];
    private readonly List<ToxicExternalServiceResource> _externalServiceResources = [];

    internal IReadOnlyList<ToxicHttpEndpointResource> HttpEndPointResources => _httpEndPointResources;
    internal IReadOnlyList<ToxicConnectionStringResource> ConnectionStringResources => _connectionsStringResources;
    internal IReadOnlyList<ToxicExternalServiceResource> ExternalServiceResources => _externalServiceResources;

    internal void AddHttpProxy(ToxicHttpEndpointResource toxicHttpEndpoint)
    {
        _httpEndPointResources.Add(toxicHttpEndpoint);

        // Add the endpoint to ToxiProxyResource so it appears in the dashboard
        Annotations.Add(new EndpointAnnotation(
            System.Net.Sockets.ProtocolType.Tcp,
            uriScheme: "http",
            name: toxicHttpEndpoint.Name,
            port: toxicHttpEndpoint.Port,
            targetPort: toxicHttpEndpoint.Port));
    }

    internal void AddConnectionStringProxy(ToxicConnectionStringResource toxicConnectionString)
    {
        _connectionsStringResources.Add(toxicConnectionString);
        // Add the endpoint to ToxiProxyResource so it appears in the dashboard
        
        Annotations.Add(new EndpointAnnotation(
            System.Net.Sockets.ProtocolType.Tcp,
            uriScheme: "tcp",
            name: toxicConnectionString.Name,
            port: toxicConnectionString.Port,
            targetPort: toxicConnectionString.Port));
    }

    /// <summary>
    /// Registers a <see cref="ToxicExternalServiceResource"/> on this ToxiProxy container
    /// and adds an HTTP endpoint annotation so the proxy port appears in the Aspire dashboard.
    /// </summary>
    internal void AddExternalServiceProxy(ToxicExternalServiceResource toxicExternalService)
    {
        _externalServiceResources.Add(toxicExternalService);
        // Add the endpoint to ToxiProxyResource so it appears in the dashboard
        Annotations.Add(new EndpointAnnotation(
            System.Net.Sockets.ProtocolType.Tcp,
            uriScheme: "http",
            name: toxicExternalService.Name,
            port: toxicExternalService.Port,
            targetPort: toxicExternalService.Port));
    }
}