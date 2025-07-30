using PanoramicData.Blazor.Interfaces;
using PanoramicData.Blazor.Models;

namespace BlazorWP.Data;

public class TreeDataProvider : IDataProviderService<TreeItem>
{
    private readonly List<TreeItem> _items = new();

    public TreeDataProvider()
    {
        // Root folder
        _items.Add(new TreeItem { Id = 1, Name = "Company", ParentId = null, IsGroup = true, Order = 1 });

        // Sales hierarchy
        _items.Add(new TreeItem { Id = 2, Name = "Sales", ParentId = 1, IsGroup = true, Order = 1 });
        _items.Add(new TreeItem { Id = 5, Name = "Domestic", ParentId = 2, IsGroup = true, Order = 1 });
        _items.Add(new TreeItem { Id = 11, Name = "Alice", ParentId = 5, Order = 1 });
        _items.Add(new TreeItem { Id = 16, Name = "Fred", ParentId = 5, Order = 2 });
        _items.Add(new TreeItem { Id = 14, Name = "Dave", ParentId = 5, Order = 3 });
        _items.Add(new TreeItem { Id = 6, Name = "International", ParentId = 2, IsGroup = true, Order = 2 });
        _items.Add(new TreeItem { Id = 21, Name = "Kevin", ParentId = 6, Order = 1 });
        _items.Add(new TreeItem { Id = 22, Name = "Jane", ParentId = 6, Order = 2 });

        // Marketing hierarchy
        _items.Add(new TreeItem { Id = 3, Name = "Marketing", ParentId = 1, IsGroup = true, Order = 2 });
        _items.Add(new TreeItem { Id = 7, Name = "Digital", ParentId = 3, IsGroup = true, Order = 1 });
        _items.Add(new TreeItem { Id = 12, Name = "Bob", ParentId = 7, Order = 1 });
        _items.Add(new TreeItem { Id = 13, Name = "Carol", ParentId = 7, Order = 2 });
        _items.Add(new TreeItem { Id = 18, Name = "Harry", ParentId = 7, Order = 3 });
        _items.Add(new TreeItem { Id = 8, Name = "Print", ParentId = 3, IsGroup = true, Order = 2 });
        _items.Add(new TreeItem { Id = 23, Name = "Mary", ParentId = 8, Order = 1 });

        // Finance hierarchy
        _items.Add(new TreeItem { Id = 4, Name = "Finance", ParentId = 1, IsGroup = true, Order = 3 });
        _items.Add(new TreeItem { Id = 9, Name = "Accounts", ParentId = 4, IsGroup = true, Order = 1 });
        _items.Add(new TreeItem { Id = 15, Name = "Emma", ParentId = 9, Order = 1 });
        _items.Add(new TreeItem { Id = 17, Name = "Gina", ParentId = 9, Order = 2 });
        _items.Add(new TreeItem { Id = 24, Name = "Ollie", ParentId = 9, Order = 3 });
        _items.Add(new TreeItem { Id = 10, Name = "Payroll", ParentId = 4, IsGroup = true, Order = 2 });
        _items.Add(new TreeItem { Id = 19, Name = "Ian", ParentId = 10, Order = 1 });
        _items.Add(new TreeItem { Id = 20, Name = "Janet", ParentId = 10, Order = 2 });
    }

    public Task<DataResponse<TreeItem>> GetDataAsync(DataRequest<TreeItem> request, CancellationToken cancellationToken)
        => Task.FromResult(new DataResponse<TreeItem>(_items, _items.Count));

    public Task<OperationResponse> DeleteAsync(TreeItem item, CancellationToken cancellationToken)
        => Task.FromResult(new OperationResponse { Success = true });

    public Task<OperationResponse> UpdateAsync(TreeItem item, IDictionary<string, object?> delta, CancellationToken cancellationToken)
    {
        var existing = _items.FirstOrDefault(x => x.Id == item.Id);
        if (existing != null)
        {
            existing.ParentId = item.ParentId;
            existing.Order = item.Order;
            existing.Name = item.Name;
            existing.IsGroup = item.IsGroup;
        }

        return Task.FromResult(new OperationResponse { Success = true });
    }

    public Task<OperationResponse> CreateAsync(TreeItem item, CancellationToken cancellationToken)
        => Task.FromResult(new OperationResponse { Success = true });
}
