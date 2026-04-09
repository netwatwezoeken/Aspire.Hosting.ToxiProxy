using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ToxiProxy;

public class ToxicConnectionStringResource
    : ToxicEndpointResource, IResourceWithConnectionString
{
    public ToxicConnectionStringResource(string name, ToxiProxyResource parent, int port, IResourceBuilder<IResourceWithConnectionString> targetResource) : base(name)
    {
        Parent = parent ?? throw new ArgumentNullException(nameof(parent));
        Port = port;
        ProxiedService = targetResource.Resource.Name;
        TargetResource = targetResource;
    }

    public IResourceBuilder<IResourceWithConnectionString> TargetResource { get ; set ; }

    public string ProxiedService { get ; set ; }

    public int TargetPort { get ; set ; }
    
    private ReferenceExpression? _connectionStringExpression;
    
    public ReferenceExpression ConnectionStringExpression
    {
        get => _connectionStringExpression ?? TargetResource.Resource.ConnectionStringExpression;
        set => _connectionStringExpression = value;
    }
    
    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        if (_connectionStringExpression != null)
        {
            return _connectionStringExpression.GetValueAsync(cancellationToken);
        }

        return ValueTask.FromResult<string?>(null);
    }
}