using D47.Core.Adventures;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public sealed class AStepCanWaitOnAnyEventTests
{
    private const string Aleoids = "$Codex_Ent_Aleoids_Genus_Name;";
    private const string Bacterial = "$Codex_Ent_Bacterial_Genus_Name;";

    private static readonly string AdventuresFile = Path.Combine(MemoryInstall.FakeRoot, "event-steps", "adventures.json");

    private static AdventureTrigger Waiting(int count = 1, params EventField[] where) => new()
    {
        Kind = TriggerKind.Event,
        Event = "ScanOrganic",
        Where = new EventFilter(where),
        Task = "analyse a sample",
        Count = count,
    };

    private static AdventureStanding Fold(AdventureTrigger counted, params string[] events)
    {
        var adventure = LanternRoute(Accepted) with
        {
            Beats =
            [
                Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
                Beat("The Count", "catalyst", counted, "Done."),
            ],
        };

        var standing = AdventureFold.Apply(
            AdventureFold.Start(adventure),
            Event($$"""{ "timestamp":"{{Stamp(Accepted.AddMinutes(1))}}", "event":"FSDJump", "SystemAddress":{{Lantern}}, "StarSystem":"Ossen's Lantern" }"""));

        return events.Select((json, index) => Event(json.Replace("@", Stamp(Accepted.AddMinutes(2 + index)), StringComparison.Ordinal)))
            .Aggregate(standing, AdventureFold.Apply);
    }

    private static string Organic(string genus) =>
        $$"""{ "timestamp":"@", "event":"ScanOrganic", "ScanType":"Analyse", "Genus":"{{genus}}" }""";

    private static string Docked(string fields) => $$"""{ "timestamp":"@", "event":"Docked"{{fields}} }""";

    private static IReadOnlyList<string> Problems(AdventureTrigger trigger) => [.. AdventureValidation.CountedProblems("Beat one", trigger)];

    [Fact]
    public void AStepFiresOnTheSecondMatchingEventAndNotOnAnotherGenus()
    {
        var trigger = Waiting(2, new EventField("Genus", Aleoids));

        Assert.Equal(1, Fold(trigger, Organic(Aleoids), Organic(Bacterial)).Counted);
        Assert.Equal(2, Fold(trigger, Organic(Aleoids), Organic(Bacterial), Organic(Aleoids)).Current);
    }

    [Fact]
    public void AStringFieldIsComparedFoldedAndIgnoringCase()
    {
        var trigger = Waiting(1, new EventField("Genus", "codex_ent_aleoids_genus"));

        Assert.Equal(2, Fold(trigger, Organic(Aleoids)).Current);
        Assert.Equal(2, Fold(Waiting(1, new EventField("Genus", " $CODEX_ENT_ALEOIDS_GENUS_NAME; ")), Organic(Aleoids)).Current);
    }

    [Fact]
    public void EveryPairHasToHold()
    {
        var trigger = Waiting(1, new EventField("Genus", Aleoids), new EventField("ScanType", "Log"));

        Assert.Equal(1, Fold(trigger, Organic(Aleoids)).Current);
    }

    [Fact]
    public void ABooleanPairMatchesOnlyAPresentAndEqualField()
    {
        var wanted = new AdventureTrigger { Kind = TriggerKind.Event, Event = "Docked", Task = "dock while wanted", Count = 1, Where = new EventFilter([new EventField("Wanted", "true")]) };
        var clean = wanted with { Where = new EventFilter([new EventField("Wanted", "false")]) };

        Assert.Equal(2, Fold(wanted, Docked(""","Wanted":true""")).Current);
        Assert.Equal(1, Fold(wanted, Docked("")).Current);
        Assert.Equal(1, Fold(wanted, Docked(""","Wanted":false""")).Current);
        Assert.Equal(2, Fold(clean, Docked(""","Wanted":false""")).Current);
        Assert.Equal(1, Fold(clean, Docked("")).Current);
    }

    [Fact]
    public void AFieldMissingFromTheEventNeverHolds()
    {
        Assert.Equal(1, Fold(Waiting(1, new EventField("Genus", Aleoids)), """{ "timestamp":"@", "event":"ScanOrganic", "ScanType":"Analyse" }""").Current);
    }

    [Fact]
    public void AnEventStepWithNoPairsCountsEveryEventOfItsName()
    {
        Assert.Equal(2, Fold(Waiting(), Organic(Aleoids)).Current);
    }

    [Fact]
    public void AFailsOnEventFailsTheChapterOnTheFirstMatch()
    {
        var route = LanternRoute(Accepted);
        var adventure = route with { Beats = [route.Beats[0] with { FailsOn = [Waiting(1, new EventField("Genus", Bacterial)) with { Count = null }] }, .. route.Beats.Skip(1)] };
        var started = AdventureFold.Start(adventure);

        Assert.False(AdventureFold.Apply(started, Event(Organic(Aleoids).Replace("@", Stamp(Accepted.AddMinutes(1)), StringComparison.Ordinal))).IsFailed);
        Assert.True(AdventureFold.Apply(started, Event(Organic(Bacterial).Replace("@", Stamp(Accepted.AddMinutes(1)), StringComparison.Ordinal))).IsFailed);
    }

    [Fact]
    public void TheWordsAreTheTask()
    {
        var trigger = Waiting(3);

        Assert.Equal("analyse a sample", trigger.Describe());
        Assert.Equal("Analyse a sample: 1 of 3", trigger.Progress(1));
        Assert.Equal("Next: analyse a sample, counted from now. Analyse a sample: 0 of 3.", trigger.HandOff());
    }

    [Fact]
    public void AnEventThatIsNotInTheTableIsRefused()
    {
        Assert.Contains("\"NoSuchEvent\"", Assert.Single(Problems(Waiting() with { Event = "NoSuchEvent" })), StringComparison.Ordinal);
    }

    [Fact]
    public void AJournalEventD47DoesNotActOnIsRefused()
    {
        var catalogue = D47.Core.Knowledge.StepEventCatalogue.Parse("journal\tNotActedOn\tyes\t\t\tx\n", string.Empty);

        Assert.False(catalogue.Exists("NotActedOn"));
    }

    [Fact]
    public void AFieldTheEventDoesNotHaveIsRefused()
    {
        var problem = Assert.Single(Problems(Waiting(1, new EventField("Colour", "red"))));

        Assert.Contains("\"Colour\"", problem, StringComparison.Ordinal);
        Assert.Contains("ScanOrganic does not have", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void AValueTheFieldDoesNotAllowIsRefused()
    {
        var problem = Assert.Single(Problems(Waiting(1, new EventField("Genus", "Nonesuch"))));

        Assert.Contains("\"Nonesuch\"", problem, StringComparison.Ordinal);
        Assert.Contains("does not allow", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ATaskThatIsEmptyOrTooLongIsRefused()
    {
        Assert.Contains("no task", Assert.Single(Problems(Waiting() with { Task = "  " })), StringComparison.Ordinal);
        Assert.Contains("at most 60", Assert.Single(Problems(Waiting() with { Task = new string('x', 61) })), StringComparison.Ordinal);
        Assert.Empty(Problems(Waiting() with { Task = new string('x', 60) }));
    }

    [Fact]
    public void AStepWithoutACountIsRefusedAndAFailsOnWithOneIs()
    {
        Assert.Contains("count", Assert.Single(Problems(Waiting() with { Count = null })), StringComparison.Ordinal);
        Assert.Contains("with a count", Assert.Single(AdventureValidation.FailsOnProblems("Beat one", Waiting())), StringComparison.Ordinal);
        Assert.Empty(AdventureValidation.FailsOnProblems("Beat one", Waiting() with { Count = null }));
    }

    [Fact]
    public void AFailsOnEventNamingAnUnknownEventIsRefused()
    {
        Assert.Single(AdventureValidation.FailsOnProblems("Beat one", Waiting() with { Count = null, Event = "NoSuchEvent" }));
    }

    [Fact]
    public void AnEventStepWithTwoPairsRoundTripsThroughTheStore()
    {
        var files = new MemoryFileSystem();
        var step = Waiting(2, new EventField("Genus", Aleoids), new EventField("ScanType", "Analyse"));
        var route = LanternRoute(Accepted);
        var adventure = route with { Beats = [route.Beats[0], Beat("The Count", "catalyst", step, "Done."), .. route.Beats.Skip(2)] };

        var book = new AdventureBook(new AdventureStore(AdventuresFile, files, NullLogger<AdventureStore>.Instance), NullLogger<AdventureBook>.Instance);
        Assert.Null(book.Write("F1", adventure));

        var reread = new AdventureStore(AdventuresFile, files, NullLogger<AdventureStore>.Instance);
        reread.Poll();

        var stored = reread.Find("F1", adventure.Key)!;
        Assert.Equal(adventure.Beats, stored.Beats);
        Assert.Equal(step, stored.Beats[1].Trigger);
        Assert.Equal(2, stored.Beats[1].Trigger.Where.Count);
    }

    [Fact]
    public void TheEventKindIsNotOfferedToAuthorsOrTheModel()
    {
        Assert.False(AdventureTrigger.IsAuthorable(TriggerKind.Event));
        Assert.Contains("event", AdventureValidation.Kinds);
    }
}
