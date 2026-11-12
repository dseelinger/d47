using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using D47.Core.Conversation;
using D47.Core.Storage;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Knowledge;
using D47.Core.Watches;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Conversation.TurnRuns;

namespace D47.Core.Tests.Watches;

/// <summary>A next-system watch is added and deleted by the Commander's words with no model call.</summary>
public class SayItAndTheNextSystemWatchIsSetTests
{
    private sealed class FakeGalaxy : StubGalaxyService
    {
        public IReadOnlyList<StationSummary> Stations { get; set; } = [];

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new StationSearchResult(query.ReferenceSystem, Stations.Count, Stations));
    }

    private sealed class Rig
    {
        public WatchStore Store { get; } =
            new(@"C:\d47\data\watches.json", new MemoryFileSystem(), NullLogger<WatchStore>.Instance);

        public FakeGalaxy Galaxy { get; } = new();

        public bool GalaxyOn { get; set; } = true;

        public string? System { get; set; }

        public FakeLlmProvider Model { get; } = new(
            new LlmStreamEvent.TextDelta("From the model."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
        {
            ToolCalls = true,
        };

        public CapabilityRegistry Registry { get; }

        public Rig()
        {
            var voice = new WatchVoice(() => Store, () => GalaxyOn ? Galaxy : null, () => System, NullLogger<WatchVoice>.Instance);
            Registry = CapabilityRegistry.Build([WatchesCapability.Create(() => Store.Watches, voice: voice)]);
        }

        public Task<string> SayAsync(string said) => SaidAsync(TurnLoops.Build(Registry, Model), said);
    }

    private static StationSummary Station(string name, string system, double distance, string? trader = "Manufactured") => new()
    {
        Name = name,
        SystemName = system,
        Type = "Coriolis Starport",
        DistanceToArrival = distance,
        TraderType = trader,
        Services = ["Market", "Material Trader"],
        SmallPads = 4,
        MediumPads = 6,
        LargePads = 2,
    };

    [Fact]
    public async Task ADictatedSentenceAddsAWatchWithNoModelCall()
    {
        var rig = new Rig();

        var said = await rig.SayAsync("Tell me the next time we're in a system with a manufactured material trader.");

        var watch = Assert.Single(rig.Store.Watches);
        Assert.Equal(WatchKind.NextSystem, watch.Kind);
        Assert.Equal("Material Trader", watch.Service);
        Assert.Equal("Manufactured", watch.ServiceKind);
        Assert.True(watch.Enabled);
        Assert.True(watch.CheckRoute);
        Assert.Equal("Manufactured material trader", watch.Name);
        Assert.Equal(
            "Watching for a Manufactured material trader, Commander. I'll tell you when we arrive in a system with one, "
            + "or when a route you plot passes through one.",
            said);
        Assert.Equal(0, rig.Model.CallCount);
    }

    [Fact]
    public async Task APadIsStored()
    {
        var rig = new Rig();

        await rig.SayAsync("let me know the next time I'm in a system with a technology broker with a large pad");

        var watch = Assert.Single(rig.Store.Watches);
        Assert.Equal("Technology Broker", watch.Service);
        Assert.Equal(PadSize.Large, watch.MinPad);
    }

    [Fact]
    public async Task TheModelIsOfferedNeitherTool()
    {
        var rig = new Rig();

        var said = await rig.SayAsync("What is the weather like in Sol?");

        Assert.Equal("From the model.", said);

        var offered = rig.Model.LastRequest!.Prompt.Tools.Select(tool => tool.Name).ToList();

        Assert.Contains(WatchesCapability.Tool, offered);
        Assert.DoesNotContain(WatchesCapability.AddTool, offered);
        Assert.DoesNotContain(WatchesCapability.DeleteTool, offered);
    }

    [Fact]
    public async Task TheModelIsRefusedBothTools()
    {
        var rig = new Rig();
        var arguments = new ToolArguments(new Dictionary<string, string> { ["service"] = "Material Trader" });

        var added = await rig.Registry.InvokeAsync(
            WatchesCapability.AddTool, arguments, TestContext.Current.CancellationToken, ToolCaller.Model);

        Assert.True(added.IsError);
        Assert.Empty(rig.Store.Watches);

        await rig.SayAsync("tell me the next time we're in a system with a material trader");

        var deleted = await rig.Registry.InvokeAsync(
            WatchesCapability.DeleteTool, arguments, TestContext.Current.CancellationToken, ToolCaller.Model);

        Assert.True(deleted.IsError);
        Assert.Single(rig.Store.Watches);
    }

    [Fact]
    public async Task AMatchInTheSystemWeAreInIsNamedAndTheWatchStaysOn()
    {
        var rig = new Rig { System = "Ngurii" };
        rig.Galaxy.Stations = [Station("Cheranovsky City", "Ngurii", 1180)];

        var said = await rig.SayAsync("tell me the next time we're in a system with a material trader");

        Assert.Equal(
            "Ngurii has one now, Commander: Cheranovsky City, 1,180 light seconds, large pad. I'll watch from the next system on.",
            said);

        var watch = Assert.Single(rig.Store.Watches);
        Assert.True(watch.Enabled);
        Assert.Null(watch.FiredSystem);
        Assert.Equal(0, rig.Model.CallCount);
    }

    [Fact]
    public async Task ASystemWithNoMatchGetsTheOrdinaryReply()
    {
        var rig = new Rig { System = "Ngurii" };
        rig.Galaxy.Stations = [Station("Cheranovsky City", "Ngurii", 1180, trader: "Raw")];

        var said = await rig.SayAsync("tell me the next time we're in a system with a manufactured material trader");

        Assert.StartsWith("Watching for a Manufactured material trader, Commander.", said, StringComparison.Ordinal);
        Assert.Single(rig.Store.Watches);
    }

    [Fact]
    public async Task WithGalaxySearchOffTheWatchIsSavedAndTheReplySaysWhatIsMissing()
    {
        var rig = new Rig { GalaxyOn = false, System = "Ngurii" };

        var said = await rig.SayAsync("tell me the next time we're in a system with a material trader");

        Assert.Single(rig.Store.Watches);
        Assert.Equal(
            "Watching for a Material trader, Commander. The watch is saved. D47 cannot tell what a system's stations have until Galaxy search is on.",
            said);
    }

    [Fact]
    public async Task AnIdenticalEnabledWatchIsNotAddedTwice()
    {
        var rig = new Rig();

        await rig.SayAsync("tell me the next time we're in a system with a material trader");
        var said = await rig.SayAsync("let me know the next time I'm in a system with a materials trader");

        Assert.Single(rig.Store.Watches);
        Assert.Equal("I am already watching for a Material trader, Commander.", said);
    }

    [Fact]
    public async Task AFullFileSaysSoAndKeepsWhatItHas()
    {
        var rig = new Rig();

        for (var i = 0; i < WatchStore.MaxWatches; i++)
        {
            Assert.Null(rig.Store.Add(new Watch(Guid.NewGuid(), $"Community Goals {i}", WatchKind.NewCommunityGoal)));
        }

        var said = await rig.SayAsync("tell me the next time we're in a system with a shipyard");

        Assert.Equal(
            "You have 32 watches, the most I keep. Delete one under Commander, Watches, then ask again.",
            said);
        Assert.Equal(WatchStore.MaxWatches, rig.Store.Watches.Count);
    }

    [Fact]
    public async Task AnUnknownServiceIsDeclinedAndNothingIsStored()
    {
        var rig = new Rig();

        var said = await rig.SayAsync("tell me the next time we're in a system with a coffee shop");

        Assert.Equal(WatchVoice.Declined("a coffee shop"), said);
        Assert.Empty(rig.Store.Watches);
        Assert.Equal(0, rig.Model.CallCount);
    }

    [Fact]
    public async Task TheStopFormDeletesTheWatchAndSaysWhenThereIsNone()
    {
        var rig = new Rig();

        await rig.SayAsync("tell me the next time we're in a system with a material trader");

        var deleted = await rig.SayAsync("stop watching for a material trader");

        Assert.Equal("Done, Commander. That watch is deleted.", deleted);
        Assert.Empty(rig.Store.Watches);

        var none = await rig.SayAsync("stop watching for a material trader");

        Assert.Equal("You have no watch for a Material trader, Commander.", none);
        Assert.Equal(0, rig.Model.CallCount);
    }

    [Fact]
    public async Task AKindDeletesOnlyThatKind()
    {
        var rig = new Rig();

        await rig.SayAsync("tell me the next time we're in a system with a raw material trader");
        await rig.SayAsync("tell me the next time we're in a system with an encoded material trader");

        await rig.SayAsync("stop watching for an encoded material trader");

        Assert.Equal("Raw", Assert.Single(rig.Store.Watches).ServiceKind);

        var ambiguous = await rig.SayAsync("tell me the next time we're in a system with an encoded material trader")
            + await rig.SayAsync("stop watching for a material trader");

        Assert.Contains("Say which kind", ambiguous, StringComparison.Ordinal);
        Assert.Equal(2, rig.Store.Watches.Count);
    }
}
