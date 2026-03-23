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
    
    private readonly List<ToxicHttpEndPointResource> _httpEndPointResources = [];
    
    internal void AddHttpProxy(ToxicHttpEndPointResource toxicHttpEndpoint)
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

    internal IReadOnlyList<ToxicHttpEndPointResource> HttpEndPointResources => _httpEndPointResources;
}