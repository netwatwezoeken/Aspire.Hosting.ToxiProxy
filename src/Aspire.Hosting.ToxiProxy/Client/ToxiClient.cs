using System.Text.Json;
using System.Text.Json.Serialization;
using Refit;

namespace Aspire.Hosting.ToxiProxy.Client;

public interface IToxiClient
{
    [Post("/proxies")]
    Task CreateProxy([Body] Proxy proxy);
    
    [Get("/proxies")]
    Task<ProxiesResponse> GetProxies();

    [Post("/proxies/{proxyName}/toxics")]
    Task AddToxic([Body] Toxic toxic, string proxyName);
}

public record Proxy(
    string name,
    bool enabled,
    string listen,
    string upstream
);

public record Toxic(
    [property: JsonPropertyName("attributes")]
    Attributes Attributes,
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("type")]
    ToxicType Type,
    [property: JsonPropertyName("stream")]
    Stream Stream,
    [property: JsonPropertyName("toxicity")]
    double Toxicity
);

public enum ToxicType
{
    [JsonStringEnumMemberName("latency")]
    Latency,
    [JsonStringEnumMemberName("bandwidth")]
    Bandwidth,
    [JsonStringEnumMemberName("slow_close")]
    SlowClose,
    [JsonStringEnumMemberName("timeout")]
    Timeout,
    [JsonStringEnumMemberName("reset_peer")]
    ResetPeer,
    [JsonStringEnumMemberName("slicer")]
    Slicer,
    [JsonStringEnumMemberName("limit_data")]
    LimitData
}

public record Attributes(
    int? Latency = null,
    int? Jitter = null,
    int? Rate = null
);

public enum Stream
{   
    [JsonStringEnumMemberName("upstream")]
    Upstream,
    [JsonStringEnumMemberName("downstream")]
    Downstream
}

public sealed class ProxiesResponse
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Raw { get; init; } = new();

    public Dictionary<string, ProxyResponse> ToProxies(JsonSerializerOptions? options = null)
        => Raw.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Deserialize<ProxyResponse>(options ?? new JsonSerializerOptions())!
        );
}

public record ProxyResponse(
    string name,
    string listen,
    string upstream,
    bool enabled
) : Proxy (name, enabled, listen, upstream);


