// Edit.razor.cs (file-scoped namespace; minimal changes to add concurrency token)

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Editor.WordPress;
using System.Net;
using Editor.Abstractions;

namespace BlazorWP.Pages;

public partial class Edit : ComponentBase
{
    [Parameter] public int? Id { get; set; }

    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] public IWordPressApiService Api { get; set; } = default!;
    [Inject] private IWordPressEditingService Editing { get; set; } = default!;   // existing

    private bool _isDirty;
    private string? Title;
    private string? Content;

    private bool _saving;
    private bool _forking;
    private string? _status;
    private bool _readOnly;
    private bool _applyReadOnlyPending;
    private EditList? _list;

    // 🆕 concurrency token from server (UTC)
    private string? _modifiedGmt;

    protected override async Task OnParametersSetAsync()
    {
        _status = null;

        if (Id is int id)
        {
            _ = await Api.GetClientAsync();
            var http = Api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

            try
            {
                using var resp = await http.GetAsync($"/wp-json/wp/v2/posts/{id}?context=edit&_fields=id,status,title,content,modified_gmt");

                if (resp.StatusCode == HttpStatusCode.NotFound)
                {
                    Nav.NavigateTo("edit", replace: true);
                    return;
                }

                resp.EnsureSuccessStatusCode();

                var page = await System.Text.Json.JsonSerializer.DeserializeAsync<WpPage>(
                    await resp.Content.ReadAsStreamAsync(),
                    options: new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    cancellationToken: System.Threading.CancellationToken.None);

                Title = page?.title?.raw ?? page?.title?.rendered ?? "";
                Content = page?.content?.raw ?? page?.content?.rendered ?? "";
                _modifiedGmt = page?.modified_gmt;   // 🆕 capture token on load
                _readOnly = false;
            }
            catch (AuthError ae) when (ae.StatusCode == HttpStatusCode.Forbidden)
            {
                using var ro = await http.GetAsync($"/wp-json/wp/v2/posts/{id}?_fields=id,status,title,content,modified_gmt");
                ro.EnsureSuccessStatusCode();

                var page = await System.Text.Json.JsonSerializer.DeserializeAsync<WpPage>(
                    await ro.Content.ReadAsStreamAsync(),
                    options: new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    cancellationToken: System.Threading.CancellationToken.None);

                Title = page?.title?.rendered ?? "";
                Content = page?.content?.rendered ?? "";
                _modifiedGmt = page?.modified_gmt;   // 🆕 still cache token in RO mode
                _status = "Read-only: you don’t have permission to edit this post.";
                _readOnly = true;
            }
            finally
            {
                _applyReadOnlyPending = true;
            }
        }
        else
        {
            Title ??= "";
            Content ??= "";
            _modifiedGmt = null; // 🆕 new draft => no token yet
            _readOnly = false;
            _applyReadOnlyPending = true;
        }
    }

    private async Task SaveAsync()
    {
        _saving = true;
        _status = null;
        try
        {
            // Build the SaveData payload. For creates (no Id) we default to draft.
            var data = new SaveData(
                Title: Title ?? "",
                Content: Content ?? "",
                Excerpt: null,
                Status: Id is int ? null : "draft", // create-as-draft; ignored on update
                Slug: null,
                Meta: null,
                TaxInput: null,
                // 🆕 send concurrency token on update so server can detect conflicts and fork if needed
                ExpectedModifiedGmt: Id is int ? _modifiedGmt : null
            );

            // Use the unified service method:
            var res = Id is int id
                ? await Editing.SaveAsync(data, id: id)            // update existing
                : await Editing.SaveAsync(data, postType: "post"); // create new

            // If a new ID was created or server forked, navigate to it
            if (Id != res.Id)
            {
                Id = res.Id;
                Nav.NavigateTo($"edit/{res.Id}", replace: true);
            }

            _status = (res.Forked ?? false) ? $"Saved (forked to #{res.Id})." : "Saved.";

            // 🆕 refresh token from server response (critical for next round-trip)
            _modifiedGmt = res.ModifiedGmt;
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
            if (_list is not null) await _list.RefreshAsync();
            StateHasChanged();
        }
    }

    private async Task ForkAsync()
    {
        if (Id is not int id)
        {
            _status = "Nothing to fork yet.";
            StateHasChanged();
            return;
        }

        _forking = true;
        _status = null;
        try
        {
            var res = await Editing.ForkAsync(id);
            Id = res.Id;
            _modifiedGmt = res.ModifiedGmt; // 🆕 keep token valid immediately after fork
            Nav.NavigateTo($"edit/{res.Id}", replace: true);
            _status = $"Forked to #{res.Id}.";
        }
        catch (Exception ex)
        {
            _status = $"Fork failed: {ex.Message}";
        }
        finally
        {
            _forking = false;
            _isDirty = false;
            await JS.InvokeVoidAsync("BlazorBridge.setDirty", "articleEditor", false);
            if (_list is not null) await _list.RefreshAsync();
            StateHasChanged();
        }
    }

    private Task NewPageAsync()
    {
        // Minimal: make this a no-ID new draft (creation happens on Save)
        Id = null;
        Title = "";
        Content = "";
        _modifiedGmt = null; // 🆕 reset token for new draft
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
