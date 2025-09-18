using System.Collections.Generic;
using System.Reflection;               // for key discovery via reflection
using TG.Blazor.IndexedDB;

namespace BlazorWP.Data;

public sealed class IndexedDbLocalStore : ILocalStore
{
    private readonly IndexedDBManager _db;
    private readonly Lazy<Task> _init;

    public IndexedDbLocalStore(IndexedDBManager db)
    {
        _db = db;
        // Open the DB once on first use so callers don't need to remember to init.
        _init = new Lazy<Task>(() => _db.OpenDb());
    }

    private Task EnsureReady() => _init.Value;

    // Parity with interface; harmless when called directly.
    public Task InitializeAsync() => EnsureReady();

    public async Task<T?> GetByKeyAsync<T>(string storeName, object key)
    {
        await EnsureReady();
        // 1.5.0-preview signature
        return await _db.GetRecordById<object, T>(storeName, key);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync<T>(string storeName)
    {
        await EnsureReady();
        var items = await _db.GetRecords<T>(storeName);
        return (items ?? new List<T>()).AsReadOnly();
    }

    public async Task AddAsync<T>(string storeName, T item)
    {
        await EnsureReady();
        await _db.AddRecord(new StoreRecord<T> { Storename = storeName, Data = item! });
    }

    public async Task PutAsync<T>(string storeName, T item)
    {
        await EnsureReady();

        // Manual "upsert" for 1.5.0-preview (no PutRecord API in this version)
        if (!TryGetItemKey(item, out var key) || key is null)
        {
            // No natural key → behave like Add
            await _db.AddRecord(new StoreRecord<T> { Storename = storeName, Data = item! });
            return;
        }

        var existing = await _db.GetRecordById<object, T>(storeName, key);
        if (existing is null)
        {
            // Insert
            await _db.AddRecord(new StoreRecord<T> { Storename = storeName, Data = item! });
        }
        else
        {
            // Update
            await _db.UpdateRecord(new StoreRecord<T> { Storename = storeName, Data = item! });
        }
    }

    public async Task DeleteAsync(string storeName, object key)
    {
        await EnsureReady();
        await _db.DeleteRecord(storeName, key);
    }

    // ----------------- helpers -----------------

    private static readonly string[] KeyPropNames = { "Id", "ID", "Key", "key", "id" };

    private static bool TryGetItemKey<T>(T item, out object? key)
    {
        key = null;
        if (item is null) return false;

        var type = item.GetType();
        var prop = KeyPropNames
            .Select(n => type.GetProperty(n, BindingFlags.Instance | BindingFlags.Public))
            .FirstOrDefault(p => p is not null && p.CanRead);

        if (prop is null) return false;

        key = prop.GetValue(item, null);
        return key is not null;
    }
}
