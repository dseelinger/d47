using System.Text;
using System.Text.RegularExpressions;
using D47.Core.Adventures;
using D47.Core.Callouts;

namespace D47.Core.Stories;

/// <summary>A primary cast member who may react: the cast id, the name the Commander knows them by, and who they are.</summary>
public sealed record StoryReactor(string Id, string Name, string Who);

/// <summary>What the model is asked when a cast member reacts to what the Commander did, and how its reply is read.</summary>
public static partial class StoryReactions
{
    /// <summary>
    /// The brief for one reaction: the event as the classifier recorded it and the primary cast to choose from. The
    /// reply names the chosen member's id, then the line.
    /// </summary>
    public static FlavourBrief Asking(Notable notable, IReadOnlyList<StoryReactor> cast)
    {
        ArgumentNullException.ThrowIfNull(notable);
        ArgumentNullException.ThrowIfNull(cast);

        var text = new StringBuilder();
        text.AppendLine($"What the Commander just did, as the game recorded it: {notable.Text}");
        text.AppendLine("The story's recurring characters, each after their id:");

        foreach (var member in cast)
        {
            text.AppendLine($"- {member.Id}: {member.Name}. {member.Who}".TrimEnd());
        }

        text.Append(
            "Choose the one of them most likely to hear of it and care, and write what they say to the Commander about "
            + "it, as that character, in the first person and in their own words. One or two sentences. React to what "
            + "the Commander did and to nothing else: state no new fact about the story, carry no clue, and do not give "
            + "advice. Reply with the character's id, a colon, and the line, and nothing else.");

        return new FlavourBrief
        {
            Speaker = "You write one line for a character in the stock story the Commander is playing.",
            Instruction = text.ToString(),
            NeedsPersona = false,
            NeedsGameState = false,
            NeedsAboutMe = false,
            NeedsScenario = false,
            NeedsStory = false,
        };
    }

    /// <summary>
    /// The chosen member's id and the line, or null when the reply names nobody in <paramref name="cast"/>, by id or by
    /// name, or carries no line. A sound tag such as <c>[sighs]</c> ahead of the name is kept at the front of the line.
    /// </summary>
    public static (string CastId, string Line)? Parse(string? reply, IReadOnlyList<StoryReactor> cast)
    {
        ArgumentNullException.ThrowIfNull(cast);

        if (string.IsNullOrWhiteSpace(reply) || reply.IndexOf(':', StringComparison.Ordinal) is not (> 0 and var colon))
        {
            return null;
        }

        var head = reply[..colon];
        var brackets = Bracketed().Matches(head).Select(match => match.Value).ToList();
        var named = Bracketed().Replace(head, " ").Trim().Trim('*', '`', '"', '-', ' ');
        var candidates = new[] { named }.Concat(brackets.Select(bracket => bracket[1..^1].Trim()));

        if (candidates.Select(candidate => Member(cast, candidate)).FirstOrDefault(member => member is not null) is not { } chosen)
        {
            return null;
        }

        var line = reply[(colon + 1)..].Trim().Trim('"', '“', '”').Trim();
        var tags = brackets.Where(bracket => bracket.StartsWith('[') && Member(cast, bracket[1..^1].Trim()) is null);

        return line.Length > 0 ? (chosen.Id, string.Join(' ', [.. tags, line])) : null;
    }

    private static StoryReactor? Member(IReadOnlyList<StoryReactor> cast, string named) =>
        cast.FirstOrDefault(member => string.Equals(member.Id, named, StringComparison.OrdinalIgnoreCase))
        ?? cast.FirstOrDefault(member => string.Equals(member.Name, named, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"\[[^\]]*\]|\([^)]*\)")]
    private static partial Regex Bracketed();
}
