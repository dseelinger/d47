using D47.Core.Campaigns;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Storage;
using D47.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class TheBgsWorkAnswerLeadsWithTheCampaignFactionTests
{
    private const string Commander =
        """{"timestamp":"2026-10-10T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""";

    private const string Startup =
        """{"timestamp":"2026-10-10T09:01:00Z","event":"SquadronStartup","SquadronName":"Greybeards","SquadronID":7,"CurrentRank":1}""";

    private const string Home =
        """{"timestamp":"2026-10-10T10:00:00Z","event":"FSDJump","StarSystem":"Home","SystemAddress":1,"Factions":[{"Name":"Neighbour Union","SquadronFaction":false},{"Name":"Kestrel Reach Compact","SquadronFaction":true}]}""";

    private const string Other =
        """{"timestamp":"2026-10-10T10:10:00Z","event":"FSDJump","StarSystem":"Other","SystemAddress":2,"Factions":[{"Name":"Campaign Party","SquadronFaction":false},{"Name":"Third Wheel","SquadronFaction":false}]}""";

    private static readonly DateTimeOffset Tick = DateTimeOffset.Parse("2026-10-10T10:30:00Z");

    private static string Mission(string time, string faction, long system) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"MissionCompleted\",\"FactionEffects\":[{\"Faction\":\"" + faction
        + "\",\"Influence\":[{\"SystemAddress\":" + system + ",\"Trend\":\"UpGood\",\"Influence\":\"+\"}]}]}";

    private static GameStateStore Store()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
        {
            Commander, Startup, Home, Other,
            Mission("2026-10-10T11:00:00Z", "Kestrel Reach Compact", 1),
            Mission("2026-10-10T11:01:00Z", "Neighbour Union", 1),
            Mission("2026-10-10T11:02:00Z", "Third Wheel", 2),
            Mission("2026-10-10T11:03:00Z", "Campaign Party", 2),
            Mission("2026-10-10T11:04:00Z", "Far Party", 9),
        })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    private static CampaignStore Campaigns(Action<CampaignStore>? set)
    {
        var campaigns = new CampaignStore("campaigns.json", new MemoryFileSystem(), NullLogger<CampaignStore>.Instance);
        set?.Invoke(campaigns);

        return campaigns;
    }

    private static async Task<string[]> Ask(CampaignStore? campaigns)
    {
        var capability = JournalCapability.Create(Store(), now: () => Tick.AddHours(3), bgsTick: () => Tick, campaigns: campaigns);
        var result = await CapabilityRegistry.Build([capability]).InvokeAsync(
            "get_bgs_work",
            ToolArguments.FromJson(null),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        return result.Spoken.Split('\n', StringSplitOptions.TrimEntries);
    }

    [Fact]
    public async Task ANamedCampaignFactionComesFirstThenItsSystemsThenTheRest()
    {
        var lines = await Ask(Campaigns(c => c.SetFaction("F1", "Campaign Party", clearIntents: false)));

        Assert.StartsWith("For your campaign faction, Campaign Party in Other:", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("Third Wheel in Other:", lines[2], StringComparison.Ordinal);
        Assert.DoesNotContain("For your", lines[2], StringComparison.Ordinal);
        Assert.DoesNotContain("squadron's faction", string.Join('\n', lines), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACampaignWithNoFactionNamedIsTheSquadronAnswer()
    {
        var without = await Ask(null);
        var with = await Ask(Campaigns(null));

        Assert.Equal(without, with);
        Assert.StartsWith("For your squadron's faction, Kestrel Reach Compact in Home:", with[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task NamingTheSquadronFactionKeepsTheSquadronWording()
    {
        var lines = await Ask(Campaigns(c => c.SetFaction("F1", "kestrel reach compact", clearIntents: false)));

        Assert.StartsWith("For your squadron's faction, Kestrel Reach Compact in Home:", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithTheCampaignOffTheAnswerIsTheSquadronAnswer()
    {
        var without = await Ask(null);
        var off = await Ask(Campaigns(c =>
        {
            c.SetFaction("F1", "Campaign Party", clearIntents: false);
            c.SetOff("F1", true);
        }));

        Assert.Equal(without, off);
    }
}
