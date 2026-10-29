namespace D47.Core.Capabilities;

/// <summary>Wording helpers shared by capabilities.</summary>
public static class SpokenText
{
    public static string Capitalised(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
