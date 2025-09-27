using System.Net;
using System.Text.Json;
using Editor.WordPress;
using Microsoft.AspNetCore.Components;

namespace BlazorWP.Pages; // ← match your project namespace

public partial class MediaPopup : ComponentBase
{

  [Inject] public IWordPressApiService Api { get; set; } = default!;

  // Public entry called from Edit.* via @ref
  public async Task<string?> OpenAsync(bool multi = false)
  {
    _visible = true;
    _items.Clear();
    _selected = null;
    _page = 0; _totalPages = 1; _error = null;
    StateHasChanged();

    await EnsureHttpAsync();
    await LoadMore();

    // Wait until user confirms/cancels
    _tcs = new TaskCompletionSource<string?>();
    return await _tcs.Task;
  }

  // ----- state -----
  private bool _visible;
  private bool _busy;
  private string? _error;
  private int _page = 0, _totalPages = 1;
  private readonly List<WpMediaItem> _items = new();
  private WpMediaItem? _selected;
  private TaskCompletionSource<string?>? _tcs;
  // Returns alt text if present, otherwise a safe title for tooltips/alt attributes
  private static string AltOrTitle(WpMediaItem i)
  {
      if (!string.IsNullOrWhiteSpace(i.alt_text))
          return i.alt_text!;
      var s = StripHtml(i.description?.rendered) ?? StripHtml(i.title?.rendered) ?? "media";
      return s;
  }

  private async Task EnsureHttpAsync()
  {
    _ = await Api.GetClientAsync();
    if (Api.HttpClient is null) throw new InvalidOperationException("WordPress HttpClient is not initialized.");
  }

  private async Task LoadMore()
  {
    if (_busy) return;
    _busy = true; _error = null;
    try
    {
      await EnsureHttpAsync();
      var http = Api.HttpClient!;
      var next = _page + 1;

      // Use the same pattern as EditList: call /wp-json/wp/v2 and read totals from headers
      var url = $"/wp-json/wp/v2/media?per_page=60&page={next}&orderby=date&order=desc" +
                "&_fields=id,media_type,mime_type,source_url,alt_text,title,description,media_details";
      using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
      if (resp.StatusCode == HttpStatusCode.BadRequest && _page > 0)
      {
        // graceful stop on invalid next page
        _totalPages = _page;
        return;
      }
      resp.EnsureSuccessStatusCode();

      // totals
      if (resp.Headers.TryGetValues("X-WP-TotalPages", out var vals) && int.TryParse(vals.FirstOrDefault(), out var tp))
        _totalPages = Math.Max(1, tp);

      await using var stream = await resp.Content.ReadAsStreamAsync();
      var pageItems = await JsonSerializer.DeserializeAsync<List<WpMediaItem>>(
        stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

      _items.AddRange(pageItems);
      _page = next;
    }
    catch (Exception ex) { _error = ex.Message; }
    finally { _busy = false; StateHasChanged(); }
  }

  private void Select(WpMediaItem i) => _selected = i;

  private async Task Confirm()
  {
    if (_selected is null) return;
    var html = BuildInsertHtml(_selected);
    _visible = false;
    _items.Clear();
    _selected = null;
    _tcs?.TrySetResult(html);
    await Task.CompletedTask;
  }

  private async Task Cancel()
  {
    _visible = false;
    _items.Clear();
    _selected = null;
    _tcs?.TrySetResult(null);
    await Task.CompletedTask;
  }

  // ----- HTML builders -----
  private static bool IsPdf(WpMediaItem i) => (i.mime_type ?? "").Contains("pdf", StringComparison.OrdinalIgnoreCase);

  private string BuildInsertHtml(WpMediaItem i)
  {
    if (string.Equals(i.media_type, "image", StringComparison.OrdinalIgnoreCase))
      return BuildImageTag(i);

    if (IsPdf(i))
    {
      var preview = BuildImageTag(i);
      return $"<a href=\"{Enc(i.source_url)}\" target=\"_blank\" rel=\"noopener noreferrer\">{preview}</a>";
    }

    var label = StripHtml(i.title?.rendered) ?? System.IO.Path.GetFileName(i.source_url ?? "") ?? "file";
    return $"<a href=\"{Enc(i.source_url)}\" target=\"_blank\" rel=\"noopener noreferrer\">{Enc(label)}</a>";
  }

  private static string Thumb(WpMediaItem i)
  {
    var sizes = i.media_details?.sizes;
    if (sizes != null && sizes.TryGetValue("thumbnail", out var t) && !string.IsNullOrWhiteSpace(t.source_url))
      return t.source_url!;
    if (sizes != null)
    {
      var any = sizes.Values.OrderBy(s => s.width).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.source_url));
      if (any?.source_url is { Length: > 0 } u) return u;
    }
    return i.source_url ?? "";
  }

  private string BuildImageTag(WpMediaItem i)
  {
    var sizes = i.media_details?.sizes ?? new Dictionary<string, WpSize>();
    var valid = sizes.Values.Where(s => !string.IsNullOrWhiteSpace(s.source_url) && s.width > 0)
                            .OrderBy(s => s.width).ToList();

    var src = valid.Count > 0 ? valid[^1].source_url ?? (i.source_url ?? "") : (i.source_url ?? "");
    var srcset = valid.Count > 0 ? string.Join(", ", valid.Select(s => $"{s.source_url} {s.width}w")) : null;

    var alt = i.alt_text;
    if (string.IsNullOrWhiteSpace(alt))
      alt = StripHtml(i.description?.rendered) ?? StripHtml(i.title?.rendered) ?? "";

    var attr = $"src=\"{Enc(src)}\" alt=\"{Enc(alt ?? "")}\" loading=\"lazy\" decoding=\"async\"";
    if (!string.IsNullOrWhiteSpace(srcset))
      attr += $" srcset=\"{Enc(srcset)}\" sizes=\"(max-width: 1024px) 100vw, 1024px\"";

    return $"<img {attr} />";
  }

  private static string Enc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
  private static string? StripHtml(string? html) =>
    string.IsNullOrEmpty(html) ? html :
    System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", "").Trim();

  // ----- Minimal DTOs (case-insensitive JSON) -----
  private sealed class WpMediaItem
  {
    public int id { get; set; }
    public string? media_type { get; set; }
    public string? mime_type { get; set; }
    public string? source_url { get; set; }
    public string? alt_text { get; set; }
    public Render? title { get; set; }
    public Render? description { get; set; }
    public MediaDetails? media_details { get; set; }
  }
  private sealed class Render { public string? rendered { get; set; } public string? raw { get; set; } }
  private sealed class MediaDetails { public Dictionary<string, WpSize>? sizes { get; set; } }
  private sealed class WpSize { public string? source_url { get; set; } public int width { get; set; } public int height { get; set; } }

}
