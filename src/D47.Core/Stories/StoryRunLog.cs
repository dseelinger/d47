using D47.Core.Adventures;

namespace D47.Core.Stories;

/// <summary>One recorded line on a run's page: its kind tag, and the beat of its chapter it was said in, or -1.</summary>
public sealed record StoryRunEntry(StoryRunLine Line, string Kind, int Beat);

/// <summary>
/// One chapter section of a run's page. <paramref name="Number"/> counts from one, or is 0 for lines said before the run's
/// first chapter; <paramref name="Name"/> is null where the chapter is no longer on file.
/// </summary>
public sealed record StoryRunSection(int Number, string? Key, string? Name, Adventure? Chapter, IReadOnlyList<StoryRunEntry> Entries)
{
    /// <summary>The day of the section's first line.</summary>
    public DateTimeOffset? Date => Entries.Count > 0 ? Entries[0].Line.Sent : null;
}

/// <summary>A run's recorded lines in chapter sections, for its page on the Story bench.</summary>
public static class StoryRunLog
{
    public const string ChapterBeat = "Objective";
    public const string Clue = "Clue";
    public const string Reaction = "Reaction";
    public const string Ending = "Ending";
    public const string StoryLine = "Story line";

    /// <summary>
    /// The sections, by chapter number, each holding its lines in posting order. A chapter's own line opens or continues its
    /// chapter; a clue, reaction, ending or other story line goes to the chapter of the line before it, and before any chapter
    /// line to the run's first chapter. <paramref name="chapter"/> finds a chapter by key, or returns null.
    /// </summary>
    public static IReadOnlyList<StoryRunSection> Of(Story run, IReadOnlyList<StoryRunLine> lines, Func<string, Adventure?> chapter)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(chapter);

        var numbers = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var index = 0; index < run.Chapters.Count; index++)
        {
            numbers.TryAdd(run.Chapters[index], index + 1);
        }

        var entries = new SortedDictionary<int, List<StoryRunEntry>>();
        var beats = new Dictionary<int, int>();
        var adventures = new Dictionary<int, Adventure?>();
        int? current = null;

        foreach (var line in lines)
        {
            var own = line.AdventureKey is { } key && numbers.TryGetValue(key, out var number) ? number : (int?)null;
            var at = own ?? current ?? (run.Chapters.Count > 0 ? 1 : 0);

            if (own is not null)
            {
                current = own;
            }

            if (!adventures.ContainsKey(at))
            {
                adventures[at] = at > 0 ? chapter(run.Chapters[at - 1]) : null;
            }

            var beat = beats.GetValueOrDefault(at, -1);

            if (own is not null && adventures[at] is { } adventure)
            {
                beat = BeatOf(adventure, line.Subject, beat);
            }

            beats[at] = beat;

            if (!entries.TryGetValue(at, out var list))
            {
                entries[at] = list = [];
            }

            list.Add(new StoryRunEntry(line, own is not null ? ChapterBeat : KindOf(line.AdventureKey), beat));
        }

        return [.. entries.Select(pair => new StoryRunSection(
            pair.Key,
            pair.Key > 0 ? run.Chapters[pair.Key - 1] : null,
            adventures[pair.Key]?.Name,
            adventures[pair.Key],
            pair.Value))];
    }

    /// <summary>The kind tag of a line posted under a story-level key.</summary>
    public static string KindOf(string? adventureKey)
    {
        if (adventureKey is null)
        {
            return StoryLine;
        }

        if (StoryClueCallout.Parse(adventureKey) is not null)
        {
            return Clue;
        }

        if (StoryReactionCallout.Parse(adventureKey) is not null)
        {
            return Reaction;
        }

        return adventureKey.StartsWith(StoryEnding.KeyPrefix, StringComparison.Ordinal) ? Ending : StoryLine;
    }

    /// <summary>
    /// The beat a chapter line was said in: the first beat at or after <paramref name="from"/> whose title is the line's
    /// subject, else <paramref name="from"/>. A line with no beat title is the opening, until a beat has been said.
    /// </summary>
    private static int BeatOf(Adventure chapter, string subject, int from)
    {
        for (var index = Math.Max(0, from); index < chapter.Beats.Count; index++)
        {
            if (string.Equals(chapter.Beats[index].Title, subject, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return from;
    }
}
