using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MiniCAD.App.Converters;

/// <summary>Formats a line-weight value (mm) for the combo boxes: 0 reads as "Anzeige".</summary>
public sealed class LineWeightLabelConverter : IValueConverter
{
    public static readonly LineWeightLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is double mm ? (mm <= 0.0 ? "Anzeige" : $"{mm:0.##} mm") : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
