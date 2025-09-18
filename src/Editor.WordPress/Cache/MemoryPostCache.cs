using System.Collections.Concurrent;
using System.Collections.Immutable;
using Editor.Abstractions;

namespace Editor.WordPress;

public sealed class MemoryPostCache : IPostCache
{
    private readonly ConcurrentDictionary<(string scope,int page), CachePage> _pages = new();
    private readonly ConcurrentDictionary<string, ImmutableDictionary<long, PostSummary>> _indexes = new();

    public Task<CachePage?> GetPageAsync(string scopeKey, int page, CancellationToken ct = default)
    {
        _pages.TryGetValue((scopeKey,page), out var pageVal);
        return Task.FromResult<CachePage?>(pageVal);
    }

    public Task UpsertPageAsync(string scopeKey, CachePage page, CancellationToken ct = default)
    {
        _pages[(scopeKey,page.Page)] = page;
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<IReadOnlyList<PostSummary>> ReadAllPagesAsync(string scopeKey, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var keys = _pages.Keys.Where(k => k.scope == scopeKey).Select(k=>k.page).OrderBy(x=>x);
        foreach (var p in keys)
        {
            if (ct.IsCancellationRequested) yield break;
            yield return _pages[(scopeKey,p)].Items;
            await Task.Yield();
        }
    }

    public Task<HashSet<long>> GetAllKnownIdsAsync(string scopeKey, CancellationToken ct = default)
    {
        if (_indexes.TryGetValue(scopeKey, out var idx))
            return Task.FromResult(idx.Keys.ToHashSet());
        return Task.FromResult(new HashSet<long>());
    }

    public Task UpsertIndexAsync(string scopeKey, IEnumerable<PostSummary> items, CancellationToken ct = default)
    {
        var map = _indexes.GetOrAdd(scopeKey, ImmutableDictionary<long,PostSummary>.Empty);
        foreach (var p in items)
            map = map.SetItem(p.Id, p);
        _indexes[scopeKey] = map;
        return Task.CompletedTask;
    }

    public Task RemoveFromIndexAsync(string scopeKey, IEnumerable<long> ids, CancellationToken ct = default)
    {
        if (_indexes.TryGetValue(scopeKey, out var map))
        {
            foreach (var id in ids) map = map.Remove(id);
            _indexes[scopeKey] = map;
        }
        return Task.CompletedTask;
    }
}
