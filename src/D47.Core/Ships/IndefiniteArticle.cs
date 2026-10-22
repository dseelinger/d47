namespace D47.Core.Ships;

public static class IndefiniteArticle
{
    /// <summary>The name with "an" before a vowel letter and "a" otherwise; "a ship" when the name is empty.</summary>
    public static string With(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? "a ship"
            : "aeiouAEIOU".Contains(name.TrimStart()[0]) ? $"an {name}" : $"a {name}";
}
