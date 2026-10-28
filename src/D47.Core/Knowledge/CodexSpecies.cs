namespace D47.Core.Knowledge;

/// <summary>The species a codex name stands for.</summary>
public static class CodexSpecies
{
    private const string VariantSeparator = " - ";

    /// <summary>The name without its colour variant, or null when blank: "Frutexa Flammasis - Red" is "Frutexa Flammasis".</summary>
    public static string? Of(string? codexName)
    {
        if (string.IsNullOrWhiteSpace(codexName))
        {
            return null;
        }

        var name = codexName.Trim();
        var at = name.IndexOf(VariantSeparator, StringComparison.Ordinal);

        return at > 0 ? name[..at].TrimEnd() : name;
    }
}
