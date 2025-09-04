namespace Editor.WordPress;

public sealed class WordPressOptions
{
    public required string BaseUrl { get; init; }
    public required string UserName { get; init; }
    public required string AppPassword { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
}
