namespace Aspire.Hosting.ToxiProxy.UnitTests;

public class ConnectionStringRewriterTests
{
    [Fact]
    public void Rewrite_Mssql_with_database_swaps_only_the_port()
    {
        const string original =
            "Server=localhost,65432;User ID=sa;Password=pw;TrustServerCertificate=true;Database=SqlDatabase;";

        var result = ConnectionStringRewriter.Rewrite(original, targetPort: 65432, proxyPort: 8668);

        Assert.Equal(
            "Server=localhost,8668;User ID=sa;Password=pw;TrustServerCertificate=true;Database=SqlDatabase;",
            result);
    }

    [Fact]
    public void Rewrite_Mssql_leaves_string_unchanged_when_target_port_absent()
    {
        const string original =
            "Server=127.0.0.1,1433;User ID=sa;Password=pw;TrustServerCertificate=true;";

        var result = ConnectionStringRewriter.Rewrite(original, targetPort: 65432, proxyPort: 8668);

        Assert.Equal(original, result);
    }
}
