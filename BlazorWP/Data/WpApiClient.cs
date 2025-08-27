using System.Net.Http;
using System.Net.Http.Json;
using WordPressPCL.Models;

namespace BlazorWP;

public interface IWordPressApi
{
    Task<Post?> GetPostAsync(int id, CancellationToken ct = default);
    // add only what your UI needs
}

public sealed class WpApiClient : IWordPressApi
{
    private readonly HttpClient _http;
    private readonly AuthState _auth;

    public WpApiClient(HttpClient http, AuthState auth)
    {
        _http = http;
        _auth = auth;
    }

    public async Task<Post?> GetPostAsync(int id, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"wp/v2/posts/{id}");
        var token = await _auth.GetAccessTokenAsync(ct);
        if (!string.IsNullOrWhiteSpace(token))
            req.Headers.Authorization = new("Bearer", token);

        var nonce = await _auth.GetNonceAsync(ct);
        if (!string.IsNullOrWhiteSpace(nonce))
            req.Headers.Add("X-WP-Nonce", nonce);

        var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<Post>(cancellationToken: ct);
    }
}

