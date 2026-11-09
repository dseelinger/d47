using System.Text.RegularExpressions;

namespace D47.Core.Conversation;

/// <summary>The model-free grammar for naming the campaign's minor faction: "our campaign faction is &lt;name&gt;".</summary>
public static partial class CampaignFactionPhrase
{
    /// <summary>The faction named in the utterance, or null when it is not a sentence this grammar takes.</summary>
    public static string? Read(string? input)
    {
        var said = (input ?? string.Empty).Replace('\u2019', '\'').Trim().TrimEnd('!', '?').Trim();

        if (said.EndsWith('.') && !said.EndsWith("..", StringComparison.Ordinal))
        {
            said = said[..^1].TrimEnd();
        }

        return Sentence().Match(said) is { Success: true } match
               && match.Groups["name"].Value.Trim() is { Length: > 0 } name
            ? name
            : null;
    }

    [GeneratedRegex(
        @"^(?:my|our|the) campaign (?:minor )?faction (?:is called|is named|is now|is) (?<name>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Sentence();
}
