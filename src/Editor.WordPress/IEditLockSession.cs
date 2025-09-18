namespace Editor.WordPress;

public interface IEditLockSession : IAsyncDisposable
{
    string PostType { get; }
    long PostId { get; }
    long UserId { get; }

    bool IsClaimed { get; }

    Task HeartbeatNowAsync(CancellationToken ct = default);
    Task ReleaseNowAsync(CancellationToken ct = default);
}
