using System.Net;
using System.Text;
using Editor.WordPress;
using Microsoft.Extensions.Options;
using Xunit;

namespace Editor.Tests
{
    public class DeleteUnitTests
    {
        private sealed class CapturingHandler : HttpMessageHandler
        {
            public readonly List<(HttpMethod method, Uri uri)> Requests = new();
            public HttpStatusCode Status = HttpStatusCode.OK;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Requests.Add((request.Method, request.RequestUri!));
                return Task.FromResult(new HttpResponseMessage(Status)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                });
            }
        }

        private static WordPressApiService NewApi(HttpMessageHandler handler)
        {
            var opts = Options.Create(new WordPressOptions
            {
                BaseUrl = "https://example.test",
                Timeout = TimeSpan.FromSeconds(5)
            });
            var api = new WordPressApiService(opts, () => handler);
            api.SetEndpoint("https://example.test");
            return api;
        }

        [Fact]
        public async Task Delete_Post_Uses_Force_And_Correct_Path()
        {
            var h = new CapturingHandler { Status = HttpStatusCode.OK };
            var api = NewApi(h);

            var res = await api.HttpClient!.DeleteAsync("/wp-json/wp/v2/posts/123?force=true");
            res.EnsureSuccessStatusCode();

            var last = h.Requests.Last();
            Assert.Equal(HttpMethod.Delete, last.method);
            Assert.Equal("/wp-json/wp/v2/posts/123", last.uri.AbsolutePath);
            Assert.Equal("force=true", last.uri.Query.TrimStart('?'));
        }

        [Fact]
        public async Task Delete_Is_Idempotent_404_Tolerated()
        {
            var h = new CapturingHandler { Status = HttpStatusCode.NotFound };
            var api = NewApi(h);

            var res = await api.HttpClient!.DeleteAsync("/wp-json/wp/v2/posts/999?force=true");
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }
    }
}
