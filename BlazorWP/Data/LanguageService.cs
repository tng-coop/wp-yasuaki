using System.Globalization;

namespace BlazorWP.Data
{
    public class LanguageService
    {
        private CultureInfo _currentCulture = new("en-US");
        public CultureInfo CurrentCulture => _currentCulture;

        public event Action? OnChange;

        public void SetCulture(string cultureCode)
        {
            var culture = new CultureInfo(cultureCode);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            _currentCulture = culture;
            OnChange?.Invoke();
        }

        public void ToggleCulture()
        {
            if (_currentCulture.Name == "ja-JP")
            {
                SetCulture("en-US");
            }
            else
            {
                SetCulture("ja-JP");
            }
        }
    }
}
