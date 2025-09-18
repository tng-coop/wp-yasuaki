using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Editor.WordPress;
using Microsoft.Extensions.Options;
using Xunit;

namespace Editor.Tests
{
    [Collection("WP EndToEnd")]
    public class PostsDeleteE2eTests
    {
        private static WordPressApiService NewApi()
        {
            var baseUrl = Environment.GetEnvironmentVariable("WP_BASE_URL")!;
            var user    = Environment.GetEnvironmentVariable("WP_USERNAME")!;
            var pass    = Environment.GetEnvironmentVariable("WP_APP_PASSWORD")!;
            return new WordPressApiService(Options.Create(new WordPressOptions
            {
                BaseUrl = baseUrl,
                UserName = user,
                AppPassword = pass,
                Timeout = TimeSpan.FromSeconds(20)
            }));
        }

        [Fact]
        public async Task HardDelete_Removes_And_Is_Idempotent()
        {
            var api = NewApi();
            var http = api.HttpClient!;

            var title = $"delete-e2e-{Guid.NewGuid():N}";
            var create = await http.PostAsJsonAsync("/wp-json/wp/v2/posts", new { title, status = "draft", content = "<p>temp</p>" });
            create.EnsureSuccessStatusCode();
            using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
            var id = created.RootElement.GetProperty("id").GetInt64();

            var del1 = await http.DeleteAsync($"/wp-json/wp/v2/posts/{id}?force=true");
            Assert.True(del1.IsSuccessStatusCode);

            var get = await http.GetAsync($"/wp-json/wp/v2/posts/{id}");
            Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

            var del2 = await http.DeleteAsync($"/wp-json/wp/v2/posts/{id}?force=true");
            Assert.Equal(HttpStatusCode.NotFound, del2.StatusCode);
        }
    }
}
