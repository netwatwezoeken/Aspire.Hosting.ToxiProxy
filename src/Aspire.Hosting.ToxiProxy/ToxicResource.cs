using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ToxiProxy;

public class ToxicResource
    : Resource, IResourceWithParent<ToxicHttpEndPointResource>, IResourceWithEndpoints, IResourceWithWaitSupport
{
    public ToxicResource(string name, Toxic toxic, ToxicHttpEndPointResource parent) : base(name)
    {
        Parent = parent ?? throw new ArgumentNullException(nameof(parent));
        Toxic = toxic;
    }

    public Toxic Toxic { get ; set ; }

    public ToxicHttpEndPointResource Parent { get; }
}
