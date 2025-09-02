namespace BlazorWP.Data
{
    public enum AppMode
    {
        Full,
        Basic
    }

    public enum AuthType
    {
        Jwt,
        Nonce
    }

    public enum Language
    {
        English,
        Japanese
    }

    public class AppFlags
    {
        public AppMode Mode { get; private set; } = AppMode.Full;
        public AuthType Auth { get; private set; } = AuthType.Jwt;
        public Language Language { get; private set; } = Language.English;

        public event Action? OnChange;

        private void NotifyStateChanged() => OnChange?.Invoke();

        public void SetAppMode(AppMode mode)
        {
            Mode = mode;
            NotifyStateChanged();
        }

        public void SetAuthMode(AuthType auth)
        {
            Auth = auth;
            NotifyStateChanged();
        }

        public void SetLanguage(Language language)
        {
            Language = language;
            NotifyStateChanged();
        }
    }
}
