using System.Globalization;

namespace BlazorWP.Data
{
    public class LanguageService
    {
        private CultureInfo _currentCulture = new("en-US");
        public CultureInfo CurrentCulture => _currentCulture;

        public void SetCulture(string cultureCode)
        {
            var culture = new CultureInfo(cultureCode);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            _currentCulture = culture;
        }
    }
}
