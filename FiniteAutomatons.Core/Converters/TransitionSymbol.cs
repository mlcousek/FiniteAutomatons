using System.ComponentModel;
using System.Globalization;

namespace FiniteAutomatons.Core.Converters;

[TypeConverter(typeof(TransitionSymbolConverter))]
public readonly struct TransitionSymbol(char value)
{
    public char Value { get; } = value;

    public static implicit operator char(TransitionSymbol symbol) => symbol.Value;
    public static implicit operator TransitionSymbol(char value) => new(value);

    public override string ToString() => Value == '\0' ? "ε" : Value.ToString();
    public string ToFormString() => Value == '\0' ? "\0" : Value.ToString();
}

public class TransitionSymbolConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string s)
        {
            if (string.IsNullOrEmpty(s) || s == "\0" || s == "ε" || s == "epsilon" || s == "eps")
                return new TransitionSymbol('\0');
            if (s.Length == 1)
                return new TransitionSymbol(s[0]);
        }
        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is TransitionSymbol sym && destinationType == typeof(string))
            return sym.ToFormString();
        return base.ConvertTo(context, culture, value, destinationType);
    }
}
