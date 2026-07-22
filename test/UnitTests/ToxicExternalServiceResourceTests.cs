using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ToxiProxy.UnitTests;

public class ToxicExternalServiceResourceTests
{
    // ── helpers ─────────────────────────────────────────────────────────────

    private static IResourceBuilder<ExternalServiceResource> BuilderFor(ExternalServiceResource resource)
        => new StubExternalServiceBuilder(resource);

    // ── Scenario: Resource captures the external service URI ─────────────────

    [Fact]
    public void TargetUri_returns_uri_from_external_resource()
    {
        var uri      = new Uri("http://api.weather.com:80");
        var external = new ExternalServiceResource("weather-api", uri);
        var parent   = new ToxiProxyResource("toxi");

        var sut = new ToxicExternalServiceResource("weatherProxy", parent, 8667, BuilderFor(external));

        Assert.Equal("api.weather.com", sut.TargetUri.Host);
        Assert.Equal(80, sut.TargetUri.Port);
        Assert.Equal(8667, sut.Port);
    }

    // ── Scenario: Construction fails when external service has no static URI ─

    [Fact]
    public void Constructor_throws_when_uri_is_null()
    {
        // ExternalServiceResource(name, ParameterResource) leaves Uri = null
        var param    = new ParameterResource("url", _ => "http://localhost", secret: false);
        var external = new ExternalServiceResource("weather-api", param);
        var parent   = new ToxiProxyResource("toxi");

        var ex = Assert.Throws<InvalidOperationException>(
            () => new ToxicExternalServiceResource("weatherProxy", parent, 8667, BuilderFor(external)));

        Assert.Contains("weather-api", ex.Message);
    }

    // ── Scenario: NormalizeHost ───────────────────────────────────────────────

    [Fact]
    public void NormalizeHost_external_host_unchanged()
        => Assert.Equal("api.weather.com",
            ToxiProxyBuilderExtensions.NormalizeHost("api.weather.com"));

    [Fact]
    public void NormalizeHost_localhost_normalised()
        => Assert.Equal("host.docker.internal",
            ToxiProxyBuilderExtensions.NormalizeHost("localhost"));

    [Fact]
    public void NormalizeHost_127_normalised()
        => Assert.Equal("host.docker.internal",
            ToxiProxyBuilderExtensions.NormalizeHost("127.0.0.1"));

    // ── Scenario: AddExternalServiceProxy registers the resource ────────────

    [Fact]
    public void AddExternalServiceProxy_adds_to_collection()
    {
        var parent   = new ToxiProxyResource("toxi");
        var external = new ExternalServiceResource("weather-api", new Uri("http://api.weather.com:80"));
        var resource = new ToxicExternalServiceResource("weatherProxy", parent, 8667, BuilderFor(external));

        parent.AddExternalServiceProxy(resource);

        Assert.Single(parent.ExternalServiceResources);
        Assert.Equal("weatherProxy", parent.ExternalServiceResources[0].Name);
        Assert.Equal(8667, parent.ExternalServiceResources[0].Port);
    }

    // ── stub ─────────────────────────────────────────────────────────────────

    private sealed class StubExternalServiceBuilder(ExternalServiceResource resource)
        : IResourceBuilder<ExternalServiceResource>
    {
        public ExternalServiceResource Resource => resource;

        public IDistributedApplicationBuilder ApplicationBuilder
            => throw new NotImplementedException();

        public IResourceBuilder<ExternalServiceResource> WithAnnotation<TAnnotation>(
            TAnnotation annotation,
            ResourceAnnotationMutationBehavior behavior = ResourceAnnotationMutationBehavior.Append)
            where TAnnotation : IResourceAnnotation
            => throw new NotImplementedException();
    }
}
