using System.Globalization;
using System.Text.RegularExpressions;

namespace Modernize;

/// <summary>
/// End-of-support dates from Microsoft's support policies (dotnet.microsoft.com/platform/support/policy),
/// checked on 22 November 2026. .NET Framework 4.7–4.8.1 follow the Windows lifecycle and have no
/// fixed end date.
/// </summary>
public static partial class SupportPolicy
{
    public const string CheckedOn = "2026-11-22";
    public const string CurrentLts = "net10.0";
    public static readonly DateOnly CurrentLtsEnds = new(2028, 11, 14);

    private static readonly Dictionary<string, DateOnly?> EndOfSupport = new(StringComparer.OrdinalIgnoreCase)
    {
        ["net40"] = new DateOnly(2016, 1, 12),
        ["net45"] = new DateOnly(2016, 1, 12),
        ["net451"] = new DateOnly(2016, 1, 12),
        ["net452"] = new DateOnly(2022, 4, 26),
        ["net46"] = new DateOnly(2022, 4, 26),
        ["net461"] = new DateOnly(2022, 4, 26),
        ["net462"] = new DateOnly(2027, 1, 12),
        ["net47"] = null,
        ["net471"] = null,
        ["net472"] = null,
        ["net48"] = null,
        ["net481"] = null,
        ["netcoreapp1.0"] = new DateOnly(2019, 6, 27),
        ["netcoreapp1.1"] = new DateOnly(2019, 6, 27),
        ["netcoreapp2.0"] = new DateOnly(2018, 10, 1),
        ["netcoreapp2.1"] = new DateOnly(2021, 8, 21),
        ["netcoreapp2.2"] = new DateOnly(2019, 12, 23),
        ["netcoreapp3.0"] = new DateOnly(2020, 3, 3),
        ["netcoreapp3.1"] = new DateOnly(2022, 12, 13),
        ["net5.0"] = new DateOnly(2022, 5, 10),
        ["net6.0"] = new DateOnly(2024, 11, 12),
        ["net7.0"] = new DateOnly(2024, 5, 14),
        ["net8.0"] = new DateOnly(2026, 11, 10),
        ["net9.0"] = new DateOnly(2026, 11, 10),
        ["net10.0"] = new DateOnly(2028, 11, 14),
    };

    /// <summary>Normalize "v4.7.2" (old csproj) or "net8.0-windows" to a short TFM such as "net472" or "net8.0".</summary>
    public static string Normalize(string framework)
    {
        ArgumentNullException.ThrowIfNull(framework);
        var f = framework.Trim();
        var legacy = LegacyVersion().Match(f);
        if (legacy.Success) return "net" + legacy.Groups[1].Value.Replace(".", "", StringComparison.Ordinal);
        var dash = f.IndexOf('-', StringComparison.Ordinal);
        return (dash > 0 ? f[..dash] : f).ToLowerInvariant();
    }

    public static bool IsNetFramework(string tfm) => NetFramework().IsMatch(Normalize(tfm));

    public static bool IsNetStandard(string tfm) => Normalize(tfm).StartsWith("netstandard", StringComparison.Ordinal);

    /// <summary>End of support, or null if none is scheduled or the TFM is unknown.</summary>
    public static DateOnly? EndOf(string tfm) => EndOfSupport.GetValueOrDefault(Normalize(tfm));

    public static bool IsKnown(string tfm) => EndOfSupport.ContainsKey(Normalize(tfm));

    public static string Describe(string tfm, DateOnly asOf)
    {
        var t = Normalize(tfm);
        if (IsNetStandard(t)) return "API contract, runs wherever its consumers run";
        if (!IsKnown(t)) return "unknown framework";
        return EndOf(t) switch
        {
            null => "supported (follows the Windows lifecycle)",
            { } end when end < asOf => $"out of support since {end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
            { } end => $"supported until {end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
        };
    }

    [GeneratedRegex(@"^v(\d+(?:\.\d+)*)$", RegexOptions.IgnoreCase)]
    private static partial Regex LegacyVersion();

    [GeneratedRegex(@"^net[1-4]\d*$")]
    private static partial Regex NetFramework();
}
