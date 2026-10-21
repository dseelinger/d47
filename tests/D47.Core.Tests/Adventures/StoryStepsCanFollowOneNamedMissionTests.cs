using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A story step takes a mission under a handle; later steps go to its destination, complete it or abandon it.</summary>
public sealed class StoryStepsCanFollowOneNamedMissionTests
{
    private const long MissionId = 1066795391;

    private static DateTimeOffset At(int minutes) => Accepted.AddMinutes(minutes);

    private static AdventureTrigger Taken(long? market = null) => new()
    {
        Kind = TriggerKind.MissionTaken,
        Handle = "parcel",
        MissionFamily = "Mission_Courier",
        MarketId = market,
        Station = market is null ? null : "Maren Anchorage",
    };

    private static AdventureTrigger Follow(TriggerKind kind) => new() { Kind = kind, Handle = "parcel" };

    private static AdventureStanding Chapter(params AdventureTrigger[] triggers)
    {
        var route = LanternRoute(Accepted);

        return AdventureFold.Start(route with
        {
            Beats = [route.Beats[0] with { Steps = [.. triggers.Select(trigger => new BeatStep { Trigger = trigger })] }],
        });
    }

    private static AdventureStanding Fold(AdventureStanding standing, params JournalEvent[] events) =>
        events.Aggregate(standing, AdventureFold.Apply);

    private static JournalEvent Accept(int minutes, string name = "Mission_Courier", long id = MissionId) =>
        Event($$"""{ "timestamp":"{{Stamp(At(minutes))}}", "event":"MissionAccepted", "Faction":"League of HR 6649 Front", "Name":"{{name}}", "DestinationSystem":"Kweleutahe", "DestinationStation":"Merchiston Dock", "MissionID":{{id}} }""");

    private static JournalEvent Mission(string kind, int minutes, long id = MissionId) =>
        Event($$"""{ "timestamp":"{{Stamp(At(minutes))}}", "event":"{{kind}}", "Name":"Mission_Courier", "MissionID":{{id}} }""");

    private static JournalEvent Undocked(int minutes) =>
        Event($$"""{ "timestamp":"{{Stamp(At(minutes))}}", "event":"Undocked", "StationName":"Some Station", "MarketID":{{Anchorage}} }""");

    [Fact]
    public void AnAcceptWhileDockedAtTheNamedMarketBindsTheHandle()
    {
        var standing = Fold(Chapter(Taken(Anchorage), Follow(TriggerKind.MissionComplete)), Docked(Anchorage, At(1)), Accept(2));

        var binding = Assert.Single(standing.Handles);
        Assert.Equal("parcel", binding.Key);
        Assert.Equal(new MissionBinding(MissionId, "Kweleutahe", "Merchiston Dock"), binding.Value);
        Assert.Equal(1, standing.CurrentStep);
    }

    [Fact]
    public void TheSameAcceptDockedElsewhereDoesNotBind()
    {
        var standing = Fold(Chapter(Taken(Anchorage), Follow(TriggerKind.MissionComplete)), Docked(Hollow, At(1)), Accept(2));

        Assert.Empty(standing.Handles);
        Assert.Equal(0, standing.CurrentStep);
    }

    [Fact]
    public void AnAcceptAfterUndockingDoesNotBind()
    {
        var standing = Fold(Chapter(Taken(Anchorage), Follow(TriggerKind.MissionComplete)), Docked(Anchorage, At(1)), Undocked(2), Accept(3));

        Assert.Empty(standing.Handles);
    }

    [Fact]
    public void ALoadedGameAlreadyDockedCountsAsDockedAtThatMarket()
    {
        var location = Event($$"""{ "timestamp":"{{Stamp(At(1))}}", "event":"Location", "StarSystem":"Dysons Hollow", "SystemAddress":{{Hollow}}, "Docked":true, "MarketID":{{Anchorage}} }""");

        var standing = Fold(Chapter(Taken(Anchorage), Follow(TriggerKind.MissionComplete)), location, Accept(2));

        Assert.Single(standing.Handles);
    }

    [Fact]
    public void ATakenStepWithNoStationBindsTheFirstMatchingAcceptAnywhere()
    {
        var standing = Fold(Chapter(Taken(), Follow(TriggerKind.MissionComplete)), Accept(1, "Mission_Delivery_Boom", 7), Accept(2), Accept(3, id: 9));

        Assert.Equal(MissionId, standing.Handles["parcel"].MissionId);
    }

    [Fact]
    public void OnlyTheBoundMissionIdCompletesTheStep()
    {
        var chapter = Chapter(Taken(), Follow(TriggerKind.MissionComplete));

        var other = Fold(chapter, Accept(1), Mission("MissionCompleted", 2, 99));
        Assert.Equal(1, other.CurrentStep);
        Assert.False(other.IsDone);

        var bound = Fold(chapter, Accept(1), Mission("MissionCompleted", 2));
        Assert.True(bound.IsDone);
    }

    [Fact]
    public void ARedirectedMissionIsNotCompleted()
    {
        var standing = Fold(Chapter(Taken(), Follow(TriggerKind.MissionComplete)), Accept(1), Mission("MissionRedirected", 2));

        Assert.False(standing.IsDone);
        Assert.False(standing.IsFailed);
    }

    [Fact]
    public void AbandoningOrExpiringFiresAnAbandonStep()
    {
        var abandoned = Fold(Chapter(Taken(), Follow(TriggerKind.MissionAbandon)), Accept(1), Mission("MissionAbandoned", 2));
        var expired = Fold(Chapter(Taken(), Follow(TriggerKind.MissionAbandon)), Accept(1), Mission("MissionFailed", 2));

        Assert.True(abandoned.IsDone);
        Assert.True(expired.IsDone);
    }

    [Fact]
    public void AbandoningWhileCompletionIsAwaitedFailsTheChapter()
    {
        var standing = Fold(Chapter(Taken(), Follow(TriggerKind.MissionComplete)), Accept(1), Mission("MissionAbandoned", 2));

        Assert.True(standing.IsFailed);
        Assert.Contains("abandon the parcel mission", standing.Failure!.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void CompletingWhileAbandonmentIsAwaitedFailsTheChapter()
    {
        var standing = Fold(Chapter(Taken(), Follow(TriggerKind.MissionAbandon)), Accept(1), Mission("MissionCompleted", 2));

        Assert.True(standing.IsFailed);
    }

    [Fact]
    public void TakingFlyingToTheDestinationAndCompletingEndsTheChapter()
    {
        var chapter = Chapter(Taken(), Follow(TriggerKind.MissionDestination), Follow(TriggerKind.MissionComplete));

        var standing = Fold(chapter, Accept(1), Jump(Lantern, At(2), "Ossens Lantern"), Jump(Veyl, At(3), "Kweleutahe"));
        Assert.Equal(2, standing.CurrentStep);

        standing = Fold(standing, Mission("MissionCompleted", 4));
        Assert.True(standing.IsDone);
        Assert.Equal(["MissionAccepted", "FSDJump", "MissionCompleted"], standing.StepFires.Select(fire => fire.By));
    }

    [Fact]
    public void DockingAtTheDestinationStationCountsAsArriving()
    {
        var docked = Event($$"""{ "timestamp":"{{Stamp(At(2))}}", "event":"Docked", "StationName":"merchiston dock", "StarSystem":"Kweleutahe", "SystemAddress":1, "MarketID":5 }""");

        var standing = Fold(Chapter(Taken(), Follow(TriggerKind.MissionDestination), Follow(TriggerKind.MissionComplete)), Accept(1), docked);

        Assert.Equal(2, standing.CurrentStep);
    }

    [Fact]
    public void AHandleIsUsedOnlyAfterTheStepThatTakesIt()
    {
        var route = LanternRoute(Accepted);
        var before = route with { Beats = [route.Beats[0] with { Steps = [new BeatStep { Trigger = Follow(TriggerKind.MissionComplete) }, new BeatStep { Trigger = Taken() }] }] };

        Assert.Contains(AdventureValidation.Problems(before), problem => problem.Contains("before any objective takes it", StringComparison.Ordinal));
        Assert.DoesNotContain(AdventureValidation.Problems(Chapter(Taken(), Follow(TriggerKind.MissionComplete)).Adventure), problem => problem.Contains("mission", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Mission_Courier", true)]
    [InlineData("Mission_OnFoot_Collect", true)]
    [InlineData("Mission_Mining", false)]
    [InlineData(null, false)]
    public void ADestinationStepIsRefusedForAFamilyWhoseAcceptNamesNone(string? family, bool allowed)
    {
        var taken = Taken() with { MissionFamily = family };
        var problems = AdventureValidation.Problems(Chapter(taken, Follow(TriggerKind.MissionDestination)).Adventure);

        Assert.Equal(allowed, !problems.Any(problem => problem.Contains("destination", StringComparison.Ordinal)));
    }

    [Fact]
    public void ANamedStationNeedsItsMarketBeforeTheStepIsReal()
    {
        Assert.False((Taken() with { Station = "Maren Anchorage" }).IsResolved);
        Assert.True(Taken(Anchorage).IsResolved);
        Assert.False((Follow(TriggerKind.MissionComplete) with { Handle = " " }).IsResolved);
    }

    [Fact]
    public void AHandleSurvivesASaveAndAReload()
    {
        var files = new MemoryFileSystem();
        var path = Path.Combine(MemoryInstall.FakeRoot, "mission-handles", "adventures.json");
        var route = LanternRoute(Accepted);
        var adventure = route with
        {
            Beats =
            [
                route.Beats[0] with { Steps = [new BeatStep { Trigger = Taken(Anchorage) }, new BeatStep { Trigger = Follow(TriggerKind.MissionDestination) }, new BeatStep { Trigger = Follow(TriggerKind.MissionComplete) }] },
                .. route.Beats.Skip(1),
            ],
        };

        var book = new AdventureBook(new AdventureStore(path, files, NullLogger<AdventureStore>.Instance), NullLogger<AdventureBook>.Instance);
        Assert.Null(book.Write("F1", adventure));

        var reread = new AdventureStore(path, files, NullLogger<AdventureStore>.Instance);
        reread.Poll();

        Assert.Equal(adventure.Beats, reread.Find("F1", adventure.Key)!.Beats);
    }

    [Fact]
    public void TheMissionKindsAreNotCountedOrAuthorable()
    {
        foreach (var kind in new[] { TriggerKind.MissionTaken, TriggerKind.MissionDestination, TriggerKind.MissionComplete, TriggerKind.MissionAbandon })
        {
            Assert.False(AdventureTrigger.IsCountedKind(kind));
            Assert.False(AdventureTrigger.IsAuthorable(kind));
            Assert.Contains(kind.ToString().ToLowerInvariant(), AdventureValidation.Kinds);
        }
    }
}
