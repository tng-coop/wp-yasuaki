using System;
using System.Net;

namespace Editor.Abstractions
{
    // Base type for WPDI domain errors
    public abstract class WpdiException : Exception
    {
        protected WpdiException(string message, Exception? inner = null) : base(message, inner) { }
    }

    public sealed class TimeoutError : WpdiException
    {
        public TimeoutError(string? message = null, Exception? inner = null)
            : base(message ?? "The request timed out.", inner) { }
    }

    public sealed class TransientHttpError : WpdiException
    {
        public HttpStatusCode? StatusCode { get; }
        public TransientHttpError(string? message = null, HttpStatusCode? statusCode = null, Exception? inner = null)
            : base(message ?? "A transient HTTP error occurred.", inner) => StatusCode = statusCode;
    }

    public sealed class AuthError : WpdiException
    {
        public HttpStatusCode StatusCode { get; }
        public AuthError(HttpStatusCode statusCode, string? message = null, Exception? inner = null)
            : base(message ?? (statusCode == HttpStatusCode.Unauthorized ? "Authentication required." : "Permission denied."), inner)
            => StatusCode = statusCode;
    }

    public sealed class ConflictError : WpdiException
    {
        public string? ClientETag { get; }
        public string? ServerETag { get; }
        public ConflictError(string? clientETag, string? serverETag, string? message = null, Exception? inner = null)
            : base(message ?? "The resource was modified by someone else.", inner)
        { ClientETag = clientETag; ServerETag = serverETag; }
    }

    public sealed class RateLimited : WpdiException
    {
        public TimeSpan? RetryAfter { get; }
        public RateLimited(TimeSpan? retryAfter, string? message = null, Exception? inner = null)
            : base(message ?? "Too many requests.", inner) => RetryAfter = retryAfter;
    }

    public sealed class ParseError : WpdiException
    {
        public string? BodySnippet { get; }
        public ParseError(string? bodySnippet, string? message = null, Exception? inner = null)
            : base(message ?? "Unexpected response format.", inner) => BodySnippet = bodySnippet;
    }

    public sealed class UnexpectedHttpError : WpdiException
    {
        public HttpStatusCode StatusCode { get; }
        public UnexpectedHttpError(HttpStatusCode status, string? message = null, Exception? inner = null)
            : base(message ?? $"HTTP {status}", inner) => StatusCode = status;
    }
}
