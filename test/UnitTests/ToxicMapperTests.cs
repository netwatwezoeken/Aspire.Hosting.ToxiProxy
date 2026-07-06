using System.Text.Json;
using Aspire.Hosting.ToxiProxy.Client;

namespace Aspire.Hosting.ToxiProxy.UnitTests;

public class ToxicMapperTests
{
    private static ToxicResource CreateToxic(string name, Toxic toxic) =>
        new(name, toxic, new TestToxicEndpointResource("proxy"));

    private static string MapAndSerialize(string name, Toxic toxic)
    {
        var mapped = ToxicMapper.MapToxic(CreateToxic(name, toxic));
        return JsonSerializer.Serialize(mapped, ToxiClientSettings.JsonSerializerOptions);
    }

    [Fact]
    public Task MapToxic_Latency_with_latency_and_jitter()
    {
        var json = MapAndSerialize(
            "latency",
            new Toxic(ToxicType.Latency, new Parameters(Latency: 123, Jitter: 7), Direction.Downstream, 0.8));

        return VerifyJson(json);
    }

    [Fact]
    public Task MapToxic_Latency_with_only_jitter()
    {
        var json = MapAndSerialize(
            "latency",
            new Toxic(ToxicType.Latency, new Parameters(Jitter: 7), Direction.Downstream, 0.8));

        return VerifyJson(json);
    }

    [Fact]
    public Task MapToxic_Latency_with_only_latency()
    {
        var json = MapAndSerialize(
            "latency",
            new Toxic(ToxicType.Latency, new Parameters(Latency: 123), Direction.Downstream, 0.8));

        return VerifyJson(json);
    }

    [Fact]
    public Task MapToxic_Bandwidth()
    {
        var json = MapAndSerialize(
            "bandwidth",
            new Toxic(ToxicType.Bandwidth, new Parameters(Bandwidth: 142), Direction.Upstream, 0.9));

        return VerifyJson(json);
    }

    [Fact]
    public Task MapToxic_SlowClose()
    {
        var json = MapAndSerialize(
            "slowClose",
            new Toxic(ToxicType.SlowClose, new Parameters(Delay: 500), Direction.Downstream, 1.0));

        return VerifyJson(json);
    }

    [Fact]
    public Task MapToxic_Timeout()
    {
        var json = MapAndSerialize(
            "timeout",
            new Toxic(ToxicType.Timeout, new Parameters(Timeout: 2500), Direction.Downstream, 0.9));

        return VerifyJson(json);
    }

    [Fact]
    public Task MapToxic_ResetPeer()
    {
        var json = MapAndSerialize(
            "resetPeer",
            new Toxic(ToxicType.ResetPeer, new Parameters(Timeout: 1500), Direction.Upstream, 0.9));

        return VerifyJson(json);
    }
}
