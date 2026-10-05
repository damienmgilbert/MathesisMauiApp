using System.Globalization;
using Mathesis.Numbers;

namespace MathesisMauiApp.Services;

/// <summary>Reads text that an exact number attribute has accepted, with the same notation the attribute uses.</summary>
public static class NumberInputs
{
    /// <summary>Parses <paramref name="text"/> as an exact rational (U+2212 counts as minus, as in the expression lexer).</summary>
    public static bool TryRead(string? text, out BigRational value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return BigRational.TryParse(text.Trim().Replace('−', '-'), CultureInfo.InvariantCulture, out value);
    }

    /// <summary>The value as a double, or <c>null</c> when the text is not a number.</summary>
    public static double? ReadDouble(string? text) => TryRead(text, out var value) ? value.ToDouble() : null;
}
