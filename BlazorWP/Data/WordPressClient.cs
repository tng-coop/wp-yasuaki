using System.Net.Http.Json;
using WordPressPCL.Models;

namespace BlazorWP;

public interface IWordPressClient
{
    Task<Post?> GetPostAsync(int id, CancellationToken ct = default);
}

public sealed class WordPressClient : IWordPressClient
{
    private readonly HttpClient _http;

    public WordPressClient(HttpClient http, AuthState auth)
    {
        _http = http;
    }

    public Task<Post?> GetPostAsync(int id, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<Post>($"wp/v2/posts/{id}", cancellationToken: ct);
}
