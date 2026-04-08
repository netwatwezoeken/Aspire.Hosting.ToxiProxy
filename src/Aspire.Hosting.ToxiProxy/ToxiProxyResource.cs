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

    internal IReadOnlyList<ToxicHttpEndpointResource> HttpEndPointResources => _httpEndPointResources;
    internal IReadOnlyList<ToxicConnectionStringResource> ConnectionStringResources => _connectionsStringResources;
    
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
}