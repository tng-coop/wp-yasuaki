// WpDI/src/Editor.WordPress/ReasonLocalizer.cs
using System.Globalization;
using System.Resources;
using Editor.Abstractions;

namespace Editor.WordPress;

public sealed class ReasonLocalizer : IReasonLocalizer
{
    // Use ResourceManager directly (works cross-platform without designer generation)
    private static readonly ResourceManager Rm =
        new("Editor.WordPress.Resources.WpdiReasons", typeof(ReasonLocalizer).Assembly);

    private readonly CultureInfo? _configured;

    public ReasonLocalizer(CultureInfo? configured = null) => _configured = configured;

    // ---------- Preferred typed API ----------
    public string Localize(ReasonCode code, ReasonArgs args)
        => Localize(code, args, _configured ?? CultureInfo.CurrentUICulture);

    public string Localize(ReasonCode code, ReasonArgs args, CultureInfo culture)
    {
        var key = code switch
        {
            ReasonCode.NotFound => "Reason_not_found",
            ReasonCode.Trashed  => "Reason_trashed",
            ReasonCode.Conflict => "Reason_conflict",
            _                   => "Reason_unknown"
        };

        var fmt = Rm.GetString(key, culture) ?? Rm.GetString("Reason_unknown", culture) ?? "Unknown error.";
        return string.Format(culture, fmt, args.Kind, args.Id);
    }

    // ---------- Back-compat shims ----------
    public string Localize(string reasonCode, object[]? reasonArgs = null)
        => Localize(reasonCode, reasonArgs, _configured ?? CultureInfo.CurrentUICulture);

    public string Localize(string reasonCode, object[]? reasonArgs, CultureInfo culture)
    {
        var code = (reasonCode ?? "").Trim().ToLowerInvariant() switch
        {
            "not_found" => ReasonCode.NotFound,
            "trashed"   => ReasonCode.Trashed,
            "conflict"  => ReasonCode.Conflict,
            _           => ReasonCode.Unknown
        };

        var (kind, id) = CoerceArgs(reasonArgs);
        return Localize(code, new ReasonArgs(kind, id), culture);
    }

    private static (string Kind, long Id) CoerceArgs(object[]? a)
    {
        if (a is { Length: > 0 })
        {
            var kind = a.Length > 0 ? a[0]?.ToString() ?? "resource" : "resource";
            long id = 0;
            if (a.Length > 1)
            {
                switch (a[1])
                {
                    case long l:   id = l; break;
                    case int i:    id = i; break;
                    case string s: long.TryParse(s, out id); break;
                }
            }
            return (kind, id);
        }
        return ("resource", 0);
    }
}

