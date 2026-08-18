using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// What the transcriber is told to expect (remediation.md 10, item 17).
/// <para>
/// Reported from a running build: "Unlock Lei Cheung" came back as "Unlockly Chung". Proper nouns
/// are where recognition fails hardest and most silently — a misheard name arrives as a plausible
/// English phrase rather than as an error — and this list is the whole defence.
/// </para>
/// <para>
/// The journal half could never have caught that one. An engineer the Commander has not unlocked
/// appears nowhere in their journal, so the one name they were saying was the one name the list
/// could not offer. The engineers are a closed shipped set, so they are added from what d47
/// already holds.
/// </para>
/// </summary>
public class TheBiasListReachesTheNamesThatFailTests
{
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

    private static CommanderGameState Somewhere() => StateFrom(
        """{"timestamp":"3311-01-01T00:01:00Z","event":"Docked","StarSystem":"Shinrarta Dezhra","StationName":"Jameson Memorial"}""");

    /// <summary>The name the Commander was actually saying.</summary>
    [Fact]
    public void TheEngineerTheCommanderWasNamingIsOffered()
    {
        Assert.Contains("Lei Cheung", ProperNouns.From(Somewhere()));
    }

    /// <summary>
    /// And the journal names still come first. Where the Commander is now beats every engineer in
    /// the galaxy, and the cap drops the tail — so the order is the policy.
    /// </summary>
    [Fact]
    public void WhereTheCommanderIsComesFirst()
    {
        var nouns = ProperNouns.From(Somewhere());

        Assert.Equal("Shinrarta Dezhra", nouns[0]);
        Assert.Equal("Jameson Memorial", nouns[1]);
    }

    /// <summary>
    /// The prompt is bounded, and a list long enough to overflow it does not fail loudly — it
    /// silently displaces the model's own context, which is worse than no biasing at all.
    /// </summary>
    [Fact]
    public void TheListStaysWithinItsBudget()
    {
        Assert.True(ProperNouns.From(Crowded()).Count <= ProperNouns.Limit);
    }

    /// <summary>
    /// Even with a fleet crowding the list, the shipped names keep their share. This is the case
    /// the defect happened in: a Commander who has played for years has a long journal, and a list
    /// that let the journal take everything would leave no room for the engineers.
    /// </summary>
    [Fact]
    public void ACrowdedJournalStillLeavesRoomForTheEngineers()
    {
        var nouns = ProperNouns.From(Crowded());
        var engineers = EngineerDirectory.All
            .Select(engineer => engineer.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(
            nouns.Any(engineers.Contains),
            "a long fleet crowded every engineer out of the bias list");
    }

    /// <summary>Nothing at all is still nothing. A Commander d47 has never seen has no list.</summary>
    [Fact]
    public void NoCommanderMeansNoList()
    {
        Assert.Empty(ProperNouns.From(state: null));
    }

    /// <summary>A Commander with a great many ships, each with a name nobody has heard before.</summary>
    private static CommanderGameState Crowded() => StateFrom(
        """{"timestamp":"3311-01-01T00:01:00Z","event":"Location","StarSystem":"Sol"}""",
        Stored());

    private static string Stored()
    {
        var ships = string.Join(
            ",",
            Enumerable.Range(0, 120).Select(number =>
                $$"""{"ShipID":{{number}},"ShipType":"Krait","Name":"Wandering Albatross {{number}}","StarSystem":"Sol","Value":1}"""));

        return $$"""
            {"timestamp":"3311-01-01T00:02:00Z","event":"StoredShips","StarSystem":"Sol","StationName":"Abraham Lincoln","ShipsHere":[{{ships}}],"ShipsRemote":[]}
            """;
    }
}
