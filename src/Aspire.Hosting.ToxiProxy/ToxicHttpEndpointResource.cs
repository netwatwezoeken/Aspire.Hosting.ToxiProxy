using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ToxiProxy;

public class ToxicHttpEndpointResource
    : ToxicEndpointResource, IResourceWithEndpoints
{
    private const string PrimaryEndpointName = "http";
    
    public ToxicHttpEndpointResource(string name, ToxiProxyResource parent, int port, IResourceBuilder<IResourceWithEndpoints> targetResource) :
        this(name, port, targetResource)
    {
        Parent = parent ?? throw new ArgumentNullException(nameof(parent));
    }
    public ToxicHttpEndpointResource(string name, int port, IResourceBuilder<IResourceWithEndpoints> targetResource) : base(name)
    {
        PrimaryEndpoint = new(this, PrimaryEndpointName);
        Port = port;
        ProxiedService = targetResource.Resource.Name;
        TargetResource = targetResource;
    }

    public IResourceBuilder<IResourceWithEndpoints> TargetResource { get ; set ; }

    public string ProxiedService { get ; set ; }

    public EndpointReference PrimaryEndpoint { get; }
}