using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// Phase 8, driven a tick at a time. Every test here supplies its own clock and its own events,
/// so a warning that only fires after two minutes is asserted without spending two minutes.
/// </summary>
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
        // The specific case a first-letter KGBFOAM test gets wrong, and the reason the
        // scoopable set is matched exactly rather than by prefix.
        Assert.False(StarClasses.IsScoopable("AeBe"));
        Assert.True(StarClasses.IsScoopable("A"));
    }

    [Fact]
    public void AnUnrecognisedClassIsUnknownRatherThanUnscoopable()
    {
        // Reporting unknown as "no fuel here" routes a Commander around a star that would have
        // refuelled them, which is its own kind of harm.
        Assert.Null(StarClasses.IsScoopable("SomeFutureClass"));
        Assert.Null(StarClasses.IsScoopable(null));
    }

    [Fact]
    public void NeutronStarsAndWhiteDwarfsAreHazardous()
    {
        Assert.True(StarClasses.IsHazardous("N"));
        Assert.True(StarClasses.IsHazardous("DAV"));
        Assert.False(StarClasses.IsHazardous("K"));

        // The suffixed giants start with D nowhere, but the guard is there because a class
        // containing an underscore is a variant name rather than a dwarf classification.
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
        // Skipping them entirely is what makes the first real event fire spuriously or not at all.
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

        // At 10 Hz a condition-based warning is true on hundreds of consecutive ticks. Without
        // this it would be said until the Commander refuelled or quit.
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

        // Status.json is rewritten several times a second. Announcing on the level rather than
        // the edge would be a warning per tick for as long as the shields stayed down.
        Assert.Empty(callout.Examine(Context(StateFrom(), down, atSecond: 2)));
    }

    /// <summary>
    /// A hull with no shield generator is not warned about (remediation.md 17, item 6).
    /// <para>
    /// Reported as *"no need to announce this on a ship without shields"*. Mining, exploration and
    /// hauling builds routinely fly unshielded, and the flag is then false for the whole session —
    /// so the edge into it is crossed on boarding, when nothing has happened. Measured in the
    /// 916-journal corpus: 527 of 2,853 Loadouts fit no generator, across 22 ships.
    /// </para>
    /// </summary>
    [Fact]
    public void AShipWithNoShieldGeneratorIsNotWarnedAboutItsShields()
    {
        var unshielded = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"hauler","ShipID":3,"Modules":[{"Slot":"PowerPlant","Item":"int_powerplant_size2_class2"},{"Slot":"MainEngines","Item":"int_engine_size2_class2"}]}""");

        var callout = new DangerCallout();

        Assert.Empty(callout.Examine(Context(unshielded, Status(StatusFlags.ShieldsUp))));
        Assert.Empty(callout.Examine(Context(unshielded, Status(StatusFlags.None), atSecond: 1)));

        // And the journal's own transition says nothing either, which is the second road to the
        // same line.
        Assert.DoesNotContain(
            callout.Examine(Context(
                unshielded,
                Status(StatusFlags.None),
                atSecond: 2,
                events: ["""{"timestamp":"3311-01-01T00:00:02Z","event":"ShieldState","ShieldsUp":false}"""])),
            announced => announced.Key == "danger.shields");
    }

    /// <summary>
    /// And an SRV's shields still are, on that same unshielded hull — which is the half the
    /// corpus had to be asked about (remediation.md 17, item 6).
    /// <para>
    /// The Commander's Hauler carries no generator and an SRV bay, and it reports shields going
    /// down <em>and coming back</em>: those are the SRV's, which are real and can be shot away. 22
    /// such events under that one hull, plus 12 more inside an explicit <c>LaunchSRV</c>. A ship's
    /// loadout answers for the ship and for nothing else.
    /// </para>
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

    /// <summary>
    /// A loadout d47 has not read says the warning anyway. The suppression is evidence that the
    /// ship has no shields, and absence of evidence is not that — a missed real shields-down call
    /// costs far more than one spurious line.
    /// </summary>
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

        // Submitting is a choice the Commander made. Reporting it back to them as an emergency
        // is noise.
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

        // Shields deliberately up: this test is about the hold, and a status with everything
        // clear would also trip the shields-down warning on the same tick.
        Assert.Empty(callout.Examine(Context(state, Status(StatusFlags.ShieldsUp, cargo: 60))));

        // Status.json reports the tonnage and never the capacity; only the Loadout event can
        // say what "full" is.
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

    [Fact]
    public void TheRouteWarningIsEvaluatedOncePerSystemNotOncePerTick()
    {
        var callout = new FuelCallout();
        var state = StateFrom(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"Anaconda","MaxJumpRange":50}""",
            """{"timestamp":"3311-01-01T00:00:01Z","event":"FSDJump","StarSystem":"Here","JumpDist":40}""");

        var route = Route(("Here", "K", 0), ("Dead End", "T", 40), ("Far", "K", 120));

        Assert.NotEmpty(callout.Examine(Context(state, Status(StatusFlags.None, fuel: 30), route)));

        // Without this the same warning is recomputed ten times a second for as long as the
        // Commander sits there deciding what to do about it.
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

        // StartJump is written while the FSD is still charging. A Commander who cancels never
        // enters hyperspace, and remarking on a jump that did not happen is worse than silence.
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

        // JumpType says "Supercruise" far more often than "Hyperspace". Treating it as a jump
        // would remark on the length of every supercruise entry.
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

        // Without the priming tick recording the system, this would announce an arrival in the
        // system the Commander has been sitting in for an hour.
        Assert.Empty(callout.Examine(Context(home, atSecond: 1)));
    }

    /// <summary>
    /// Docking used to announce that the station offered engineering, and it no longer does.
    /// <para>
    /// It is not a fact about the station. 3,726 of the 3,759 dockings in the corpus advertise
    /// the service, and all 33 that do not are construction depots — so the callout fired on
    /// 99.1% of dockings and told the Commander something that is true of everywhere they can
    /// dock. Inverting it, which is what the report suggested, would have said nothing about a
    /// construction site instead (remediation.md, "Ray Gateway offers engineering").
    /// </para>
    /// </summary>
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

        // This is what "primed from the session backlog" buys: the second unit collected after
        // startup is not announced as the first.
        Assert.Empty(callout.Examine(Context(state, events: [collect], atSecond: 1)));
    }

    [Fact]
    public void PercentageMilestonesAreSilentWhileTheCapacityIsUnknown()
    {
        var callout = new MaterialMilestoneCallout();
        var collect = """{"timestamp":"3311-01-01T00:00:01Z","event":"MaterialCollected","Category":"Raw","Name":"iron","Count":150}""";

        var announced = callout.Examine(Context(StateFrom(collect), events: [collect])).ToArray();

        // The shipped behaviour. Elite reports no material's cap anywhere, and announcing a
        // percentage of a number d47 does not have would be inventing the number.
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

    /// <summary>
    /// Trading a full material away and gathering it again announces it again.
    /// <para>
    /// <b>This was reported as "materials stop announcing until I restart the app".</b> The
    /// tracker only ever counted up: filling a material at Jameson's Crash Site set its highest
    /// announced milestone to 100, and emptying it at a materials trader left that 100 in place.
    /// Every later collection then found no threshold it had not already passed, so the material
    /// went permanently silent — permanently, because the tracker is in memory and a restart is
    /// what cleared it. Fill and empty a few at one trader stop and most of what a Commander is
    /// gathering has gone quiet at once, which is what "no more announcements" was.
    /// </para>
    /// </summary>
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

        // And then traded away at a materials trader, which the inventory folds and the tracker
        // has to follow.
        store.Apply(Event("""{"timestamp":"3311-01-01T00:00:02Z","event":"MaterialTrade","MarketID":1,"TraderType":"raw","Paid":{"Material":"iron","Category":"Raw","Quantity":101},"Received":{"Material":"nickel","Category":"Raw","Quantity":10}}"""));

        Assert.Equal(0, store.Active!.Materials.Find("iron")?.Count ?? 0);

        // A tick with nothing on it, which is what the tick loop is mostly made of. This is where
        // the trade is noticed: spending a material raises no MaterialCollected, so a tracker
        // that only looked when something was picked up would next see iron already on its way
        // back up and could not tell that apart from its never having moved.
        Assert.Empty(callout.Examine(Context(store.Active, atSecond: 2)));

        said.Clear();

        // Gathering it again. A quarter full is a milestone again, because it is one.
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

    /// <summary>
    /// Spending a couple below a threshold and picking them back up does not re-announce it.
    /// The tracker follows the count down, and a milestone the Commander is still past is one
    /// they have already been told about.
    /// </summary>
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
