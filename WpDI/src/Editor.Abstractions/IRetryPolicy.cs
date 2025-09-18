using System;

namespace Editor.Abstractions
{
    public interface IRetryPolicy
    {
        /// <summary>
        /// Return true if the operation should be retried. Provide a delay suggestion.
        /// attempt is 1-based.
        /// </summary>
        bool ShouldRetry(Exception error, int attempt, out TimeSpan delay);
    }
}
