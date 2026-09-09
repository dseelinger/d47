using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class CalloutTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState StateFrom(params string[] lines)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        foreach (var line in lines)
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    private static CalloutContext Context(
        CommanderGameState? state = null,
        GameStatus? status = null,
        NavRoute? route = null,
        IEnumerable<string>? events = null,
        bool priming = false,
        int atSecond = 0) =>
        new(
            Start.AddSeconds(atSecond),
            priming,
            state,
            status ?? GameStatus.Unknown,
            route ?? NavRoute.None,
            [.. (events ?? []).Select(Event)]);

    private static GameStatus Status(StatusFlags flags, double? fuel = null, double? cargo = null) =>
        new()
        {
            Flags = flags | StatusFlags.InMainShip,
            FuelMain = fuel,
            Cargo = cargo,
            ReadAt = Start,
        };

    // ---- Star classification ------------------------------------------------------------

    [Theory]
    [InlineData("K", true)]
    [InlineData("G", true)]
    [InlineData("M", true)]
    [InlineData("O", true)]
    [InlineData("K_OrangeGiant", true)]
    [InlineData("M_RedSuperGiant", true)]
    [InlineData("A_BlueWhiteSuperGiant", true)]
    [InlineData("N", false)]
    [InlineData("DA", false)]
    [InlineData("H", false)]
    [InlineData("T", false)]
    [InlineData("Y", false)]
    public void ScoopabilityIsDecidedByClassNotByFirstLetter(string starClass, bool scoopable) =>
        Assert.Equal(scoopable, StarClasses.IsScoopable(starClass));

    [Fact]
    public void HerbigAeBeIsNotScoopableDespiteStartingWithA()
    {
        // The specific case a first-letter KGBFOAM test gets wrong, and the reason the scoopable set is
        // matched exactly rather than by prefix.
        Assert.False(StarClasses.IsScoopable("AeBe"));
        Assert.True(StarClasses.IsScoopable("A"));
    }

    [Fact]
    public void AnUnrecognisedClassIsUnknownRatherThanUnscoopable()
    {
        // Reporting unknown as "no fuel here" routes a Commander around a star that would have refuelled
        // them, which is its own kind of harm.
        Assert.Null(StarClasses.IsScoopable("SomeFutureClass"));
        Assert.Null(StarClasses.IsScoopable(null));
    }

    [Fact]
    public void NeutronStarsAndWhiteDwarfsAreHazardous()
    {
        Assert.True(StarClasses.IsHazardous("N"));
        Assert.True(StarClasses.IsHazardous("DAV"));
        Assert.False(StarClasses.IsHazardous("K"));

        // The suffixed giants start with D nowhere, but the guard is there because a class containing an
        // underscore is a variant name rather than a dwarf classification.
        Assert.False(StarClasses.IsWhiteDwarf("K_OrangeGiant"));
    }

    // ---- The engine ---------------------------------------------------------------------

    private sealed class FixedCallout(string id, params Announcement[] announcements) : ICallout
    {
        public string Id => id;

        public int Examined { get; private set; }

        public IEnumerable<Announcement> Examine(CalloutContext context)
        {
            Examined++;
            return announcements;
        }
    }

    private sealed class ThrowingCallout : ICallout
    {
        public string Id => "broken";

        public IEnumerable<Announcement> Examine(CalloutContext context) =>
            throw new InvalidOperationException("callout bug");
    }

    private static CalloutEngine Engine(params ICallout[] callouts)
    {
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance);

        foreach (var callout in callouts)
        {
            engine.Add(callout);
        }

        return engine;
    }

    [Fact]
    public void NothingIsAnnouncedWhilePriming()
    {
        var engine = Engine(new FixedCallout("test", new Announcement("k", "backlog")));

        engine.Tick(Context(priming: true));

        // Starting d47 after Elite must not read out the last two hours.
        Assert.Empty(engine.Drain());
    }

    [Fact]
    public void CalloutsStillRunWhilePrimingSoTheyCanFoldTheBacklog()
    {
        var callout = new FixedCallout("test", new Announcement("k", "backlog"));
        var engine = Engine(callout);

        engine.Tick(Context(priming: true));

        // A callout has to see the backlog to know what "changed" means on the first live tick.
        Assert.Equal(1, callout.Examined);
    }

    [Fact]
    public void ABrokenCalloutDoesNotSilenceTheOnesAfterIt()
    {
        var engine = Engine(new ThrowingCallout(), new FixedCallout("after", new Announcement("k", "still here")));

        engine.Tick(Context());

        var drained = Assert.Single(engine.Drain());
        Assert.Equal("still here", drained.Text);
    }

    [Fact]
    public void TheSameWarningIsNotRepeatedWhileItsCooldownRuns()
    {
        var announcement = new Announcement("fuel.low", "Fuel low.") { Cooldown = TimeSpan.FromMinutes(2) };
        var engine = Engine(new FixedCallout("test", announcement));

        engine.Tick(Context(atSecond: 0));
        engine.Tick(Context(atSecond: 30));
        engine.Tick(Context(atSecond: 60));

        // At 10 Hz a condition-based warning is true on hundreds of consecutive ticks.
        Assert.Single(engine.Drain());

        engine.Tick(Context(atSecond: 200));
        Assert.Single(engine.Drain());
    }

    [Fact]
    public void OneCalloutCanBeSilencedWithoutSilencingTheRest()
    {
        var engine = Engine(
            new FixedCallout("route", new Announcement("route", "chatty")),
            new FixedCallout("danger", new Announcement("danger", "important")));

        engine.SetEnabled("route", false);
        engine.Tick(Context());

        // Finding route progress chatty is not a reason to lose the interdiction warning.
        var drained = Assert.Single(engine.Drain());
        Assert.Equal("important", drained.Text);
    }

    /// <summary>
    /// A callout switched off within seconds of it speaking is reported, so the debrief can ask about
 /// it at the end of the session.
    /// </summary>
    [Fact]
    public void SilencingAWarningSecondsAfterItFiredIsReported()
    {
        var engine = Engine(new FixedCallout("fuel", new Announcement("fuel.low", "fuel is low")));

        CalloutSilenced? reported = null;
        engine.Silenced += silenced => reported = silenced;

        engine.Tick(Context());
        Assert.Single(engine.Drain());

        engine.SetEnabled("fuel", false, Start.AddSeconds(4));

        Assert.NotNull(reported);
        Assert.Equal("fuel", reported.Id);
        Assert.Equal(TimeSpan.FromSeconds(4), reported.After);
    }

    /// <summary>And switching one off an hour later is what it looks like — an unrelated decision.</summary>
    [Fact]
    public void SilencingAWarningLongAfterwardsIsNotReported()
    {
        var engine = Engine(new FixedCallout("fuel", new Announcement("fuel.low", "fuel is low")));

        var reported = false;
        engine.Silenced += _ => reported = true;

        engine.Tick(Context());
        engine.Drain();

        engine.SetEnabled("fuel", false, Start.AddHours(1));

        Assert.False(reported);
    }

    /// <summary>The state is not the fact; the transition is.</summary>
    [Fact]
    public void ReapplyingTheSameSettingReportsNothing()
    {
        var engine = Engine(new FixedCallout("fuel", new Announcement("fuel.low", "fuel is low")));

        engine.Tick(Context());
        engine.Drain();

        var reports = 0;
        engine.Silenced += _ => reports++;

        engine.SetEnabled("fuel", false, Start.AddSeconds(2));
        engine.SetEnabled("fuel", false, Start.AddSeconds(3));
        engine.SetEnabled("fuel", false, Start.AddSeconds(4));

        Assert.Equal(1, reports);
    }

    /// <summary>
    /// A callout silenced without ever having spoken says nothing about anything, and a caller with no
    /// clock is not asking.
    /// </summary>
    [Fact]
    public void NothingIsReportedForACalloutThatNeverSpokeOrForACallerWithNoClock()
    {
        var engine = Engine(new FixedCallout("fuel", new Announcement("fuel.low", "fuel is low")));

        var reported = false;
        engine.Silenced += _ => reported = true;

        engine.SetEnabled("fuel", false, Start);
        Assert.False(reported);

        engine.SetEnabled("route", true);
        engine.Tick(Context());
        engine.Drain();

        engine.SetEnabled("fuel", true);
        engine.SetEnabled("fuel", false);
        Assert.False(reported);
    }

    [Fact]
    public void DrainingTakesEverythingOnceAndLeavesTheQueueEmpty()
    {
        var engine = Engine(new FixedCallout("test", new Announcement("k", "said")));

        engine.Tick(Context());

        Assert.Single(engine.Drain());
        Assert.Empty(engine.Drain());
    }

    // ---- Danger -------------------------------------------------------------------------

    [Fact]
    public void ShieldsGoingDownIsUrgentAndFiresOnTheEdge()
    {
        var callout = new DangerCallout();
        var up = Status(StatusFlags.ShieldsUp);
        var down = Status(StatusFlags.None);

        Assert.Empty(callout.Examine(Context(StateFrom(), up)));

        var announced = Assert.Single(callout.Examine(Context(StateFrom(), down, atSecond: 1)));
        Assert.Equal(CalloutUrgency.Urgent, announced.Urgency);
        Assert.Contains("Shields", announced.Text);

        // Status.json is rewritten several times a second.
        Assert.Empty(callout.Examine(Context(StateFrom(), down, atSecond: 2)));
    }

    [Fact]
    public void AShipWithNoShieldGeneratorIsNotWarnedAboutItsShields()
    {
        var unshielded = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"hauler","ShipID":3,"Modules":[{"Slot":"PowerPlant","Item":"int_powerplant_size2_class2"},{"Slot":"MainEngines","Item":"int_engine_size2_class2"}]}""");

        var callout = new DangerCallout();

        Assert.Empty(callout.Examine(Context(unshielded, Status(StatusFlags.ShieldsUp))));
        Assert.Empty(callout.Examine(Context(unshielded, Status(StatusFlags.None), atSecond: 1)));

        // And the journal's own transition says nothing either, which is the second road to the same line.
        Assert.DoesNotContain(
            callout.Examine(Context(
                unshielded,
                Status(StatusFlags.None),
                atSecond: 2,
                events: ["""{"timestamp":"3311-01-01T00:00:02Z","event":"ShieldState","ShieldsUp":false}"""])),
            announced => announced.Key == "danger.shields");
    }

    /// <summary>
    /// And an SRV's shields still are, on that same unshielded hull — which is the half the corpus had
 /// to be asked about.
    /// </summary>
    [Fact]
    public void AnSrvsShieldsAreStillWarnedAboutUnderAnUnshieldedHull()
    {
        var unshielded = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"hauler","ShipID":3,"Modules":[{"Slot":"PowerPlant","Item":"int_powerplant_size2_class2"},{"Slot":"BuggyBay","Item":"int_buggybay_size2_class2"}]}""");

        var inSrv = new GameStatus { Flags = StatusFlags.InSrv, ReadAt = Start };

        var announced = Assert.Single(new DangerCallout().Examine(Context(
            unshielded,
            inSrv,
            events: ["""{"timestamp":"3311-01-01T00:00:01Z","event":"ShieldState","ShieldsUp":false}"""])));

        Assert.Equal("danger.shields", announced.Key);
    }

    /// <summary>A loadout d47 has not read says the warning anyway.</summary>
    [Fact]
    public void AnUnknownLoadoutStillWarns()
    {
        var callout = new DangerCallout();

        Assert.Empty(callout.Examine(Context(StateFrom(), Status(StatusFlags.ShieldsUp))));

        Assert.Single(callout.Examine(Context(StateFrom(), Status(StatusFlags.None), atSecond: 1)));
    }

    [Fact]
    public void SubmittingToAnInterdictionIsNotAnnouncedAsAnEmergency()
    {
        var callout = new DangerCallout();

        var submitted = callout.Examine(Context(
            StateFrom(),
            events: ["""{"timestamp":"3311-01-01T00:00:01Z","event":"Interdicted","Submitted":true,"Interdictor":"Someone"}"""]));

        // Submitting is a choice the Commander made.
        Assert.DoesNotContain(submitted, a => a.Key == "danger.interdicted");
    }

    [Fact]
    public void HullDamageReportsTheIntegrityTheEventCarried()
    {
        var callout = new DangerCallout();

        var announced = callout.Examine(Context(
            StateFrom(),
            events: ["""{"timestamp":"3311-01-01T00:00:01Z","event":"HullDamage","Health":0.62,"PlayerPilot":true}"""]))
            .Single(a => a.Key == "danger.hull");

        Assert.Equal(CalloutUrgency.Urgent, announced.Urgency);
        Assert.Contains("62", announced.Text);
    }

    [Fact]
    public void AFullHoldIsMeasuredAgainstTheLoadoutsCapacity()
    {
        var callout = new DangerCallout();
        var state = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"Python","CargoCapacity":64}""");

        // Shields deliberately up: this test is about the hold, and a status with everything clear would also
        // trip the shields-down warning on the same tick.
        Assert.Empty(callout.Examine(Context(state, Status(StatusFlags.ShieldsUp, cargo: 60))));

        // Status.json reports the tonnage and never the capacity; only the Loadout event can say what "full"
        // is.
        var announced = Assert.Single(
            callout.Examine(Context(state, Status(StatusFlags.ShieldsUp, cargo: 64), atSecond: 1)));
        Assert.Contains("full", announced.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DangerFlagsAreIgnoredWhileOnFoot()
    {
        var callout = new DangerCallout();
        var onFoot = new GameStatus { Flags = StatusFlags.None, ReadAt = Start };

        // A shields-down warning while walking around a concourse is noise.
        Assert.Empty(callout.Examine(Context(StateFrom(), onFoot)));
    }

    // ---- Fuel and the strand case -------------------------------------------------------

    private static NavRoute Route(params (string System, string Class, double X)[] hops) => new()
    {
        Hops = [.. hops.Select(hop => new RouteHop(hop.System, hop.Class) { Position = (hop.X, 0, 0) })],
        ReadAt = Start,
    };

    [Fact]
    public void AnUnscoopableNextStarWithAnUnreachableOneBeyondItIsUrgent()
    {
        var callout = new FuelCallout();
        var state = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"Anaconda","MaxJumpRange":50,"FuelCapacity":{"Main":32}}""",
            """{"timestamp":"3311-01-01T00:00:01Z","event":"FSDJump","StarSystem":"Here","JumpDist":40}""");

        // Next hop is a brown dwarf, and the hop after it is 80 ly away against a 50 ly range.
        var route = Route(("Here", "K", 0), ("Dead End", "T", 40), ("Far", "K", 120));

        var announced = callout
            .Examine(Context(state, Status(StatusFlags.None, fuel: 30), route))
            .Single(a => a.Key == "fuel.route.strand");

        Assert.Equal(CalloutUrgency.Urgent, announced.Urgency);
        Assert.Contains("Dead End", announced.Text);
        Assert.Contains("80", announced.Text);
    }

    [Fact]
    public void AScoopableNextStarProducesNoRouteWarningAtAll()
    {
        var callout = new FuelCallout();
        var state = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"Anaconda","MaxJumpRange":50,"FuelCapacity":{"Main":32}}""",
            """{"timestamp":"3311-01-01T00:00:01Z","event":"FSDJump","StarSystem":"Here","JumpDist":40}""");

        var route = Route(("Here", "K", 0), ("Fine", "G", 40), ("Far", "K", 120));

        Assert.DoesNotContain(
            callout.Examine(Context(state, Status(StatusFlags.None, fuel: 30), route)),
            a => a.Key.StartsWith("fuel.route", StringComparison.Ordinal));
    }

    /// <summary>
    /// Every route warning here is a claim about scooping, so a ship with no Fuel Scoop fitted hears
 /// none of them.
    /// </summary>
    [Theory]
    [InlineData("int_fuelscoop_size6_class5", true)]
    [InlineData("int_cargorack_size6_class1", false)]
    public void TheStrandWarningIsOnlySaidToAShipThatCanScoop(string module, bool warned)
    {
        var callout = new FuelCallout();
        var state = StateFrom(
            $$"""{"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"Anaconda","MaxJumpRange":50,"FuelCapacity":{"Main":32},"Modules":[{"Slot":"Slot03_Size6","Item":"{{module}}","On":true,"Health":1.0}]}""",
            """{"timestamp":"3311-01-01T00:00:01Z","event":"FSDJump","StarSystem":"Here","JumpDist":40}""");

        var route = Route(("Here", "K", 0), ("Dead End", "T", 40), ("Far", "K", 120));

        Assert.Equal(
            warned,
            callout
                .Examine(Context(state, Status(StatusFlags.None, fuel: 30), route))
                .Any(a => a.Key.StartsWith("fuel.route", StringComparison.Ordinal)));
    }

    /// <summary>The tank warnings are not scoop talk and stay.</summary>
    [Fact]
    public void LowFuelIsStillSaidToAShipWithNoScoop()
    {
        var callout = new FuelCallout();
        var state = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"Anaconda","FuelCapacity":{"Main":32},"Modules":[{"Slot":"Slot03_Size6","Item":"int_cargorack_size6_class1","On":true,"Health":1.0}]}""");

        Assert.Empty(callout.Examine(Context(state, Status(StatusFlags.None, fuel: 16))));

        var low = callout
            .Examine(Context(state, Status(StatusFlags.None, fuel: 6), atSecond: 1))
            .Single(a => a.Key == "fuel.low");

        Assert.Contains("19", low.Text);
    }

    /// <summary>
    /// An unread loadout is not a ship with no scoop. d47 joined this session in flight; suppressing a
    /// stranding warning on no evidence at all is the expensive mistake.
    /// </summary>
    [Fact]
    public void AnUnreadLoadoutStillHearsTheStrandWarning()
    {
        var callout = new FuelCallout();
        var state = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"FSDJump","StarSystem":"Here","JumpDist":40}""");

        var route = Route(("Here", "K", 0), ("Dead End", "T", 40), ("Far", "K", 120));

        Assert.NotEmpty(callout.Examine(Context(state, Status(StatusFlags.None, fuel: 30), route)));
    }

    [Fact]
    public void TheRouteWarningIsEvaluatedOncePerSystemNotOncePerTick()
    {
        var callout = new FuelCallout();
        var state = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"Anaconda","MaxJumpRange":50}""",
            """{"timestamp":"3311-01-01T00:00:01Z","event":"FSDJump","StarSystem":"Here","JumpDist":40}""");

        var route = Route(("Here", "K", 0), ("Dead End", "T", 40), ("Far", "K", 120));

        Assert.NotEmpty(callout.Examine(Context(state, Status(StatusFlags.None, fuel: 30), route)));

        // Without this the same warning is recomputed ten times a second for as long as the Commander sits
        // there deciding what to do about it.
        Assert.Empty(callout.Examine(Context(state, Status(StatusFlags.None, fuel: 30), route, atSecond: 1)));
    }

    [Fact]
    public void LowFuelIsMeasuredAgainstTheTankTheLoadoutReported()
    {
        var callout = new FuelCallout();
        var state = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"Anaconda","FuelCapacity":{"Main":32}}""");

        Assert.Empty(callout.Examine(Context(state, Status(StatusFlags.None, fuel: 16))));

        var low = callout
            .Examine(Context(state, Status(StatusFlags.None, fuel: 6), atSecond: 1))
            .Single(a => a.Key == "fuel.low");

        Assert.Contains("19", low.Text);
    }

    [Fact]
    public void CriticalFuelOutranksLowFuel()
    {
        var callout = new FuelCallout();
        var state = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"Anaconda","FuelCapacity":{"Main":32}}""");

        var critical = callout
            .Examine(Context(state, Status(StatusFlags.None, fuel: 2)))
            .Single(a => a.Key == "fuel.critical");

        Assert.Equal(CalloutUrgency.Urgent, critical.Urgency);
    }

    // ---- Long jump ----------------------------------------------------------------------

    [Fact]
    public void ALongJumpIsRemarkedOnOnlyAfterHyperspaceIsActuallyEntered()
    {
        var callout = new LongJumpCallout { Threshold = TimeSpan.FromSeconds(20) };
        var state = StateFrom();

        // StartJump is written while the FSD is still charging.
        callout.Examine(Context(state, events:
            ["""{"timestamp":"3311-01-01T00:00:00Z","event":"StartJump","JumpType":"Hyperspace","StarSystem":"Far Away"}"""]))
            .ToArray();

        Assert.Empty(callout.Examine(Context(state, atSecond: 10)));

        var remark = Assert.Single(callout.Examine(Context(state, atSecond: 25)));
        Assert.Contains("25 seconds", remark.Text);

        // Once, not once per tick.
        Assert.Empty(callout.Examine(Context(state, atSecond: 30)));
    }

    [Fact]
    public void ASupercruiseStartJumpIsNotAJump()
    {
        var callout = new LongJumpCallout { Threshold = TimeSpan.FromSeconds(20) };
        var state = StateFrom();

        callout.Examine(Context(state, events:
            ["""{"timestamp":"3311-01-01T00:00:00Z","event":"StartJump","JumpType":"Supercruise"}"""]))
            .ToArray();

        // JumpType says "Supercruise" far more often than "Hyperspace".
        Assert.Empty(callout.Examine(Context(state, atSecond: 60)));
    }

    [Fact]
    public void ArrivingEndsTheJumpBeingTimed()
    {
        var callout = new LongJumpCallout { Threshold = TimeSpan.FromSeconds(20) };
        var state = StateFrom();

        callout.Examine(Context(state, events:
            ["""{"timestamp":"3311-01-01T00:00:00Z","event":"StartJump","JumpType":"Hyperspace","StarSystem":"Far Away"}"""]))
            .ToArray();

        callout.Examine(Context(state, atSecond: 5, events:
            ["""{"timestamp":"3311-01-01T00:00:05Z","event":"FSDJump","StarSystem":"Far Away","JumpDist":30}"""]))
            .ToArray();

        Assert.Empty(callout.Examine(Context(state, atSecond: 60)));
    }

    // ---- Route progress -----------------------------------------------------------------

    [Fact]
    public void RouteProgressIsReportedEveryNJumps()
    {
        var callout = new RouteCallout { EveryNJumps = 3 };
        var route = Route(("A", "K", 0), ("B", "K", 10), ("C", "K", 20), ("D", "K", 30));
        var jump = """{"timestamp":"3311-01-01T00:00:01Z","event":"FSDJump","StarSystem":"A","JumpDist":10}""";

        var state = StateFrom(jump);

        Assert.DoesNotContain(
            callout.Examine(Context(state, route: route, events: [jump])), a => a.Key == "route.progress");
        Assert.DoesNotContain(
            callout.Examine(Context(state, route: route, events: [jump], atSecond: 1)), a => a.Key == "route.progress");

        var progress = callout
            .Examine(Context(state, route: route, events: [jump], atSecond: 2))
            .Single(a => a.Key == "route.progress");

        Assert.Contains("3 jumps remaining", progress.Text);
        Assert.Contains("scoopable", progress.Text);
    }

    [Theory]
    [InlineData("""{"Slot":"Slot03_Size6","Item":"int_fuelscoop_size6_class5","On":true,"Health":1.0}""", true)]
    [InlineData("""{"Slot":"Slot03_Size6","Item":"int_cargorack_size6_class1","On":true,"Health":1.0}""", false)]
    public void ScoopabilityIsOnlySaidToAShipThatCanScoop(string module, bool said)
    {
        var callout = new RouteCallout { EveryNJumps = 1 };
        var route = Route(("A", "K", 0), ("B", "K", 10), ("C", "K", 20));
        var jump = """{"timestamp":"3311-01-01T00:00:01Z","event":"FSDJump","StarSystem":"A","JumpDist":10}""";

        var state = StateFrom(
            $$"""{"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"Python","Modules":[{{module}}]}""",
            jump);

        var progress = callout
            .Examine(Context(state, route: route, events: [jump]))
            .Single(a => a.Key == "route.progress");

        // The clause is dropped rather than reworded: "not scoopable" would imply the others would have been.
        Assert.Equal(said, progress.Text.Contains("scoopable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Next is B", progress.Text);
    }

    [Fact]
    public void AnUnreadLoadoutIsNotAShipWithNoScoop()
    {
        var callout = new RouteCallout { EveryNJumps = 1 };
        var route = Route(("A", "K", 0), ("B", "K", 10), ("C", "K", 20));
        var jump = """{"timestamp":"3311-01-01T00:00:01Z","event":"FSDJump","StarSystem":"A","JumpDist":10}""";

        // d47 joined this session in flight.
        var progress = callout
            .Examine(Context(StateFrom(jump), route: route, events: [jump]))
            .Single(a => a.Key == "route.progress");

        Assert.Contains("scoopable", progress.Text);
    }

    [Fact]
    public void AHazardOnTheVeryNextJumpIsSaidRegardlessOfTheReportingInterval()
    {
        var callout = new RouteCallout { EveryNJumps = 100 };
        var route = Route(("A", "K", 0), ("B", "N", 10));
        var jump = """{"timestamp":"3311-01-01T00:00:01Z","event":"FSDJump","StarSystem":"A","JumpDist":10}""";

        var hazard = callout
            .Examine(Context(StateFrom(jump), route: route, events: [jump]))
            .Single(a => a.Key == "route.hazard");

        // "Every 100 jumps" would land on the neutron star roughly never.
        Assert.Equal(CalloutUrgency.Urgent, hazard.Urgency);
        Assert.Contains("neutron", hazard.Text);
    }

    // ---- Arrival ------------------------------------------------------------------------

    [Fact]
    public void ArrivingHomeIsAnnouncedOnceAndOnlyOnArrival()
    {
        var callout = new ArrivalCallout { HomeSystem = "Shinrarta Dezhra" };

        var elsewhere = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"FSDJump","StarSystem":"Sol","JumpDist":1}""");
        Assert.Empty(callout.Examine(Context(elsewhere)));

        var home = StateFrom(
            """{"timestamp":"3311-01-01T00:00:01Z","event":"FSDJump","StarSystem":"Shinrarta Dezhra","JumpDist":1}""");
        var announced = Assert.Single(callout.Examine(Context(home, atSecond: 1)));
        Assert.Contains("Shinrarta Dezhra", announced.Text);

        // Sitting in the system is not arriving in it again.
        Assert.Empty(callout.Examine(Context(home, atSecond: 2)));
    }

    [Fact]
    public void PrimingRecordsWhereTheCommanderIsWithoutAnnouncingArrival()
    {
        var callout = new ArrivalCallout { HomeSystem = "Shinrarta Dezhra" };
        var home = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Location","StarSystem":"Shinrarta Dezhra"}""");

        Assert.Empty(callout.Examine(Context(home, priming: true)));

        // Without the priming tick recording the system, this would announce an arrival in the system the
        // Commander has been sitting in for an hour.
        Assert.Empty(callout.Examine(Context(home, atSecond: 1)));
    }

    [Fact]
    public void DockingSomewhereWithEngineeringIsNotWorthSaying()
    {
        var callout = new ArrivalCallout();

        var announced = callout.Examine(Context(StateFrom(), events:
            ["""{"timestamp":"3311-01-01T00:00:01Z","event":"Docked","StationName":"Farseer Inc","StarSystem":"Deciat","StationServices":["dock","refuel","engineer"]}"""]));

        Assert.DoesNotContain(announced, a => a.Key == "arrival.engineer");
    }

    // ---- Material milestones ------------------------------------------------------------

    [Fact]
    public void TheFirstUnitOfAMaterialIsAnnouncedWithoutNeedingACapacity()
    {
        var callout = new MaterialMilestoneCallout();
        var collect = """{"timestamp":"3311-01-01T00:00:01Z","event":"MaterialCollected","Category":"Raw","Name":"iron","Name_Localised":"Iron","Count":1}""";

        var announced = Assert.Single(callout.Examine(Context(StateFrom(collect), events: [collect])));

        Assert.Contains("First", announced.Text);
        Assert.Contains("Iron", announced.Text);
    }

    [Fact]
    public void AMaterialAlreadyHeldAtStartupDoesNotCountAsAFirstUnit()
    {
        var callout = new MaterialMilestoneCallout();
        var collect = """{"timestamp":"3311-01-01T00:00:01Z","event":"MaterialCollected","Category":"Raw","Name":"iron","Count":9}""";
        var state = StateFrom(collect);

        Assert.Empty(callout.Examine(Context(state, events: [collect], priming: true)));

        // This is what "primed from the session backlog" buys: the second unit collected after startup is not
        // announced as the first.
        Assert.Empty(callout.Examine(Context(state, events: [collect], atSecond: 1)));
    }

    [Fact]
    public void PercentageMilestonesAreSilentWhileTheCapacityIsUnknown()
    {
        var callout = new MaterialMilestoneCallout();
        var collect = """{"timestamp":"3311-01-01T00:00:01Z","event":"MaterialCollected","Category":"Raw","Name":"iron","Count":150}""";

        var announced = callout.Examine(Context(StateFrom(collect), events: [collect])).ToArray();

        // The shipped behaviour.
        Assert.Single(announced);
        Assert.StartsWith("materials.first.", announced[0].Key);
    }

    [Fact]
    public void PercentageMilestonesFireOnceEachWhenACapacityIsSupplied()
    {
        var callout = new MaterialMilestoneCallout { Capacity = _ => 100 };
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        var said = new List<string>();

        foreach (var (count, second) in (ReadOnlySpan<(int, int)>)[(1, 1), (30, 2), (60, 3), (80, 4), (100, 5)])
        {
            var line = $$"""{"timestamp":"3311-01-01T00:00:0{{second}}Z","event":"Materials","Raw":[{"Name":"iron","Count":{{count}}}],"Manufactured":[],"Encoded":[]}""";
            var collect = $$"""{"timestamp":"3311-01-01T00:00:0{{second}}Z","event":"MaterialCollected","Category":"Raw","Name":"iron","Count":1}""";

            store.Apply(Event(line));
            store.Apply(Event(collect));

            said.AddRange(callout
                .Examine(Context(store.Active, events: [collect], atSecond: second))
                .Select(a => a.Key));
        }

        Assert.Equal(
            [
                "materials.first.iron",
                "materials.milestone.iron",  // 30% crosses 25
                "materials.milestone.iron",  // 60% crosses 50
                "materials.milestone.iron",  // 80% crosses 75
                "materials.full.iron",
            ],
            said);
    }

    /// <summary>Trading a full material away and gathering it again announces it again.</summary>
    [Fact]
    public void AMaterialTradedAwayAndGatheredAgainIsAnnouncedAgain()
    {
        var callout = new MaterialMilestoneCallout { Capacity = _ => 100 };
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        var said = new List<string>();

        void Collect(int held, int second)
        {
            var snapshot = $$"""{"timestamp":"3311-01-01T00:00:0{{second}}Z","event":"Materials","Raw":[{"Name":"iron","Count":{{held}}}],"Manufactured":[],"Encoded":[]}""";
            var collect = $$"""{"timestamp":"3311-01-01T00:00:0{{second}}Z","event":"MaterialCollected","Category":"Raw","Name":"iron","Count":1}""";

            store.Apply(Event(snapshot));
            store.Apply(Event(collect));

            said.AddRange(callout
                .Examine(Context(store.Active, events: [collect], atSecond: second))
                .Select(a => a.Key));
        }

        // Filled up.
        Collect(100, 1);
        Assert.Contains("materials.full.iron", said);

        // And then traded away at a materials trader, which the inventory folds and the tracker has to
        // follow.
        store.Apply(Event("""{"timestamp":"3311-01-01T00:00:02Z","event":"MaterialTrade","MarketID":1,"TraderType":"raw","Paid":{"Material":"iron","Category":"Raw","Quantity":101},"Received":{"Material":"nickel","Category":"Raw","Quantity":10}}"""));

        Assert.Equal(0, store.Active!.Materials.Find("iron")?.Count ?? 0);

        // A tick with nothing on it, which is what the tick loop is mostly made of.
        Assert.Empty(callout.Examine(Context(store.Active, atSecond: 2)));

        said.Clear();

        // Gathering it again.
        Collect(26, 3);

        Assert.Equal(["materials.milestone.iron"], said);

        // And so is the rest of the way back up.
        said.Clear();
        Collect(51, 4);
        Collect(76, 5);
        Collect(100, 6);

        Assert.Equal(
            ["materials.milestone.iron", "materials.milestone.iron", "materials.full.iron"],
            said);
    }

    /// <summary>Spending a couple below a threshold and picking them back up does not re-announce it.</summary>
    [Fact]
    public void DippingBelowAThresholdAndBackDoesNotRepeatIt()
    {
        var callout = new MaterialMilestoneCallout { Capacity = _ => 100 };
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        var said = new List<string>();

        void Collect(int held, int second)
        {
            var snapshot = $$"""{"timestamp":"3311-01-01T00:00:0{{second}}Z","event":"Materials","Raw":[{"Name":"iron","Count":{{held}}}],"Manufactured":[],"Encoded":[]}""";
            var collect = $$"""{"timestamp":"3311-01-01T00:00:0{{second}}Z","event":"MaterialCollected","Category":"Raw","Name":"iron","Count":1}""";

            store.Apply(Event(snapshot));
            store.Apply(Event(collect));

            said.AddRange(callout
                .Examine(Context(store.Active, events: [collect], atSecond: second))
                .Select(a => a.Key));
        }

        Collect(60, 1);
        said.Clear();

        // Still over half, so nothing has been un-passed.
        Collect(55, 2);
        Collect(58, 3);

        Assert.Empty(said);
    }
}
