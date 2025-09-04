using System.Diagnostics;
using FluentAssertions;
using Xunit;
using System.Text.RegularExpressions;

[CollectionDefinition("WP EndToEnd", DisableParallelization = true)]
public class WpEndToEndCollection : ICollectionFixture<WpCliCleanupFixture> { }

public sealed class WpCliCleanupFixture : IAsyncLifetime
{
    private static string ResolveWpPath()
        => Environment.GetEnvironmentVariable("WP_CLI")?.Trim() switch
        {
            { Length: > 0 } s => s,
            _ => "wp"
        };

    private static async Task<(int Code, string Out, string Err)> RunAsync(string file, string args, int timeoutMs = 15000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var p = Process.Start(psi)!;

        var stdOutTask = p.StandardOutput.ReadToEndAsync();
        var stdErrTask = p.StandardError.ReadToEndAsync();

        if (!p.WaitForExit(timeoutMs))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw new TimeoutException($"Command timed out: {file} {args}");
        }

        var stdout = await stdOutTask;
        var stderr = await stdErrTask;
        return (p.ExitCode, stdout, stderr);
    }

    private static async Task<int> GetAdminIdAsync(string wp)
    {
        var fromEnv = Environment.GetEnvironmentVariable("WP_ADMIN_ID");
        if (int.TryParse(fromEnv, out var envId) && envId > 0) return envId;

        var (code, output, err) = await RunAsync(wp, "user list --role=administrator --field=ID --format=ids");
        code.Should().Be(0, $"wp user list (admins) should succeed. stderr: {err}");

        var first = output.Split(new[] { ' ', '\n', '\r', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries)
                          .FirstOrDefault();
        int.TryParse(first, out var adminId).Should().BeTrue("must have at least one admin user");
        adminId.Should().BeGreaterThan(0);
        return adminId;
    }

    private static async Task DeleteNonAdminsAsync(string wp, int adminId)
    {
        var (codeList, ids, errList) = await RunAsync(wp, "user list --field=ID --role__not_in=administrator --format=ids");
        codeList.Should().Be(0, $"wp user list (non-admins) should succeed. stderr: {errList}");

        ids = (ids ?? "").Trim();
        if (string.IsNullOrEmpty(ids))
        {
            Console.WriteLine("WP-CLI cleanup: no non-admin users to delete.");
            return;
        }

        Console.WriteLine($"WP-CLI cleanup: deleting non-admin users: {ids}");
        var (codeDel, _, errDel) = await RunAsync(wp, $"user delete {ids} --reassign={adminId} --yes", timeoutMs: 30000);
        codeDel.Should().Be(0, $"wp user delete should succeed. stderr: {errDel}");
    }

    public async Task InitializeAsync()
    {
        var wp = ResolveWpPath();

        // Sanity check wp-cli is available
        var (verCode, verOut, verErr) = await RunAsync(wp, "--version");
        verCode.Should().Be(0, $"wp --version must work before tests. stderr: {verErr}");
        Console.WriteLine($"Using WP-CLI: {verOut.Trim()}");

        var adminId = await GetAdminIdAsync(wp);
        Console.WriteLine($"WP-CLI cleanup: adminId = {adminId}");
        await DeleteNonAdminsAsync(wp, adminId);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

