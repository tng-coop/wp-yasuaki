using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Editor.Abstractions;

namespace Editor.WordPress.Http
{
    /// <summary>
    /// DelegatingHandler that applies per-request timeout (linked CTS),
    /// retries/backoff for transient errors, and maps errors to expected behaviors.
    /// Works in WASM (doesn't rely solely on HttpClient.Timeout).
    /// </summary>
    public sealed class PolicyHandler : DelegatingHandler
    {
        private readonly IRetryPolicy _policy;
        private readonly TimeSpan _perRequestTimeout;

        public PolicyHandler(IRetryPolicy policy, TimeSpan? perRequestTimeout = null)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _perRequestTimeout = perRequestTimeout ?? TimeSpan.FromSeconds(30);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var attempt = 0;

            while (true)
            {
                attempt++;
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linked.CancelAfter(_perRequestTimeout);

                try
                {
                    var rsp = await base.SendAsync(request, linked.Token).ConfigureAwait(false);

                    if ((int)rsp.StatusCode is >= 200 and < 300)
                        return rsp;

                    // -------- Non-success handling --------
                    var s = rsp.StatusCode;

                    // 1) Auth failures → throw (no retry)
                    if (s is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                        throw new AuthError(s);

                    // 2) Edit conflicts → throw (no retry)
                    if ((int)s == 412 || s == HttpStatusCode.Conflict)
                        throw new ConflictError(clientETag: null, serverETag: rsp.Headers.ETag?.Tag);

                    // 3) Rate limit → throw typed (policy may honor Retry-After)
                    if ((int)s == 429)
                    {
                        TimeSpan? ra = null;
                        var h = rsp.Headers.RetryAfter;
                        if (h?.Delta is { } d) ra = d; else if (h?.Date is { } when) ra = when - DateTimeOffset.UtcNow;
                        throw new RateLimited(ra);
                    }

                    // 4) Server errors → throw HttpRequestException (tests expect this)
                    if ((int)s >= 500)
                        throw new HttpRequestException($"Server error {(int)s} ({s})");

                    // 5) Other 4xx (e.g., 404/410) → PASS THROUGH so callers can handle idempotency / nulls
                    return rsp;
                }
                catch (OperationCanceledException oce) when (!ct.IsCancellationRequested)
                {
                    var mapped = new TimeoutError(inner: oce);
                    if (_policy.ShouldRetry(mapped, attempt, out var delay))
                    {
                        await Task.Delay(delay, ct).ConfigureAwait(false);
                        continue;
                    }
                    throw mapped;
                }
                catch (HttpRequestException hre)
                {
                    // Transport error (DNS reset, socket, etc.) → treat as transient
                    if (_policy.ShouldRetry(hre, attempt, out var delay))
                    {
                        await Task.Delay(delay, ct).ConfigureAwait(false);
                        continue;
                    }
                    throw;
                }
                catch (WpdiException wex)
                {
                    // Typed domain errors go through retry policy where applicable
                    if (_policy.ShouldRetry(wex, attempt, out var delay))
                    {
                        await Task.Delay(delay, ct).ConfigureAwait(false);
                        continue;
                    }
                    throw;
                }
            }
        }
    }
}
