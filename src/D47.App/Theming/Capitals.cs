using Avalonia.Data.Converters;

namespace D47.App.Theming;

/// <summary>Converters that draw a label in capitals.</summary>
public static class Capitals
{
    /// <summary>Upper-cases a string and passes any other content, a control included, through unchanged.</summary>
    public static readonly IValueConverter Text =
        new FuncValueConverter<object?, object?>(value => value is string text ? text.ToUpperInvariant() : value);
}
