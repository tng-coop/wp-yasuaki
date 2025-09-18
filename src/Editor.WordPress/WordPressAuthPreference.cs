namespace Editor.WordPress;

public sealed record WordPressAuthPreference
{
    private WordPressAuthPreference(
        WordPressAuthMode mode,
        WordPressBasicCredentials? credentials,
        Func<CancellationToken, ValueTask<string?>>? nonceFactory)
    {
        Mode = mode;
        BasicCredentials = credentials;
        NonceFactory = nonceFactory;
    }

    public WordPressAuthMode Mode { get; }
    public WordPressBasicCredentials? BasicCredentials { get; }
    public Func<CancellationToken, ValueTask<string?>>? NonceFactory { get; }

    public static WordPressAuthPreference None { get; } = new(WordPressAuthMode.None, null, null);

    public static WordPressAuthPreference AppPassword(string username, string appPassword)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username must be provided for AppPassword auth.", nameof(username));
        if (string.IsNullOrWhiteSpace(appPassword))
            throw new ArgumentException("AppPassword must be provided for AppPassword auth.", nameof(appPassword));

        return new WordPressAuthPreference(
            WordPressAuthMode.AppPassword,
            new WordPressBasicCredentials(username, appPassword),
            nonceFactory: null);
    }

    public static WordPressAuthPreference Nonce(Func<CancellationToken, ValueTask<string?>> nonceFactory)
    {
        if (nonceFactory is null)
            throw new ArgumentNullException(nameof(nonceFactory));

        return new WordPressAuthPreference(WordPressAuthMode.Nonce, null, nonceFactory);
    }

    public static WordPressAuthPreference Nonce(Func<ValueTask<string?>> nonceFactory)
    {
        if (nonceFactory is null)
            throw new ArgumentNullException(nameof(nonceFactory));

        return Nonce(_ => nonceFactory());
    }
}

public readonly record struct WordPressBasicCredentials(string UserName, string AppPassword);
