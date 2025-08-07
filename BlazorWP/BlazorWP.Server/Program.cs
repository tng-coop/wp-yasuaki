using System.Net.Http;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

var app = builder.Build();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.Map("/api/wp-proxy", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration config) =>
{
    var path = context.Request.Query["path"].ToString();
    if (string.IsNullOrEmpty(path))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Missing path");
        return;
    }

    var baseUrl = config["WordPress:BaseUrl"];
    if (string.IsNullOrEmpty(baseUrl))
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync("WordPress base URL not configured.");
        return;
    }

    var targetUri = new Uri(new Uri(baseUrl), path);
    var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUri);

    if (context.Request.Headers.TryGetValue("Authorization", out var auth))
    {
        requestMessage.Headers.TryAddWithoutValidation("Authorization", auth.ToArray());
    }
    if (context.Request.Headers.TryGetValue("X-WP-Nonce", out var nonce))
    {
        requestMessage.Headers.TryAddWithoutValidation("X-WP-Nonce", nonce.ToArray());
    }

    if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        requestMessage.Content = new StreamContent(context.Request.Body);
        foreach (var header in context.Request.Headers)
        {
            if (header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }
    }

    var client = httpClientFactory.CreateClient();
    using var responseMessage = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

    context.Response.StatusCode = (int)responseMessage.StatusCode;
    foreach (var header in responseMessage.Headers)
    {
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }
    foreach (var header in responseMessage.Content.Headers)
    {
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }

    await responseMessage.Content.CopyToAsync(context.Response.Body);
});

app.MapFallbackToFile("index.html");

app.Run();
