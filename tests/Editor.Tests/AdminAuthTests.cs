// WpDI/tests/Editor.Tests/AdminAuthTests.cs
using System.Net;
using Microsoft.Extensions.Options;
using Xunit;
using Editor.WordPress; // WordPressApiService, WordPressOptions

[Collection("WP EndToEnd")]
public class AdminAuthTests
{
    private static WordPressApiService NewApi()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WP_BASE_URL");
        var user    = Environment.GetEnvironmentVariable("WP_USERNAME");
        var pass    = Environment.GetEnvironmentVariable("WP_APP_PASSWORD");

        Assert.False(string.IsNullOrWhiteSpace(baseUrl), "WP_BASE_URL is not set.");
        Assert.False(string.IsNullOrWhiteSpace(user),    "WP_USERNAME is not set.");
        Assert.False(string.IsNullOrWhiteSpace(pass),    "WP_APP_PASSWORD is not set.");

        var opts = Options.Create(new WordPressOptions
        {
            BaseUrl     = baseUrl!,
            UserName    = user!,
            AppPassword = pass!,
            Timeout     = TimeSpan.FromSeconds(15)
        });
        return new WordPressApiService(opts);
    }

    [Fact]
    public async Task Api_Root_Is_Reachable()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WP_BASE_URL");
        Assert.False(string.IsNullOrWhiteSpace(baseUrl), "WP_BASE_URL is not set.");

        // unauthenticated client
        using var http = new HttpClient { BaseAddress = new Uri(baseUrl!) };
        var resp = await http.GetAsync("/wp-json/wp/v2/settings/");
        Assert.Contains(resp.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
    }

    [Fact]
    public async Task Settings_Is_Protected_With_Bad_Creds()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WP_BASE_URL");
        Assert.False(string.IsNullOrWhiteSpace(baseUrl), "WP_BASE_URL is not set.");

        using var http = new HttpClient { BaseAddress = new Uri(baseUrl!) };
        // supply intentionally wrong basic auth header
        var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("admin:DefinitelyWrongPassword123!"));
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);

        var resp = await http.GetAsync("/wp-json/wp/v2/settings");
        Assert.Contains(resp.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
    }

    [Fact]
    public async Task Settings_Allows_Admin_With_Correct_AppPassword()
    {
        var api  = NewApi();
        var http = api.HttpClient!;

        var pass = Environment.GetEnvironmentVariable("WP_APP_PASSWORD") ?? "";
        var user = Environment.GetEnvironmentVariable("WP_USERNAME") ?? "";
        var masked = pass.Length > 4 ? pass[..4] + "****" : "(too short)";
        Console.WriteLine($"Using WP_USERNAME={user}, WP_APP_PASSWORD starts with '{masked}'");

        var resp = await http.GetAsync("/wp-json/wp/v2/settings/");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"title\"", json);
    }

    [Fact]
    public async Task Me_Endpoint_Returns_Current_User_When_Authed()
    {
        var api  = NewApi();
        var http = api.HttpClient!;

        var resp = await http.GetAsync("/wp-json/wp/v2/users/me/");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"id\"", body);
        Assert.Contains("\"name\"", body);
    }
}
