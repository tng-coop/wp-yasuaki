using System.Collections.Generic;
using TG.Blazor.IndexedDB;

namespace BlazorWP.Data;

public sealed class IndexedDbLocalStore : ILocalStore
{
    private readonly IndexedDBManager _db;

    public IndexedDbLocalStore(IndexedDBManager db) => _db = db;

    public Task InitializeAsync() => _db.OpenDb();

    public async Task<T?> GetByKeyAsync<T>(string storeName, object key)
        => await _db.GetRecordById<T>(storeName, key);

    public async Task<IReadOnlyList<T>> GetAllAsync<T>(string storeName)
        => (await _db.GetRecords<T>(storeName)).AsReadOnly();

    public Task AddAsync<T>(string storeName, T item)
        => _db.AddRecord(new StoreRecord<T> { Storename = storeName, Data = item });

    public Task PutAsync<T>(string storeName, T item)
        => _db.UpdateRecord(new StoreRecord<T> { Storename = storeName, Data = item });

    public Task DeleteAsync(string storeName, object key)
        => _db.DeleteRecord(storeName, key);
}
