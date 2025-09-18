using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;

namespace TestSupport;

// Non-destructive, thread-safe unique-name provider.
public sealed class RunUniqueFixture
{
    private int _counter = 0;

    public string RunId { get; } = BuildRunId();

    private static string BuildRunId()
    {
        var ts  = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
        var pid = Process.GetCurrentProcess().Id;
        Span<byte> rnd = stackalloc byte[2];
        RandomNumberGenerator.Fill(rnd);
        var rand = Convert.ToHexString(rnd).ToLowerInvariant();
        return $"{ts}-{pid}-{rand}";
    }

    public string Next(string prefix = "Test")
    {
        var serial = Interlocked.Increment(ref _counter);
        return $"{prefix}-{RunId}-{serial:D4}";
    }
}
