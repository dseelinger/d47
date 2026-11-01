using D47.Core.Callouts;
using D47.Core.Logbook;
using D47.Core.Tests.Logbook;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts.Recap;

public class TheRecapNamesWhatTheCommanderEndedInTests
{
    private static readonly DateTimeOffset Evening = new(3311, 4, 2, 19, 0, 0, TimeSpan.Zero);

    private readonly JournalCorpus _corpus = new();

    private static string LoadGameOnFoot(DateTimeOffset at) =>
        JournalCorpus.Event(
            at,
            "LoadGame",
            "\"Name\":\"Jameson\",\"FID\":\"F1\",\"Ship\":\"TacticalSuit_Class3\",\"ShipID\":4293000005,\"Credits\":1000000");

    private static string Embark(DateTimeOffset at) =>
        JournalCorpus.Event(at, "Embark", "\"SRV\":false,\"Taxi\":false,\"Multicrew\":false");

    private static string Loadout(DateTimeOffset at, string shipName = "") =>
        JournalCorpus.Event(
            at,
            "Loadout",
            $"\"Ship\":\"cutter\",\"Ship_Localised\":\"Imperial Cutter\",\"ShipID\":12,\"ShipName\":\"{shipName}\"");

    private static string Disembark(DateTimeOffset at) =>
        JournalCorpus.Event(at, "Disembark", "\"SRV\":false,\"Taxi\":false,\"Multicrew\":false,\"OnStation\":true");

    private static string SuitLoadout(DateTimeOffset at, string suit = "tacticalsuit_class3") =>
        JournalCorpus.Event(at, "SuitLoadout", $"\"SuitID\":1,\"SuitName\":\"{suit}\",\"LoadoutID\":4293000005");

    private LogDigest Digest(params string[] lines)
    {
        _corpus.Journal(Evening, lines);

        var range = new LogRange
        {
            Span = LogSpan.Session,
            From = Evening.AddMinutes(-1),
            To = Evening.AddDays(1),
            Label = "the last session",
        };

        return new LogDigestBuilder(_corpus.FileSystem, NullLogger<LogDigestBuilder>.Instance).Build(_corpus.Files, range);
    }

    [Fact]
    public void ABoardedShipReplacesTheSuitTheSessionOpenedIn()
    {
        var digest = Digest(
            LoadGameOnFoot(Evening),
            Embark(Evening.AddMinutes(1)),
            Loadout(Evening.AddMinutes(1)),
            JournalCorpus.Event(Evening.AddMinutes(2), "Undocked", "\"StationName\":\"Suri Park\""),
            JournalCorpus.Event(Evening.AddMinutes(3), "Docked", "\"StationName\":\"Suri Park\",\"StarSystem\":\"Anlave\""));

        Assert.False(digest.OnFoot);
        Assert.Equal("a Imperial Cutter", digest.Ship);
        Assert.Contains("in a Imperial Cutter", RecapCallout.Compose(digest), StringComparison.Ordinal);
        Assert.DoesNotContain("Suit", RecapCallout.Compose(digest), StringComparison.Ordinal);
    }

    [Fact]
    public void ALoadoutWithAShipNameNamesTheShipInQuotes()
    {
        var digest = Digest(LoadGameOnFoot(Evening), Embark(Evening.AddMinutes(1)), Loadout(Evening.AddMinutes(1), "Gryphon"));

        Assert.Contains("\"Gryphon\"", digest.Ship, StringComparison.Ordinal);
    }

    [Fact]
    public void ASessionThatNeverBoardsStillNamesTheSuit()
    {
        var digest = Digest(LoadGameOnFoot(Evening), SuitLoadout(Evening));

        Assert.True(digest.OnFoot);
        Assert.Contains("Suit", digest.Ship, StringComparison.Ordinal);
    }

    [Fact]
    public void ADisembarkAfterFlyingEndsOnFootInTheSuitLoadoutsSuit()
    {
        var digest = Digest(
            JournalCorpus.LoadGame(Evening),
            SuitLoadout(Evening, "explorationsuit_class1"),
            Disembark(Evening.AddMinutes(5)),
            SuitLoadout(Evening.AddMinutes(5), "explorationsuit_class1"));

        Assert.True(digest.OnFoot);
        Assert.Contains("Artemis", digest.Ship, StringComparison.Ordinal);
    }

    [Fact]
    public void ABoardingAfterADisembarkNamesTheShipAgain()
    {
        var digest = Digest(
            JournalCorpus.LoadGame(Evening),
            SuitLoadout(Evening),
            Disembark(Evening.AddMinutes(5)),
            SuitLoadout(Evening.AddMinutes(5)),
            Embark(Evening.AddMinutes(9)),
            Loadout(Evening.AddMinutes(9)));

        Assert.False(digest.OnFoot);
        Assert.Contains("Cutter", digest.Ship, StringComparison.Ordinal);
    }

    [Fact]
    public void ASuitLoadoutWhileFlyingDoesNotPutTheCommanderOnFoot()
    {
        var digest = Digest(JournalCorpus.LoadGame(Evening), SuitLoadout(Evening));

        Assert.False(digest.OnFoot);
        Assert.Contains("Python", digest.Ship, StringComparison.Ordinal);
    }
}
