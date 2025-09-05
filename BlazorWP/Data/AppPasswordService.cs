using System.Threading.Tasks;

namespace BlazorWP;

public sealed class AppPasswordService
{
    private readonly LocalStorageJsInterop _storage;
    private const string KeyUser = "app_user";
    private const string KeyPass = "app_pass";

    public AppPasswordService(LocalStorageJsInterop storage) => _storage = storage;

    public async Task SetAsync(string username, string appPassword)
    {
        await _storage.SetItemAsync(KeyUser, username);
        await _storage.SetItemAsync(KeyPass, appPassword);
    }

    public async Task<(string Username, string AppPassword)?> GetAsync()
    {
        var u = await _storage.GetItemAsync(KeyUser);
        var p = await _storage.GetItemAsync(KeyPass);
        return string.IsNullOrWhiteSpace(u) || string.IsNullOrWhiteSpace(p) ? null : (u!, p!);
    }

    public Task ClearAsync() => Task.WhenAll(
        _storage.DeleteAsync(KeyUser).AsTask(),
        _storage.DeleteAsync(KeyPass).AsTask()
    );

    public async Task<bool> HasAsync() => (await GetAsync()) is not null;
}
