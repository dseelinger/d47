using D47.Core.Adventures;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>A run's page lists its recorded lines in chapter sections; a line said between chapters belongs to the one before it.</summary>
public sealed class ARunsLinesFallIntoTheirChaptersTests
{
    private static readonly DateTimeOffset Picked = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Story Run = new()
    {
        Id = "tide",
        Title = "The Long Tide",
        PublicLayer = "A test.",
        PickedAt = Picked,
        Chapters = ["chapter-one", "chapter-two"],
    };

    private static readonly Dictionary<string, Adventure> Chapters = new()
    {
        ["chapter-one"] = Chapter("chapter-one", "Low tide", "The lights go out", "The second ring"),
        ["chapter-two"] = Chapter("chapter-two", "The second ring", "A voice", "Silence", "A voice"),
    };

    private static Adventure Chapter(string key, string name, params string[] beats) =>
        new() { Key = key, Name = name, Beats = [.. beats.Select(title => new AdventureBeat { Title = title })] };

    private static int _minute;

    private static StoryRunLine Line(string? key, string subject, string body) =>
        new() { Key = body, Sent = Picked.AddMinutes(++_minute), AdventureKey = key, Subject = subject, Body = body };

    private static IReadOnlyList<StoryRunSection> Sections(params StoryRunLine[] lines) =>
        StoryRunLog.Of(Run, lines, key => Chapters.GetValueOrDefault(key));

    private static List<string> Bodies(StoryRunSection section) => [.. section.Entries.Select(entry => entry.Line.Body)];

    [Fact]
    public void AClueBetweenTwoChaptersBelongsToTheChapterBeforeIt()
    {
        var sections = Sections(
            Line("chapter-one", "The lights go out", "one"),
            Line("story.clue.tide.0", "The Long Tide", "clue"),
            Line("chapter-two", "A voice", "two"));

        Assert.Equal([1, 2], sections.Select(section => section.Number));
        Assert.Equal(["one", "clue"], Bodies(sections[0]));
        Assert.Equal(["two"], Bodies(sections[1]));
        Assert.Equal(StoryRunLog.Clue, sections[0].Entries[1].Kind);
        Assert.Equal("Low tide", sections[0].Name);
    }

    [Fact]
    public void ALineSaidBeforeAnyChapterOpensTheFirstChapter()
    {
        var sections = Sections(
            Line(StoryLines.Key("tide"), "The Long Tide", "introduction"),
            Line("chapter-one", "Low tide", "opening"));

        var section = Assert.Single(sections);
        Assert.Equal(1, section.Number);
        Assert.Equal(["introduction", "opening"], Bodies(section));
    }

    [Fact]
    public void ALineOfANewerChapterReturnsToItsOwnSection()
    {
        var sections = Sections(
            Line("chapter-two", "A voice", "late"),
            Line("chapter-one", "The second ring", "early"));

        Assert.Equal([1, 2], sections.Select(section => section.Number));
        Assert.Equal(["early"], Bodies(sections[0]));
    }

    [Fact]
    public void EachLineCarriesTheBeatItWasSaidIn()
    {
        var sections = Sections(
            Line("chapter-two", "The second ring", "opening"),
            Line("chapter-two", "A voice", "first voice"),
            Line(StoryReactionCallout.Key(new StoryReactionDue("tide", 1), "ren"), "The Long Tide", "reaction"),
            Line("chapter-two", "Silence", "silence"),
            Line("chapter-two", "A voice", "second voice"));

        Assert.Equal([-1, 0, 0, 1, 2], Assert.Single(sections).Entries.Select(entry => entry.Beat));
    }

    [Fact]
    public void EachLineIsTaggedWithItsKind()
    {
        var sections = Sections(
            Line("chapter-one", "The lights go out", "beat"),
            Line("story.clue.tide.0", "The Long Tide", "clue"),
            Line(StoryReactionCallout.Key(new StoryReactionDue("tide", 1), "ren"), "The Long Tide", "reaction"),
            Line(StoryEnding.Key("tide"), "The Long Tide", "ending"),
            Line(StoryLines.Key("tide"), "The Long Tide", "story line"));

        Assert.Equal(
            [StoryRunLog.ChapterBeat, StoryRunLog.Clue, StoryRunLog.Reaction, StoryRunLog.Ending, StoryRunLog.StoryLine],
            Assert.Single(sections).Entries.Select(entry => entry.Kind));
    }

    [Fact]
    public void AChapterNoLongerOnFileKeepsItsNumberWithoutAName()
    {
        var sections = StoryRunLog.Of(Run, [Line("chapter-two", "A voice", "two")], _ => null);

        var section = Assert.Single(sections);
        Assert.Equal(2, section.Number);
        Assert.Null(section.Name);
        Assert.Equal(-1, section.Entries[0].Beat);
    }
}
