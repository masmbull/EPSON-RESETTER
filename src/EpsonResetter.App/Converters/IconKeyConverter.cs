using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace EpsonResetter.App.Converters;

/// <summary>Looks up an icon geometry by its resource key (for example "Icon.Printer").</summary>
public sealed class IconKeyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key)
        {
            return null;
        }

        return Application.Current?.TryFindResource(key) as Geometry;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}