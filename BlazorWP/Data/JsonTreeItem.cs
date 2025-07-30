namespace BlazorWP.Data;

public class JsonTreeItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public bool IsLeaf { get; set; }
    public string Display => IsLeaf && Value != null ? $"{Name}: {Value}" : Name;
}
