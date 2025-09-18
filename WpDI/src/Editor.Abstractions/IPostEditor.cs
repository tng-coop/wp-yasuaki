// Editor.Abstractions/IPostEditor.cs
namespace Editor.Abstractions;

public interface IPostEditor
{
    Task<EditResult> CreateAsync(string title, string html, CancellationToken ct = default);
    Task<EditResult> UpdateAsync(long id, string html, string lastSeenModifiedUtc, CancellationToken ct = default);

    // NEW: read server-side modified time (for LWW preflight) via WPDI
    Task<string?> GetLastModifiedUtcAsync(long id, CancellationToken ct = default);

    // NEW: set status (draft/pending/publish…) via WPDI
    Task<EditResult> SetStatusAsync(long id, string status, CancellationToken ct = default);

    // NEW: delete via WPDI (force = true for hard delete)
    Task DeleteAsync(long id, bool force = true, CancellationToken ct = default);
}
