using System.Net;

namespace Editor.WordPress;

public sealed class WordPressApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string Body { get; }
    public WordPressApiException(HttpStatusCode status, string body)
        : base($"WordPress API error {(int)status}") { StatusCode = status; Body = body; }
}
