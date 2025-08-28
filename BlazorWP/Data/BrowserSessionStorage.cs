using System.Runtime.InteropServices.JavaScript;

namespace BlazorWP;

public static partial class BrowserSessionStorage
{
    [JSImport("sessionStorage.getItem", "globalThis")]
    public static partial string? GetItem(string key);

    [JSImport("sessionStorage.setItem", "globalThis")]
    public static partial void SetItem(string key, string value);

    [JSImport("sessionStorage.removeItem", "globalThis")]
    public static partial void RemoveItem(string key);
}
