namespace Aspire.Hosting.ToxiProxy.UnitTests;

/// <summary>
/// Minimal concrete <see cref="ToxicEndpointResource"/> used only by unit tests so that
/// <see cref="ToxicResource"/> instances can be created (and mapped) without constructing
/// an <see cref="Aspire.Hosting.ApplicationModel.IResourceBuilder{T}"/> or booting a host.
/// </summary>
internal sealed class TestToxicEndpointResource(string name) : ToxicEndpointResource(name);
