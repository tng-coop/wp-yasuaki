namespace BlazorWP.Data;

public class JsonTreeNode
{
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public bool IsLeaf { get; set; }
    public List<JsonTreeNode> Children { get; set; } = new();

    public string Display => IsLeaf && Value != null ? $"{Name}: {Value}" : Name;
}
