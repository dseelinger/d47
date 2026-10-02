using System.Globalization;
using D47.Core.Adventures;
using D47.Core.Stories;

namespace D47.Core.Logbook;

/// <summary>An adventure beat the Commander reached, with the journal event that fired it.</summary>
public sealed record LogStoryBeat(string Statement, LogSource Source)
{
    /// <summary>
    /// The beats reached in <paramref name="standings"/>, oldest first. A beat with no recorded event is left out,
    /// because a log sentence must trace to one.
    /// </summary>
    public static IReadOnlyList<LogStoryBeat> From(IReadOnlyList<AdventureStanding> standings, Func<string, Story?> story)
    {
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentNullException.ThrowIfNull(story);

        List<LogStoryBeat> beats = [];

        foreach (var standing in standings)
        {
            var adventure = standing.Adventure;
            var chapterOf = adventure.StoryId is { } id ? story(id) : null;

            for (var index = 0; index < standing.Fired.Count && index < standing.FiredBy.Count && index < adventure.Beats.Count; index++)
            {
                var title = adventure.Beats[index].Title;
                var statement = chapterOf is { } owner && owner.Chapters.ToList().FindIndex(key => string.Equals(key, adventure.Key, StringComparison.OrdinalIgnoreCase)) is >= 0 and var at
                    ? $"Reached \"{title}\", chapter {(at + 1).ToString(CultureInfo.InvariantCulture)} of {owner.Title}."
                    : $"Reached \"{title}\" in the adventure {adventure.Name}.";

                beats.Add(new LogStoryBeat(statement, new LogSource(standing.FiredBy[index], standing.Fired[index])));
            }
        }

        return [.. beats.OrderBy(beat => beat.Source.At)];
    }
}
