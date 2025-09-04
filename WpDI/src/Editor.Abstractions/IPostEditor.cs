namespace Editor.Abstractions;

public interface IPostEditor
{
    Task<EditResult> CreateAsync(string title, string html, CancellationToken ct = default);
    Task<EditResult> UpdateAsync(long id, string html, CancellationToken ct = default);
}

public sealed record EditResult(long Id, string Url, string Status);
