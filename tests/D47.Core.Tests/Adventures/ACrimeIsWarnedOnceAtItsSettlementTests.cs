using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public sealed class ACrimeIsWarnedOnceAtItsSettlementTests : IDisposable
{
    private const string Illegal = "Mission_OnFoot_AssassinationIllegal_Covert_MB";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-crime-warning", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private AdventureCallout Wired(string? family, bool running = true)
    {
        var store = new AdventureStore(Path.Combine(_folder, "adventures.json"), NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);

        if (running)
        {
            book.Write("F1", LanternRoute(Accepted) with
            {
                Beats =
                [
                    Beat("The Lantern", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
                    Beat("The Job", "catalyst", new AdventureTrigger { Kind = TriggerKind.Mission, Count = 1, MissionFamily = family }, "Take the job."),
                ],
            });
        }

        book.CatchUp([]);

        return new AdventureCallout(book);
    }

    private static CommanderGameState State(params string[] missions)
    {
        var store = new GameStateStore();
        store.Apply(Commander("F1", Accepted));

        foreach (var (mission, index) in missions.Select((mission, index) => (mission, index)))
        {
            store.Apply(Event(
                $$"""{ "timestamp":"{{Stamp(Accepted)}}", "event":"MissionAccepted", "Name":"{{Illegal}}", "LocalisedName":"Kill", "MissionID":{{index + 1}}, "DestinationSettlement":"{{mission}}" }"""));
        }

        return store.Active!;
    }

    private static JournalEvent Approach(string settlement, string government, DateTimeOffset at) =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"ApproachSettlement", "Name":"{{settlement}}", "StationGovernment":"$government_{{government}};", "StationGovernment_Localised":"{{government}}" }""");

    private static CalloutContext At(DateTimeOffset now, CommanderGameState state, params JournalEvent[] events) =>
        new(now, false, state, GameStatus.Unknown, NavRoute.None, events);

    private static IEnumerable<Announcement> Crimes(IEnumerable<Announcement> said) =>
        said.Where(announcement => announcement.Key.StartsWith(AdventureCallout.CrimePrefix, StringComparison.Ordinal));

    [Fact]
    public void ADictatorshipSettlementIsWarnedAboutOnceAndAnAnarchyOneNever()
    {
        var callout = Wired(Illegal);
        var state = State("Hayashi Mining Exchange", "Eesuola Prospecting Facility");
        callout.Examine(At(Accepted.AddMinutes(1), state, Jump(Lantern, Accepted.AddMinutes(1)))).ToList();

        var warned = Assert.Single(Crimes(callout.Examine(At(
            Accepted.AddMinutes(2), state, Approach("Hayashi Mining Exchange", "Dictatorship", Accepted.AddMinutes(2))))));

        Assert.Equal(
            "Hayashi Mining Exchange is run by a Dictatorship faction. This job is a crime here. An Anarchy-run settlement would leave you clean.",
            warned.Text);

        Assert.Empty(Crimes(callout.Examine(At(
            Accepted.AddMinutes(3), state, Approach("Hayashi Mining Exchange", "Dictatorship", Accepted.AddMinutes(3))))));

        Assert.Empty(Crimes(callout.Examine(At(
            Accepted.AddMinutes(4), state, Approach("Eesuola Prospecting Facility", "Anarchy", Accepted.AddMinutes(4))))));
    }

    [Fact]
    public void NothingIsSaidWhileNoStoryIsRunning()
    {
        var callout = Wired(Illegal, running: false);

        Assert.Empty(Crimes(callout.Examine(At(
            Accepted.AddMinutes(2), State("Hayashi Mining Exchange"), Approach("Hayashi Mining Exchange", "Dictatorship", Accepted.AddMinutes(2))))));
    }

    [Fact]
    public void NothingIsSaidWhileTheCurrentBeatIsNotAnIllegalMission()
    {
        var state = State("Hayashi Mining Exchange");

        // The arrive beat has not fired yet.
        Assert.Empty(Crimes(Wired(Illegal).Examine(At(
            Accepted.AddMinutes(1), state, Approach("Hayashi Mining Exchange", "Dictatorship", Accepted.AddMinutes(1))))));

        // The current beat is a mission beat for a lawful family.
        var lawful = Wired("Mission_OnFoot_Assassination");
        lawful.Examine(At(Accepted.AddMinutes(1), state, Jump(Lantern, Accepted.AddMinutes(1)))).ToList();

        Assert.Empty(Crimes(lawful.Examine(At(
            Accepted.AddMinutes(2), state, Approach("Hayashi Mining Exchange", "Dictatorship", Accepted.AddMinutes(2))))));
    }

    [Fact]
    public void NothingIsSaidAboutASettlementThatIsNotTheMissionsTarget()
    {
        var callout = Wired(Illegal);
        var state = State("Hayashi Mining Exchange");
        callout.Examine(At(Accepted.AddMinutes(1), state, Jump(Lantern, Accepted.AddMinutes(1)))).ToList();

        Assert.Empty(Crimes(callout.Examine(At(
            Accepted.AddMinutes(2), state, Approach("Another Outpost", "Dictatorship", Accepted.AddMinutes(2))))));
    }
}
