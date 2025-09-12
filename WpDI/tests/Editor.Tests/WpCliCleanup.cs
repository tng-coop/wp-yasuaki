using System.Diagnostics;
using Xunit;

[CollectionDefinition("WP EndToEnd", DisableParallelization = true)]
public class WpEndToEndCollection : ICollectionFixture<WpCliCleanupFixture> { }

public sealed class WpCliCleanupFixture : IAsyncLifetime
{
    private static string ResolveWpExe()
        => Environment.GetEnvironmentVariable("WP_CLI")?.Trim() is { Length: > 0 } s ? s : "wp";

    private static string? ResolveWpRoot()
        => Environment.GetEnvironmentVariable("WP_PATH")?.Trim();

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

    private static Task<(int Code, string Out, string Err)> RunWpAsync(string wp, string wpPath, string args, int timeoutMs = 15000)
        => RunAsync(wp, $"--path=\"{wpPath}\" {args}", timeoutMs);

    private static async Task<int> GetAdminIdAsync(string wp, string wpPath)
    {
        var fromEnv = Environment.GetEnvironmentVariable("WP_ADMIN_ID");
        if (int.TryParse(fromEnv, out var envId) && envId > 0) return envId;

        var (code, output, err) = await RunWpAsync(wp, wpPath, "user list --role=administrator --field=ID --format=ids");
        Assert.True(code == 0, $"wp user list (admins) should succeed. stderr: {err}");

        var first = output.Split(new[] { ' ', '\n', '\r', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries)
                          .FirstOrDefault();
        Assert.True(int.TryParse(first, out var adminId), "must have at least one admin user");
        Assert.True(adminId > 0);
        return adminId;
    }

    private static async Task DeleteAllPostsAsync(string wp, string wpPath)
    {
        var (codeList, ids, errList) = await RunWpAsync(
            wp, wpPath,
            "post list --post_type=any --post_status=any --field=ID --format=ids",
            timeoutMs: 30000);

        Assert.True(codeList == 0, $"wp post list should succeed. stderr: {errList}");

        ids = (ids ?? "").Trim();
        if (string.IsNullOrEmpty(ids))
        {
            Console.WriteLine("WP-CLI cleanup: no posts to delete.");
            return;
        }

        var allIds = ids.Split(new[] { ' ', '\n', '\r', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
        Console.WriteLine($"WP-CLI cleanup: deleting {allIds.Length} posts...");

        const int batchSize = 200;
        for (int i = 0; i < allIds.Length; i += batchSize)
        {
            var batch = string.Join(" ", allIds.Skip(i).Take(batchSize));
            var (codeDel, _, errDel) = await RunWpAsync(
                wp, wpPath,
                $"post delete {batch} --force",
                timeoutMs: 60000);

            Assert.True(codeDel == 0, $"wp post delete should succeed. stderr: {errDel}");
        }
    }

    private static async Task DeleteNonAdminsAsync(string wp, string wpPath)
    {
        var (codeList, ids, errList) = await RunWpAsync(
            wp, wpPath,
            "user list --field=ID --role__not_in=administrator --format=ids");

        Assert.True(codeList == 0, $"wp user list (non-admins) should succeed. stderr: {errList}");

        ids = (ids ?? "").Trim();
        if (string.IsNullOrEmpty(ids))
        {
            Console.WriteLine("WP-CLI cleanup: no non-admin users to delete.");
            return;
        }

        Console.WriteLine($"WP-CLI cleanup: deleting non-admin users (and their content): {ids}");
        var (codeDel, _, errDel) = await RunWpAsync(
            wp, wpPath,
            $"user delete {ids} --yes",
            timeoutMs: 60000);

        Assert.True(codeDel == 0, $"wp user delete should succeed. stderr: {errDel}");
    }

    public async Task InitializeAsync()
    {
        var wp = ResolveWpExe();
        var wpPath = ResolveWpRoot();
        if (wpPath is null)
        {
            Console.WriteLine("WP_PATH not set, skipping WP-CLI cleanup.");
            return;
        }

        var (verCode, verOut, verErr) = await RunWpAsync(wp, wpPath, "--version");
        Assert.True(verCode == 0, $"wp --version must work. stderr: {verErr}");
        Console.WriteLine($"Using WP-CLI: {verOut.Trim()}");

        // 1) Delete all posts first
        await DeleteAllPostsAsync(wp, wpPath);

        // 2) Delete all non-admin users (and their content)
        await DeleteNonAdminsAsync(wp, wpPath);

        // 3) Sanity: ensure at least one admin exists
        var _ = await GetAdminIdAsync(wp, wpPath);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
