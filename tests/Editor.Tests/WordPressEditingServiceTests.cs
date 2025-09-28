using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xunit;

// real implementations
using Editor.WordPress;
using Microsoft.Extensions.Options;

namespace Editor.Tests
{
    /// <summary>
    /// End-to-end tests for WordPressEditingService against a real WordPress instance.
    /// Env vars required: WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD.
    /// No mocks; these tests use the live REX endpoints via the service.
    ///
    /// xUnit v2 note: we "early return" when not configured (tests will show as Passed).
    /// </summary>
    [Collection("WP EndToEnd")]
    public class WordPressEditingServiceTests : IClassFixture<WordPressServerFixture>, IClassFixture<RunUniqueFixture>
    {
        private readonly WordPressServerFixture _wp;
        private readonly RunUniqueFixture _unique;
        private readonly WordPressEditingService? _editing;

        public WordPressEditingServiceTests(WordPressServerFixture wp, RunUniqueFixture unique)
        {
            _wp = wp;
            _unique = unique;
            _editing = _wp.IsConfigured ? _wp.Editing : null;
        }

        private bool NotConfiguredReturn() => !_wp.IsConfigured;

        [Fact]
        [Trait("Category", "WP-E2E")]
        public async Task Save_DraftTwice_NoFork()
        {
            if (NotConfiguredReturn()) return;
            var editing = _editing!;

            string title1 = _unique.Next("REX SaveOK");
            int postId = await _wp.CreatePostAsync(new()
            {
                ["title"] = title1,
                ["content"] = "v1",
                ["status"] = "draft"
            });

            var res1 = await editing.SaveAsync(
                new SaveData(
                    Title: "REX SaveOK A1",
                    Content: null,
                    Excerpt: null,
                    Status: null,
                    Slug: null,
                    Meta: null,
                    TaxInput: null,
                    ExpectedModifiedGmt: null
                ),
                id: postId // update existing post
            );

            Assert.False(res1.Forked.GetValueOrDefault(), "Should not fork on first save");
            Assert.Equal(postId, res1.Id);

            string token = !string.IsNullOrWhiteSpace(res1.ModifiedGmt)
                ? res1.ModifiedGmt!
                : await _wp.GetModifiedGmtAsync(postId);
            Assert.False(string.IsNullOrWhiteSpace(token));

            var res2 = await editing.SaveAsync(
                new SaveData(
                    Title: null,
                    Content: "v2 - concurrency pass",
                    Excerpt: null,
                    Status: null,
                    Slug: null,
                    Meta: null,
                    TaxInput: null,
                    ExpectedModifiedGmt: token
                ),
                id: postId
            );

            Assert.False(res2.Forked.GetValueOrDefault(), "Should not fork on second save");
            Assert.Equal(postId, res2.Id);

            var p = await _wp.GetPostAsync(postId, "id,title,content");
            Assert.Contains("REX SaveOK A1", p.Title?.Rendered ?? "");
            Assert.Contains("concurrency pass", WebUtility.HtmlDecode(p.Content?.Rendered ?? ""));
        }

        [Fact]
        [Trait("Category", "WP-E2E")]
        public async Task Fork_ForkOfFork_RetainsRootOriginal()
        {
            if (NotConfiguredReturn()) return;
            var editing = _editing!;

            int origId = await _wp.CreatePostAsync(new()
            {
                ["title"] = _unique.Next("REX Root"),
                ["content"] = "root",
                ["status"] = "publish"
            });

            var f1 = await editing.ForkAsync(origId);
            _wp.RegisterPost(f1.Id); // ensure cleanup
            Assert.Equal(origId, f1.OriginalPostId ?? 0);

            var f2 = await editing.ForkAsync(f1.Id);
            _wp.RegisterPost(f2.Id); // ensure cleanup
            Assert.Equal(origId, f2.OriginalPostId ?? 0);
        }

        [Fact]
        [Trait("Category", "WP-E2E")]
        public async Task Save_Conflict_CreatesFork_Then_Publish_OverwritesOriginal()
        {
            if (NotConfiguredReturn()) return;
            var editing = _editing!;

            int origId = await _wp.CreatePostAsync(new()
            {
                ["title"] = _unique.Next("REX Orig"),
                ["content"] = "Original body",
                ["status"] = "publish"
            });

            var saveRes = await editing.SaveAsync(
                new SaveData(
                    Title: "REX Updated via conflict",
                    Content: "Updated body (conflict path)",
                    Excerpt: null,
                    Status: null,
                    Slug: null,
                    Meta: null,
                    TaxInput: null,
                    ExpectedModifiedGmt: "1970-01-01 00:00:00"
                ),
                id: origId
            );

            Assert.True(saveRes.Forked);
            Assert.Equal(origId, saveRes.OriginalPostId);
            int draftId = saveRes.Id;
            _wp.RegisterPost(draftId); // ensure cleanup of forked draft (later trashed)

            var pubRes = await editing.PublishAsync(draftId);
            Assert.True(pubRes.UsedOriginal);
            Assert.Equal(origId, pubRes.PublishedId);

            var orig = await _wp.GetPostAsync(origId, "status,title,content");
            Assert.Equal("publish", orig.Status);
            Assert.Contains("REX Updated via conflict", orig.Title?.Rendered ?? "");
            Assert.Contains("Updated body (conflict path)", WebUtility.HtmlDecode(orig.Content?.Rendered ?? ""));

            var draft = await _wp.GetPostAsync(draftId, "status");
            Assert.Equal("trash", draft.Status);
        }

        [Fact]
        [Trait("Category", "WP-E2E")]
        public async Task Publish_WhenOriginalTrashed_UntrashesAndUpdatesOriginal()
        {
            if (NotConfiguredReturn()) return;
            var editing = _editing!;

            int origId = await _wp.CreatePostAsync(new()
            {
                ["title"] = _unique.Next("REX Untrash"),
                ["content"] = "live",
                ["status"] = "publish"
            });

            var staging = await editing.ForkAsync(origId);
            int stgId = staging.Id;
            _wp.RegisterPost(stgId); // staging will be trashed on publish; ensure cleanup

            await _wp.DeletePostAsync(origId, force: false);

            await editing.SaveAsync(
                new SaveData(
                    Title: _unique.Next("REX Untrash New"),
                    Content: "updated before publish",
                    Excerpt: null,
                    Status: null,
                    Slug: null,
                    Meta: null,
                    TaxInput: null,
                    ExpectedModifiedGmt: null
                ),
                id: stgId
            );

            var pubRes = await editing.PublishAsync(stgId);
            Assert.True(pubRes.UsedOriginal);
            Assert.Equal(origId, pubRes.PublishedId);

            var orig = await _wp.GetPostAsync(origId, "status,title");
            Assert.Equal("publish", orig.Status);
            Assert.Contains("REX Untrash New", orig.Title?.Rendered ?? "");

            var stg = await _wp.GetPostAsync(stgId, "status");
            Assert.Equal("trash", stg.Status);
        }

        [Fact]
        [Trait("Category", "WP-E2E")]
        public async Task Publish_WhenOriginalDeleted_PublishesStagingItself()
        {
            if (NotConfiguredReturn()) return;
            var editing = _editing!;

            int origId = await _wp.CreatePostAsync(new()
            {
                ["title"] = _unique.Next("REX HardDel"),
                ["content"] = "live",
                ["status"] = "publish"
            });

            var staging = await editing.ForkAsync(origId);
            int stgId = staging.Id;
            _wp.RegisterPost(stgId); // staging becomes published; ensure cleanup

            await _wp.DeletePostAsync(origId, force: true);

            var pubRes = await editing.PublishAsync(stgId);
            Assert.False(pubRes.UsedOriginal);
            Assert.Equal(stgId, pubRes.PublishedId);

            var nowPublished = await _wp.GetPostAsync(stgId, "status,meta");
            Assert.Equal("publish", nowPublished.Status);

            if (nowPublished.Meta != null && nowPublished.Meta.TryGetValue("_rex_original_post_id", out var val))
            {
                bool cleared = val.ValueKind == JsonValueKind.Null
                               || (val.ValueKind == JsonValueKind.String && string.IsNullOrEmpty(val.GetString()))
                               || (val.ValueKind == JsonValueKind.Number && val.GetInt32() == 0);
                Assert.True(cleared, $"Expected _rex_original_post_id cleared, got={val}");
            }
        }

        [Fact]
        [Trait("Category", "WP-E2E")]
        public async Task Fork_NonexistentPost_ThrowsNotFound()
        {
            if (NotConfiguredReturn()) return;
            var editing = _editing!;

            int nonExistentId = 2147480000;

            var ex = await Record.ExceptionAsync(async () => await editing.ForkAsync(nonExistentId));
            Assert.NotNull(ex);

            if (ex is HttpRequestException hre && hre.StatusCode.HasValue)
            {
                Assert.Equal(HttpStatusCode.NotFound, hre.StatusCode.Value);
            }
            else
            {
                Assert.Contains("404", ex!.Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        [Trait("Category", "WP-E2E")]
        public async Task Save_Ignores_OriginalPostId_Meta()
        {
            if (NotConfiguredReturn()) return;
            var editing = _editing!;

            int postId = await _wp.CreatePostAsync(new()
            {
                ["title"] = _unique.Next("REX IgnoreMeta"),
                ["content"] = "body",
                ["status"] = "draft"
            });

            var res = await editing.SaveAsync(
                new SaveData(
                    Title: "Ignore meta attempt",
                    Content: null,
                    Excerpt: null,
                    Status: null,
                    Slug: null,
                    Meta: new Dictionary<string, object>
                    {
                        ["_rex_original_post_id"] = 12345
                    },
                    TaxInput: null,
                    ExpectedModifiedGmt: null
                ),
                id: postId
            );

            Assert.Equal(postId, res.Id);

            var post = await _wp.GetPostAsync(postId, "id,meta,title");
            Assert.Equal("Ignore meta attempt", post.Title?.Rendered ?? "");

            var meta = post.Meta ?? new Dictionary<string, JsonElement>();
            if (meta.TryGetValue("_rex_original_post_id", out var val))
            {
                bool ignored = val.ValueKind == JsonValueKind.Null
                               || (val.ValueKind == JsonValueKind.String && string.IsNullOrEmpty(val.GetString()))
                               || (val.ValueKind == JsonValueKind.Number && val.GetInt32() == 0);
                Assert.True(ignored, $"_rex_original_post_id meta was not ignored/cleared, value={val}");
            }
        }

        [Fact]
        [Trait("Category", "WP-E2E")]
        public async Task Fork_Inherits_Categories_FromOriginal()
        {
            if (NotConfiguredReturn()) return;
            var editing = _editing!;

            int catId = await _wp.CreateCategoryAsync(_unique.Next("rex-cat"));

            int origId = await _wp.CreatePostAsync(new()
            {
                ["title"] = _unique.Next("REX Cats"),
                ["content"] = "with cats",
                ["status"] = "publish",
                ["categories"] = new[] { catId }
            });

            var fork = await editing.ForkAsync(origId);
            _wp.RegisterPost(fork.Id); // ensure cleanup
            var fp = await _wp.GetPostAsync(fork.Id, "id,categories");
            int[] cats = fp.Categories ?? Array.Empty<int>();
            Assert.Contains(catId, cats);
        }
    }

    // === Fixtures & DTOs (no mocks) =============================================

    public sealed class WordPressServerFixture : IAsyncLifetime
    {
        public readonly string? BaseUrl = Environment.GetEnvironmentVariable("WP_BASE_URL");
        public readonly string? Username = Environment.GetEnvironmentVariable("WP_USERNAME");
        public readonly string? AppPassword = Environment.GetEnvironmentVariable("WP_APP_PASSWORD");

        public HttpClient? Client { get; private set; }
        public Uri? ApiBase { get; private set; }

        public WordPressApiService? Api { get; private set; }
        public WordPressEditingService? Editing { get; private set; }

        private readonly ConcurrentBag<int> _createdPosts = new();

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(BaseUrl) &&
            !string.IsNullOrWhiteSpace(Username) &&
            !string.IsNullOrWhiteSpace(AppPassword);

        public async Task InitializeAsync()
        {
            if (!IsConfigured) return;

            ApiBase = new Uri(new Uri(BaseUrl!.TrimEnd('/') + "/"), "wp-json/");
            Client = new HttpClient { BaseAddress = ApiBase };
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{AppPassword}"));
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);

            using var ping = await Client.GetAsync("wp/v2").ConfigureAwait(false);
            ping.EnsureSuccessStatusCode();

            // *** Construct API with IOptions<WordPressOptions> + handler factory ***
            var opts = Options.Create(new WordPressOptions
            {
                BaseUrl = BaseUrl!,        // e.g., "https://example.com"
                UserName = Username!,      // note: UserName (not Username)
                AppPassword = AppPassword!
            });

            Api = new WordPressApiService(opts, () => new HttpClientHandler());
            Editing = new WordPressEditingService(Api);
        }

        public async Task DisposeAsync()
        {
            if (Client == null) return;

            while (_createdPosts.TryTake(out var id))
            {
                try { await DeletePostAsync(id, force: true); } catch { /* ignore */ }
            }

            Client.Dispose();
        }

        public void RegisterPost(int id) => _createdPosts.Add(id);

        public async Task<int> CreatePostAsync(Dictionary<string, object?> payload)
        {
            var res = await Client!.PostAsJsonAsync("wp/v2/posts", payload).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();

            // Read as JsonElement and extract id robustly
            var root = await res.Content.ReadFromJsonAsync<JsonElement>();
            if (!root.TryGetProperty("id", out var idNode))
                throw new InvalidOperationException("CreatePostAsync: response missing 'id'");

            int id = idNode.ValueKind switch
            {
                JsonValueKind.Number => idNode.GetInt32(),
                JsonValueKind.String when int.TryParse(idNode.GetString(), out var n) => n,
                _ => throw new InvalidOperationException($"CreatePostAsync: unexpected 'id' kind: {idNode.ValueKind}")
            };

            RegisterPost(id);
            return id;
        }


        public async Task DeletePostAsync(int id, bool force)
        {
            var res = await Client!.DeleteAsync($"wp/v2/posts/{id}?force={(force ? "true" : "false")}").ConfigureAwait(false);
            res.EnsureSuccessStatusCode();
        }

        public async Task<string> GetModifiedGmtAsync(int id)
        {
            var obj = await Client!.GetFromJsonAsync<Dictionary<string, string>>(
                $"wp/v2/posts/{id}?context=edit&_fields=modified_gmt");
            return obj?["modified_gmt"] ?? "";
        }

        public async Task<WpPost> GetPostAsync(int id, string fieldsCsv)
        {
            string q = $"wp/v2/posts/{id}?context=edit&_fields={fieldsCsv}";
            var p = await Client!.GetFromJsonAsync<WpPost>(q);
            if (p is null) throw new InvalidOperationException($"Could not fetch post {id}");
            return p;
        }

        public async Task<int> CreateCategoryAsync(string name)
        {
            var res = await Client!.PostAsJsonAsync("wp/v2/categories", new { name }).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();

            var root = await res.Content.ReadFromJsonAsync<JsonElement>();
            if (!root.TryGetProperty("id", out var idNode))
                throw new InvalidOperationException("CreateCategoryAsync: response missing 'id'");

            int id = idNode.ValueKind switch
            {
                JsonValueKind.Number => idNode.GetInt32(),
                JsonValueKind.String when int.TryParse(idNode.GetString(), out var n) => n,
                _ => throw new InvalidOperationException($"CreateCategoryAsync: unexpected 'id' kind: {idNode.ValueKind}")
            };

            return id;
        }
    }

    public sealed class RunUniqueFixture
    {
        private int _n = 0;
        public string Next(string prefix) =>
            $"{prefix} #{Environment.MachineName} {DateTime.UtcNow:yyyyMMdd-HHmmss}-{System.Threading.Interlocked.Increment(ref _n)}";
    }

    // Minimal DTO for REST verification
    public sealed record WpPost
    {
        [JsonPropertyName("id")] public int Id { get; init; }
        [JsonPropertyName("status")] public string? Status { get; init; }
        [JsonPropertyName("meta")] public Dictionary<string, JsonElement>? Meta { get; init; }
        [JsonPropertyName("title")] public RenderedText? Title { get; init; }
        [JsonPropertyName("content")] public RenderedText? Content { get; init; }
        [JsonPropertyName("categories")] public int[]? Categories { get; init; }
    }

    public sealed record RenderedText
    {
        [JsonPropertyName("rendered")] public string? Rendered { get; init; }
    }
}
