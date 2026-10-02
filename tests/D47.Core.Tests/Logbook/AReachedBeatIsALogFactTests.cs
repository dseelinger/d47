using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Logbook;
using D47.Core.Stories;
using D47.Core.Tests.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Logbook;

public sealed class AReachedBeatIsALogFactTests
{
    private const string Labour = "LTT 7786 Labour";

    private static readonly AdventureTrigger Dock = new() { Kind = TriggerKind.Dock, MarketId = Anchorage };

    private static readonly AdventureTrigger Bonds = new() { Kind = TriggerKind.Bond, Count = 2, Faction = Labour };

    private static readonly LogRange Window = new()
    {
        Span = LogSpan.Session,
        From = Accepted.AddMinutes(-1),
        To = Accepted.AddDays(1),
        Label = "the last session",
    };

    private static Adventure Chapter(string? storyId) => LanternRoute(Accepted) with
    {
        Name = "The Marker, three",
        StoryId = storyId,
        Beats = [Beat("The Anchorage", "setup", Dock, "Docked."), Beat("The Count", "catalyst", Bonds, "Done.")],
    };

    private static JournalEvent Bond(int minutes) =>
        Event($$"""{ "timestamp":"{{Stamp(Accepted.AddMinutes(minutes))}}", "event":"FactionKillBond", "Reward":80000, "AwardingFaction":"{{Labour}}", "VictimFaction":"Others" }""");

    private static AdventureStanding TwoBeatsReached(Adventure adventure) =>
        new JournalEvent[] { Docked(Anchorage, Accepted.AddMinutes(1)), Bond(2), Bond(3) }
            .Aggregate(AdventureFold.Start(adventure), AdventureFold.Apply);

    private static Story TheMarker(Adventure chapter) => new()
    {
        Id = "the-marker",
        Title = "The Marker",
        PublicLayer = "A public card.",
        Chapters = ["one", "two", chapter.Key],
    };

    private static LogDigest Digest(Adventure adventure)
    {
        var story = TheMarker(adventure);
        var beats = LogStoryBeat.From([TwoBeatsReached(adventure)], id => id == story.Id ? story : null);

        return new LogDigestBuilder(NullLogger<LogDigestBuilder>.Instance).Build([], Window, beats);
    }

    [Fact]
    public void TwoBeatsAreTwoStoryFactsEachSourcedToItsEvent()
    {
        var digest = Digest(Chapter("the-marker"));
        var facts = digest.Facts.Where(fact => fact.Kind == LogFactKind.Story).ToList();

        Assert.Equal(2, facts.Count);
        Assert.Equal("Reached \"The Anchorage\", chapter 3 of The Marker.", facts[0].Statement);
        Assert.Equal("Docked", Assert.Single(facts[0].Sources).Event);
        Assert.Equal("Reached \"The Count\", chapter 3 of The Marker.", facts[1].Statement);

        var last = Assert.Single(facts[1].Sources);

        Assert.Equal("FactionKillBond", last.Event);
        Assert.Equal(Accepted.AddMinutes(3), last.At);
    }

    [Fact]
    public void AnAdventureOutsideAStoryNamesItself()
    {
        var digest = Digest(Chapter(null));

        Assert.Contains(digest.Facts, fact => fact.Statement == "Reached \"The Anchorage\" in the adventure The Marker, three.");
    }

    [Fact]
    public void ABeatOutsideTheWindowIsNotInTheLog()
    {
        var adventure = Chapter("the-marker");
        var beats = LogStoryBeat.From([TwoBeatsReached(adventure)], _ => null);
        var narrow = Window with { From = Accepted.AddMinutes(2), To = Accepted.AddMinutes(10) };

        var digest = new LogDigestBuilder(NullLogger<LogDigestBuilder>.Instance).Build([], narrow, beats);

        Assert.Equal(
            "Reached \"The Count\" in the adventure The Marker, three.",
            Assert.Single(digest.Facts, fact => fact.Kind == LogFactKind.Story).Statement);
    }

    [Fact]
    public void NoSealedTextAppearsInADigestOfAStoryChapter()
    {
        var digest = Digest(Chapter("the-marker"));
        var text = string.Join('\n', digest.Facts.Select(fact => fact.Statement + " " + fact.Provenance()));

        var leaks = StoryFixtures.Catalog.Secrets
            .SelectMany(secret => secret.Texts().SelectMany(field => Sentences(field.Text).Select(sentence => (secret.Id, field.Field, sentence))))
            .Where(entry => text.Contains(entry.sentence, StringComparison.OrdinalIgnoreCase))
            .Select(entry => $"{entry.Id} ({entry.Field})")
            .ToList();

        Assert.True(leaks.Count == 0, string.Join(Environment.NewLine, leaks));
    }

    private static IEnumerable<string> Sentences(string text) =>
        text.Split(['.', '?', '!', ';', ':', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(sentence => sentence.Length >= 24);
}
