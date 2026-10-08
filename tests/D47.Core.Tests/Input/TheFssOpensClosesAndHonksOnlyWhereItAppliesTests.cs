using D47.Core.Actions;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Input;

public class TheFssOpensClosesAndHonksOnlyWhereItAppliesTests
{
    private static readonly EliteBinds Binds = new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings =
        [
            new EliteBinding("ExplorationFSSEnter", "Primary", "Keyboard", "Key_S"),
            new EliteBinding("ExplorationFSSQuit", "Primary", "Keyboard", "Key_Q"),
            new EliteBinding("ExplorationFSSDiscoveryScan", "Primary", "Keyboard", "Key_H"),
        ],
    };

    private static (CapabilityRegistry Registry, KeywordRouter Router, RecordingGameInput Input) Build(GuiFocus focus)
    {
        var input = new RecordingGameInput();
        var surface = new ActionSurface
        {
            Binds = () => Binds,
            Status = () => new GameStatus
            {
                Flags = StatusFlags.InMainShip,
                GuiFocus = focus,
                ReadAt = DateTimeOffset.UnixEpoch,
            },
            Input = input,
            Enabled = () => true,
        };

        var registry = CapabilityRegistry.Build(ActionCapabilities.All(surface));

        return (registry, new KeywordRouter(registry), input);
    }

    private static async Task<ToolResult> Say(
        (CapabilityRegistry Registry, KeywordRouter Router, RecordingGameInput Input) rig, string phrase)
    {
        var match = rig.Router.MatchToolCommand(phrase);
        Assert.NotNull(match);

        return await rig.Registry.InvokeAsync(match.ToolName, match.Arguments, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("open the fss", "fss_open")]
    [InlineData("full spectrum scanner", "fss_open")]
    [InlineData("fss", "fss_open")]
    [InlineData("close the fss", "fss_close")]
    [InlineData("leave the fss", "fss_close")]
    [InlineData("exit the fss", "fss_close")]
    [InlineData("discovery scan", "fss_discovery_scan")]
    [InlineData("honk", "fss_discovery_scan")]
    public void EachPhraseRoutesWithoutTheModel(string phrase, string id)
    {
        var match = Build(GuiFocus.None).Router.MatchToolCommand(phrase);

        Assert.NotNull(match);
        Assert.Equal("control_interface", match.ToolName);
        Assert.True(match.Arguments.TryGetString("action", out var action));
        Assert.Equal(id, action);
    }

    [Fact]
    public async Task OpeningTheFssPressesItsKey()
    {
        var rig = Build(GuiFocus.None);

        Assert.False((await Say(rig, "open the fss")).IsError);
        Assert.NotEmpty(rig.Input.Steps);
    }

    [Fact]
    public async Task OpeningTheFssWhileItIsOpenSaysSoAndSendsNothing()
    {
        var rig = Build(GuiFocus.FssMode);
        var result = await Say(rig, "open the fss");

        Assert.True(result.IsError);
        Assert.Contains("The FSS is already open.", result.Content);
        Assert.Empty(rig.Input.Steps);
    }

    [Theory]
    [InlineData("close the fss")]
    [InlineData("honk")]
    public async Task ClosingOrScanningOutsideTheFssSaysItIsNotOpenAndSendsNothing(string phrase)
    {
        var rig = Build(GuiFocus.None);
        var result = await Say(rig, phrase);

        Assert.True(result.IsError);
        Assert.Contains("The FSS is not open.", result.Content);
        Assert.Empty(rig.Input.Steps);
    }

    [Fact]
    public async Task TheDiscoveryScanHoldsItsKeyForTheHonkCharge()
    {
        var rig = Build(GuiFocus.FssMode);

        Assert.False((await Say(rig, "discovery scan")).IsError);

        var held = rig.Input.Steps.Where(s => s.Kind == InputStepKind.Delay).Aggregate(TimeSpan.Zero, (sum, s) => sum + s.Delay);

        Assert.Equal(HonkOnArrival.Charge, held);
    }

    [Fact]
    public void TheInterfaceToolListsTheThreeFssActions()
    {
        var tool = ActionCapabilities.All(ActionSurface.Inert).SelectMany(c => c.Tools).Single(t => t.Name == "control_interface");
        var allowed = tool.Parameters.Single(p => p.Name == "action").AllowedValues!;

        Assert.Contains("fss_open", allowed);
        Assert.Contains("fss_close", allowed);
        Assert.Contains("fss_discovery_scan", allowed);
    }
}
