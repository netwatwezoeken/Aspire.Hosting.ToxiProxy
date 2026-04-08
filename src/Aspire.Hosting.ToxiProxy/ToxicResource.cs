using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ToxiProxy;

public class ToxicResource
    : Resource, IResourceWithParent<ToxicEndpointResource>, IResourceWithEndpoints, IResourceWithWaitSupport
{
    public ToxicResource(string name, Toxic toxic, ToxicEndpointResource parent) : base(name)
    {
        Parent = parent ?? throw new ArgumentNullException(nameof(parent));
        Toxic = toxic;
    }

    public Toxic Toxic { get ; set ; }

    public ToxicEndpointResource Parent { get; }
}
