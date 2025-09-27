using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json; // added
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BlazorWP; // LocalStorageJsInterop
using Editor.WordPress;
using Microsoft.AspNetCore.Components;

namespace BlazorWP.Pages;

public partial class EditList : IDisposable
{
    // From parent Edit.razor
    [Parameter] public int? SelectedId { get; set; }

    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] public IWordPressApiService Api { get; set; } = default!;
    [Inject] private LocalStorageJsInterop Storage { get; set; } = default!;

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
    private string? _error;

    // ---- Bulk selection state ----
    private readonly HashSet<int> _selected = new();
    private bool _bulkBusy;
    private string? _bulkStatus;

    private CancellationTokenSource? _loadCts;

    // Personalization + paging
    private int _pageSize = 10; // default
    private int _page = 1;
    private int _totalPages = 1;
    private int _totalPosts = 0;
    private string _orderBy = "modified"; // latest first by default
    private string _order = "desc";

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    // LocalStorage keys
    private const string KeySearch   = "editlist.search";
    private const string KeyPageSize = "editlist.pagesize";
    private const string KeyOrderBy  = "editlist.orderby";
    private const string KeyOrder    = "editlist.order";

    protected override async Task OnInitializedAsync()
    {
        await LoadPreferencesAsync();
        await LoadPageAsync();
    }

    private Task SearchAsync()
    {
        _page = 1; // new search starts at first page
        return PersistPreferencesThenLoadAsync();
    }

    private Task RefreshPosts() => LoadPageAsync();

    private void NavigateToPost(int id)
    {
        if (id > 0) Nav.NavigateTo($"edit/{id}");
    }

    private static string FormatLocal(string? modifiedGmt)
    {
        if (string.IsNullOrWhiteSpace(modifiedGmt)) return "";
        if (DateTimeOffset.TryParse(
                modifiedGmt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var utc))
        {
            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }
        return modifiedGmt!;
    }

    // ----- UI events -----

    private async Task OnPageSizeChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var n))
        {
            _pageSize = Math.Clamp(n, 10, 100);
            _page = 1;
            await PersistPreferencesThenLoadAsync();
        }
    }

    private async Task OnPageChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var p))
        {
            _page = Math.Max(1, p);
            await LoadPageAsync();
        }
    }

    private async Task ClearPersonalizationAsync()
    {
        try
        {
            await Storage.DeleteAsync(KeySearch);
            await Storage.DeleteAsync(KeyPageSize);
            await Storage.DeleteAsync(KeyOrderBy);
            await Storage.DeleteAsync(KeyOrder);
        }
        catch { /* ignore JS exceptions */ }

        _search = null;
        _pageSize = 10;
        _orderBy = "modified";
        _order = "desc";
        _page = 1;
        await LoadPageAsync();
    }

    // ----- Prefs -----

    private async Task LoadPreferencesAsync()
    {
        try
        {
            var savedSearch = await Storage.GetItemAsync(KeySearch);
            if (!string.IsNullOrEmpty(savedSearch)) _search = savedSearch;

            var savedSize = await Storage.GetItemAsync(KeyPageSize);
            if (int.TryParse(savedSize, out var size)) _pageSize = Math.Clamp(size, 10, 100);

            var savedOrderBy = await Storage.GetItemAsync(KeyOrderBy);
            if (!string.IsNullOrWhiteSpace(savedOrderBy)) _orderBy = savedOrderBy!;

            var savedOrder = await Storage.GetItemAsync(KeyOrder);
            if (string.Equals(savedOrder, "asc", StringComparison.OrdinalIgnoreCase)) _order = "asc";
        }
        catch { /* ignore JS exceptions */ }
    }

    private async Task PersistPreferencesThenLoadAsync()
    {
        try
        {
            await Storage.SetItemAsync(KeySearch, _search ?? "");
            await Storage.SetItemAsync(KeyPageSize, _pageSize.ToString(CultureInfo.InvariantCulture));
            await Storage.SetItemAsync(KeyOrderBy, _orderBy);
            await Storage.SetItemAsync(KeyOrder, _order);
        }
        catch { /* ignore JS exceptions */ }

        await LoadPageAsync();
    }

    // ----- Data load: SINGLE PAGE ONLY -----

    private async Task LoadPageAsync()
    {
        // cancel any in-flight request
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        _loadingPosts = true;
        _error = null;

        try
        {
            _selected.Clear(); // clear any stale selection when we reload
            _posts.Clear();

            // Ensure HttpClient is initialized via your WP service
            _ = await Api.GetClientAsync();
            var http = Api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

            var baseQuery = "/wp-json/wp/v2/posts?context=edit"
                          + $"&_fields=id,title,modified_gmt,status"
                          + $"&per_page={_pageSize}"
                          + $"&orderby={_orderBy}&order={_order}";

            if (!string.IsNullOrWhiteSpace(_search))
                baseQuery += "&search=" + Uri.EscapeDataString(_search.Trim());

            var statuses = "any";
            var attempts = 0;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var url = $"{baseQuery}&status={statuses}&page={_page}";
                using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

                // Handle status=any fall-back and invalid page fall-back
                if (resp.StatusCode == HttpStatusCode.BadRequest)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);

                    // 1) If "any" is rejected, fall back to explicit statuses once
                    if (statuses == "any" && attempts < 2)
                    {
                        statuses = "publish,draft,pending,private,future";
                        attempts++;
                        continue;
                    }

                    // 2) If page is invalid, reset to 1 once
                    if (_page > 1 && attempts < 3 &&
                        (body.Contains("rest_post_invalid_page_number", StringComparison.OrdinalIgnoreCase) ||
                         body.Contains("rest_invalid_page_number", StringComparison.OrdinalIgnoreCase)))
                    {
                        _page = 1;
                        attempts++;
                        continue;
                    }
                }

                resp.EnsureSuccessStatusCode();

                // Totals for UI (if headers are present)
                _totalPosts = TryParseHeader(resp, "X-WP-Total") ?? 0;
                _totalPages = TryParseHeader(resp, "X-WP-TotalPages") ?? Math.Max(1, _page);

                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                var raw = await JsonSerializer.DeserializeAsync<List<WpListItem>>(stream, _jsonOptions, ct) ?? new();

                if (raw.Count == 0 && _page > 1)
                {
                    // Graceful fall back to page 1 if page became meaningless
                    _page = 1;
                    attempts++;
                    if (attempts <= 3) continue;
                }

                foreach (var x in raw)
                {
                    _posts.Add(new PostListItem
                    {
                        Id = x.id,
                        Title = ExtractTitle(x.title),
                        Status = x.status,
                        ModifiedGmt = x.modified_gmt
                    });
                }

                // If server didn’t send totals (rare), guess total pages for label
                if (_totalPages <= 0)
                    _totalPages = Math.Max(1, (_posts.Count > 0 ? _page : 1));

                break; // single-page load finished
            }
        }
        catch (OperationCanceledException) { /* ignored */ }
        catch (Exception ex) { _error = ex.Message; }
        finally
        {
            _loadingPosts = false;
            StateHasChanged();
        }
    }

    private static int? TryParseHeader(HttpResponseMessage resp, string name)
    {
        if (resp.Headers.TryGetValues(name, out var values))
        {
            var v = values.FirstOrDefault();
            if (int.TryParse(v, out var n)) return n;
        }
        return null;
    }

    private static string ExtractTitle(RenderWrapper? title)
    {
        var raw = title?.raw;
        if (!string.IsNullOrWhiteSpace(raw)) return raw.Trim();

        var rendered = title?.rendered;
        if (!string.IsNullOrWhiteSpace(rendered)) return rendered.Trim();

        return "(untitled)";
    }

    // ----- Bulk selection + actions -----

    private void ToggleRow(int id, bool isChecked)
    {
        if (isChecked) _selected.Add(id); else _selected.Remove(id);
    }

    private void ToggleSelectAll(ChangeEventArgs e)
    {
        var on = e.Value is bool b && b;
        if (on)
        {
            _selected.Clear();
            foreach (var p in _posts) _selected.Add(p.Id);
        }
        else
        {
            _selected.Clear();
        }
    }

    private void ClearSelection() => _selected.Clear();

    private async Task BulkChangeStatusAsync(string newStatus)
    {
        if (_bulkBusy || _selected.Count == 0) return;

        _bulkBusy = true;
        _bulkStatus = "Working…";
        StateHasChanged();

        try
        {
            _ = await Api.GetClientAsync();
            var http = Api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

            var ids = _selected.ToList();
            int ok = 0, fail = 0;
            using var gate = new SemaphoreSlim(6); // be polite to WP

            var ops = ids.Select(async id =>
            {
                await gate.WaitAsync();
                try
                {
                    var url = $"/wp-json/wp/v2/posts/{id}";
                    var payload = new { status = newStatus };
                    using var resp = await http.PostAsJsonAsync(url, payload);
                    resp.EnsureSuccessStatusCode();
                    Interlocked.Increment(ref ok);
                }
                catch
                {
                    Interlocked.Increment(ref fail);
                }
                finally { gate.Release(); }
            });

            await Task.WhenAll(ops);
            _bulkStatus = $"Updated {ok}/{ids.Count}" + (fail > 0 ? $" ({fail} failed)" : "");
        }
        finally
        {
            _bulkBusy = false;
            _selected.Clear();
            await LoadPageAsync();
        }
    }

    private async Task BulkTrashAsync()
    {
        if (_bulkBusy || _selected.Count == 0) return;

        _bulkBusy = true;
        _bulkStatus = "Trashing…";
        StateHasChanged();

        try
        {
            _ = await Api.GetClientAsync();
            var http = Api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

            var ids = _selected.ToList();
            int ok = 0, fail = 0;
            using var gate = new SemaphoreSlim(6);

            var ops = ids.Select(async id =>
            {
                await gate.WaitAsync();
                try
                {
                    var url = $"/wp-json/wp/v2/posts/{id}?force=false"; // move to Trash, do not hard-delete
                    using var resp = await http.DeleteAsync(url);
                    resp.EnsureSuccessStatusCode();
                    Interlocked.Increment(ref ok);
                }
                catch
                {
                    Interlocked.Increment(ref fail);
                }
                finally { gate.Release(); }
            });

            await Task.WhenAll(ops);
            _bulkStatus = $"Moved to trash {ok}/{ids.Count}" + (fail > 0 ? $" ({fail} failed)" : "");
        }
        finally
        {
            _bulkBusy = false;
            _selected.Clear();
            await LoadPageAsync();
        }
    }

    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }
}