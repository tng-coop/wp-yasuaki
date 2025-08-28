using System.Runtime.InteropServices.JavaScript;

namespace BlazorWP;

public static partial class BrowserStorage
{
    [JSImport("localStorage.getItem", "globalThis")]
    public static partial string? GetItem(string key);

    [JSImport("localStorage.setItem", "globalThis")]
    public static partial void SetItem(string key, string value);

    [JSImport("localStorage.removeItem", "globalThis")]
    public static partial void RemoveItem(string key);

    [JSImport("localStorage.clear", "globalThis")]
    public static partial void Clear();

    [JSImport("keys", "./js/storageUtils.js")]
    public static partial string[] Keys();

    [JSImport("itemInfo", "./js/storageUtils.js")]
    public static partial LocalStorageItemInfo ItemInfo(string key);

    [JSImport("deleteItem", "./js/storageUtils.js")]
    public static partial void DeleteItem(string key);

    public class LocalStorageItemInfo
    {
        public string? Value { get; set; }
        public string? LastUpdated { get; set; }
    }
}
