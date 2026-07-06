using Aspire.Hosting.ToxiProxy.Client;
using Stream = Aspire.Hosting.ToxiProxy.Client.Stream;

namespace Aspire.Hosting.ToxiProxy;

/// <summary>
/// Pure mapping from the domain resource model to the ToxiProxy client payloads
/// (<see cref="Client.Proxy"/> / <see cref="Client.Toxic"/>). Contains no HTTP calls
/// and no endpoint-allocation logic, so it can be unit-tested without booting a container.
/// </summary>
internal static class ToxicMapper
{
    /// <summary>
    /// Builds the <see cref="Client.Proxy"/> payload for a proxy that listens on
    /// <paramref name="listenPort"/> and forwards to <paramref name="upstream"/>.
    /// </summary>
    internal static Proxy BuildProxy(string name, int listenPort, string upstream) =>
        new(
            name,
            true,
            $"0.0.0.0:{listenPort}",
            upstream
        );

    /// <summary>
    /// Maps every <see cref="ToxicResource"/> attached to a proxy to its client payload,
    /// preserving order.
    /// </summary>
    internal static IReadOnlyList<Client.Toxic> MapToxics(IEnumerable<ToxicResource> toxics)
    {
        ArgumentNullException.ThrowIfNull(toxics);
        return toxics.Select(MapToxic).ToList();
    }

    /// <summary>
    /// Maps a single domain <see cref="ToxicResource"/> to its ToxiProxy client payload.
    /// Throws for toxic types that are not (yet) supported, so unmapped types fail fast
    /// instead of being silently ignored.
    /// </summary>
    internal static Client.Toxic MapToxic(ToxicResource toxicResource)
    {
        ArgumentNullException.ThrowIfNull(toxicResource);

        var toxic = toxicResource.Toxic;
        var stream = toxic.Direction == Direction.Downstream ? Stream.Downstream : Stream.Upstream;

        var attributes = toxic.Type switch
        {
            ToxicType.Latency => new Attributes(
                Latency: toxic.Parameters.Latency,
                Jitter: toxic.Parameters.Jitter),
            ToxicType.Bandwidth => new Attributes(
                Rate: toxic.Parameters.Bandwidth),
            ToxicType.SlowClose => new Attributes(
                Delay: toxic.Parameters.Delay),
            ToxicType.Timeout => new Attributes(
                Timeout: toxic.Parameters.Timeout),
            ToxicType.ResetPeer => new Attributes(
                Timeout: toxic.Parameters.Timeout),
            ToxicType.Slicer => new Attributes(
                AverageSize: toxic.Parameters.AverageSize,
                SizeVariation: toxic.Parameters.SizeVariation,
                Delay: toxic.Parameters.Delay),
            _ => throw new DistributedApplicationException(
                $"Unsupported toxic type '{toxic.Type}' for toxic '{toxicResource.Name}'.")
        };

        return new Client.Toxic(
            attributes,
            toxicResource.Name,
            toxic.Type,
            stream,
            toxic.Toxicity
        );
    }
}
