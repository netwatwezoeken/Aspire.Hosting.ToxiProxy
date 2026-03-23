namespace Aspire.Hosting.ToxiProxy;

public record Parameters(
    int? Latency = null,
    int? Jitter = null,
    int? Bandwidth = null
);
