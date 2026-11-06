using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.PlannerWorld;

namespace D47.Core.Tests.Stories;

/// <summary>
/// An act-one chapter is planned in code: chapter 1 and a chapter with the beacon in reach travel to place objectives and end on
/// the beacon, a swap into an owned ship coming first; a chapter whose beacon is out of reach ends on reach.
/// </summary>
public sealed class AnActOneChapterIsPlannedInCodeTests
{
    private static readonly uint[] Seeds = [.. Enumerable.Range(1, 40).Select(seed => (uint)seed)];

    private static readonly AdventureBeacon Beacon = new(13872878396833, "IC 2391 Sector MX-T b3-6", EasierWith: 18);

    private static readonly AdventureBeaconAway Away = new("IC 2391 Sector MX-T b3-6", 900, "no fuel scoop is fitted", EasierWith: 18);

    private static readonly StarPosition Parked = new(400, 0, 0);

    private static readonly TriggerKind[] PlaceKinds = [TriggerKind.Arrive, TriggerKind.Dock, TriggerKind.Land, TriggerKind.Scan, TriggerKind.Site];

    private static ChapterInputs Travelling(AdventureBeacon? beacon = null, AdventureBeaconAway? away = null) =>
        Inputs() with { Beacon = beacon, BeaconAway = away, Stage = StoryStage.ActOne };

    [Fact]
    public void ATravelChapterHasNoActivityAndEndsOnTheBeacon()
    {
        foreach (var seed in Seeds)
        {
            var plan = Planned(Travelling(Beacon), Places(), seed);

            Assert.DoesNotContain(plan.Objectives, objective => objective.Purpose == PlannedPurpose.Activity);
            Assert.InRange(plan.Objectives.Count, ChapterPlanner.FewestObjectives, ChapterPlanner.TravelObjectives);
            Assert.Equal(PlannedPurpose.Beacon, plan.Objectives[^1].Purpose);
            Assert.Equal(Beacon.Trigger(), Assert.Single(plan.Objectives[^1].Steps));
            Assert.All(plan.Objectives.SkipLast(1).SelectMany(objective => objective.Steps), step => Assert.Contains(step.Kind, PlaceKinds));
        }
    }

    [Fact]
    public void ASwapIntoAnOwnedShipComesFirstAndTheBeaconLast()
    {
        var beacon = Beacon with { SwapTo = "Krait Mk II, parked at Far Port in Fixture Far" };

        foreach (var seed in Seeds)
        {
            var plan = Planned(Travelling(beacon with { SwapHull = "krait_mkii" }), Places(), seed);

            Assert.Equal(PlannedPurpose.Swap, plan.Objectives[0].Purpose);
            Assert.Equal(new AdventureTrigger { Kind = TriggerKind.Board, ShipType = "krait_mkii" }, Assert.Single(plan.Objectives[0].Steps));
            Assert.Equal(TriggerKind.Beacon, plan.Objectives[^1].Steps[^1].Kind);
        }
    }

    [Fact]
    public void AfterTheSwapEachHopIsMeasuredFromWhereTheShipIsParked()
    {
        var beacon = Beacon with { SwapHull = "krait_mkii", SwapSystem = "Fixture Far" };
        var places = Places() with { SwapAt = Parked };

        foreach (var seed in Seeds)
        {
            var plan = Planned(Travelling(beacon), places, seed);
            var at = Parked;

            foreach (var step in Placed(plan))
            {
                var next = places.System(step.System)!.Position!.Value;

                Assert.True(at.DistanceTo(next) <= Reach, $"seed {seed}: {step.System} is beyond the reach of the stop before: {Describe(plan)}");
                at = next;
            }
        }
    }

    [Fact]
    public void AChapterWhoseBeaconIsOutOfReachEndsOnReachAfterItsWayBackToBase()
    {
        var inputs = Travelling(away: Away) with { Base = new ShipBase("Fixture Far", 600, "Far Port", 2) };
        var places = Places() with { Base = new ChapterBase("Fixture Far", 6, "Far Port", 600, Parked) };

        foreach (var seed in Seeds)
        {
            var plan = Planned(inputs, places, seed);

            Assert.Equal(PlannedPurpose.Reach, plan.Objectives[^1].Purpose);
            Assert.Equal(Away.Trigger(), Assert.Single(plan.Objectives[^1].Steps));
            Assert.Equal(600, plan.Objectives[^2].Steps[^1].MarketId);
        }
    }

    [Fact]
    public void ADrawnChapterWhoseBeaconIsOutOfReachKeepsItsActivitiesAndEndsOnReach()
    {
        foreach (var seed in Seeds)
        {
            var plan = Planned(Inputs(Bounty, Codex) with { BeaconAway = Away, Stage = StoryStage.ActOne }, Places(), seed);

            Assert.Equal(2, plan.Objectives.Count(objective => objective.Purpose == PlannedPurpose.Activity));
            Assert.Equal(TriggerKind.Reach, plan.Objectives[^1].Steps[^1].Kind);
        }
    }

    [Fact]
    public void ATravelChapterMayStopAtAGuardianSiteWithinReach()
    {
        var site = new ChapterSite(AdventureTrigger.GuardianSite, "Fixture Beta", 3, new StarPosition(120, 0, 0), "Fixture Beta 5") { BodyId = 5 };
        var places = Places() with { Sites = [site] };

        var steps = Seeds.SelectMany(seed => Steps(Planned(Travelling(Beacon), places, seed))).Where(step => step.Kind == TriggerKind.Site).ToList();

        Assert.NotEmpty(steps);
        Assert.All(steps, step => Assert.Equal(new AdventureTrigger { Kind = TriggerKind.Site, Filter = AdventureTrigger.GuardianSite, SystemAddress = 3, System = "Fixture Beta", BodyId = 5, Body = "Fixture Beta 5" }, step));
    }

    [Theory]
    [InlineData(false, 1, null, true, false)]
    [InlineData(false, 1, null, false, false)]
    [InlineData(false, 2, null, true, false)]
    [InlineData(false, 2, null, false, true)]
    [InlineData(false, 2, 1, false, false)]
    [InlineData(true, 1, null, null, true)]
    public void ChapterOneAndABeaconChapterTravelAndALaterActOneChapterOutOfReachDraws(bool scanned, int number, int? finale, bool? inReach, bool expected)
    {
        var reach = inReach is { } known ? new BeaconReach(Beacon.SystemAddress, Beacon.System, 900, known, known ? null : "no fuel scoop is fitted") : null;

        Assert.Equal(expected, StoryDirector.Draws(scanned, number, finale, reach));
    }

    [Fact]
    public async Task ChapterOneIsWrittenInOneLinesCallWithNoFieldForAPlaceAKindOrACount()
    {
        var story = new AdventureStory("fixture-story", "Fixture Story", "Public", "Hidden", Chapter: 1, DaysRunning: 0, DaysSinceBeacon: null, Stage: "Act One")
        {
            Beacon = Beacon with { SwapTo = "Python, parked at Alpha Port in Fixture Alpha", SwapHull = "python" },
        };
        var ask = PlannedChapterWorld.Ask(story);
        var plan = await PlannedChapterWorld.PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider([
            RoundScriptedLlmProvider.Saying(PlannedChapterWorld.Lines(plan.Objectives.Count)),
            RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NoObjections),
        ]);

        var outcome = await PlannedChapterWorld.Generator(provider).GenerateAsync(ask, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(["story_lines", "story_fidelity", "story_plain"], provider.Requests.Select(request => request.ResponseSchema?.Name));
        Assert.Empty(PlannedChapterWorld.Properties(provider.Requests[0].ResponseSchema!.SchemaJson).Intersect(["kind", "system", "station", "body", "count", "ship"]));
        Assert.Equal(TriggerKind.Board, outcome.Draft!.Beats[0].Steps[0].Trigger.Kind);
        Assert.Equal(TriggerKind.Beacon, outcome.Draft.Beats[^1].Steps[^1].Trigger.Kind);
        Assert.Contains("The chapter's first objective is the swap into that ship.", provider.Requests[0].Prompt.History[0].Text, StringComparison.Ordinal);
    }
}
