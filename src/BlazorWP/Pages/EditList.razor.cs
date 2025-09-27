using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Editor.WordPress;
using Microsoft.AspNetCore.Components;

namespace BlazorWP.Pages
{
    public partial class EditList : ComponentBase
    {
        // From parent Edit.razor
        [Parameter] public int? SelectedId { get; set; }

        [Inject] private NavigationManager Nav { get; set; } = default!;

        [Inject] public IWordPressApiService Api { get; set; } = default!;

        private sealed class RenderWrapper { public string? rendered { get; set; } public string? raw { get; set; } }
        private sealed class WpListItem
        {
            public int id { get; set; }
            public string? status { get; set; }
            public string? modified_gmt { get; set; }
            public RenderWrapper? title { get; set; }
        }
        private sealed class PostListItem
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public string? Status { get; set; }
            public string? ModifiedGmt { get; set; }
        }

        private readonly List<PostListItem> _posts = new();
        private bool _loadingPosts;
        private string? _search;

        protected override async Task OnInitializedAsync() => await LoadPostsAsync();

        private Task SearchAsync() => LoadPostsAsync();
        private Task RefreshPosts() => LoadPostsAsync();

        private void NavigateToPost(int id)
        {
            if (id > 0) Nav.NavigateTo($"edit/{id}");
        }

        private static string FormatLocal(string? modifiedGmt)
        {
            if (string.IsNullOrWhiteSpace(modifiedGmt)) return "";
            if (DateTime.TryParse(modifiedGmt, CultureInfo.InvariantCulture,
                                  DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                  out var utc))
            {
                return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            }
            return modifiedGmt!;
        }

        private async Task LoadPostsAsync()
        {
            _loadingPosts = true;
            try
            {
                _posts.Clear();

                _ = await Api.GetClientAsync();
                var http = Api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");
                var q = "/wp-json/wp/v2/posts?context=edit"
                      + "&per_page=50&status=any"
                      + "&_fields=id,title,modified_gmt,status";

                if (!string.IsNullOrWhiteSpace(_search))
                    q += "&search=" + Uri.EscapeDataString(_search.Trim());

                using var resp = await http.GetAsync(q);
                resp.EnsureSuccessStatusCode();

                await using var stream = await resp.Content.ReadAsStreamAsync();
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var raw = await JsonSerializer.DeserializeAsync<List<WpListItem>>(stream, opts) ?? new();

                foreach (var x in raw)
                {
                    _posts.Add(new PostListItem
                    {
                        Id = x.id,
                        Title = (x.title?.raw ?? x.title?.rendered ?? "(untitled)").Trim(),
                        Status = x.status,
                        ModifiedGmt = x.modified_gmt
                    });
                }
            }
            finally
            {
                _loadingPosts = false;
                StateHasChanged();
            }
        }

        
    }
}
