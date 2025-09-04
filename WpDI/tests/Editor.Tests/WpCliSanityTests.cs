using System.Diagnostics;
using System.Text.RegularExpressions;
using FluentAssertions;

public class WpCliSanityTests
{
    private static (int ExitCode, string StdOut, string StdErr) Run(string fileName, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(10_000); // 10s timeout

        return (p.ExitCode, stdout, stderr);
    }

    private static string ResolveWpPath()
    {
        var env = Environment.GetEnvironmentVariable("WP_CLI");
        if (!string.IsNullOrWhiteSpace(env))
            return env!;

        // Fallback to PATH
        return "wp";
    }

    [Fact]
    public void WpCli_Is_Reachable_And_Reports_Version()
    {
        var wp = ResolveWpPath();

        // 1) Check version first — does not require a WP install and is fast
        var (codeVer, outVer, errVer) = Run(wp, "--version");
        codeVer.Should().Be(0, $"wp --version should succeed but stderr was: {errVer}");
        outVer.Should().MatchRegex(new Regex(@"WP-CLI\s+\d+\.\d+(\.\d+)?", RegexOptions.IgnoreCase));

        // 2) Optional: info command — also lightweight
        var (codeInfo, outInfo, errInfo) = Run(wp, "--info");
        codeInfo.Should().Be(0, $"wp --info should succeed but stderr was: {errInfo}");
        outInfo.Should().ContainAny("WP-CLI root dir", "PHP binary", "OS:", "Shell:");
        
        // For visibility in test logs
        Console.WriteLine($"WP_CLI used: {wp}");
        Console.WriteLine(outVer.Trim());
    }
}

