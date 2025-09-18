using System.Net;

namespace Editor.Abstractions;

/// Centralized mapping from exceptions (and optional raw response)
/// to a single WpdiErrorKind your UI/tests can switch on.
public static class WpdiErrorClassifier
{
    public static WpdiErrorKind Classify(Exception ex, HttpResponseMessage? response = null)
        => ex switch
        {
            AuthError a          => a.StatusCode == HttpStatusCode.Forbidden
                                      ? WpdiErrorKind.Forbidden
                                      : WpdiErrorKind.Unauthorized,
            ConflictError        => WpdiErrorKind.Conflict,
            RateLimited          => WpdiErrorKind.RateLimited,
            TimeoutError         => WpdiErrorKind.Timeout,
            HttpRequestException => WpdiErrorKind.ServerError, // 5xx/transport
            _ when response is not null => MapFromResponse(response),
            _                           => WpdiErrorKind.Unknown
        };

    private static WpdiErrorKind MapFromResponse(HttpResponseMessage r)
        => r.StatusCode switch
        {
            HttpStatusCode.NotFound  => WpdiErrorKind.NotFound,
            HttpStatusCode.Gone      => WpdiErrorKind.Gone,
            >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError
                                       => WpdiErrorKind.ClientError,
            _                          => WpdiErrorKind.Unknown
        };
}
