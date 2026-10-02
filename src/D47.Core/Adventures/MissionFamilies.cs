namespace D47.Core.Adventures;

/// <summary>Mission families a <see cref="TriggerKind.Mission"/> beat names, as prefixes of <c>MissionCompleted.Name</c>.</summary>
public static class MissionFamilies
{
    public const string Prefix = "Mission_";

    /// <summary>
    /// Families no beat may use and no beat counts, each with the issue whose spike clears it. A family
    /// matches a mission whose name starts with it.
    /// </summary>
    public static IReadOnlyList<(string Family, int Issue)> SetAside { get; } =
    [
        ("Mission_Massacre_Skimmer", 721),
        ("Mission_Disable", 722),
        ("Mission_Hack", 723),
        ("Mission_OnFoot_Hack", 723),
        ("Mission_Scan", 724),
        ("Mission_RS_", 725),
        ("Mission_DS_", 725),
    ];

    /// <summary>Whether a mission name or a family starts with a set-aside family.</summary>
    public static bool IsSetAside(string? name) =>
        name is not null && SetAside.Any(entry => name.StartsWith(entry.Family, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether a completed mission counts toward a beat for <paramref name="family"/>; null counts every mission.</summary>
    public static bool Counts(string? family, string? name) =>
        name is not null
        && !IsSetAside(name)
        && (string.IsNullOrWhiteSpace(family) || name.StartsWith(family.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The family in words — "courier" for <c>Mission_Courier</c>, "on foot" for <c>Mission_OnFoot</c> — or empty for none.</summary>
    public static string Word(string? family)
    {
        if (string.IsNullOrWhiteSpace(family))
        {
            return string.Empty;
        }

        var rest = family.Trim();

        if (rest.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            rest = rest[Prefix.Length..];
        }

        var words = new System.Text.StringBuilder();

        foreach (var character in rest.TrimEnd('_'))
        {
            if (character == '_')
            {
                words.Append(' ');
            }
            else
            {
                if (char.IsUpper(character) && words.Length > 0 && words[^1] != ' ')
                {
                    words.Append(' ');
                }

                words.Append(char.ToLowerInvariant(character));
            }
        }

        return words.ToString();
    }
}
