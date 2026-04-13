using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MindMapApp.Converters;

/// <summary>
/// Convertit une chaîne hexadécimale (#RRGGBB ou #AARRGGBB) en SolidColorBrush.
/// Retourne Transparent si la valeur est nulle, vide ou invalide.
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public static readonly HexToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                return new SolidColorBrush(Color.Parse(hex));
            }
            catch { /* chaîne invalide → Transparent */ }
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
