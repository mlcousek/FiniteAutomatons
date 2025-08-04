using System.ComponentModel;
using System.Globalization;

namespace FiniteAutomatons.Core.Models.DoMain;

[TypeConverter(typeof(TransitionSymbolConverter))]
public readonly struct TransitionSymbol(char value)
{
    public char Value { get; } = value;

    public static implicit operator char(TransitionSymbol symbol)
    {
        return symbol.Value;
    }

    public static implicit operator TransitionSymbol(char value)
    {
        return new(value);
    }

    public override string ToString()
    {
        return Value == '\0' ? "ε" : Value.ToString();
    }

    public string ToFormString()
    {
        return Value == '\0' ? "\0" : Value.ToString();
    }
}

public class TransitionSymbolConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string stringValue)
        {
            if (string.IsNullOrEmpty(stringValue) || stringValue == "\0" || stringValue == "ε" || stringValue == "epsilon" || stringValue == "eps")
            {
                return new TransitionSymbol('\0');
            }

            if (stringValue.Length == 1)
            {
                return new TransitionSymbol(stringValue[0]);
            }
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is TransitionSymbol symbol && destinationType == typeof(string))
        {
            return symbol.ToFormString();
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}

public class State
{
    public int Id { get; set; }
    public bool IsStart { get; set; }
    public bool IsAccepting { get; set; }
}
