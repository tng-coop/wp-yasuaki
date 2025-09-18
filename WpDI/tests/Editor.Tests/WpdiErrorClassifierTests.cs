using System.Net;
using System.Net.Http;
using Editor.Abstractions;
using Xunit;

public class WpdiErrorClassifierTests
{
    [Fact]
    public void Classify_AuthError_401_Unauthorized()
    {
        var kind = WpdiErrorClassifier.Classify(new AuthError(HttpStatusCode.Unauthorized));
        Assert.Equal(WpdiErrorKind.Unauthorized, kind);
    }

    [Fact]
    public void Classify_AuthError_403_Forbidden()
    {
        var kind = WpdiErrorClassifier.Classify(new AuthError(HttpStatusCode.Forbidden));
        Assert.Equal(WpdiErrorKind.Forbidden, kind);
    }

    [Fact]
    public void Classify_ConflictError()
    {
        var kind = WpdiErrorClassifier.Classify(new ConflictError(null, "\"etag\""));
        Assert.Equal(WpdiErrorKind.Conflict, kind);
    }

    [Fact]
    public void Classify_RateLimited()
    {
        var kind = WpdiErrorClassifier.Classify(new RateLimited(TimeSpan.FromSeconds(3)));
        Assert.Equal(WpdiErrorKind.RateLimited, kind);
    }

    [Fact]
    public void Classify_Timeout()
    {
        var kind = WpdiErrorClassifier.Classify(new TimeoutError());
        Assert.Equal(WpdiErrorKind.Timeout, kind);
    }

    [Fact]
    public void Classify_ServerError_From_HttpRequestException()
    {
        var kind = WpdiErrorClassifier.Classify(new HttpRequestException("boom"));
        Assert.Equal(WpdiErrorKind.ServerError, kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound,  WpdiErrorKind.NotFound)]
    [InlineData(HttpStatusCode.Gone,      WpdiErrorKind.Gone)]
    [InlineData(HttpStatusCode.BadRequest, WpdiErrorKind.ClientError)]
    public void Classify_From_Response_4xx_PassThrough(HttpStatusCode code, WpdiErrorKind expected)
    {
        using var resp = new HttpResponseMessage(code);
        var kind = WpdiErrorClassifier.Classify(new Exception(), resp);
        Assert.Equal(expected, kind);
    }

    [Fact]
    public void Classify_Unknown_Fallback()
    {
        var kind = WpdiErrorClassifier.Classify(new Exception("weird"));
        Assert.Equal(WpdiErrorKind.Unknown, kind);
    }
}
