namespace Editor.WordPress;

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

/// <summary>
/// Helper utilities for applying WordPress Basic Authentication headers.
/// </summary>
public static class BasicAuth
{
    /// <summary>
    /// Returns true when the given request should not include auth headers.
    /// WordPress rejects credentials on the API root ("/wp-json/wp/v2").
    /// </summary>
    /// <param name="request">The outgoing HTTP request.</param>
    /// <returns>True when auth should be skipped.</returns>
    public static bool ShouldSkip(HttpRequestMessage request)
    {
        var uri = request.RequestUri;
        if (uri == null) return false;
        var path = uri.AbsolutePath.TrimEnd('/');
        return path.EndsWith("/wp-json/wp/v2", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Applies a Basic Authentication header for the given credentials unless <see cref="ShouldSkip"/>.
    /// </summary>
    public static void Apply(HttpRequestMessage request, string username, string appPassword)
    {
        if (ShouldSkip(request)) return;

        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{appPassword}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
    }
}
