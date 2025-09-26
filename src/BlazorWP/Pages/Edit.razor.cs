using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop; 
using Editor.WordPress;

namespace BlazorWP.Pages
{
    public partial class Edit : ComponentBase
    {
        [Parameter] public int? Id { get; set; }

        [Inject] public NavigationManager Nav { get; set; } = default!;
        [Inject] public IWordPressApiService Api { get; set; } = default!;


        private bool _isDirty;
        private string? Title;
        private string? Content;

        private bool _saving;
        private string? _status;

        protected override async Task OnParametersSetAsync()
        {
            _status = null;

            if (Id is int id)
            {
                // Load existing page
                _ = await Api.GetClientAsync();
                var http = Api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

                using var resp = await http.GetAsync($"/wp-json/wp/v2/posts/{id}?context=edit&_fields=id,status,title,content,modified_gmt");
                resp.EnsureSuccessStatusCode();

                var page = await System.Text.Json.JsonSerializer.DeserializeAsync<WpPage>(
                    await resp.Content.ReadAsStreamAsync(),
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (page is not null)
                {
                    Title = page.title?.raw ?? page.title?.rendered ?? "";
                    Content = page.content?.raw ?? page.content?.rendered ?? "";
                }
            }
            else
            {
                // Keep whatever is in the editor; default to empty strings
                Title ??= "";
                Content ??= "";
            }
        }

        private async Task SaveAsync()
        {
            _saving = true;
            _status = null;
            try
            {
                _ = await Api.GetClientAsync();
                var http = Api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

                // Build rex save payload (id omitted when creating)
                var data = new Dictionary<string, object?>
                {
                    ["post_title"] = Title ?? "",
                    ["post_content"] = Content ?? "",
                    // leave status null for updates; use "draft" on create below
                };

                var payload = new Dictionary<string, object?>
                {
                    ["data"] = data,
                    ["post_type"] = "post"
                };

                if (Id is int id)
                {
                    payload["id"] = id;
                }
                else
                {
                    // creation path
                    data["post_status"] = "draft";
                }

                var json = System.Text.Json.JsonSerializer.Serialize(
                    payload,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    }
                );

                using var res = await http.PostAsync(
                    "/wp-json/rex/v1/save",
                    new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")
                );
                res.EnsureSuccessStatusCode();

                using var doc = await System.Text.Json.JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());
                var root = doc.RootElement;

                // Get returned ID and update route if it changed (i.e., creation)
                var newId = root.GetProperty("id").GetInt32();
                if (Id != newId)
                {
                    Id = newId;
                    Nav.NavigateTo($"edit/{newId}", replace: true);
                }

                _status = root.TryGetProperty("forked", out var f) && f.ValueKind == System.Text.Json.JsonValueKind.True
                    ? $"Saved (forked to #{newId})."
                    : "Saved.";
            }
            catch (Exception ex)
            {
                _status = $"Save failed: {ex.Message}";
            }
            finally
            {
                _saving = false;
                _isDirty = false;
                await JS.InvokeVoidAsync("BlazorBridge.setDirty", "articleEditor", false);
                StateHasChanged();
            }
        }

        private Task NewPageAsync()
        {
            // Minimal: make this a no-ID new draft (creation happens on Save)
            Id = null;
            Title = "";
            Content = "";
            _status = "New draft (no ID). Fill in and Save.";
            Nav.NavigateTo("edit", replace: true);
            return Task.CompletedTask;
        }

        // Minimal DTOs (match WP field casing; we deserialize case-insensitively)
        private sealed class WpRender { public string? rendered { get; set; } public string? raw { get; set; } }
        private sealed class WpPage
        {
            public int id { get; set; }
            public string? status { get; set; }
            public WpRender? title { get; set; }
            public WpRender? content { get; set; }
            public string? modified_gmt { get; set; }
        }
    }
}
