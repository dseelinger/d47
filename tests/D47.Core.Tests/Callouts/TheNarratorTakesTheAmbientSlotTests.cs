using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>With a stock core aboard, the Narrator speaks at its own gap, with or without a story.</summary>
public class TheNarratorTakesTheAmbientSlotTests
{
    private static readonly DateTimeOffset T0 = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan OwnGap = TimeSpan.FromMinutes(30);

    private const StatusFlags Docked = StatusFlags.Docked | StatusFlags.InMainShip;

    private static NarratorCallout Narrator(Func<bool> stock, bool story = false) =>
        new(new NearbyFight())
        {
            Interval = OwnGap,
            Longest = OwnGap,
            HasStory = () => story,
            StockCoreAboard = stock,
        };

    private static CalloutContext At(DateTimeOffset now, StatusFlags flags = Docked) =>
        new(now, false, null, GameStatus.Unknown with { Flags = flags }, NavRoute.None, []);

    [Fact]
    public void WithCovasAboardAndNoStoryTheNarratorSpeaksNoSoonerThanItsOwnGap()
    {
        var narrator = Narrator(() => true);
        _ = narrator.Examine(At(T0)).ToArray();

        Assert.Empty(narrator.Examine(At(T0 + OwnGap - TimeSpan.FromSeconds(1))));

        var said = Assert.Single(narrator.Examine(At(T0 + OwnGap + TimeSpan.FromSeconds(1))));

        Assert.Equal(NarratorCallout.Key, said.Key);
        Assert.Equal(VoiceRole.Narrator, said.Voice);
        Assert.Equal(OwnGap, said.Cooldown);
    }

    [Fact]
    public void SwitchedOffTheNarratorStaysSilentWithCovasAboard()
    {
        var narrator = Narrator(() => true);
        narrator.Enabled = () => false;
        _ = narrator.Examine(At(T0)).ToArray();

        Assert.Empty(narrator.Examine(At(T0 + OwnGap * 3)));
    }

    [Fact]
    public void ALeastGapOfZeroSilencesTheNarratorWithCovasAboard()
    {
        var narrator = Narrator(() => true);
        narrator.Interval = TimeSpan.Zero;
        _ = narrator.Examine(At(T0)).ToArray();

        Assert.Empty(narrator.Examine(At(T0 + OwnGap * 3)));
    }

    [Fact]
    public void WithAnotherCoreAboardTheNarratorNeedsAStory()
    {
        var withStory = Narrator(() => false, story: true);
        _ = withStory.Examine(At(T0)).ToArray();

        Assert.Single(withStory.Examine(At(T0 + OwnGap + TimeSpan.FromSeconds(1))));

        var withoutStory = Narrator(() => false);
        _ = withoutStory.Examine(At(T0)).ToArray();

        Assert.Empty(withoutStory.Examine(At(T0 + OwnGap * 3)));
    }

    [Fact]
    public void TwoNarrationsAreNeverCloserThanTheLeastOrFurtherThanTheMost()
    {
        var narrator = Narrator(() => true);
        narrator.Interval = TimeSpan.FromMinutes(30);
        narrator.Longest = TimeSpan.FromMinutes(60);

        var now = T0;
        _ = narrator.Examine(At(now)).ToArray();

        for (var round = 0; round < 8; round++)
        {
            var last = now;
            now += TimeSpan.FromSeconds(1);

            while (!narrator.Examine(At(now)).Any())
            {
                now += TimeSpan.FromSeconds(30);
            }

            Assert.InRange(now - last, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(60) + TimeSpan.FromSeconds(31));
        }
    }

    [Fact]
    public void NothingIsNarratedInSupercruiseWithCovasAboard()
    {
        const StatusFlags Supercruise = StatusFlags.Supercruise | StatusFlags.InMainShip;
        var narrator = Narrator(() => true);
        _ = narrator.Examine(At(T0, Supercruise)).ToArray();

        Assert.Empty(narrator.Examine(At(T0 + OwnGap, Supercruise)));
    }

    [Fact]
    public void ANarrationWithNoCharacterSheetNamesTheCommanderFromTheJournal()
    {
        Assert.Equal("Commander Jameson.", CommanderStory.SheetOrName(null, "Jameson"));
        Assert.Equal("Commander Jameson.", CommanderStory.SheetOrName("  ", " Jameson "));
        Assert.Equal("Vale, she/her.", CommanderStory.SheetOrName("Vale, she/her.", "Jameson"));
        Assert.Null(CommanderStory.SheetOrName(null, null));
    }

    [Fact]
    public void ASettingsFileThatStillHoldsTheTipSwitchesLoads()
    {
        var loaded = System.Text.Json.JsonSerializer.Deserialize<D47.Core.Configuration.CalloutSettings>(
            "{\"NarratorD47Tips\":true,\"NarratorEliteTips\":false,\"NarratorSeconds\":1800}");

        Assert.Equal(1800, loaded!.NarratorSeconds);
    }
}
