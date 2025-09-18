using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Editor.Abstractions;
using Editor.WordPress.Http;
using Xunit;

public class PolicyHandlerContractTests
{
    private sealed class StubPrimary : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Responder = _ => new(HttpStatusCode.OK);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(Responder(request));
    }

    private static HttpClient ClientReturning(HttpStatusCode code)
    {
        var stub = new StubPrimary { Responder = _ => new HttpResponseMessage(code) };
        var policy = new PolicyHandler(new ProdRetryPolicy(maxAttempts: 1)) { InnerHandler = stub };
        return new HttpClient(policy) { BaseAddress = new Uri("https://example.test") };
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task AuthCodes_Throw_AuthError(HttpStatusCode code)
    {
        using var http = ClientReturning(code);
        var ex = await Record.ExceptionAsync(() => http.GetAsync("/wp-json/wp/v2/settings"));
        Assert.IsType<AuthError>(ex);
    }

    [Theory]
    [InlineData(412)]
    [InlineData(409)]
    public async Task ConflictCodes_Throw_ConflictError(int code)
    {
        using var http = ClientReturning((HttpStatusCode)code);
        var ex = await Record.ExceptionAsync(() => http.PutAsync("/x", new StringContent("{}")));
        Assert.IsType<ConflictError>(ex);
    }

    [Fact]
    public async Task RateLimit_Throws_RateLimited()
    {
        var stub = new StubPrimary
        {
            Responder = _ =>
            {
                var r = new HttpResponseMessage((HttpStatusCode)429);
                r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
                return r;
            }
        };
        var policy = new PolicyHandler(new ProdRetryPolicy(maxAttempts: 1)) { InnerHandler = stub };
        using var http = new HttpClient(policy) { BaseAddress = new Uri("https://example.test") };

        var ex = await Record.ExceptionAsync(() => http.GetAsync("/x"));
        Assert.IsType<RateLimited>(ex);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task ServerErrors_Throw_HttpRequestException(HttpStatusCode code)
    {
        using var http = ClientReturning(code);
        var ex = await Record.ExceptionAsync(() => http.GetAsync("/x"));
        Assert.IsType<HttpRequestException>(ex);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Other4xx_PassThrough(HttpStatusCode code)
    {
        using var http = ClientReturning(code);
        using var rsp = await http.GetAsync("/x");
        Assert.Equal(code, rsp.StatusCode);
    }

    [Fact]
    public async Task Timeout_Throws_TimeoutError_NoRetry()
    {
        // Primary that delays until cancelled and DOES NOT swallow the cancellation.
        var never = new HttpMessageHandlerThatDelays();
        var policy = new PolicyHandler(new NoRetryPolicy(), perRequestTimeout: TimeSpan.FromMilliseconds(200))
        { InnerHandler = never };

        using var http = new HttpClient(policy) { BaseAddress = new Uri("https://example.test") };
        var ex = await Record.ExceptionAsync(() => http.GetAsync("/hang"));
        Assert.IsType<TimeoutError>(ex);
        Assert.Equal(1, never.Calls);
    }

    private sealed class HttpMessageHandlerThatDelays : HttpMessageHandler
    {
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            // Let the cancellation throw so PolicyHandler can map it to TimeoutError.
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
