using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ToxiProxy;

public class ToxicHttpEndPointResource
    : Resource, IResourceWithParent<ToxiProxyResource>, IResourceWithEndpoints, IResourceWithWaitSupport
{
    private const string PrimaryEndpointName = "http";
    
    public ToxicHttpEndPointResource(string name, ToxiProxyResource parent, int port, IResourceBuilder<IResourceWithEndpoints> targetResource) : base(name)
    {
        Parent = parent ?? throw new ArgumentNullException(nameof(parent));
        PrimaryEndpoint = new(this, PrimaryEndpointName);
        Port = port;
        ProxiedService = targetResource.Resource.Name;
        TargetResource = targetResource;
    }

    public IResourceBuilder<IResourceWithEndpoints> TargetResource { get ; set ; }

    public string ProxiedService { get ; set ; }

    public int TargetPort { get ; set ; }

    public int Port { get ; set ; }

    public EndpointReference PrimaryEndpoint { get; }
    
    public ToxiProxyResource Parent { get; }
    
    private readonly List<ToxicResource> _toxiResources = [];
    
    internal void AddToxic(ToxicResource toxic)
    {
        _toxiResources.Add(toxic);
    }
    
    internal IReadOnlyList<ToxicResource> ToxiResources => _toxiResources;
}