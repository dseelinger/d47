using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// Invented background chatter (#244): a marker now and then, from a callout that follows the
/// ambient timing rules — and never a line of text, because chatter is model-written or it is
/// nothing (#245).
/// </summary>
public class NpcChatterCalloutTests
{
    private static readonly DateTimeOffset T0 = new(3311, 1, 1, 12, 0, 0, TimeSpan.Zero);

    // Longest pinned to the interval, so the timing tests drive a fixed cadence; the range is
    // its own test below.
    private static NpcChatterCallout Callout() => new()
    {
        Interval = TimeSpan.FromMinutes(20),
        Longest = TimeSpan.FromMinutes(20),
        Settle = TimeSpan.Zero,
    };

    private static GameStatus In(StatusFlags flags) => GameStatus.Unknown with { Flags = flags };

    private static CalloutContext Context(
        DateTimeOffset now,
        StatusFlags flags = StatusFlags.Docked | StatusFlags.InMainShip,
        bool priming = false) =>
        new(now, priming, State: null, In(flags), NavRoute.None, []);

    [Fact]
    public void TheFirstTickSeedsAndTheIntervalHoldsAfterIt()
    {
        var callout = Callout();

        // The first tick seeds the clock: silence for one whole interval after launch, exactly
        // the ambient rule.
        Assert.Empty(callout.Examine(Context(T0)));
        Assert.Empty(callout.Examine(Context(T0 + TimeSpan.FromMinutes(19))));

        var emitted = callout.Examine(Context(T0 + TimeSpan.FromMinutes(21))).ToList();

        var marker = Assert.Single(emitted);
        Assert.StartsWith(NpcChatter.KeyPrefix, marker.Key, StringComparison.Ordinal);

        // A marker, never a line: empty text is what makes a missed road speak nothing rather
        // than something (#245).
        Assert.Equal(string.Empty, marker.Text);

        // And the interval holds again after it.
        Assert.Empty(callout.Examine(Context(T0 + TimeSpan.FromMinutes(22))));
    }

    [Fact]
    public void OffOrZeroOrUnknownIsSilence()
    {
        var off = Callout();
        off.Enabled = () => false;
        Assert.Empty(off.Examine(Context(T0)).ToArray());
        Assert.Empty(off.Examine(Context(T0 + TimeSpan.FromHours(2))));

        var zero = Callout();
        zero.Interval = TimeSpan.Zero;
        Assert.Empty(zero.Examine(Context(T0 + TimeSpan.FromHours(2))));

        var nowhere = Callout();
        Assert.Empty(nowhere.Examine(Context(T0)).ToArray());
        Assert.Empty(nowhere.Examine(Context(T0 + TimeSpan.FromHours(2), StatusFlags.None)));
    }

    [Fact]
    public void PrimingFoldsTheBacklog()
    {
        var callout = Callout();

        // Seeded by a real first tick, so what priming folds is an exchange that would
        // otherwise be due.
        _ = callout.Examine(Context(T0)).ToArray();

        Assert.Empty(callout.Examine(Context(T0 + TimeSpan.FromHours(2), priming: true)));
    }

    /// <summary>
    /// The gap between exchanges varies inside [Interval, Longest] rather than ticking
    /// (asked for 2026-08-31) — and deterministically, off the pick counter, because a
    /// recorded session has to replay to the same spacing.
    /// </summary>
    [Fact]
    public void TheGapVariesInsideTheRangeAndReplaysTheSame()
    {
        var callout = new NpcChatterCallout
        {
            Interval = TimeSpan.FromMinutes(20),
            Longest = TimeSpan.FromMinutes(40),
            Settle = TimeSpan.Zero,
        };

        _ = callout.Examine(Context(T0)).ToArray();

        // Nineteen minutes is inside no possible gap; forty is past every one. Whatever the
        // hash dealt this cycle, the floor and the ceiling hold.
        Assert.Empty(callout.Examine(Context(T0 + TimeSpan.FromMinutes(19))));
        Assert.Single(callout.Examine(Context(T0 + (TimeSpan.FromMinutes(40) + TimeSpan.FromSeconds(1)))).ToArray());

        // And the same drive again lands on the same cycle boundaries: the spacing comes off
        // the pick counter, never a clock or a seed.
        var replay = new NpcChatterCallout
        {
            Interval = TimeSpan.FromMinutes(20),
            Longest = TimeSpan.FromMinutes(40),
            Settle = TimeSpan.Zero,
        };

        _ = replay.Examine(Context(T0)).ToArray();
        Assert.Single(replay.Examine(Context(T0 + (TimeSpan.FromMinutes(40) + TimeSpan.FromSeconds(1)))).ToArray());
    }

    /// <summary>
    /// The pairing ladder is deterministic off the pick counter — no Core component reads a
    /// clock or a seed. Every fourth exchange addresses the Commander; the controller only
    /// exists somewhere to be docked at.
    /// </summary>
    [Fact]
    public void TheControllerOnlySpeaksWhereThereIsADock()
    {
        for (var pick = 0; pick < 12; pick++)
        {
            Assert.NotEqual(NpcChatterKind.Controller, NpcChatterCallout.KindFor(pick, docked: false));
        }

        Assert.Equal(NpcChatterKind.Controller, NpcChatterCallout.KindFor(0, docked: true));
        Assert.Equal(NpcChatterKind.Hail, NpcChatterCallout.KindFor(3, docked: true));
        Assert.Equal(NpcChatterKind.Hail, NpcChatterCallout.KindFor(7, docked: false));
    }
}

/// <summary>
/// The exchange itself: what the model is asked, and how strictly the reply is read.
/// </summary>
public class NpcChatterScriptTests
{
    [Fact]
    public void TheKindTravelsOnTheKeyLikeTheAmbientSituation()
    {
        Assert.Equal(NpcChatterKind.Controller, NpcChatter.KindOf("npc.chatter.controller"));
        Assert.Equal(NpcChatterKind.Hail, NpcChatter.KindOf("npc.chatter.hail"));
        Assert.Equal(NpcChatterKind.Passersby, NpcChatter.KindOf("npc.chatter.passersby"));

        // A key nothing recognises composes the harmless kind rather than failing.
        Assert.Equal(NpcChatterKind.Passersby, NpcChatter.KindOf("npc.chatter.spelunking"));
    }

    [Fact]
    public void EveryKindForbidsQuestionsToTheCommanderAndRealPeople()
    {
        foreach (var kind in Enum.GetValues<NpcChatterKind>())
        {
            var instruction = NpcChatter.Instruction(kind);

            Assert.Contains("nobody asks the Commander a question", instruction, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Never name or imitate a real person", instruction, StringComparison.Ordinal);
            Assert.Contains("Name: words", instruction, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AScriptIsReadStrictlyAndCapped()
    {
        var lines = NpcChatter.Parse(
            "Vera Kolt: Pad nine again. Third time today.\n"
            + "Dock Control: Take it up with scheduling, Kolt.\n"
            + "not a line at all\n"
            + "Vera Kolt: One day I will.\n"
            + "Dock Control: One day you will still be on pad nine.\n"
            + "Extra Voice: This one is past the cap.",
            NpcChatterKind.Controller);

        Assert.Equal(NpcChatter.MostLines, lines.Count);
        Assert.Equal("Vera Kolt", lines[0].Name);
        Assert.Equal("Pad nine again. Third time today.", lines[0].Text);
        Assert.DoesNotContain(lines, line => line.Name == "Extra Voice");
    }

    [Fact]
    public void AFragmentIsSilenceRatherThanHalfAScene()
    {
        // One surviving line of a two-person exchange is a fragment, and silence beats a
        // fragment — the same judgement the ambient drop makes (#245).
        Assert.Empty(NpcChatter.Parse("Dock Control: Cleared.", NpcChatterKind.Controller));
        Assert.Empty(NpcChatter.Parse("nothing parseable here", NpcChatterKind.Passersby));
        Assert.Empty(NpcChatter.Parse(null, NpcChatterKind.Passersby));

        // A hail is one person saying a line or two, so one line is whole.
        Assert.Single(NpcChatter.Parse("Old Hand: Fine ship. Keep her polished.", NpcChatterKind.Hail));
    }

    [Fact]
    public void ALineAboutBeingAModelDoesNotSurvive()
    {
        var lines = NpcChatter.Parse(
            "Vera Kolt: As an AI language model I cannot discuss pad assignments.\n"
            + "Dock Control: Quiet night out here.\n"
            + "Vera Kolt: Too quiet, the drives hum louder than the bar.",
            NpcChatterKind.Passersby);

        Assert.Equal(2, lines.Count);
        Assert.DoesNotContain(lines, line => line.Text.Contains("language model", StringComparison.OrdinalIgnoreCase));
    }

    // The Commander's own carrier (#249): its two posts speak in the voices he cast for them,
    // and nobody invents a jump it is not making.

    private static readonly CarrierState Mine = CarrierState.None with
    {
        CallSign = "K7Q-B4Z",
        Name = "Nomad's Rest",
        CarrierId = 3_700_123_456,
        StarSystem = "Shinrarta Dezhra",
    };

    private static JournalLocation At(
        string system,
        bool docked = false,
        string? station = null,
        string? stationType = null,
        FlightMode mode = FlightMode.Normal,
        long? marketId = null) =>
        new(system, null, docked, station) { Mode = mode, StationType = stationType, MarketId = marketId };

    [Fact]
    public void TheCarrierIsAtHandOnItsDeckOrInItsSpaceAndNowhereElse()
    {
        // Set down on it, identified by the market id the docking wrote.
        Assert.True(NpcChatterCarrier.Of(
            Mine,
            At("Shinrarta Dezhra", docked: true, station: "K7Q-B4Z", stationType: "FleetCarrier",
                mode: FlightMode.Docked, marketId: 3_700_123_456)).Present);

        // And sharing the space around it.
        Assert.True(NpcChatterCarrier.Of(Mine, At("Shinrarta Dezhra")).Present);

        // Supercruise is not that space, whatever the system says.
        Assert.False(NpcChatterCarrier.Of(
            Mine, At("Shinrarta Dezhra", mode: FlightMode.Supercruise)).Present);

        // Nor is a pad inside somebody else's station in the same system — that tower is theirs,
        // and casting it as the carrier's is the reported fault pointed the other way.
        Assert.False(NpcChatterCarrier.Of(
            Mine,
            At("Shinrarta Dezhra", docked: true, station: "Jameson Memorial",
                stationType: "Orbis", mode: FlightMode.Docked, marketId: 128_666_762)).Present);

        // A carrier parked elsewhere is owned and not at hand.
        var away = NpcChatterCarrier.Of(Mine, At("Sol"));

        Assert.True(away.Owned);
        Assert.False(away.Present);

        // And no carrier at all is none of the above.
        Assert.False(NpcChatterCarrier.Of(CarrierState.None, At("Sol")).Owned);
        Assert.False(NpcChatterCarrier.Of(null, null).Owned);
    }

    [Fact]
    public void ItsTowerAndCaptainCarryTheCastRolesAndNobodyElseDoes()
    {
        var here = NpcChatterCarrier.Of(Mine, At("Shinrarta Dezhra"));

        var lines = NpcChatter.Parse(
            "Tower: Pad four is yours, Rikkard. Mind the strut this time.\n"
            + "Ana Rikkard: One scrape, and I hear about it for a year.\n"
            + "Carrier Captain: The strut remembers, Rikkard.\n"
            + "Captain Reyes: Some of us have cargo to shift.",
            NpcChatterKind.Controller,
            here);

        Assert.Equal(4, lines.Count);
        Assert.Equal(VoiceRole.TowerControl, lines[0].Role);
        Assert.Equal(VoiceRole.CarrierCaptain, lines[2].Role);

        // An invented pilot is an invented nobody, rank or no rank: "Captain Reyes" is a person
        // with a title, not the captain of this ship.
        Assert.Null(lines[1].Role);
        Assert.Null(lines[3].Role);

        // And away from his carrier, a controller called Tower is a station's.
        var elsewhere = NpcChatter.Parse(
            "Tower: Pad four is yours, Rikkard.\nAna Rikkard: On my way.",
            NpcChatterKind.Controller,
            NpcChatterCarrier.Of(Mine, At("Sol", docked: true, station: "Abraham Lincoln",
                stationType: "Orbis", mode: FlightMode.Docked)));

        Assert.All(elsewhere, line => Assert.Null(line.Role));
    }

    [Fact]
    public void AnInventedDepartureTakesTheWholeExchangeWithIt()
    {
        var here = NpcChatterCarrier.Of(Mine, At("Shinrarta Dezhra"));

        // Its own tower needs no subject: "we" is the carrier.
        Assert.Empty(NpcChatter.Parse(
            "Tower: Last call, we jump in twenty minutes.\n"
            + "Ana Rikkard: Then I am not unloading first.",
            NpcChatterKind.Controller,
            here));

        // Somebody else has to say which carrier they mean — and then it goes the same way,
        // whether or not the Commander is anywhere near it.
        Assert.Empty(NpcChatter.Parse(
            "Ana Rikkard: Heard Nomad's Rest is casting off tonight.\n"
            + "Vera Kolt: Always is, that one.",
            NpcChatterKind.Passersby,
            NpcChatterCarrier.Of(Mine, At("Sol"))));

        // A freighter crew's own jump is their own business and survives.
        var theirs = NpcChatter.Parse(
            "Ana Rikkard: I jump for Sol as soon as the pad clears.\n"
            + "Vera Kolt: Take the long way, the lane is thick tonight.",
            NpcChatterKind.Passersby,
            here);

        Assert.Equal(2, theirs.Count);
    }

    [Fact]
    public void AJumpThatIsActuallyScheduledMayBeTalkedAbout()
    {
        var leaving = NpcChatterCarrier.Of(
            Mine with { DestinationSystem = "Sol" }, At("Shinrarta Dezhra"));

        Assert.True(leaving.JumpScheduled);

        var lines = NpcChatter.Parse(
            "Tower: Last call, we jump for Sol in twenty minutes.\n"
            + "Ana Rikkard: Then I am not unloading first.",
            NpcChatterKind.Controller,
            leaving);

        Assert.Equal(2, lines.Count);
    }

    [Fact]
    public void TheInstructionNamesTheTwoPostsAndRefusesTheJumpItIsNotMaking()
    {
        var here = NpcChatterCarrier.Of(Mine, At("Shinrarta Dezhra"));

        var controller = NpcChatter.Instruction(NpcChatterKind.Controller, here);

        Assert.Contains("own fleet carrier Nomad's Rest", controller, StringComparison.Ordinal);
        Assert.Contains("exactly Tower or exactly Captain", controller, StringComparison.Ordinal);
        Assert.Contains("no jump scheduled", controller, StringComparison.Ordinal);

        // The jump rule holds wherever the carrier is: the live game state names it parked three
        // hundred light years away just as clearly.
        var away = NpcChatter.Instruction(
            NpcChatterKind.Passersby, NpcChatterCarrier.Of(Mine, At("Sol")));

        Assert.Contains("no jump scheduled", away, StringComparison.Ordinal);
        Assert.DoesNotContain("exactly Tower", away, StringComparison.Ordinal);

        // And a Commander with no carrier hears about neither.
        var none = NpcChatter.Instruction(NpcChatterKind.Passersby, NpcChatterCarrier.None);

        Assert.DoesNotContain("carrier", none, StringComparison.OrdinalIgnoreCase);
    }
}
