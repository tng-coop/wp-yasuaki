using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorWP;

public class JwtService
{
    private const string WpEndpointKey = "wpEndpoint";
    private const string SiteInfoKey = "siteinfo";

    public JwtService()
    {
    }

    private class JwtInfo
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Token { get; set; }
    }

    private static string GetJwtInfoKey(string endpoint) => endpoint;
    private static string GetOldJwtInfoKey(string endpoint) => $"jwtInfo:{endpoint}";
    private static string GetJwtTokenKey(string endpoint) => $"jwtToken:{endpoint}";

    private async Task<Dictionary<string, JwtInfo>> LoadSiteInfoAsync()
    {
        var json = BrowserStorage.GetItem(SiteInfoKey);
        if (string.IsNullOrEmpty(json))
        {
            return new();
        }
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JwtInfo>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<List<string>> GetSiteInfoKeysAsync()
    {
        var data = await LoadSiteInfoAsync();
        return data.Keys.OrderBy(k => k).ToList();
    }

    private Task SaveSiteInfoAsync(Dictionary<string, JwtInfo> data)
    {
        var json = JsonSerializer.Serialize(data);
        BrowserStorage.SetItem(SiteInfoKey, json);
        return Task.CompletedTask;
    }

    private async Task<JwtInfo?> LoadJwtInfoAsync(string endpoint)
    {
        var data = await LoadSiteInfoAsync();
        if (data.TryGetValue(endpoint, out var info))
        {
            return info;
        }

        var key = GetJwtInfoKey(endpoint);
        var json = BrowserStorage.GetItem(key);
        if (string.IsNullOrEmpty(json))
        {
            var oldKey = GetOldJwtInfoKey(endpoint);
            json = BrowserStorage.GetItem(oldKey);
        }

        if (string.IsNullOrEmpty(json))
        {
            var token = BrowserStorage.GetItem(GetJwtTokenKey(endpoint));
            if (!string.IsNullOrEmpty(token))
            {
                info = new JwtInfo { Token = token };
                data[endpoint] = info;
                await SaveSiteInfoAsync(data);
                return info;
            }
            return null;
        }

        try
        {
            info = JsonSerializer.Deserialize<JwtInfo>(json);
        }
        catch
        {
            info = new JwtInfo { Token = json };
        }

        if (info != null)
        {
            data[endpoint] = info;
            await SaveSiteInfoAsync(data);
        }

        return info;
    }

    public async Task<string?> GetCurrentJwtAsync()
    {
        var endpoint = BrowserStorage.GetItem(WpEndpointKey);
        if (string.IsNullOrEmpty(endpoint))
        {
            return null;
        }

        var info = await LoadJwtInfoAsync(endpoint);
        var token = info?.Token;

        if (!string.IsNullOrEmpty(token))
        {
            BrowserStorage.SetItem("jwtToken", token);
        }
        else
        {
            BrowserStorage.DeleteItem("jwtToken");
        }

        return token;
    }

    public async Task<List<string>> GetCurrentUserRolesAsync()
    {
        var token = await GetCurrentJwtAsync();
        var roles = new List<string>();
        if (string.IsNullOrEmpty(token))
        {
            return roles;
        }

        var parts = token.Split('.');
        if (parts.Length < 2)
        {
            return roles;
        }

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }

        try
        {
            var bytes = Convert.FromBase64String(payload);
            using var doc = JsonDocument.Parse(bytes);
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("user", out var user))
            {
                if (user.TryGetProperty("roles", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in arr.EnumerateArray())
                    {
                        var r = el.GetString();
                        if (!string.IsNullOrEmpty(r))
                        {
                            roles.Add(r);
                        }
                    }
                }
                else if (user.TryGetProperty("role", out var single) && single.ValueKind == JsonValueKind.String)
                {
                    var r = single.GetString();
                    if (!string.IsNullOrEmpty(r))
                    {
                        roles.Add(r);
                    }
                }
            }
        }
        catch
        {
            // ignore parse errors
        }

        return roles;
    }
}
