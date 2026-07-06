namespace Aspire.Hosting.ToxiProxy;

/// <summary>
/// Pure rewriting of a target resource's connection string so that it points at the
/// ToxiProxy listen port instead of the original upstream port. Contains no HTTP calls
/// and no endpoint-allocation logic, so it can be unit-tested without booting a container.
/// </summary>
internal static class ConnectionStringRewriter
{
    /// <summary>
    /// Rewrites <paramref name="original"/> by swapping the upstream <paramref name="targetPort"/>
    /// for the ToxiProxy <paramref name="proxyPort"/>.
    /// </summary>
    /// <param name="original">The original connection string produced by the proxied resource.</param>
    /// <param name="targetPort">The upstream port the resource currently listens on.</param>
    /// <param name="proxyPort">The port the ToxiProxy proxy listens on.</param>
    /// <returns>The rewritten connection string.</returns>
    internal static string Rewrite(string original, int targetPort, int proxyPort)
    {
        ArgumentNullException.ThrowIfNull(original);
        return original.Replace($"{targetPort};", $"{proxyPort};");
    }
}
