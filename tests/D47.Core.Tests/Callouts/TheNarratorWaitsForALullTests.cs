using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The Narrator speaks only in a settled, quiet moment, and only with a story to tell.</summary>
public class TheNarratorWaitsForALullTests
{
    private static readonly DateTimeOffset T0 = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Gap = TimeSpan.FromMinutes(30);

    private const StatusFlags Docked = StatusFlags.Docked | StatusFlags.InMainShip;

    private static NarratorCallout Narrator(NearbyFight? fight = null, bool story = true) =>
        new(fight ?? new NearbyFight())
        {
            Interval = Gap,
            Longest = Gap,
            HasStory = () => story,
        };

    private static CalloutContext At(DateTimeOffset now, StatusFlags flags = Docked, ChatterSaid? lastChatter = null) =>
        new(now, false, null, GameStatus.Unknown with { Flags = flags }, NavRoute.None, [], lastChatter);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void ANarrationIsAnnouncedOnceTheGapHasPassedAndNotBefore()
    {
        var narrator = Narrator();

        Assert.Empty(narrator.Examine(At(T0)));
        Assert.Empty(narrator.Examine(At(T0 + Gap - TimeSpan.FromMinutes(1))));

        var said = Assert.Single(narrator.Examine(At(T0 + Gap + TimeSpan.FromSeconds(1))));

        Assert.Equal(NarratorCallout.Key, said.Key);
        Assert.Equal(VoiceRole.Narrator, said.Voice);
        Assert.Equal(string.Empty, said.Text);
        Assert.Empty(narrator.Examine(At(T0 + Gap + TimeSpan.FromMinutes(2))));
    }

    [Fact]
    public void NothingIsNarratedWhileAFightIsOn()
    {
        var fight = new NearbyFight();
        var narrator = Narrator(fight);
        _ = narrator.Examine(At(T0)).ToArray();

        var due = T0 + Gap + TimeSpan.FromSeconds(1);
        fight.Fold(Event("""{"timestamp":"3312-05-01T12:30:00Z","event":"UnderAttack","Target":"You"}"""), due, priming: false);

        Assert.Empty(narrator.Examine(At(due)));
        Assert.Empty(narrator.Examine(At(due, Docked | StatusFlags.InDanger)));
    }

    [Theory]
    [InlineData(StatusFlags.Supercruise | StatusFlags.InMainShip)]
    [InlineData(StatusFlags.Supercruise | StatusFlags.ScoopingFuel | StatusFlags.InMainShip)]
    public void NothingIsNarratedInSupercruise(StatusFlags flags)
    {
        var narrator = Narrator();
        _ = narrator.Examine(At(T0, flags)).ToArray();

        Assert.Empty(narrator.Examine(At(T0 + Gap * 3, flags)));
    }

    [Fact]
    public void NothingIsNarratedOnTheHeelsOfAnAmbientRemark()
    {
        var narrator = Narrator();
        _ = narrator.Examine(At(T0)).ToArray();

        var due = T0 + Gap + TimeSpan.FromSeconds(1);
        var remark = new ChatterSaid(due - TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5));

        Assert.Empty(narrator.Examine(At(due, lastChatter: remark)));
        Assert.Single(narrator.Examine(At(due + CalloutEngine.ChatterSpacing, lastChatter: remark)));
    }

    [Fact]
    public void WithNoSheetBackstoryOrScenarioNothingIsNarrated()
    {
        var narrator = Narrator(story: false);
        _ = narrator.Examine(At(T0)).ToArray();

        Assert.Empty(narrator.Examine(At(T0 + Gap * 3)));
    }

    [Fact]
    public void SwitchedOffNothingIsNarrated()
    {
        var narrator = Narrator();
        narrator.Enabled = () => false;
        _ = narrator.Examine(At(T0)).ToArray();

        Assert.Empty(narrator.Examine(At(T0 + Gap * 3)));
    }

    [Fact]
    public void ANarrationWaitsForTheSituationToSettle()
    {
        var narrator = Narrator();
        _ = narrator.Examine(At(T0)).ToArray();

        var landed = T0 + Gap + TimeSpan.FromMinutes(1);
        const StatusFlags Landed = StatusFlags.Landed | StatusFlags.InMainShip;

        Assert.Empty(narrator.Examine(At(landed, Landed)));
        Assert.Single(narrator.Examine(At(landed + narrator.Settle, Landed)));
    }
}
