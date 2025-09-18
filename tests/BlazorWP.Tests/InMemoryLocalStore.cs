using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BlazorWP.Data;

namespace BlazorWP.Tests;

public sealed class InMemoryLocalStore : ILocalStore
{
    // storeName -> list of (Key, Value)
    private readonly Dictionary<string, List<(object Key, object? Value)>> _stores =
        new(StringComparer.Ordinal);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task<T?> GetByKeyAsync<T>(string storeName, object key)
    {
        if (_stores.TryGetValue(storeName, out var list))
        {
            var found = list.FirstOrDefault(p => Equals(p.Key, key)).Value;
            return Task.FromResult(found is T t ? t : default(T?));
        }
        return Task.FromResult<T?>(default);
    }

    public Task<IReadOnlyList<T>> GetAllAsync<T>(string storeName)
    {
        var list = _stores.TryGetValue(storeName, out var pairs)
            ? pairs.Select(p => p.Value).OfType<T>().ToList()
            : new List<T>();

        return Task.FromResult<IReadOnlyList<T>>(list);
    }

    public Task AddAsync<T>(string storeName, T item)
    {
        if (!_stores.TryGetValue(storeName, out var list))
        {
            list = new List<(object Key, object? Value)>();
            _stores[storeName] = list;
        }

        // Prefer an item's own Id/ID/Key property if it exists; else use a GUID (like auto key).
        var key = TryGetItemKey(item, out var itemKey) ? itemKey! : Guid.NewGuid();
        list.Add((key, item));
        return Task.CompletedTask;
    }

    public Task PutAsync<T>(string storeName, T item)
    {
        if (!_stores.TryGetValue(storeName, out var list))
        {
            list = new List<(object Key, object? Value)>();
            _stores[storeName] = list;
        }

        // Upsert by natural key if available; otherwise behave like Add (simple, predictable).
        if (TryGetItemKey(item, out var itemKey))
        {
            var idx = list.FindIndex(p => Equals(p.Key, itemKey));
            if (idx >= 0) list[idx] = (itemKey!, item);
            else list.Add((itemKey!, item));
        }
        else
        {
            list.Add((Guid.NewGuid(), item));
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string storeName, object key)
    {
        if (_stores.TryGetValue(storeName, out var list))
        {
            var idx = list.FindIndex(p => Equals(p.Key, key));
            if (idx >= 0) list.RemoveAt(idx);
        }
        return Task.CompletedTask;
    }

    // --- helpers ---

    private static readonly string[] KeyPropNames = { "Id", "ID", "Key", "key", "id" };


    private static bool TryGetItemKey<T>(T item, out object? key)
    {
        key = null;
        if (item is null) return false;

        // Look for a public readable property named Id/ID/Key
        var type = item.GetType();
        var prop = KeyPropNames
            .Select(n => type.GetProperty(n, BindingFlags.Instance | BindingFlags.Public))
            .FirstOrDefault(p => p is not null && p.CanRead);

        if (prop is null) return false;

        key = prop.GetValue(item, null);
        return key is not null;
    }
}
