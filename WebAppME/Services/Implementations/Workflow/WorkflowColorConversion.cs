using System.Globalization;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// Converts between the raw HSL triplets the canvas speaks ("51 100% 50%", the same shape the
/// theme's CSS variables use) and the #rrggbb hex an HTML colour input speaks.
///
/// This lives in a .cs file rather than inline in StatusLegend.razor for two reasons: the Razor
/// parser treats a leading '&lt;' in a relational pattern as the start of a tag, and — more
/// usefully — the round-trip is fiddly enough to deserve its own tests.
/// </summary>
public static class WorkflowColorConversion
{
    private const string Fallback = "#000000";

    /// <summary>HSL triplet to #rrggbb. Returns black for anything unparseable, so a corrupt
    /// stored value shows an obviously-wrong swatch rather than throwing in a render.</summary>
    public static string HslToHex(string? hslTriplet)
    {
        if (string.IsNullOrWhiteSpace(hslTriplet)) return Fallback;

        var parts = hslTriplet.Replace("%", "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var h)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var s)
            || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var l))
        {
            return Fallback;
        }

        s /= 100;
        l /= 100;

        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = l - c / 2;

        double r, g, b;
        var hue = ((h % 360) + 360) % 360;

        if (hue < 60) { r = c; g = x; b = 0; }
        else if (hue < 120) { r = x; g = c; b = 0; }
        else if (hue < 180) { r = 0; g = c; b = x; }
        else if (hue < 240) { r = 0; g = x; b = c; }
        else if (hue < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return $"#{To255(r + m):x2}{To255(g + m):x2}{To255(b + m):x2}";
    }

    /// <summary>#rrggbb to an HSL triplet. Returns the neutral grey the palette uses for idle if
    /// the input is not a well-formed hex colour.</summary>
    public static string HexToHsl(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || hex.Length != 7 || hex[0] != '#') return "0 0% 62%";

        int ri, gi, bi;
        try
        {
            ri = Convert.ToInt32(hex.Substring(1, 2), 16);
            gi = Convert.ToInt32(hex.Substring(3, 2), 16);
            bi = Convert.ToInt32(hex.Substring(5, 2), 16);
        }
        catch (FormatException)
        {
            return "0 0% 62%";
        }

        var r = ri / 255d;
        var g = gi / 255d;
        var b = bi / 255d;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        var d = max - min;

        double h = 0, s = 0;
        if (d > 0.0001)
        {
            s = d / (1 - Math.Abs(2 * l - 1));

            if (max == r) h = 60 * ((g - b) / d % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);

            if (h < 0) h += 360;
        }

        var ci = CultureInfo.InvariantCulture;
        return string.Create(ci, $"{Math.Round(h)} {Math.Round(s * 100)}% {Math.Round(l * 100)}%");
    }

    private static int To255(double v) => (int)Math.Round(Math.Clamp(v, 0, 1) * 255);
}
