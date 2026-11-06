using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class TheBgsWorkAnswerPutsTheSquadronFactionFirstTests
{
    private const string Commander =
        """{"timestamp":"2026-10-10T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""";

    private const string Startup =
        """{"timestamp":"2026-10-10T09:01:00Z","event":"SquadronStartup","SquadronName":"Greybeards","SquadronID":7,"CurrentRank":1}""";

    private const string Home =
        """{"timestamp":"2026-10-10T10:00:00Z","event":"FSDJump","StarSystem":"Home","SystemAddress":1,"Factions":[{"Name":"Neighbour Union","SquadronFaction":false},{"Name":"Kestrel Reach Compact","SquadronFaction":true}]}""";

    private static readonly DateTimeOffset Tick = DateTimeOffset.Parse("2026-10-10T10:30:00Z");

    private static string Mission(string time, string faction, long system) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"MissionCompleted\",\"FactionEffects\":[{\"Faction\":\"" + faction
        + "\",\"Influence\":[{\"SystemAddress\":" + system + ",\"Trend\":\"UpGood\",\"Influence\":\"+\"}]}]}";

    private static GameStateStore Store(params string[] events)
    {
        var store = new GameStateStore();

        foreach (var line in events)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    private static async Task<string> Ask(GameStateStore store)
    {
        var capability = JournalCapability.Create(store, now: () => Tick.AddHours(3), bgsTick: () => Tick);
        var result = await CapabilityRegistry.Build([capability]).InvokeAsync(
            "get_bgs_work",
            ToolArguments.FromJson(null),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        return result.Spoken;
    }

    private static string[] Lines(string answer) => answer.Split('\n', StringSplitOptions.TrimEntries);

    [Fact]
    public async Task TheSquadronFactionComesFirstThenItsSystemsThenTheRest()
    {
        var store = Store(
            Commander, Startup, Home,
            Mission("2026-10-10T11:00:00Z", "Far Party", 9),
            Mission("2026-10-10T11:01:00Z", "Far Party", 9),
            Mission("2026-10-10T11:02:00Z", "Neighbour Union", 1),
            Mission("2026-10-10T11:03:00Z", "Kestrel Reach Compact", 1));

        var lines = Lines(await Ask(store));

        Assert.StartsWith("For your squadron's faction, Kestrel Reach Compact in Home:", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("Neighbour Union in Home:", lines[2], StringComparison.Ordinal);
        Assert.StartsWith("Far Party in a system not in your recent journals:", lines[3], StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyTheSquadronFactionsOwnPairCarriesTheWording()
    {
        var store = Store(
            Commander, Startup, Home,
            Mission("2026-10-10T11:02:00Z", "Neighbour Union", 1),
            Mission("2026-10-10T11:03:00Z", "Kestrel Reach Compact", 1));

        var said = await Ask(store);

        Assert.Contains("For your squadron's faction, Kestrel Reach Compact", said, StringComparison.Ordinal);
        Assert.DoesNotContain("For your squadron's faction, Neighbour", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoSquadronFactionTheOrderIsMostEntriesFirst()
    {
        var store = Store(
            Commander, Startup,
            Mission("2026-10-10T11:00:00Z", "Far Party", 9),
            Mission("2026-10-10T11:01:00Z", "Far Party", 9),
            Mission("2026-10-10T11:03:00Z", "Kestrel Reach Compact", 1));

        var said = await Ask(store);
        var lines = Lines(said);

        Assert.StartsWith("Far Party", lines[1], StringComparison.Ordinal);
        Assert.DoesNotContain("your squadron's faction", said, StringComparison.Ordinal);
    }
}
