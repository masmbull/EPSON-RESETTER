using System.Windows;
using System.Windows.Media;

namespace EpsonResetter.App.Controls;

/// <summary>Attached geometry used by button templates to draw an optional icon.</summary>
public static class IconService
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.RegisterAttached(
            "Data",
            typeof(Geometry),
            typeof(IconService),
            new PropertyMetadata(null));

    public static void SetData(DependencyObject element, Geometry? value) => element.SetValue(DataProperty, value);

    public static Geometry? GetData(DependencyObject element) => (Geometry?)element.GetValue(DataProperty);
}