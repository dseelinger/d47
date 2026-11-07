using D47.Core.Adventures;
using D47.Core.Goals;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.PlannerWorld;

namespace D47.Core.Tests.Stories;

/// <summary>The chapter planner builds a drawn chapter's objectives from gathered places, and every plan meets the rules the dry run holds a drawn chapter to.</summary>
public sealed class ADrawnChapterIsPlannedInCodeTests
{
    private static readonly uint[] Seeds = [.. Enumerable.Range(1, 40).Select(seed => (uint)seed)];

    [Fact]
    public void TheSameInputsAndSeedGiveTheSamePlan()
    {
        var inputs = Inputs(Bounty, Codex);

        Assert.Equal(Describe(Planned(inputs, Places(), 11)), Describe(Planned(inputs, Places(), 11)));
    }

    [Fact]
    public void AnotherSeedChoosesOtherPlaces()
    {
        var plans = Seeds.Select(seed => Describe(Planned(Inputs(Bounty, Codex), Places(), seed))).Distinct().Count();

        Assert.True(plans > 1, "every seed gave the same plan");
    }

    [Fact]
    public void EachDrawnActivityKeepsItsDrawnCount()
    {
        foreach (var seed in Seeds)
        {
            var steps = Steps(Planned(Inputs(Bounty, Codex, Salvage), Places(), seed)).ToList();

            Assert.Equal(5, Assert.Single(steps, step => step.Kind == TriggerKind.Bounty).Count);
            Assert.Equal(4, Assert.Single(steps, step => step.Kind == TriggerKind.Codex).Count);
            Assert.Equal(3, Assert.Single(steps, step => step.Kind == TriggerKind.Salvage).Count);
        }
    }

    [Fact]
    public void AChapterHasThreeToEightObjectivesAndAtMostTwoStoryStops()
    {
        foreach (var seed in Seeds)
        {
            var plan = Planned(Inputs(Bounty), Places(), seed);

            Assert.InRange(plan.Objectives.Count, ChapterPlanner.FewestObjectives, ChapterPlanner.MostObjectives);
            Assert.InRange(plan.Objectives.Count(objective => objective.Purpose == PlannedPurpose.Stop), 2, ChapterPlanner.MostStops);
        }
    }

    [Fact]
    public void EveryHopKeepsToTheReach()
    {
        var places = Places();

        foreach (var seed in Seeds)
        {
            var at = Here;

            foreach (var step in Placed(Planned(Inputs(Bounty, Codex), places, seed)))
            {
                var next = places.System(step.System)!.Position!.Value;

                Assert.True(at.DistanceTo(next) <= Reach, $"seed {seed}: {step.System} is beyond the reach");
                at = next;
            }
        }
    }

    [Fact]
    public void APlaceBeforeAnActivityIsItsFirstStep()
    {
        var slot = new DrawnActivity(TriggerKind.Sell, null, 20) { PlaceBefore = true };

        foreach (var seed in Seeds)
        {
            var objective = Assert.Single(Planned(Inputs(slot), Places(), seed).Objectives, objective => objective.Purpose == PlannedPurpose.Activity);

            Assert.Equal(2, objective.Steps.Count);
            Assert.True(objective.Steps[0].Kind is TriggerKind.Arrive or TriggerKind.Dock);
            Assert.Equal(TriggerKind.Sell, objective.Steps[1].Kind);
        }
    }

    [Fact]
    public void AnIllegalMissionFollowsAnArriveOrDockInAnAnarchySystem()
    {
        var family = MissionFamilies.Illegal[0];
        var places = Places() with { Systems = [.. Places().Systems.Select(system => system.Name == "Fixture Beta" ? system with { Government = "Anarchy" } : system)] };

        foreach (var seed in Seeds)
        {
            var objective = Planned(Inputs(new DrawnActivity(TriggerKind.Mission, family, 2) { PlaceBefore = true }), places, seed).Objectives.Single(objective => objective.Purpose == PlannedPurpose.Activity);

            Assert.True(objective.Steps[0].Kind is TriggerKind.Arrive or TriggerKind.Dock);
            Assert.Equal("Fixture Beta", objective.Steps[0].System);
            Assert.Equal(family, objective.Steps[1].MissionFamily);
        }

        AssertRefused(ChapterRules.Anarchy, Inputs(new DrawnActivity(TriggerKind.Mission, family, 2) { PlaceBefore = true }), Places());
    }

    [Fact]
    public void AKillBondOnFootFollowsADockWithFrontlineSolutionsInASystemAtWar()
    {
        var slot = new DrawnActivity(TriggerKind.OnFootBond, null, 8) { PlaceBefore = true };
        var places = Places() with
        {
            Systems = [.. Places().Systems.Select(system => system.Name is "Fixture Alpha" or "Fixture Gamma" ? system with { AtWar = true } : system)],
            Stations = [.. Places().Stations.Select(station => station.Name is "Alpha Port" or "Beta Port" ? station with { Services = [AdventureResolver.FrontlineSolutions] } : station)],
        };

        foreach (var seed in Seeds)
        {
            var objective = Planned(Inputs(slot), places, seed).Objectives.Single(objective => objective.Purpose == PlannedPurpose.Activity);

            Assert.Equal(TriggerKind.Dock, objective.Steps[0].Kind);
            Assert.Equal("Alpha Port", objective.Steps[0].Station);
            Assert.Equal(TriggerKind.OnFootBond, objective.Steps[1].Kind);
        }

        AssertRefused(ChapterRules.Frontline, Inputs(slot), Places());
    }

    [Theory]
    [InlineData(TriggerKind.Bond)]
    [InlineData(TriggerKind.Conflict)]
    public void ABondOrConflictFollowsAStopInASystemAtWar(TriggerKind kind)
    {
        var slot = new DrawnActivity(kind, null, 2) { PlaceBefore = kind == TriggerKind.Conflict };
        var places = Places() with { Systems = [.. Places().Systems.Select(system => system.Name == "Fixture Gamma" ? system with { AtWar = true } : system)] };

        foreach (var seed in Seeds)
        {
            var objective = Planned(Inputs(slot), places, seed).Objectives.Single(objective => objective.Purpose == PlannedPurpose.Activity);

            Assert.Equal("Fixture Gamma", objective.Steps[0].System);
            Assert.Equal(kind, objective.Steps[1].Kind);
        }

        AssertRefused(ChapterRules.War, Inputs(slot), Places());
    }

    [Fact]
    public void ADockIsAtAStationWithAPadForTheShip()
    {
        var inputs = Inputs(new DrawnActivity(TriggerKind.Sell, null, 20) { PlaceBefore = true }) with { Ship = new ShipLoadout { Type = "anaconda", Modules = [] } };
        var places = Places() with { Stations = [.. Places().Stations.Select(station => station.Name == "Alpha Port" ? station : station with { LargePads = 0 })] };

        foreach (var seed in Seeds)
        {
            Assert.All(Steps(Planned(inputs, places, seed)).Where(step => step.Kind == TriggerKind.Dock), dock => Assert.Equal("Alpha Port", dock.Station));
        }

        var frontline = new DrawnActivity(TriggerKind.OnFootBond, null, 8) { PlaceBefore = true };
        var small = Places() with
        {
            Systems = [.. Places().Systems.Select(system => system with { AtWar = true })],
            Stations = [Station("Alpha Port", "Fixture Alpha", 2, 200, 110, frontline: true, large: 0)],
        };

        AssertRefused(ChapterRules.Pad, inputs with { Slots = [frontline] }, small);
    }

    [Fact]
    public void NoStepIsInAPermitOrStarterSystem()
    {
        string[] locked = ["Shinrarta Dezhra", Knowledge.TableContents.StarterSystems.First()];
        var places = Places() with
        {
            Systems = [.. Places().Systems, Place(locked[0], 90, 105), Place(locked[1], 91, 115)],
            Stations = [.. Places().Stations, Station("Locked Port", locked[0], 90, 900, 105), Station("Starter Port", locked[1], 91, 910, 115)],
        };

        foreach (var seed in Seeds)
        {
            Assert.DoesNotContain(Placed(Planned(Inputs(Bounty), places, seed)), step => locked.Contains(step.System));
        }

        var onlyLocked = new ChapterPlaces { Systems = [Place(locked[0], 90, 105)] };

        AssertRefused(ChapterRules.Permit, Inputs(Bounty), onlyLocked);
    }

    [Fact]
    public void NoDockIsAtAClosedMarket()
    {
        var places = Places() with { ClosedMarkets = [200, 300] };

        foreach (var seed in Seeds)
        {
            Assert.DoesNotContain(Steps(Planned(Inputs(Bounty), places, seed)), step => step.MarketId is 200 or 300);
        }

        var frontline = new DrawnActivity(TriggerKind.OnFootBond, null, 8) { PlaceBefore = true };
        var closed = new ChapterPlaces
        {
            Systems = [Place(Start, 1, 100), Place("Fixture Alpha", 2, 110, war: true)],
            Stations = [Station("Alpha Port", "Fixture Alpha", 2, 200, 110, frontline: true)],
            ClosedMarkets = [200],
        };

        AssertRefused(ChapterRules.ClosedMarket, Inputs(frontline), closed);
    }

    [Fact]
    public void AHopBeyondTheReachIsRefused()
    {
        var places = new ChapterPlaces { Systems = [Place(Start, 1, 100), Place("Fixture Far", 6, 400, war: true)] };

        AssertRefused(ChapterRules.Hop, Inputs(new DrawnActivity(TriggerKind.Conflict, null, 1) { PlaceBefore = true }), places);
    }

    [Theory]
    [InlineData(TriggerKind.Map)]
    [InlineData(TriggerKind.Signal)]
    [InlineData(TriggerKind.Organic)]
    public void AMapSignalOrOrganicActivityIsNamedOnlyWhereTheSupplyMeetsItsCount(TriggerKind kind)
    {
        ChapterSystem Supplied(ChapterSystem system, int supply) => kind switch
        {
            TriggerKind.Map => system with { Mappable = supply },
            TriggerKind.Signal => system with { Signals = supply },
            _ => system with { Species = supply },
        };

        var places = Places() with
        {
            Systems = [.. Places().Systems.Select(system => system.Name switch
            {
                "Fixture Alpha" => Supplied(system, 5),
                "Fixture Beta" => Supplied(system, 2),
                "Fixture Far" => Supplied(system, 9),
                _ => system,
            })],
        };
        var slot = new DrawnActivity(kind, null, 3);

        foreach (var seed in Seeds)
        {
            Assert.Equal("Fixture Alpha", Steps(Planned(Inputs(slot), places, seed)).Single(step => step.Kind == kind).System);
            Assert.Null(Steps(Planned(Inputs(slot), Places(), seed)).Single(step => step.Kind == kind).System);
        }
    }

    [Fact]
    public void ABoardGoalLeavesTheCreditReserve()
    {
        var board = new GoalClimbStep("ship", "Fly an Anaconda") { Objective = new AdventureTrigger { Kind = TriggerKind.Board, ShipType = "anaconda" } };
        var price = EliteSpecifications.Ship("anaconda")!.Cost!.Value;

        var rich = Planned(Inputs(Bounty) with { Goals = [board], Credits = ChapterFit.Needed(price) }, Places());

        Assert.Contains(rich.Objectives, objective => objective.Purpose == PlannedPurpose.Goal && objective.Steps[0].ShipType == "anaconda");
        AssertRefused(ChapterRules.Reserve, Inputs(Bounty) with { Goals = [board], Credits = price }, Places());
    }

    [Fact]
    public void ASightseeingChapterStopsAtItsSight()
    {
        var sight = new NotablePlace("Fixture Nebula", "Nebula", "Fixture Delta", 5, new StarPosition(130, 0, 0));

        foreach (var seed in Seeds)
        {
            var objective = Assert.Single(Planned(Inputs(Bounty), Places() with { Sights = [sight] }, seed).Objectives, objective => objective.Purpose == PlannedPurpose.Sight);

            Assert.Equal("Fixture Delta", objective.Steps.Single().System);
            Assert.True(objective.Steps.Single().Kind is TriggerKind.Arrive or TriggerKind.Land or TriggerKind.Scan);
        }

        var away = new NotablePlace("Fixture Distant", "Nebula", "Fixture Far", 6, new StarPosition(400, 0, 0));

        AssertRefused(ChapterRules.Hop, Inputs(Bounty), Places() with { Sights = [away] });
    }

    [Fact]
    public void ASightBeyondTheReachIsReachedInHops()
    {
        var sight = new NotablePlace("Fixture Far Nebula", "Nebula", "Fixture Nebula", 9, new StarPosition(230, 0, 0));
        var places = new ChapterPlaces
        {
            Systems = [Place(Start, 1, 100), Place("Fixture Side", 7, 80), Place("Fixture Way", 8, 145), Place("Fixture Nearer", 10, 190), Place("Fixture Nebula", 9, 230)],
            Sights = [sight],
        };

        foreach (var seed in Seeds)
        {
            var plan = Planned(Inputs(Bounty), places, seed);
            var steps = plan.Objectives.Single(objective => objective.Purpose == PlannedPurpose.Sight).Steps;
            var at = Here;

            Assert.Equal("Fixture Nebula", steps[^1].System);

            foreach (var step in Placed(plan))
            {
                var next = places.System(step.System)!.Position!.Value;

                Assert.True(at.DistanceTo(next) <= Reach, $"seed {seed}: {step.System} is beyond the reach");
                at = next;
            }
        }
    }

    [Fact]
    public void AChapterOutsideTheBubbleEndsInsideTheSolBand()
    {
        var start = new StarPosition(1000, 0, 0);
        var band = ChapterBand.Of(start)!.Value;
        var inputs = Inputs(Bounty) with { Position = start, Band = band, Reach = 300 };
        var places = new ChapterPlaces
        {
            Systems = [Place(Start, 1, 1000), Place("Fixture Out", 2, 980), Place("Fixture Mid", 3, 1060)],
            InBand = new ChapterArea(ChapterAreaState.Listed, [Station("Band Port", "Fixture Band", 4, 400, 800)], [Body("Fixture Band 1", "Fixture Band", 4, 1, 800)]),
        };

        foreach (var seed in Seeds)
        {
            var plan = Planned(inputs, places, seed);
            var last = Placed(plan).Last();

            Assert.Equal(PlannedPurpose.Band, plan.Objectives[^1].Purpose);
            Assert.Equal("Fixture Band", last.System);
        }

        AssertRefused(ChapterRules.Band, inputs, places with { InBand = new ChapterArea(ChapterAreaState.Listed, [], []) });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ATravellingChapterEndsFarEnoughTowardItsTarget(bool home)
    {
        var start = new StarPosition(3000, 0, 0);
        var leg = home ? new ChapterLeg(HomeLeg.Sol, StarPosition.Origin, true, false) : new ChapterLeg("Fixture Target", new StarPosition(0, 0, 0), false, false);
        var inputs = Inputs(Bounty) with { Position = start, Leg = leg, Reach = 500 };
        var places = new ChapterPlaces
        {
            Systems = [Place(Start, 1, 3000), Place("Fixture Near", 2, 2900), Place("Fixture Way", 3, 2550)],
            Pull = new ChapterArea(ChapterAreaState.Listed, [Station("Way Port", "Fixture Way", 3, 300, 2550)], []),
        };

        foreach (var seed in Seeds)
        {
            var plan = Planned(inputs, places, seed);

            Assert.Equal(PlannedPurpose.Leg, plan.Objectives[^1].Purpose);
            Assert.Equal("Fixture Way", Placed(plan).Last().System);
        }

        AssertRefused(home ? ChapterRules.HomeLeg : ChapterRules.Pull, inputs, places with { Systems = [Place(Start, 1, 3000), Place("Fixture Near", 2, 2900)], Pull = new ChapterArea(ChapterAreaState.Listed, [], []) });
    }

    [Fact]
    public void AChapterFarFromTheBaseEndsWithTheStepBackToIt()
    {
        var inputs = Inputs(Bounty) with { Base = new ShipBase("Fixture Home", 7000, "Home Port", 3) };
        var far = Places() with { Base = new ChapterBase("Fixture Home", 70, "Home Port", 7000, new StarPosition(700, 0, 0)) };

        foreach (var seed in Seeds)
        {
            var last = Planned(inputs, far, seed).Objectives[^1].Steps[^1];

            Assert.Equal(TriggerKind.Dock, last.Kind);
            Assert.Equal(7000, last.MarketId);
        }

        var near = Places() with { Base = new ChapterBase("Fixture Home", 70, "Home Port", 7000, new StarPosition(105, 0, 0)) };

        Assert.DoesNotContain(Steps(Planned(inputs, near)), step => step.MarketId == 7000);
    }

    [Fact]
    public void AChapterUsesAtMostOneMissionDestination()
    {
        var places = Places() with
        {
            Stations = [],
            Bodies = [],
            Missions =
            [
                new ChapterMissionStop(1, "Fixture Alpha", 2, new StarPosition(110, 0, 0), "Alpha Port", 200),
                new ChapterMissionStop(2, "Fixture Beta", 3, new StarPosition(120, 0, 0), "Beta Port", 300),
                new ChapterMissionStop(3, "Fixture Gamma", 4, new StarPosition(90, 0, 0), "Gamma Port", 400),
            ],
        };
        var docks = Seeds.Select(seed => Steps(Planned(Inputs(Bounty), places, seed)).Count(step => step.Kind == TriggerKind.Dock)).ToList();

        Assert.Contains(docks, count => count == 1);
        Assert.All(docks, count => Assert.InRange(count, 0, 1));
    }

    [Fact]
    public void MilestonesComeOnlyFromTheGoalsStep()
    {
        TriggerKind[] milestones = [TriggerKind.Board, TriggerKind.Rank, TriggerKind.Engineer, TriggerKind.Unlock, TriggerKind.Crew, .. TeamBeats.Kinds];

        foreach (var seed in Seeds)
        {
            Assert.DoesNotContain(Steps(Planned(Inputs(Bounty, Codex), Places(), seed)), step => milestones.Contains(step.Kind));
        }

        var rank = new GoalClimbStep("combat", "Combat Elite") { Objective = new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Combat", Rank = 4 } };
        var goal = Assert.Single(Planned(Inputs(Bounty) with { Goals = [rank] }, Places()).Objectives, objective => objective.Purpose == PlannedPurpose.Goal);

        Assert.Equal(TriggerKind.Rank, goal.Steps.Single().Kind);
    }

    [Fact]
    public void ATeamGoalTheCommanderCannotMeetIsRefused()
    {
        var jump = TeamBeats.Kinds.First(kind => TeamBeats.Why(kind, false, false, 10_000_000) is not null);
        var step = new GoalClimbStep("team", "Team") { Objective = new AdventureTrigger { Kind = jump } };

        AssertRefused(ChapterRules.Team, Inputs(Bounty) with { Goals = [step] }, Places());
    }

    [Fact]
    public void ARefusedActivityIsRefused()
    {
        var inputs = Inputs(Bounty) with { Refused = [RefusedActivities.Key(TriggerKind.Bounty, null)!] };

        AssertRefused(ChapterRules.Refused, inputs, Places());
    }

    [Fact]
    public void ATagLimitsThePlacesDrawnWhereSomeCandidateHasIt()
    {
        var places = Places() with { Systems = [.. Places().Systems.Select(system => system.Name == "Fixture Beta" ? system with { Government = "Anarchy" } : system)] };
        var inputs = Inputs(Bounty) with { Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ChapterTags.Anarchy } };

        foreach (var seed in Seeds)
        {
            Assert.All(
                Planned(inputs, places, seed).Objectives.Where(objective => objective.Purpose == PlannedPurpose.Stop),
                stop => Assert.Equal("Fixture Beta", stop.Steps.Single().System));
        }
    }

    [Fact]
    public void APlanAtATurningStageCarriesATurningLabel()
    {
        foreach (var stage in new[] { StoryStage.Midpoint, StoryStage.AllIsLost, StoryStage.FunAndGames })
        {
            foreach (var seed in Seeds)
            {
                var plan = Planned(Inputs(Bounty) with { Stage = stage }, Places(), seed);

                Assert.Contains(plan.Objectives, objective => AdventureStanding.IsTurning(objective.Function) && objective.Function == ChapterFunctions.TurnAt(stage));
                Assert.Equal(ChapterFunctions.Setup, plan.Objectives[0].Function);
                Assert.Equal(ChapterFunctions.Resolution, plan.Objectives[^1].Function);
            }
        }
    }

    [Fact]
    public void AnUnknownPositionIsRefused() =>
        AssertRefused(ChapterRules.Position, Inputs(Bounty) with { Position = null }, Places());

    [Fact]
    public void ContentOverEightObjectivesIsRefused()
    {
        var goals = Enumerable.Range(0, 6).Select(index => new GoalClimbStep($"g{index}", "Goal") { Objective = new AdventureTrigger { Kind = TriggerKind.Rank, Career = "Trade", Rank = 9 } }).ToList();

        AssertRefused(ChapterRules.Count, Inputs(Bounty, Codex, Salvage) with { Goals = goals }, Places());
    }

    private static void AssertRefused(string rule, ChapterInputs inputs, ChapterPlaces places)
    {
        var result = ChapterPlanner.Plan(inputs, places, 3);

        Assert.Null(result.Plan);
        Assert.Equal(rule, result.Refusal?.Rule);
    }
}
