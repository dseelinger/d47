using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Mining;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Mining;

/// <summary>The mining target is set by phrase or tool and kept per Commander between sessions (#607).</summary>
public class AMiningTargetIsSetByVoiceTests
{
    private readonly MemoryFileSystem _files = new();

    private MiningTargetStore Store()
    {
        var store = new MiningTargetStore(Path.Combine(@"C:\d47-test", "mining.json"), _files, NullLogger<MiningTargetStore>.Instance);
        store.Load();
        return store;
    }

    private static CapabilityRegistry Registry(MiningTargetStore store, string frontierId = "F1") =>
        CapabilityRegistry.Build([MiningCapability.Create(store, () => frontierId)]);

    private static ToolArguments Arguments(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal));

    [Fact]
    public async Task SayingMiningTargetPainiteSetsItWithoutTheModel()
    {
        var install = new MemoryInstall();
        var router = TestSurface.For(install).Router;

        Assert.Null(router.MatchSetting("mining target painite"));

        var match = router.MatchToolCommand("mining target painite");

        Assert.NotNull(match);
        Assert.Equal(MiningCapability.SetTool, match.ToolName);

        var store = Store();
        var result = await Registry(store).InvokeAsync(
            match.ToolName, match.Arguments, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(new MiningTarget("Painite", null), store.For("F1"));
    }

    [Fact]
    public void EveryMaterialHasAPhrase()
    {
        var install = new MemoryInstall();
        var router = TestSurface.For(install).Router;

        foreach (var material in MiningTarget.Materials)
        {
            var match = router.MatchToolCommand($"mining target {material.ToLowerInvariant()}");

            Assert.True(match?.ToolName == MiningCapability.SetTool, $"\"mining target {material}\" did not match.");
            Assert.Equal(material, match.Arguments.Values["material"]);
        }
    }

    [Fact]
    public async Task APercentageComesThroughTheModel()
    {
        var store = Store();

        var result = await Registry(store).InvokeAsync(
            MiningCapability.SetTool,
            Arguments(("material", "platinum"), ("percent", "25")),
            TestContext.Current.CancellationToken,
            caller: ToolCaller.Model);

        Assert.False(result.IsError);
        Assert.Equal(new MiningTarget("Platinum", 25), store.For("F1"));
    }

    [Fact]
    public async Task AnUnknownMaterialIsRefused()
    {
        var store = Store();

        var result = await Registry(store).InvokeAsync(
            MiningCapability.SetTool, Arguments(("material", "cheese")), TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Null(store.For("F1"));
    }

    [Fact]
    public async Task ClearingTheTargetRemovesIt()
    {
        var install = new MemoryInstall();
        var match = TestSurface.For(install).Router.MatchToolCommand("clear the mining target");

        Assert.NotNull(match);
        Assert.Equal(MiningCapability.ClearTool, match.ToolName);

        var store = Store();
        store.Set("F1", new MiningTarget("Painite", null));

        var result = await Registry(store).InvokeAsync(
            match.ToolName, match.Arguments, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Null(store.For("F1"));
        Assert.Null(Store().For("F1"));
    }

    [Fact]
    public void TheTargetSurvivesARestartForTheSameCommanderOnly()
    {
        Store().Set("F1", new MiningTarget("Platinum", 25));

        var reopened = Store();

        Assert.Equal(new MiningTarget("Platinum", 25), reopened.For("F1"));
        Assert.Null(reopened.For("F2"));
    }
}
