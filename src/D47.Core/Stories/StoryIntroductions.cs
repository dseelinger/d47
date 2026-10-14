using D47.Core.Adventures;

namespace D47.Core.Stories;

/// <summary>The narrator's line saying who a cast member is, said before the member first speaks in a story.</summary>
public static class StoryIntroductions
{
    public const string KeyPrefix = "story.introduction.";

    /// <summary>Between the story id and the cast id, which may contain dots.</summary>
    private const char Separator = '/';

    /// <summary>The key of an announcement that introduces cast member <paramref name="castId"/> of story <paramref name="storyId"/>.</summary>
    public static string Key(string storyId, string castId) => $"{KeyPrefix}{storyId}{Separator}{castId}";

    /// <summary>The story and cast ids of an introduction's key, or null for any other key.</summary>
    public static (string StoryId, string CastId)? Parse(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = key[KeyPrefix.Length..];
        var at = rest.IndexOf(Separator, StringComparison.Ordinal);

        return at > 0 && at < rest.Length - 1 ? (rest[..at], rest[(at + 1)..]) : null;
    }

    /// <summary>
    /// "{Name} is {role}." for cast member <paramref name="castId"/> as a Commander of <paramref name="gender"/> meets them;
    /// null when the member is introduced already, has no role, or is not in the cast.
    /// </summary>
    public static string? Line(Story story, StorySecret secret, string? gender, string? castId)
    {
        ArgumentNullException.ThrowIfNull(story);

        return castId is null || story.Introduced.Contains(castId, StringComparer.Ordinal) ? null : Introducing(secret, gender, castId);
    }

    /// <summary>Whether cast member <paramref name="castId"/> may react or be answered: introduced, or with no role to introduce.</summary>
    public static bool Met(Story story, StorySecret secret, string castId)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(secret);

        return story.Introduced.Contains(castId, StringComparer.Ordinal)
               || secret.Cast.FirstOrDefault(member => string.Equals(member.Id, castId, StringComparison.Ordinal)) is not { Role.Length: > 0 };
    }

    /// <summary>
    /// The chapter with the narrator's introduction before each cast member's first line, unless the member is introduced
    /// already or earlier in the chapter. The opening and the beats before <paramref name="from"/> are left as they are and
    /// count as earlier; from -1 walks the opening too.
    /// </summary>
    public static Adventure Introduce(Adventure chapter, Story story, StorySecret secret, string? gender, int from = -1)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        ArgumentNullException.ThrowIfNull(story);

        var seen = new HashSet<string>(story.Introduced, StringComparer.Ordinal);

        if (from >= 0)
        {
            foreach (var line in chapter.OpeningLines.Concat(chapter.Beats.Take(from).SelectMany(beat => beat.Lines)))
            {
                if (line.Introduces is { } id)
                {
                    seen.Add(id);
                }
            }
        }

        var opening = from < 0 ? Walk(chapter.OpeningLines, secret, gender, seen) : chapter.OpeningLines;
        var start = Math.Max(from, 0);

        return chapter with
        {
            OpeningLines = opening,
            Beats = [.. chapter.Beats.Take(start), .. chapter.Beats.Skip(start).Select(beat => beat with { Lines = Walk(beat.Lines, secret, gender, seen) })],
        };
    }

    private static List<AdventureLine> Walk(IReadOnlyList<AdventureLine> lines, StorySecret secret, string? gender, HashSet<string> seen)
    {
        var walked = new List<AdventureLine>(lines.Count);

        foreach (var line in lines)
        {
            if (line.Introduces is { } id)
            {
                seen.Add(id);
            }
            else if (line.Speaker is { } speaker && !seen.Contains(speaker) && Introducing(secret, gender, speaker) is { } introduction)
            {
                walked.Add(new AdventureLine { Text = introduction, Speaker = StorySpeaker.Narrator, Introduces = speaker });
                seen.Add(speaker);
            }

            walked.Add(line);
        }

        return walked;
    }

    private static string? Introducing(StorySecret secret, string? gender, string castId)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return secret.Cast.FirstOrDefault(member => string.Equals(member.Id, castId, StringComparison.Ordinal)) is { Role: { Length: > 0 } role } member
               && member.Shown(secret.Id, gender).Name is { Length: > 0 } name
            ? $"{name} is {role.Trim()}."
            : null;
    }
}
