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

    /// <summary>Folds every line into the state, then hands the last one to the callout as this tick's event.</summary>
    private static List<Announcement> Replay(bool priming, params string[] lines)
    {
        var store = new GameStateStore();
        var events = new[] { Commander }.Concat(lines).Select(Event).ToList();

        foreach (var journalEvent in events)
        {
            store.Apply(journalEvent);
        }

        var context = new CalloutContext(
            Start, priming, store.Active, GameStatus.Unknown, NavRoute.None, [events[^1]]);

        return [.. new PromotionCallout().Examine(context)];
    }

    private static List<Announcement> Replay(params string[] lines) => Replay(priming: false, lines);

    private static string Promotion(string ladder, int rank) =>
        $$"""{"timestamp":"3311-01-01T00:01:00Z","event":"Promotion","{{ladder}}":{{rank}}}""";

    [Fact]
    public void ATradePromotionToEliteFourIsSaidByTheShip()
    {
        var said = Assert.Single(Replay(Promotion("Trade", 12)));

        Assert.Equal(VoiceRole.ShipAi, said.Voice);
        Assert.Equal("Promoted. Trade, Elite IV.", said.Text);
    }

    [Fact]
    public void ANavyPromotionUsesTheNavalRankName() =>
        Assert.Equal("Promoted. Empire, Serf.", Assert.Single(Replay(Promotion("Empire", 2))).Text);

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
}
