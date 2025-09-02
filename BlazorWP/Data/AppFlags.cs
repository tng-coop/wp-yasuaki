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

    public class AppFlags
    {
        public AppMode Mode { get; private set; } = AppMode.Full;
        public AuthType Auth { get; private set; } = AuthType.Jwt;

        public void SetAppMode(AppMode mode)
        {
            Mode = mode;
        }

        public void SetAuthMode(AuthType auth)
        {
            Auth = auth;
        }
    }
}
