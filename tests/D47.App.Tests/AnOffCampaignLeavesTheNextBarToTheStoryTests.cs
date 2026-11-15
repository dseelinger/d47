using D47.App.Panel;
using D47.Core.Campaigns;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>With the campaign turned off, the NEXT bar gets no campaign line: the story shows with no toggle, or nothing.</summary>
public sealed class AnOffCampaignLeavesTheNextBarToTheStoryTests
{
    private const string Faction = "Kestrel Reach Compact";

    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private static readonly ObjectiveStripText Story =
        new("NEXT", "Scan the beacon in ", "Sol", "Sol", null, false, false, "THE SALT ROAD");

    private readonly CampaignStore _store = new("campaigns.json", new MemoryFileSystem(), NullLogger<CampaignStore>.Instance);

    private readonly CommanderGameState _state = new(new CommanderIdentity("F1", "Fixture"));

    private readonly Queue<Func<Task>> _dispatched = new();

    private readonly CampaignFrontsWatch _watch;

    private readonly CampaignFocus _focus;

    public AnOffCampaignLeavesTheNextBarToTheStoryTests()
    {
        _store.SetFaction("F1", Faction, clearIntents: false);

        var famine = new FactionSystem
        {
            Name = "Diso",
            Influence = 0.50,
            Controls = true,
            ActiveStates = ["Famine"],
            Others = [new FactionNeighbour("Lave Purple Gang", 0.10)],
            ReportedAt = Now.AddDays(-1),
            Population = 1_000_000,
        };

        _watch = new CampaignFrontsWatch(
            (_, _, _) => Task.FromResult(new FactionReport(Faction, "Cooperative", "Independent", [famine], [])),
            () => _state,
            _store,
            null,
            () => null,
            null,
            NullLogger<CampaignFrontsWatch>.Instance)
        {
            Dispatch = _dispatched.Enqueue,
        };

        _focus = new CampaignFocus(_store, () => _watch.Snapshot, () => _state, () => null, () => Now);
    }

    private async Task Poll()
    {
        _watch.Poll(Now);

        while (_dispatched.TryDequeue(out var work))
        {
            await work();
        }

        _watch.Poll(Now);
        _focus.Poll();
    }

    [Fact]
    public async Task TheStoryShowsWithNoToggleOnceTheCampaignIsOff()
    {
        await Poll();
        Assert.NotNull(_focus.Accept());

        var running = ObjectiveStrip.Choose(Story, ObjectiveStrip.DescribeCampaign(_focus.NextAction(), null), NextBarSource.Campaign)!;
        Assert.Equal(NextBarSource.Campaign, running.Source);
        Assert.True(running.Toggle);

        _store.SetOff("F1", off: true);
        await Poll();

        var campaign = ObjectiveStrip.DescribeCampaign(_focus.NextAction(), null);
        Assert.Null(campaign);

        var off = ObjectiveStrip.Choose(Story, campaign, NextBarSource.Campaign)!;
        Assert.Equal(NextBarSource.Story, off.Source);
        Assert.False(off.Toggle);
        Assert.Null(ObjectiveStrip.Choose(null, campaign, NextBarSource.Campaign));
    }
}
