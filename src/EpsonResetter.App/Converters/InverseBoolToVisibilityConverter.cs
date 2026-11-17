using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EpsonResetter.App.Converters;

/// <summary>Inverts a boolean to visibility: true collapses, false shows.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool flag && flag ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}