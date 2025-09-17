namespace Editor.WordPress;

public sealed class WordPressOptions
{
    public string BaseUrl { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string AppPassword { get; init; } = string.Empty;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
}
