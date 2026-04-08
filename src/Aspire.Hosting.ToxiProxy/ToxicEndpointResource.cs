using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ToxiProxy;

public abstract class ToxicEndpointResource(string name) : 
    Resource(name), IResourceWithParent<ToxiProxyResource>
{
    public ToxiProxyResource Parent { get; protected set; }
    
    public int Port { get ; set ; }
    
    private readonly List<ToxicResource> _toxiResources = [];
    
    internal void AddToxic(ToxicResource toxic)
    {
        _toxiResources.Add(toxic);
    }
    
    internal IReadOnlyList<ToxicResource> ToxiResources => _toxiResources;
}
