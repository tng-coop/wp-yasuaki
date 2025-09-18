namespace Editor.WordPress;

public interface IEditLockService
{
    /// <summary>
    /// Open a session: claim lock, start heartbeats, release on dispose.
    /// </summary>
    Task<IEditLockSession> OpenAsync(
        string postType, long postId, long userId,
        EditLockOptions? options = null,
        CancellationToken ct = default);
}
