using System.Net;
using System.Text.Json;
using Editor.Abstractions;
using Editor.WordPress;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

public class StreamOptionsDiTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public readonly List<Uri> Requests = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);

            var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
            int perPage = query.TryGetValue("per_page", out var values) && int.TryParse(values, out var n) ? n : 1;
            int count = Math.Min(perPage, 3);

            var json = "[" + string.Join(',', Enumerable.Range(1, count).Select(i => FakePostJson(i))) + "]";
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(resp);

            static string FakePostJson(int i) =>
                $"{{\"id\":{i},\"title\":{{\"rendered\":\"p{i}\"}},\"status\":\"draft\",\"link\":\"/p/{i}\",\"modified_gmt\":\"2024-01-01T00:00:00\"}}";
        }
    }

    // … rest of tests unchanged …
}
