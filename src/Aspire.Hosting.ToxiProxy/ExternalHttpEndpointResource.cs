using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ToxiProxy;

/// <summary>
/// A resource that represents an external HTTP endpoint.
/// </summary>
public class ExternalHttpEndpointResource : Resource, IResourceWithEndpoints
{
    internal const string PrimaryEndpointName = "http";

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalHttpEndpointResource"/> class.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    public ExternalHttpEndpointResource(string name) : base(name)
    {
        PrimaryEndpoint = new EndpointReference(this, PrimaryEndpointName);
    }

    /// <summary>
    /// Gets the primary endpoint for the HTTP resource.
    /// </summary>
    public EndpointReference PrimaryEndpoint { get; }
}
