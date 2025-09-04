using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Xunit;

public class AdminAuthTests
{
    [Fact]
    public async Task Can_List_Users_With_Admin_AppPassword()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WP_BASE_URL");
        var user    = Environment.GetEnvironmentVariable("WP_USERNAME");
        var pass    = Environment.GetEnvironmentVariable("WP_APP_PASSWORD");

        // If not configured, just no-op (keeps CI green without extra packages)
        if (string.IsNullOrWhiteSpace(baseUrl) ||
            string.IsNullOrWhiteSpace(user) ||
            string.IsNullOrWhiteSpace(pass))
        {
            Console.WriteLine("WP_* env vars not set; skipping the live WordPress call.");
            return;
        }

        using var http = new HttpClient { BaseAddress = new Uri(baseUrl!), Timeout = TimeSpan.FromSeconds(15) };
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        var resp = await http.GetAsync("/wp-json/wp/v2/users?per_page=2");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("id");
    }
}

