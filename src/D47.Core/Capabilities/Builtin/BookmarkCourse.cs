using D47.Core.Conversation;

namespace D47.Core.Capabilities.Builtin;

/// <summary>"Set course for" a system a Commander named (#488).</summary>
public static class BookmarkCourse
{
    private const string CarrierTail = " my carrier";

    private const string FleetCarrierTail = " my fleet carrier";

    /// <summary>
    /// The ways <see cref="CarrierCourse"/> asks for the carrier, with the name in place of "my carrier" —
    /// derived so a change there stays in step here.
    /// </summary>
    public static IReadOnlyList<string> Spellings(string name) =>
        [
            .. CarrierCourse.CourseSpellings
                .Select(Asking)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(asking => asking + " " + name),
        ];

    private static string Asking(string spelling) =>
        spelling.EndsWith(FleetCarrierTail, StringComparison.Ordinal)
            ? spelling[..^FleetCarrierTail.Length]
            : spelling[..^CarrierTail.Length];

    /// <summary>The commands, one per spelling per bookmark this Commander has made.</summary>
    public static IEnumerable<DynamicCommand> Phrases(BookmarkStore? store, Func<string> frontierId)
    {
        var fid = frontierId();

        if (store is null || fid.Length == 0)
        {
            yield break;
        }

        foreach (var bookmark in store.For(fid))
        {
            foreach (var spelling in Spellings(bookmark.Name))
            {
                yield return new DynamicCommand(
                    spelling,
                    NavigationCapability.Id,
                    "plot_course",
                    new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = bookmark.System });
            }
        }
    }
}

/// <summary>Checking a bookmark name before it is allowed to exist (#488).</summary>
public static class BookmarkValidation
{
    /// <summary>A bookmark name has to be sayable and has to not collide with anything already meaningful.</summary>
    public const int MaxNameLength = 40;

    /// <summary>
    /// Checks a bookmark name against names this Commander already uses and phrases already taken.
    /// <see cref="CarrierCourse.CourseSpellings"/> are always reserved, whether or not a carrier is known.
    /// </summary>
    public static string? Problem(
        string name, IReadOnlyCollection<string> existingNames, IReadOnlyCollection<string> taken)
    {
        var trimmed = name?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return "A bookmark needs a name.";
        }

        if (trimmed.Length > MaxNameLength)
        {
            return $"\"{trimmed}\" is longer than {MaxNameLength} characters.";
        }

        if (!trimmed.All(c => char.IsLetterOrDigit(c) || c == ' '))
        {
            return $"\"{trimmed}\" has punctuation in it. Bookmark names are words, so they can be said out loud.";
        }

        if (existingNames.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            return $"You already have a bookmark called \"{trimmed}\".";
        }

        var reserved = taken
            .Select(KeywordRouter.Utterance)
            .Concat(CarrierCourse.CourseSpellings.Select(KeywordRouter.Utterance))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var spelling in BookmarkCourse.Spellings(trimmed))
        {
            var utterance = KeywordRouter.Utterance(spelling);

            if (reserved.Contains(utterance))
            {
                return $"\"{utterance}\" is already a command D47 understands, so a bookmark cannot take that name.";
            }
        }

        return null;
    }

    /// <summary>
    /// <paramref name="taken"/> less the phrases <paramref name="ownName"/> generates, so a rename is checked
    /// against every other bookmark and command rather than against its own current phrases.
    /// </summary>
    public static IReadOnlyCollection<string> Less(IReadOnlyCollection<string> taken, string ownName)
    {
        var own = BookmarkCourse.Spellings(ownName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return [.. taken.Where(phrase => !own.Contains(phrase))];
    }
}
