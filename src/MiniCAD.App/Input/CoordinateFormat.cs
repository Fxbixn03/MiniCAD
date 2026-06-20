using System.Globalization;
using MiniCAD.App.Configuration;

namespace MiniCAD.App.Input;

/// <summary>
/// Parses and formats coordinate numbers using the user's configured decimal separator
/// (comma by default, switchable to a point in the settings). Centralizing it here keeps the
/// coordinate input field and the status-bar readout consistent.
/// </summary>
public static class CoordinateFormat
{
    /// <summary>The active decimal separator (",", or "." if the user switched).</summary>
    public static string Separator
    {
        get
        {
            string s = AppConfig.Instance.DecimalSeparator;
            return string.IsNullOrEmpty(s) ? "," : s;
        }
    }

    private static NumberFormatInfo Format()
    {
        var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        nfi.NumberDecimalSeparator = Separator;
        return nfi;
    }

    /// <summary>Parses a coordinate value entered with the configured separator (sign allowed).</summary>
    public static bool TryParse(string? text, out double value)
        => double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, Format(), out value);

    /// <summary>Formats a value with the configured separator.</summary>
    public static string ToText(double value, string format = "0.###")
        => value.ToString(format, Format());
}
