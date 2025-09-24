using System.Text.Json;

internal static class JsonElementExtensions
{
    // Works when you have: JsonElement meta
    public static JsonElement? GetPropertyOrDefault(this JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        return element.TryGetProperty(name, out var v) ? v : (JsonElement?)null;
    }

    // Works when you have: JsonElement? meta
    public static JsonElement? GetPropertyOrDefault(this JsonElement? element, string name)
    {
        if (element is null) return null;
        var e = element.Value;
        if (e.ValueKind != JsonValueKind.Object) return null;
        return e.TryGetProperty(name, out var v) ? v : (JsonElement?)null;
    }
}
