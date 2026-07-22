using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ToxiProxy;

/// <summary>
/// An Aspire resource that represents a ToxiProxy proxy in front of an external
/// service declared with <see cref="IDistributedApplicationBuilder.AddExternalService"/>.
/// </summary>
/// <remarks>
/// <para>
/// The upstream for this proxy is derived from the static <see cref="Uri"/> of the
/// wrapped <see cref="ExternalServiceResource"/>. Parameterised external services
/// (those constructed from a <c>ParameterResource</c> URL) are not supported; passing
/// one will throw an <see cref="InvalidOperationException"/> at construction time.
/// </para>
/// <para>
/// <c>IResourceBuilder&lt;ExternalServiceResource&gt;</c> is typed concretely because
/// Aspire does not currently define a shared <c>IResourceWithUri</c> interface.
/// </para>
/// </remarks>
public class ToxicExternalServiceResource
    : ToxicEndpointResource, IResourceWithEndpoints, IResourceWithWaitSupport
{
    private const string PrimaryEndpointName = "http";

    /// <summary>
    /// Initialises a new <see cref="ToxicExternalServiceResource"/>.
    /// </summary>
    /// <param name="name">The name of the proxy resource.</param>
    /// <param name="parent">The <see cref="ToxiProxyResource"/> that owns this proxy.</param>
    /// <param name="port">The host port the ToxiProxy listener will bind to.</param>
    /// <param name="targetResource">
    /// The external service to proxy. Its <see cref="ExternalServiceResource.Uri"/> must
    /// be a static (non-parameterised) URI.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="targetResource"/> has no static URI configured.
    /// </exception>
    public ToxicExternalServiceResource(
        string name,
        ToxiProxyResource parent,
        int port,
        IResourceBuilder<ExternalServiceResource> targetResource)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(targetResource);

        if (targetResource.Resource.Uri is null)
            throw new InvalidOperationException(
                $"ExternalServiceResource '{targetResource.Resource.Name}' has no static URI. " +
                $"Only external services created with a static URI are supported by AddExternalServiceProxy.");

        Parent = parent;
        Port = port;
        TargetResource = targetResource;
        PrimaryEndpoint = new EndpointReference(this, PrimaryEndpointName);
    }

    /// <summary>The builder for the wrapped external service.</summary>
    public IResourceBuilder<ExternalServiceResource> TargetResource { get; }

    /// <summary>
    /// The static URI of the wrapped external service.
    /// Guaranteed non-null: validated at construction time.
    /// </summary>
    public Uri TargetUri => TargetResource.Resource.Uri!;

    /// <summary>The primary HTTP endpoint reference for this proxy.</summary>
    public EndpointReference PrimaryEndpoint { get; }
}
