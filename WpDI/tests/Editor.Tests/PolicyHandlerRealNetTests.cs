using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Editor.Abstractions;
using Editor.WordPress.Http;
using Xunit;

public class PolicyHandlerRealNetTests
{
    private static HttpClient RealClient(IRetryPolicy policy, TimeSpan? perRequestTimeout = null, string? baseUrl = null)
    {
        // Real primary: SocketsHttpHandler via HttpClientHandler
        var primary = new HttpClientHandler();
        var policyHandler = new PolicyHandler(policy, perRequestTimeout) { InnerHandler = primary };

        // NOTE: baseUrl is optional because some tests use absolute URIs.
        var http = new HttpClient(policyHandler);
        if (!string.IsNullOrEmpty(baseUrl)) http.BaseAddress = new Uri(baseUrl);
        return http;
    }

    [Fact]
    public async Task RefusedConnection_Retries_ThenThrows_HttpRequestException_Fast()
    {
        // Port 9 (discard) is typically closed locally -> immediate "connection refused".
        // Keep backoff tiny so the test is fast.
        var policy = new ProdRetryPolicy(maxAttempts: 2, baseDelay: TimeSpan.FromMilliseconds(25));
        using var http = RealClient(policy, perRequestTimeout: TimeSpan.FromMilliseconds(300));

        var ex = await Record.ExceptionAsync(() => http.GetAsync("http://127.0.0.1:9/"));
        Assert.IsType<HttpRequestException>(ex);
    }

    [Fact]
    public async Task DnsFailure_Throws_HttpRequestException_Fast()
    {
        // .invalid is guaranteed to never resolve by RFC 2606
        var policy = new NoRetryPolicy(); // single attempt keeps it snappy
        using var http = RealClient(policy, perRequestTimeout: TimeSpan.FromMilliseconds(300));

        var ex = await Record.ExceptionAsync(() => http.GetAsync("http://does-not-exist.invalid/"));
        Assert.IsType<HttpRequestException>(ex);
    }

    [Fact]
    public async Task UnroutableIp_With_TinyTimeout_Produces_TimeoutError()
    {
        // 10.255.255.1 is commonly non-routable in private nets and will hang until timeout.
        var policy = new NoRetryPolicy();
        using var http = RealClient(policy, perRequestTimeout: TimeSpan.FromMilliseconds(200));

        var ex = await Record.ExceptionAsync(() => http.GetAsync("http://10.255.255.1/"));
        Assert.IsType<TimeoutError>(ex);
    }
}
