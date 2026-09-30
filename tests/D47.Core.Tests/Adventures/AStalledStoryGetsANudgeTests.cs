using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// A story that has waited three play sessions and seven days at its next beat makes the next narration a
/// nudge toward it.
/// </summary>
public class AStalledStoryGetsANudgeTests : IDisposable
{
    private static readonly TimeSpan Gap = TimeSpan.FromMinutes(30);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-adventure-nudge", Guid.NewGuid().ToString("N"));

    public AStalledStoryGetsANudgeTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    internal static JournalEvent LoadGame(DateTimeOffset at) =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"LoadGame", "FID":"F1", "Commander":"Tester", "GameMode":"Solo" }""");

    internal static CalloutContext At(DateTimeOffset now) =>
        new(now, false, null, GameStatus.Unknown with { Flags = StatusFlags.Docked | StatusFlags.InMainShip }, NavRoute.None, [], null);

    internal static NarratorCallout Narrator(AdventureBook book) =>
        new(new NearbyFight())
        {
            Interval = Gap,
            Longest = Gap,
            HasStory = () => true,
            Adventures = () => book.Active("F1"),
        };

    /// <summary>Begun, the first beat reached a minute in, then a LoadGame on each of the given days.</summary>
    internal AdventureBook Stalled(params int[] loadDays)
    {
        var store = new AdventureStore(Path.Combine(_folder, "adventures.json"), NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);
        book.Write("F1", LanternRoute(Accepted));

        book.Observe(Jump(Lantern, Accepted.AddMinutes(1)), "F1");

        foreach (var day in loadDays)
        {
            book.Observe(LoadGame(Accepted.AddDays(day)), "F1");
        }

        return book;
    }

    /// <summary>The first narration a callout makes once its first full gap has passed.</summary>
    internal static Announcement FirstNarration(NarratorCallout narrator, DateTimeOffset from)
    {
        Assert.Empty(narrator.Examine(At(from)));
        return Assert.Single(narrator.Examine(At(from + Gap + TimeSpan.FromSeconds(1))));
    }

    [Fact]
    public void ThreeSessionsAndSevenDaysMakeTheNextNarrationANudge()
    {
        var book = Stalled(2, 4, 6);

        var said = FirstNarration(Narrator(book), Accepted.AddDays(8));

        Assert.Equal(NarratorCallout.NudgePrefix + "the-lantern-route", said.Key);
        Assert.Equal("the-lantern-route", NarratorCallout.Nudged(said.Key));
    }

    [Fact]
    public void TwoSessionsAreNotEnough()
    {
        var said = FirstNarration(Narrator(Stalled(2, 4)), Accepted.AddDays(8));

        Assert.Equal(NarratorCallout.Key, said.Key);
    }

    [Fact]
    public void SixDaysAreNotEnough()
    {
        var said = FirstNarration(Narrator(Stalled(1, 2, 3)), Accepted.AddDays(6));

        Assert.Equal(NarratorCallout.Key, said.Key);
    }

    [Fact]
    public void ANudgeRestartsTheWait()
    {
        var book = Stalled(2, 4, 6);
        var nudgedAt = Accepted.AddDays(8);

        book.Told("F1", "the-lantern-route", new AdventureTold { Kind = AdventureToldKind.Nudge, Text = "Nudged.", At = nudgedAt });

        foreach (var day in new[] { 9, 10, 11 })
        {
            book.Observe(LoadGame(Accepted.AddDays(day)), "F1");
        }

        var standing = Assert.Single(book.Active("F1"));

        Assert.False(AdventureNudge.IsDue(standing, nudgedAt.AddDays(6)));
        Assert.True(AdventureNudge.IsDue(standing, nudgedAt.AddDays(7)));
    }

    [Fact]
    public void ALoadGameQueuesNoBeat()
    {
        var book = Stalled(2, 4, 6);

        Assert.Single(book.Drain());
        Assert.Single(Assert.Single(book.Active("F1")).Fired);
    }

    [Fact]
    public void TheBriefCarriesTheTriggerTheDistanceAndTheSpineButNotTheLine()
    {
        var standing = Assert.Single(Stalled(2, 4, 6).Active("F1"));
        var facts = AdventureNudge.Facts(standing, lightYears: 42.4);

        var brief = FlavourBriefs.For(
            new Announcement(NarratorCallout.NudgePrefix + "the-lantern-route", facts), personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.Contains("scan The Quiet Field A 2 in The Quiet Field", brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("42 light years", brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("An outpost abandoned in 3302 still runs a beacon.", brief.Instruction, StringComparison.Ordinal);
        Assert.DoesNotContain("Filed in 3306.", brief.Instruction, StringComparison.Ordinal);
        Assert.DoesNotContain("The beacon speaks to one person by name.", brief.Instruction, StringComparison.Ordinal);
        Assert.DoesNotContain("forty kilometres", brief.Instruction, StringComparison.Ordinal);
    }
}
