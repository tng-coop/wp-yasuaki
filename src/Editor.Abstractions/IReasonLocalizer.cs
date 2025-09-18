// WpDI/src/Editor.Abstractions/IReasonLocalizer.cs
using System.Globalization;

namespace Editor.Abstractions;

/// Localizes structured reason codes (e.g., "not_found", "trashed", "conflict") to display strings.
public interface IReasonLocalizer
{
    /// Localize using the DI-configured culture (or CurrentUICulture if none configured).
    string Localize(string reasonCode, object[]? reasonArgs = null);

    /// Localize explicitly for a given culture.
    string Localize(string reasonCode, object[]? reasonArgs, CultureInfo culture);
}

