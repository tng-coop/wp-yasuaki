namespace BlazorWP.Data
{
    public enum AppMode
    {
        Full,
        Basic
    }

    public class AppFlags
    {
        public AppMode Mode { get; private set; } = AppMode.Full;

        public void SetAppMode(AppMode mode)
        {
            Mode = mode;
        }
    }
}
