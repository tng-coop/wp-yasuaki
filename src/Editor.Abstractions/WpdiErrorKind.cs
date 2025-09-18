namespace Editor.Abstractions;

/// High-level, UI-friendly buckets for outcomes.
/// Covers known errors and leaves room for safe fallbacks.
public enum WpdiErrorKind
{
    None = 0,       // success / not an error
    Unauthorized,   // 401
    Forbidden,      // 403
    Conflict,       // 409 / 412
    RateLimited,    // 429 (Retry-After honored upstream)
    Timeout,        // request timed out
    ServerError,    // 5xx or transport reset (HttpRequestException)
    NotFound,       // 404
    Gone,           // 410
    ClientError,    // any other 4xx we didn’t special-case
    Unknown         // everything else (defensive fallback)
}
