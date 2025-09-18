using System;

namespace Editor.Abstractions
{
    public sealed class NoRetryPolicy : IRetryPolicy
    {
        public bool ShouldRetry(Exception error, int attempt, out TimeSpan delay)
        { delay = TimeSpan.Zero; return false; }
    }

    public sealed class ProdRetryPolicy : IRetryPolicy
    {
        private readonly int _maxAttempts;
        private readonly TimeSpan _baseDelay;
        private readonly Random _rng = new();

        public ProdRetryPolicy(int maxAttempts = 3, TimeSpan? baseDelay = null)
        { _maxAttempts = Math.Max(1, maxAttempts); _baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(250); }

        public bool ShouldRetry(Exception error, int attempt, out TimeSpan delay)
        {
            // Only retry for transient network/5xx or ratelimit.
            var isTransient = error is TransientHttpError || error is System.Net.Http.HttpRequestException;
            var isRateLimit = error is RateLimited;
            if (!(isTransient || isRateLimit) || attempt >= _maxAttempts)
            { delay = TimeSpan.Zero; return false; }

            var expo = Math.Pow(2, attempt - 1);
            var baseMs = _baseDelay.TotalMilliseconds * expo;
            var jitter = _rng.NextDouble() * baseMs * 0.2; // ±20%
            var total = TimeSpan.FromMilliseconds(baseMs + jitter);

            if (isRateLimit && error is RateLimited rl && rl.RetryAfter is { } ra)
                total = ra; // honor Retry-After

            delay = total;
            return true;
        }
    }
}
