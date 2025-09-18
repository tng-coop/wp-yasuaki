// WpDI/src/Editor.Abstractions/IPostEditor.cs
namespace Editor.Abstractions;

public interface IPostEditor
{
    Task<EditResult> CreateAsync(string title, string html, CancellationToken ct = default);
    Task<EditResult> UpdateAsync(long id, string html, string lastSeenModifiedUtc, CancellationToken ct = default);

    // read server-side modified time (for LWW preflight)
    Task<string?> GetLastModifiedUtcAsync(long id, CancellationToken ct = default);

    // set status (draft/pending/publish…)
    Task<EditResult> SetStatusAsync(long id, string status, CancellationToken ct = default);

    // delete (force = true for hard delete)
    Task DeleteAsync(long id, bool force = true, CancellationToken ct = default);
}
