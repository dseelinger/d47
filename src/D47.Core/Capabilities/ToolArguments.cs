using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace D47.Core.Capabilities;

/// <summary>
/// Arguments for one tool call. Flat string values, matching the flat scalar schema in
/// <see cref="ToolParameter"/>; the provider seam is responsible for flattening whatever
/// the model actually sent.
/// </summary>
public sealed class ToolArguments(IReadOnlyDictionary<string, string> values)
{
    public static ToolArguments Empty { get; } = new(new Dictionary<string, string>());

    public IReadOnlyDictionary<string, string> Values { get; } = values;

    public bool TryGetString(string name, [NotNullWhen(true)] out string? value) =>
        Values.TryGetValue(name, out value) && value is not null;

    public bool TryGetInt32(string name, out int value)
    {
        value = 0;
        return Values.TryGetValue(name, out var raw)
               && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    public bool TryGetBoolean(string name, out bool value)
    {
        value = false;
        return Values.TryGetValue(name, out var raw) && bool.TryParse(raw, out value);
    }
}
