using System.Globalization;

namespace BlazorWP.Data
{
    public class LanguageService
    {
        private CultureInfo _currentCulture = new("en-US");

        public CultureInfo CurrentCulture => _currentCulture;

        public CultureInfo Current => _currentCulture;

        public bool IsJapanese => _currentCulture.Name == "ja-JP";

        public event Action? OnChange;

        public void Set(string cultureCode)
        {
            var culture = new CultureInfo(cultureCode);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            _currentCulture = culture;
            OnChange?.Invoke();
        }

        public void Toggle()
        {
            Set(IsJapanese ? "en-US" : "ja-JP");
        }

        // Backwards compatibility
        public void SetCulture(string cultureCode) => Set(cultureCode);

        public void ToggleCulture() => Toggle();
    }
}
