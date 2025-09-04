using System.Collections.Generic;
using System.Linq;
using BlazorWP.Data;

namespace BlazorWP.Tests;

public sealed class InMemoryLocalStore : ILocalStore
{
    private readonly Dictionary<string, List<object>> _stores = new(StringComparer.Ordinal);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task<T?> GetByKeyAsync<T>(string storeName, object key)
    {
        var list = _stores.GetValueOrDefault(storeName) ?? [];
        var match = list.Cast<T>().FirstOrDefault();
        return Task.FromResult(match);
    }

    public Task<IReadOnlyList<T>> GetAllAsync<T>(string storeName)
    {
        var list = _stores.GetValueOrDefault(storeName)?.Cast<T>().ToList() ?? new List<T>();
        return Task.FromResult<IReadOnlyList<T>>(list);
    }

    public Task AddAsync<T>(string storeName, T item)
    {
        if (!_stores.TryGetValue(storeName, out var list))
        {
            list = new List<object>();
            _stores[storeName] = list;
        }
        list.Add(item!);
        return Task.CompletedTask;
    }

    public Task PutAsync<T>(string storeName, T item) => AddAsync(storeName, item);

    public Task DeleteAsync(string storeName, object key) => Task.CompletedTask;
}
