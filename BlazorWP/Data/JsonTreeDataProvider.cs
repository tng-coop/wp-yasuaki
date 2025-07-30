using PanoramicData.Blazor.Interfaces;
using PanoramicData.Blazor.Models;

namespace BlazorWP.Data;

public class JsonTreeDataProvider : IDataProviderService<JsonTreeItem>
{
    private readonly List<JsonTreeItem> _items;

    public JsonTreeDataProvider(List<JsonTreeItem> items)
    {
        _items = items;
    }

    public Task<DataResponse<JsonTreeItem>> GetDataAsync(DataRequest<JsonTreeItem> request, CancellationToken cancellationToken)
        => Task.FromResult(new DataResponse<JsonTreeItem>(_items, _items.Count));

    public Task<OperationResponse> DeleteAsync(JsonTreeItem item, CancellationToken cancellationToken)
        => Task.FromResult(new OperationResponse { Success = true });

    public Task<OperationResponse> UpdateAsync(JsonTreeItem item, IDictionary<string, object?> delta, CancellationToken cancellationToken)
        => Task.FromResult(new OperationResponse { Success = true });

    public Task<OperationResponse> CreateAsync(JsonTreeItem item, CancellationToken cancellationToken)
        => Task.FromResult(new OperationResponse { Success = true });
}
