using D47.Core.Adventures;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public class AFirstFootfallObjectiveCountsEachNewBodyOnceTests
{
    private const string Fresh = """{ "timestamp":"@", "event":"Scan", "SystemAddress":1, "BodyID":4, "BodyName":"Ossen 4", "WasFootfalled":false }""";

    private const string Walked = """{ "timestamp":"@", "event":"Scan", "SystemAddress":1, "BodyID":4, "BodyName":"Ossen 4", "WasFootfalled":true }""";

    private const string Disembark = """{ "timestamp":"@", "event":"Disembark", "SRV":false, "OnPlanet":true, "OnStation":false, "SystemAddress":1, "BodyID":4 }""";

    private static AdventureStanding Fold(int count, params (int Minute, string Line)[] lines)
    {
        var adventure = LanternRoute(Accepted) with
        {
            Beats =
            [
                Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Go."),
                Beat("The Walk", "catalyst", new AdventureTrigger { Kind = TriggerKind.Footfall, Count = count }, "Done."),
            ],
        };

        var standing = AdventureFold.Start(adventure);
        var world = AdventureWorld.Empty;

        foreach (var (minute, line) in lines.Prepend((1, $$"""{ "timestamp":"@", "event":"FSDJump", "SystemAddress":{{Lantern}}, "StarSystem":"Ossen's Lantern" }""")))
        {
            var journalEvent = Event(line.Replace("@", Stamp(Accepted.AddMinutes(minute)), StringComparison.Ordinal));
            world = world.Apply(journalEvent);
            standing = AdventureFold.Apply(standing, journalEvent, world);
        }

        return standing;
    }

    [Fact]
    public void ADisembarkOnAFreshlyScannedBodyCounts() =>
        Assert.True(Fold(1, (2, Fresh), (3, Disembark)).IsDone);

    [Fact]
    public void ASecondDisembarkOnTheSameBodyDoesNotCount() =>
        Assert.Equal(1, Fold(2, (2, Fresh), (3, Disembark), (4, Disembark)).Counted);

    [Fact]
    public void ADisembarkOnABodyAlreadyWalkedOnDoesNotCount() =>
        Assert.False(Fold(1, (2, Walked), (3, Disembark)).IsDone);

    [Fact]
    public void AScanFoldedBeforeAcceptanceStillCounts() =>
        Assert.True(Fold(1, (-5, Fresh), (3, Disembark)).IsDone);

    [Fact]
    public void AFootfallObjectiveIsCountedAndSaysSo()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.Footfall, Count = 1 };

        Assert.True(trigger.IsCounted);
        Assert.Equal("First footfalls: 0 of 1", trigger.Progress(0));
    }
}
