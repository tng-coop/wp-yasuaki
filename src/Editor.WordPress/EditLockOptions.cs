namespace Editor.WordPress;

public sealed class EditLockOptions
{
    /// <summary>Heartbeat interval (default for production).</summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Optional callback if another user is already locking.</summary>
    public Action<long /*userId*/>? OnForeignLockDetected { get; init; }
}
