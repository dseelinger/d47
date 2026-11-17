using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>One remark from the ship's AI on every promotion, and none for an old one (#453).</summary>
public class TheShipRemarksOnEveryPromotionTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private const string Commander =
        """{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CalloutContext Tick(
        GameStateStore store, DateTimeOffset now, bool priming, params JournalEvent[] events) =>
        new(now, priming, store.Active, GameStatus.Unknown, NavRoute.None, events);

    /// <summary>Folds every line into the state, hands the last one to the callout as a tick's event, then ticks again past the hold.</summary>
    private static List<Announcement> Replay(bool priming, params string[] lines)
    {
        var store = new GameStateStore();
        var events = new[] { Commander }.Concat(lines).Select(Event).ToList();

        foreach (var journalEvent in events)
        {
            store.Apply(journalEvent);
        }

        var callout = new PromotionCallout();
        var said = callout.Examine(Tick(store, Start, priming, events[^1])).ToList();

        said.AddRange(callout.Examine(Tick(store, Start + PromotionCallout.Hold + TimeSpan.FromSeconds(1), priming)));

        return said;
    }

    private static List<Announcement> Replay(params string[] lines) => Replay(priming: false, lines);

    private static string Promotion(string ladder, int rank) =>
        $$"""{"timestamp":"3311-01-01T00:01:00Z","event":"Promotion","{{ladder}}":{{rank}}}""";

    [Fact]
    public void ATradePromotionToEliteFourIsSaidByTheShip()
    {
        var said = Assert.Single(Replay(Promotion("Trade", 12)));

        Assert.Equal(VoiceRole.ShipAi, said.Voice);
        Assert.Equal("Promoted. Trade Rank 12 of 13, Elite IV.", said.Text);
    }

    [Fact]
    public void ANavyPromotionUsesTheNavalRankName() =>
        Assert.Equal("Promoted. Imperial Navy Rank 2 of 14, Serf.", Assert.Single(Replay(Promotion("Empire", 2))).Text);

    [Theory]
    [InlineData("Combat")]
    [InlineData("Trade")]
    [InlineData("Explore")]
    [InlineData("Soldier")]
    [InlineData("Exobiologist")]
    [InlineData("CQC")]
    [InlineData("Empire")]
    [InlineData("Federation")]
    public void EveryLadderIsAnnouncedInTheRanksOwnNames(string ladder)
    {
        foreach (var rank in new[] { 3, 8, 10 })
        {
            var said = Assert.Single(Replay(Promotion(ladder, rank)));

            Assert.Contains(new RankStanding(ladder, rank).Describe(), said.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("% into it", said.Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AProgressPercentBeforeThePromotionIsNotSaid()
    {
        var said = Assert.Single(Replay(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Rank","Combat":2,"Trade":3}""",
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Progress","Combat":40,"Trade":90}""",
            Promotion("Trade", 4)));

        Assert.DoesNotContain("%", said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void APowerplayPromotionNamesThePowerAndTheRank()
    {
        var said = Assert.Single(Replay(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"PowerplayRank","Power":"Li Yong-Rui","Rank":8}"""));

        Assert.Equal(VoiceRole.ShipAi, said.Voice);
        Assert.Equal("Powerplay rank 8 with Li Yong-Rui.", said.Text);
    }

    [Fact]
    public void JoiningAPowerIsNotAPromotion() =>
        Assert.Empty(Replay(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"PowerplayRank","Power":"Li Yong-Rui","Rank":0}"""));

    [Fact]
    public void AnOldPromotionReadAtStartupSaysNothing() =>
        Assert.Empty(Replay(priming: true, Promotion("Trade", 12)));

    [Fact]
    public void APromotionIsAlwaysPutToTheModel()
    {
        var chance = new RewordChance(new Random(1));

        foreach (var said in Replay(Promotion("Combat", 5)).Concat(Replay(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"PowerplayRank","Power":"Li Yong-Rui","Rank":8}""")))
        {
            Assert.True(chance.ShouldReword(said, rewordPercent: 0));

            var brief = FlavourBriefs.For(said, personalityEnabled: true);
            Assert.NotNull(brief);
            Assert.Contains(said.Text, brief.Instruction, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void WithPersonalityOffTheFixedLineIsSpoken() =>
        Assert.Null(FlavourBriefs.For(Assert.Single(Replay(Promotion("Trade", 12))), personalityEnabled: false));

    private static List<Announcement> ReplayTicks(params (int Seconds, string? Line)[] ticks)
    {
        var store = new GameStateStore();
        store.Apply(Event(Commander));
        var callout = new PromotionCallout();
        var said = new List<Announcement>();

        foreach (var (seconds, line) in ticks)
        {
            var events = new List<JournalEvent>();

            if (line is not null)
            {
                events.Add(Event(line));
                store.Apply(events[0]);
            }

            said.AddRange(callout.Examine(Tick(store, Start.AddSeconds(seconds), priming: false, [.. events])));
        }

        return said;
    }

    [Fact]
    public void TwoRanksGainedAtOnceAreSaidAsTheHigherOnly()
    {
        var said = ReplayTicks(
            (0, Promotion("Explore", 1)), (1, Promotion("Explore", 2)), (2, null), (6, null));

        Assert.Equal("Promoted. Exploration Rank 2 of 13, Scout.", Assert.Single(said).Text);
    }

    [Fact]
    public void PromotionsOnDifferentLaddersAreEachSaidOnce()
    {
        var said = ReplayTicks(
            (0, Promotion("Explore", 1)), (0, Promotion("Trade", 3)), (1, Promotion("Explore", 2)), (6, null));

        Assert.Equal(2, said.Count);
        Assert.Contains(said, a => a.Text == "Promoted. Exploration Rank 2 of 13, Scout.");
        Assert.Contains(said, a => a.Key == "promotion.trade");
    }

    [Fact]
    public void ALonePromotionWaitsOutTheHoldBeforeItIsSaid()
    {
        var said = ReplayTicks((0, Promotion("Trade", 12)), (1, null));

        Assert.Empty(said);
        Assert.Single(ReplayTicks((0, Promotion("Trade", 12)), (4, null)));
    }

    [Fact]
    public void APromotionHeldWhileTheCalloutWasOffIsDropped() =>
        Assert.Empty(ReplayTicks((0, Promotion("Trade", 12)), (120, null)));

    [Fact]
    public void PrimingClearsAHeldPromotion()
    {
        var store = new GameStateStore();
        store.Apply(Event(Commander));
        var promotion = Event(Promotion("Trade", 12));
        store.Apply(promotion);
        var callout = new PromotionCallout();

        Assert.Empty(callout.Examine(Tick(store, Start, priming: false, promotion)));
        Assert.Empty(callout.Examine(Tick(store, Start.AddSeconds(1), priming: true)));
        Assert.Empty(callout.Examine(Tick(store, Start.AddSeconds(10), priming: false)));
    }
}
