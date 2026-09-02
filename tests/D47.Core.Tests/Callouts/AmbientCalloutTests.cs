using D47.Core.Callouts;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// Remarks said because nothing has happened (Phase 11, "Ambient Voice").
/// <para>
/// Almost everything here is about timing. The lines are the easy part; a companion that says
/// the right thing at the wrong moment is worse than one that says nothing.
/// </para>
/// </summary>
public class AmbientCalloutTests
{
    private static GameStatus In(StatusFlags flags) => GameStatus.Unknown with { Flags = flags };

    private static CalloutContext At(DateTimeOffset now, GameStatus status, bool priming = false) =>
        new(now, priming, State: null, status, NavRoute.None, []);

    // Longest pinned to the interval, so the timing tests below drive a fixed cadence; the
    // range has its own test (#258).
    private static AmbientCallout Callout() => new()
    {
        Enabled = () => true,
        Interval = TimeSpan.FromMinutes(15),
        Longest = TimeSpan.FromMinutes(15),
        Settle = TimeSpan.FromSeconds(90),
    };

    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    [Theory]
    [InlineData(StatusFlags.Docked | StatusFlags.InMainShip, AmbientSituation.Docked)]
    [InlineData(StatusFlags.Landed | StatusFlags.InMainShip, AmbientSituation.Landed)]
    [InlineData(StatusFlags.Supercruise | StatusFlags.InMainShip, AmbientSituation.Supercruise)]
    [InlineData(StatusFlags.InMainShip, AmbientSituation.NormalSpace)]
    [InlineData(StatusFlags.InSrv, AmbientSituation.Srv)]
    [InlineData(StatusFlags.None, AmbientSituation.None)]
    public void TheSituationComesFromStatusAlone(StatusFlags flags, AmbientSituation expected)
    {
        Assert.Equal(expected, AmbientLines.Situate(In(flags)));
    }

    [Fact]
    public void ScoopingOutranksSupercruiseBecauseItIsTheMoreSpecificThingToBeDoing()
    {
        var scooping = In(StatusFlags.Supercruise | StatusFlags.ScoopingFuel | StatusFlags.InMainShip);

        Assert.Equal(AmbientSituation.Scooping, AmbientLines.Situate(scooping));
    }

    [Fact]
    public void EverySituationHasTenLines()
    {
        // The checklist asks for ten per state covered. A situation with fewer is one that will
        // repeat itself noticeably inside a session.
        foreach (var situation in Enum.GetValues<AmbientSituation>().Where(s => s != AmbientSituation.None))
        {
            Assert.Equal(10, AmbientLines.For(situation).Count);
        }
    }

    [Fact]
    public void NothingIsSaidInTheFirstInterval()
    {
        // A remark while the Commander is still reading the panel is not ambient, it is an
        // interruption.
        var callout = Callout();
        var docked = In(StatusFlags.Docked | StatusFlags.InMainShip);

        Assert.Empty(callout.Examine(At(Start, docked)));
        Assert.Empty(callout.Examine(At(Start.AddMinutes(10), docked)));
    }

    [Fact]
    public void ARemarkArrivesOnceTheIntervalAndTheSettleHaveBothPassed()
    {
        var callout = Callout();
        var docked = In(StatusFlags.Docked | StatusFlags.InMainShip);

        callout.Examine(At(Start, docked)).ToArray();

        var spoken = callout.Examine(At(Start.AddMinutes(16), docked)).ToArray();

        var remark = Assert.Single(spoken);
        Assert.StartsWith(AmbientCallout.KeyPrefix, remark.Key, StringComparison.Ordinal);
        Assert.Equal(CalloutUrgency.Routine, remark.Urgency);
        Assert.Contains(remark.Text, AmbientLines.For(AmbientSituation.Docked));
    }

    /// <summary>
    /// The index the stock line was picked with travels on the announcement, so the model-written
    /// replacement can be chosen by the same count (Phase 43). It is the only
    /// deterministic index a flavour call has.
    /// </summary>
    [Fact]
    public void EachRemarkCarriesWhichOneItIs()
    {
        var callout = Callout();
        var docked = In(StatusFlags.Docked | StatusFlags.InMainShip);
        var cruising = In(StatusFlags.Supercruise | StatusFlags.InMainShip);

        callout.Examine(At(Start, docked)).ToArray();

        var first = Assert.Single(callout.Examine(At(Start.AddMinutes(16), docked)));

        callout.Examine(At(Start.AddMinutes(20), cruising)).ToArray();
        var second = Assert.Single(callout.Examine(At(Start.AddMinutes(40), cruising)));

        Assert.Equal(0, first.Variant);
        Assert.Equal(1, second.Variant);
        Assert.Equal(AmbientLines.Pick(AmbientSituation.Supercruise, 1), second.Text);
    }

    [Fact]
    public void ASituationThatHasNotSettledIsNotRemarkedOn()
    {
        // Status.json flips several times a minute during an approach. Without the settle, a
        // remark about being docked arrives as the Commander is lifting off.
        var callout = Callout();

        callout.Examine(At(Start, In(StatusFlags.Supercruise | StatusFlags.InMainShip))).ToArray();

        // Long past the interval, but the situation changed a second ago.
        var justChanged = At(Start.AddMinutes(20), In(StatusFlags.Docked | StatusFlags.InMainShip));

        Assert.Empty(callout.Examine(justChanged));
    }

    [Fact]
    public void TheSameSituationIsNotRemarkedOnTwiceRunning()
    {
        // Two remarks about supercruise, an interval apart, is where ambient stops sounding
        // like company.
        var callout = Callout();
        var cruising = In(StatusFlags.Supercruise | StatusFlags.InMainShip);

        callout.Examine(At(Start, cruising)).ToArray();
        Assert.Single(callout.Examine(At(Start.AddMinutes(16), cruising)));

        Assert.Empty(callout.Examine(At(Start.AddMinutes(40), cruising)));
    }

    [Fact]
    public void ADifferentSituationIsRemarkedOnEvenAfterOneJustWas()
    {
        var callout = Callout();

        callout.Examine(At(Start, In(StatusFlags.Supercruise | StatusFlags.InMainShip))).ToArray();
        callout.Examine(At(Start.AddMinutes(16), In(StatusFlags.Supercruise | StatusFlags.InMainShip))).ToArray();

        var docked = In(StatusFlags.Docked | StatusFlags.InMainShip);
        callout.Examine(At(Start.AddMinutes(40), docked)).ToArray();

        Assert.Single(callout.Examine(At(Start.AddMinutes(45), docked)));
    }

    [Fact]
    public void AnIntervalOfZeroSilencesThem()
    {
        // Offered because a Commander reaching for "less" reaches for the number, not the switch.
        var callout = Callout();
        callout.Interval = TimeSpan.Zero;

        var docked = In(StatusFlags.Docked | StatusFlags.InMainShip);

        callout.Examine(At(Start, docked)).ToArray();
        Assert.Empty(callout.Examine(At(Start.AddHours(3), docked)));
    }

    [Fact]
    public void PersonalityOffMeansNoAmbientRemarks()
    {
        // The checklist puts "no ambient remarks" in that item's acceptance criteria, which
        // makes this the one callout the personality switch reaches.
        var callout = Callout();
        callout.Enabled = () => false;

        var docked = In(StatusFlags.Docked | StatusFlags.InMainShip);

        callout.Examine(At(Start, docked)).ToArray();
        Assert.Empty(callout.Examine(At(Start.AddHours(3), docked)));
    }

    [Fact]
    public void NothingIsSaidFromTheBacklog()
    {
        var callout = Callout();
        var docked = In(StatusFlags.Docked | StatusFlags.InMainShip);

        Assert.Empty(callout.Examine(At(Start, docked, priming: true)));
        Assert.Empty(callout.Examine(At(Start.AddHours(2), docked, priming: true)));
    }

    [Fact]
    public void TheLineChosenIsTheSameOnEveryRunOfTheSameSession()
    {
        // No Core component reads a clock or a random seed, and a recorded session has to
        // replay to the line it produced live.
        Assert.Equal(
            AmbientLines.Pick(AmbientSituation.Docked, 3),
            AmbientLines.Pick(AmbientSituation.Docked, 3));

        Assert.NotEqual(
            AmbientLines.Pick(AmbientSituation.Docked, 3),
            AmbientLines.Pick(AmbientSituation.Docked, 4));
    }

    /// <summary>
    /// The gap between remarks varies inside [Interval, Longest] rather than ticking
    /// (<a href="https://github.com/dseelinger/d47/issues/258">#258</a>) — and
    /// deterministically, off the pick counter, because a recorded session has to replay to the
    /// same spacing.
    /// </summary>
    [Fact]
    public void TheGapVariesInsideTheRangeAndReplaysTheSame()
    {
        static AmbientCallout Spread() => new()
        {
            Enabled = () => true,
            Interval = TimeSpan.FromMinutes(15),
            Longest = TimeSpan.FromMinutes(30),
            Settle = TimeSpan.Zero,
        };

        var docked = In(StatusFlags.Docked | StatusFlags.InMainShip);
        var cruising = In(StatusFlags.Supercruise | StatusFlags.InMainShip);

        var callout = Spread();

        callout.Examine(At(Start, docked)).ToArray();
        Assert.Single(callout.Examine(At(Start.AddMinutes(15), docked)).ToArray());

        // Sixteen minutes on from that one is past the floor and still inside this cycle's gap,
        // which is the whole of the change: the wait grew, and it grew by something other than a
        // fixed amount.
        Assert.Empty(callout.Examine(At(Start.AddMinutes(31), cruising)));

        // And thirty minutes on is past every gap the range can deal.
        Assert.Single(callout.Examine(At(Start.AddMinutes(45).AddSeconds(1), cruising)).ToArray());

        // The same drive again lands on the same cycle boundaries: the spacing comes off the
        // pick counter, never a clock or a seed.
        var replay = Spread();

        replay.Examine(At(Start, docked)).ToArray();
        Assert.Single(replay.Examine(At(Start.AddMinutes(15), docked)).ToArray());
        Assert.Empty(replay.Examine(At(Start.AddMinutes(31), cruising)));
        Assert.Single(replay.Examine(At(Start.AddMinutes(45).AddSeconds(1), cruising)).ToArray());
    }

    /// <summary>
    /// The two edge cases the chatter pair already has, and they are the same two on purpose:
    /// two rows of the same kind disagreeing about their own edges is worse than neither having
    /// the spread. A maximum at the minimum pins the cadence, and one below it reads as equal.
    /// </summary>
    [Theory]
    [InlineData(15)]
    [InlineData(5)]
    public void AMaximumAtOrBelowTheMinimumPinsTheCadence(int longestMinutes)
    {
        var callout = new AmbientCallout
        {
            Enabled = () => true,
            Interval = TimeSpan.FromMinutes(15),
            Longest = TimeSpan.FromMinutes(longestMinutes),
            Settle = TimeSpan.Zero,
        };

        var docked = In(StatusFlags.Docked | StatusFlags.InMainShip);
        var cruising = In(StatusFlags.Supercruise | StatusFlags.InMainShip);

        callout.Examine(At(Start, docked)).ToArray();

        // Two cycles, both landing on the interval exactly. Under a spread the second would have
        // been the one to move, since the first pick's fraction is zero.
        Assert.Single(callout.Examine(At(Start.AddMinutes(15), docked)).ToArray());
        Assert.Single(callout.Examine(At(Start.AddMinutes(30), cruising)).ToArray());
    }
}
