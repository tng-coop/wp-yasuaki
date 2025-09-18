namespace BlazorWP.Data;

public interface ILocalStore
{
    Task InitializeAsync();
    Task<T?> GetByKeyAsync<T>(string storeName, object key);
    Task<IReadOnlyList<T>> GetAllAsync<T>(string storeName);
    Task AddAsync<T>(string storeName, T item);
    Task PutAsync<T>(string storeName, T item);
    Task DeleteAsync(string storeName, object key);
}
