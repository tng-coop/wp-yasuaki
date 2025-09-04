using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Xunit;

public class AdminAuthTests
{
    private static HttpClient NewClient(string baseUrl, bool allowInsecure = true)
    {
        if (allowInsecure && baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            return new HttpClient(handler) { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(15) };
        }

        return new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(15) };
    }

    private static void SetBasicAuth(HttpClient http, string user, string pass)
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    private static (string BaseUrl, string User, string Pass) RequireEnv()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WP_BASE_URL");
        var user    = Environment.GetEnvironmentVariable("WP_USERNAME");
        var pass    = Environment.GetEnvironmentVariable("WP_APP_PASSWORD");

        Assert.False(string.IsNullOrWhiteSpace(baseUrl), "WP_BASE_URL is not set.");
        Assert.False(string.IsNullOrWhiteSpace(user),    "WP_USERNAME is not set.");
        Assert.False(string.IsNullOrWhiteSpace(pass),    "WP_APP_PASSWORD is not set.");

        return (baseUrl!, user!, pass!);
    }

    [Fact]
    public async Task Api_Root_Is_Reachable()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WP_BASE_URL");
        Assert.False(string.IsNullOrWhiteSpace(baseUrl), "WP_BASE_URL is not set.");

        using var http = NewClient(baseUrl!);
        var resp = await http.GetAsync("/wp-json/");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Settings_Is_Protected_With_Bad_Creds()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WP_BASE_URL");
        Assert.False(string.IsNullOrWhiteSpace(baseUrl), "WP_BASE_URL is not set.");

        using var http = NewClient(baseUrl!);
        SetBasicAuth(http, "admin", "DefinitelyWrongPassword123!");

        var resp = await http.GetAsync("/wp-json/wp/v2/settings");
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Settings_Allows_Admin_With_Correct_AppPassword()
    {
        var (baseUrl, user, pass) = RequireEnv();

        var masked = pass.Length > 4 ? pass[..4] + "****" : "(too short)";
        Console.WriteLine($"Using WP_USERNAME={user}, WP_APP_PASSWORD starts with '{masked}'");

        using var http = NewClient(baseUrl);
        SetBasicAuth(http, user, pass);

        var resp = await http.GetAsync("/wp-json/wp/v2/settings");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await resp.Content.ReadAsStringAsync();
        json.Should().Contain("\"title\"");   // settings payload contains site settings like title, etc.
    }

    [Fact]
    public async Task Me_Endpoint_Returns_Current_User_When_Authed()
    {
        var (baseUrl, user, pass) = RequireEnv();

        using var http = NewClient(baseUrl);
        SetBasicAuth(http, user, pass);

        var resp = await http.GetAsync("/wp-json/wp/v2/users/me");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("\"id\"");
        body.Should().Contain("\"name\"");
    }
}

