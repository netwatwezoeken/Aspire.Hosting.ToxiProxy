using Aspire.Hosting.ToxiProxy.Client;

namespace Aspire.Hosting.ToxiProxy;

public record Toxic(
    ToxicType Type,
    Parameters Parameters,
    Direction Direction,
    double Toxicity
);
