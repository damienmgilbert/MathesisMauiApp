using System.Collections;
using System.Globalization;

namespace MathesisMauiApp.Converters;

/// <summary>True when the bound collection has at least one item; <c>ConverterParameter=invert</c> reverses it. (The toolkit's list converters break the XAML source generator in this app, so this one is local.)</summary>
public sealed class HasItemsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var any = value switch
        {
            null => false,
            ICollection collection => collection.Count > 0,
            IEnumerable sequence => sequence.GetEnumerator().MoveNext(),
            _ => true,
        };
        return parameter is string text && text.Equals("invert", StringComparison.OrdinalIgnoreCase) ? !any : any;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
