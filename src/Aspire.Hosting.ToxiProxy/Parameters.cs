namespace Aspire.Hosting.ToxiProxy;

public record Parameters(
    int? Latency = null,
    int? Jitter = null,
    int? Bandwidth = null,
    int? Delay = null,
    int? Timeout = null,
    int? AverageSize = null,  // slicer, bytes
    int? SizeVariation = null, // slicer, bytes
    long? Bytes = null        // limit_data
);
