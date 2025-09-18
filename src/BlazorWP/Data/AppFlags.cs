using BlazorWP;
using System;
using System.Threading.Tasks;

namespace BlazorWP.Data
{
    public enum AppMode
    {
        Full,
        Basic
    }

    public enum AuthType
    {
        AppPass,
        Nonce
    }

    public enum Language
    {
        English,
        Japanese
    }

    public class AppFlags
    {
        private readonly LocalStorageJsInterop _storage;

        public AppFlags(LocalStorageJsInterop storage)
        {
            _storage = storage;
        }

        public AppMode Mode { get; private set; } = AppMode.Full;
        public AuthType Auth { get; private set; } = AuthType.AppPass;
        public Language Language { get; private set; } = Language.English;
        public string WpUrl { get; private set; } = string.Empty;

        public event Action? OnChange;

        private void NotifyStateChanged() => OnChange?.Invoke();

        public async Task SetAppMode(AppMode mode)
        {
            Mode = mode;
            try
            {
                await _storage.SetItemAsync("appmode", mode == AppMode.Basic ? "basic" : "full");
            }
            catch { }
            NotifyStateChanged();
        }

        public async Task SetAuthMode(AuthType auth)
        {
            Auth = auth;
            try
            {
                await _storage.SetItemAsync("auth", auth == AuthType.Nonce ? "nonce" : "apppass");
            }
            catch { }
            NotifyStateChanged();
        }

        public async Task SetLanguage(Language language)
        {
            Language = language;
            try
            {
                await _storage.SetItemAsync("lang", language == Language.Japanese ? "jp" : "en");
            }
            catch { }
            NotifyStateChanged();
        }

        public async Task SetWpUrl(string url)
        {
            WpUrl = url;
            try
            {
                await _storage.SetItemAsync("wpEndpoint", url);
            }
            catch { }
            NotifyStateChanged();
        }
    }
}
