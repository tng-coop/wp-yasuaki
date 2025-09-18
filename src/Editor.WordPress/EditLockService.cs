using System.Net.Http.Json;
using System.Text.Json;

namespace Editor.WordPress;

public sealed class EditLockService : IEditLockService
{
    private readonly HttpClient _http;
    private readonly EditLockOptions _defaults;

    public EditLockService(HttpClient http, EditLockOptions? defaults = null)
    {
        _http = http;
        _defaults = defaults ?? new EditLockOptions();
    }

    public async Task<IEditLockSession> OpenAsync(string postType, long postId, long userId,
                                                  EditLockOptions? options = null,
                                                  CancellationToken ct = default)
    {
        var opt = options ?? _defaults;
        var session = new Session(_http, postType, postId, userId, opt);
        await session.ClaimAsync(ct);
        session.StartTimer();
        return session;
    }

    private sealed class Session : IEditLockSession
    {
        private readonly HttpClient _http;
        private readonly EditLockOptions _opt;
        private readonly CancellationTokenSource _cts = new();
        private PeriodicTimer? _timer;

        public Session(HttpClient http, string postType, long postId, long userId, EditLockOptions opt)
        {
            _http = http; PostType = postType; PostId = postId; UserId = userId; _opt = opt;
        }

        public string PostType { get; }
        public long PostId { get; }
        public long UserId { get; }
        public bool IsClaimed { get; private set; }

        public async Task HeartbeatNowAsync(CancellationToken ct = default)
            => await PostMetaAsync(new { _edit_lock = $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:{UserId}" }, ct);

        public async Task ReleaseNowAsync(CancellationToken ct = default)
        {
            StopTimer();
            await PostMetaAsync(new { _edit_lock = "" }, ct);
            IsClaimed = false;
        }

        public async ValueTask DisposeAsync()
        {
            try { await ReleaseNowAsync(_cts.Token); } catch { }
            _cts.Cancel();
        }

        internal async Task ClaimAsync(CancellationToken ct)
        {
            // advisory: detect existing lock
            try
            {
                var res = await _http.GetAsync($"/wp-json/wp/v2/{PostType}/{PostId}?context=edit&_fields=meta._edit_lock", ct);
                if (res.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
                    if (doc.RootElement.TryGetProperty("meta", out var meta) &&
                        meta.TryGetProperty("_edit_lock", out var le))
                    {
                        var raw = le.GetString();
                        if (!string.IsNullOrWhiteSpace(raw) && raw!.Contains(':'))
                        {
                            var parts = raw.Split(':');
                            if (long.TryParse(parts[1], out var uid) && uid != UserId)
                                _opt.OnForeignLockDetected?.Invoke(uid);
                        }
                    }
                }
            } catch { }

            await PostMetaAsync(new { _edit_lock = $"{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:{UserId}", _edit_last = UserId }, ct);
            IsClaimed = true;
        }

        internal void StartTimer()
        {
            if (_opt.HeartbeatInterval <= TimeSpan.Zero) return;
            _timer = new PeriodicTimer(_opt.HeartbeatInterval);
            _ = RunAsync();
        }

        private async Task RunAsync()
        {
            try
            {
                while (await _timer!.WaitForNextTickAsync(_cts.Token))
                {
                    try { await HeartbeatNowAsync(_cts.Token); }
                    catch { }
                }
            }
            catch (OperationCanceledException) { }
        }

        private void StopTimer() => _cts.Cancel();

        private async Task PostMetaAsync(object meta, CancellationToken ct)
        {
            var payload = new { meta };
            using var res = await _http.PostAsJsonAsync($"/wp-json/wp/v2/{PostType}/{PostId}", payload, cancellationToken: ct);
            res.EnsureSuccessStatusCode();
        }
    }
}
