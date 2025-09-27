using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using BlazorWP;
using Editor.WordPress;

namespace BlazorWP.Pages;

public partial class MediaPopup : ComponentBase
{
  [Inject] public IWordPressApiService Api { get; set; } = default!;
  [Inject] private LocalStorageJsInterop Storage { get; set; } = default!;
  [Inject] private ClipboardJsInterop Clipboard { get; set; } = default!;

  // ----- entry point -----
  public async Task<string?> OpenAsync(bool multi = false)
  {
    _visible = true;
    _items.Clear();
    _multi.Clear();
    _single = null;
    _multiMode = multi;
    _page = 0; _totalPages = 1; _error = null;
    _tab = "library"; // default to Library view
    _uploadStatus = null;

    await LoadPrefsAsync();
    StateHasChanged();

    await EnsureHttpAsync();
    await LoadMore();

    _tcs = new TaskCompletionSource<string?>();
    return await _tcs.Task;
  }

  // ----- state -----
  private bool _visible;
  private bool _busy;
  private bool _savingAlt;
  private string? _error;
  private string _tab = "library"; // upload | library
  private bool IsUploadTab => _tab == "upload";
  private int _page = 0, _totalPages = 1;
  private readonly List<WpMediaItem> _items = new();
  private readonly HashSet<int> _multi = new();
  private WpMediaItem? _single;
  private TaskCompletionSource<string?>? _tcs;

  // Filters + prefs
  private string? _search;
  private string _type = "all"; // all|image|audio|video|pdf
  private string? _month;        // yyyy-MM
  private ImgSizeChoice _imgSize = ImgSizeChoice.Auto;
  private LinkToChoice _linkTo = LinkToChoice.None;
  private string? _altDraft;
  private string? _uploadStatus;

  private readonly List<(string Label, string Value)> _months = Enumerable.Range(0, 24)
    .Select(offset => DateTime.UtcNow.AddMonths(-offset))
    .Select(dt => ($"{dt:MMMM yyyy}", dt.ToString("yyyy-MM")))
    .ToList();

  private enum ImgSizeChoice { Auto, Thumbnail, Medium, Large, Full }
  private enum LinkToChoice { None, MediaFile, AttachmentPage }

  private string InsertButtonLabel
    => _multiMode ? ($"Insert {_multi.Count} item" + (_multi.Count == 1 ? string.Empty : "s"))
                  : "Insert";

  private bool CanInsert => _multiMode ? _multi.Count > 0 : _single is not null;

  private void SwitchTab(string tab)
  {
    _tab = tab;
    StateHasChanged();
  }

  private void ClearSelection()
  {
    _multi.Clear();
    _single = null;
  }

  private void ToggleSelect(WpMediaItem i)
  {
    if (_multiMode)
    {
      if (_multi.Contains(i.id)) _multi.Remove(i.id); else _multi.Add(i.id);
      _single = _multi.Count == 1 ? _items.FirstOrDefault(x => x.id == _multi.First()) : null;
      if (_single is not null) _altDraft = _single.alt_text ?? string.Empty;
    }
    else
    {
      _single = i;
      _altDraft = _single.alt_text ?? string.Empty;
    }
  }

  private async Task CopyUrl()
  {
    if (_single?.source_url is { Length: > 0 } url)
      await Clipboard.CopyAsync(url);
  }

  private async Task SaveAltAsync()
  {
    if (_single is null || _savingAlt) return;
    _savingAlt = true;
    try
    {
      await EnsureHttpAsync();
      var http = Api.HttpClient!;
      using var res = await http.PostAsJsonAsync($"/wp-json/wp/v2/media/{_single.id}", new { alt_text = _altDraft ?? string.Empty });
      res.EnsureSuccessStatusCode();
      // Update local model
      _single.alt_text = _altDraft ?? string.Empty;
    }
    catch (Exception ex) { _error = ex.Message; }
    finally { _savingAlt = false; StateHasChanged(); }
  }

  private async Task EnsureHttpAsync()
  {
    _ = await Api.GetClientAsync();
    if (Api.HttpClient is null) throw new InvalidOperationException("WordPress HttpClient is not initialized.");
  }

  private async Task LoadPrefsAsync()
  {
    try
    {
      _search = await Storage.GetItemAsync("mediapopup.search");
      _type   = (await Storage.GetItemAsync("mediapopup.type")) ?? _type;
      _month  = await Storage.GetItemAsync("mediapopup.month");
      if (Enum.TryParse(await Storage.GetItemAsync("mediapopup.imgsize"), out ImgSizeChoice s)) _imgSize = s;
      if (Enum.TryParse(await Storage.GetItemAsync("mediapopup.linkto"), out LinkToChoice l)) _linkTo = l;
    }
    catch { /* ignore JS errors */ }
  }

  private async Task PersistPrefsAsync()
  {
    try
    {
      await Storage.SetItemAsync("mediapopup.search", _search ?? "");
      await Storage.SetItemAsync("mediapopup.type", _type);
      await Storage.SetItemAsync("mediapopup.month", _month ?? "");
      await Storage.SetItemAsync("mediapopup.imgsize", _imgSize.ToString());
      await Storage.SetItemAsync("mediapopup.linkto", _linkTo.ToString());
    }
    catch { }
  }

  private async Task ApplyFiltersAsync()
  {
    await PersistPrefsAsync();
    _page = 0; _totalPages = 1; _items.Clear();
    await LoadMore();
  }

  private Task RefreshAsync() => LoadMore(reset: true);

  private async Task LoadMore(bool reset = false)
  {
    if (_busy) return;
    _busy = true; _error = null;
    try
    {
      await EnsureHttpAsync();
      var http = Api.HttpClient!;
      var next = reset ? 1 : _page + 1;

      var query = new List<string>
      {
        $"/wp-json/wp/v2/media?per_page=60&page={next}&orderby=date&order=desc",
        "&_fields=id,media_type,mime_type,source_url,link,alt_text,title,description,date_gmt,media_details"
      };

      if (!string.IsNullOrWhiteSpace(_search))
        query.Add("&search=" + Uri.EscapeDataString(_search.Trim()));

      if (_type != "all")
      {
        if (string.Equals(_type, "pdf", StringComparison.OrdinalIgnoreCase))
          query.Add("&mime_type=application/pdf");
        else
          query.Add("&media_type=" + _type);
      }

      if (!string.IsNullOrWhiteSpace(_month) && DateTime.TryParse(_month + "-01", out var monthStart))
      {
        var after = new DateTime(monthStart.Year, monthStart.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var before = after.AddMonths(1).AddSeconds(-1);
        query.Add("&after=" + Uri.EscapeDataString(after.ToString("yyyy-MM-dd'T'HH:mm:ss")));
        query.Add("&before=" + Uri.EscapeDataString(before.ToString("yyyy-MM-dd'T'HH:mm:ss")));
      }

      var url = string.Concat(query);

      using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
      if (resp.StatusCode == HttpStatusCode.BadRequest && _page > 0)
      {
        _totalPages = _page; // graceful stop
        return;
      }
      resp.EnsureSuccessStatusCode();

      if (resp.Headers.TryGetValues("X-WP-TotalPages", out var vals) && int.TryParse(vals.FirstOrDefault(), out var tp))
        _totalPages = Math.Max(1, tp);

      await using var stream = await resp.Content.ReadAsStreamAsync();
      var pageItems = await JsonSerializer.DeserializeAsync<List<WpMediaItem>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

      if (reset) _items.Clear();
      _items.AddRange(pageItems);
      _page = next;
    }
    catch (Exception ex) { _error = ex.Message; }
    finally { _busy = false; StateHasChanged(); }
  }

  private async Task OnFilesSelected(InputFileChangeEventArgs e)
  {
    await EnsureHttpAsync();
    var http = Api.HttpClient!;
    var files = e.GetMultipleFiles(20);

    int ok = 0, fail = 0;
    foreach (var file in files)
    {
      try
      {
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream(maxAllowedSize: 20 * 1024 * 1024);
        var sc = new StreamContent(stream);
        sc.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
        content.Add(sc, "file", file.Name);

        using var resp = await http.PostAsync("/wp-json/wp/v2/media", content);
        resp.EnsureSuccessStatusCode();

        var created = await resp.Content.ReadFromJsonAsync<WpMediaItem>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (created is not null)
        {
          _items.Insert(0, created);
          ok++;
        }
      }
      catch (Exception ex)
      {
        fail++;
        _error = ex.Message;
      }
    }

    _uploadStatus = $"Uploaded {ok} file" + (ok == 1 ? string.Empty : "s") + (fail > 0 ? $", {fail} failed" : "") + ". Switched to library.";
    SwitchTab("library");
  }

  private async Task Confirm()
  {
    if (!CanInsert) return;

    IEnumerable<WpMediaItem> selection = _multiMode
      ? _items.Where(i => _multi.Contains(i.id))
      : (_single is null ? Enumerable.Empty<WpMediaItem>() : new[] { _single });

    var blocks = selection.Select(i => BuildInsertHtml(i, _imgSize, _linkTo));
    var html = string.Join("\n", blocks);

    _visible = false;
    _items.Clear();
    _multi.Clear();
    _single = null;
    _tcs?.TrySetResult(html);
    await Task.CompletedTask;
  }

  private async Task Cancel()
  {
    _visible = false;
    _items.Clear();
    _multi.Clear();
    _single = null;
    _tcs?.TrySetResult(null);
    await Task.CompletedTask;
  }

  private void HandleKeyDown(KeyboardEventArgs e)
  {
    if (e.Key == "Escape") _ = Cancel();
    if (e.Key == "Enter" && CanInsert) _ = Confirm();
  }

  // ----- helpers -----
  private static bool IsPdf(WpMediaItem i) => (i.mime_type ?? "").Contains("pdf", StringComparison.OrdinalIgnoreCase);

  private static string AltOrTitle(WpMediaItem i)
  {
    if (!string.IsNullOrWhiteSpace(i.alt_text)) return i.alt_text!;
    var s = StripHtml(i.description?.rendered) ?? StripHtml(i.title?.rendered) ?? "media";
    return s;
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

  private string BuildInsertHtml(WpMediaItem i, ImgSizeChoice size = ImgSizeChoice.Auto, LinkToChoice linkTo = LinkToChoice.None)
  {
    if (string.Equals(i.media_type, "image", StringComparison.OrdinalIgnoreCase))
    {
      var img = BuildImageTag(i, size);
      return linkTo switch
      {
        LinkToChoice.MediaFile      => $"<a href=\"{Enc(i.source_url)}\" target=\"_blank\" rel=\"noopener noreferrer\">{img}</a>",
        LinkToChoice.AttachmentPage => $"<a href=\"{Enc(i.link)}\">{img}</a>",
        _                           => img
      };
    }

    if (IsPdf(i))
    {
      var preview = BuildImageTag(i, size);
      return $"<a href=\"{Enc(i.source_url)}\" target=\"_blank\" rel=\"noopener noreferrer\">{preview}</a>";
    }

    var label = StripHtml(i.title?.rendered) ?? System.IO.Path.GetFileName(i.source_url ?? "") ?? "file";
    return $"<a href=\"{Enc(i.source_url)}\" target=\"_blank\" rel=\"noopener noreferrer\">{Enc(label)}</a>";
  }

  private string BuildImageTag(WpMediaItem i, ImgSizeChoice size)
  {
    var sizes = i.media_details?.sizes ?? new Dictionary<string, WpSize>();
    var valid = sizes.Values.Where(s => !string.IsNullOrWhiteSpace(s.source_url) && s.width > 0)
                            .OrderBy(s => s.width).ToList();

    string src;
    string? srcset = null;

    string? pick(string key)
      => sizes.TryGetValue(key, out var s) && !string.IsNullOrWhiteSpace(s.source_url) ? s.source_url : null;

    src = size switch
    {
      ImgSizeChoice.Thumbnail => pick("thumbnail") ?? pick("medium") ?? i.source_url ?? "",
      ImgSizeChoice.Medium    => pick("medium") ?? pick("large") ?? i.source_url ?? "",
      ImgSizeChoice.Large     => pick("large") ?? i.source_url ?? "",
      ImgSizeChoice.Full      => i.source_url ?? "",
      _ /* Auto */             => valid.Count > 0 ? valid[^1].source_url ?? (i.source_url ?? "") : (i.source_url ?? "")
    } ?? i.source_url ?? "";

    if (valid.Count > 0)
      srcset = string.Join(", ", valid.Select(s => $"{s.source_url} {s.width}w"));

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
    Regex.Replace(html, "<.*?>", "").Trim();

  // ----- Minimal DTOs (case-insensitive JSON) -----
  private sealed class WpMediaItem
  {
    public int id { get; set; }
    public string? media_type { get; set; }
    public string? mime_type { get; set; }
    public string? source_url { get; set; }
    public string? link { get; set; }
    public string? alt_text { get; set; }
    public Render? title { get; set; }
    public Render? description { get; set; }
    public DateTimeOffset? date_gmt { get; set; }
    public MediaDetails? media_details { get; set; }
  }
  private sealed class Render { public string? rendered { get; set; } public string? raw { get; set; } }
  private sealed class MediaDetails { public Dictionary<string, WpSize> sizes { get; set; } = new(); }
  private sealed class WpSize { public string? source_url { get; set; } public int width { get; set; } public int height { get; set; } }

  // multi-select mode flag
  private bool _multiMode;
}