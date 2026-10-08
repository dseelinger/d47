using D47.Core.Actions;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Input;

public class PanelTabsAndSensorRangeAreSpokenTests
{
    private static readonly EliteBinds Binds = new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings =
        [
            new EliteBinding("GalaxyMapHome", "Primary", "Keyboard", "Key_H"),
            new EliteBinding("HMDReset", "Primary", "Keyboard", "Key_R"),
            new EliteBinding("CycleNextPage", "Primary", "Keyboard", "Key_E"),
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

    [Theory]
    [InlineData("next tab", "next_tab")]
    [InlineData("next page", "next_tab")]
    [InlineData("previous tab", "previous_tab")]
    [InlineData("previous page", "previous_tab")]
    [InlineData("ui focus", "ui_focus")]
    [InlineData("focus the panels", "ui_focus")]
    [InlineData("quick comms", "quick_comms")]
    [InlineData("open quick comms", "quick_comms")]
    [InlineData("orbit lines", "orbit_lines")]
    [InlineData("toggle orbit lines", "orbit_lines")]
    [InlineData("galaxy map home", "galaxy_map_home")]
    [InlineData("centre on my system", "galaxy_map_home")]
    [InlineData("increase sensor range", "sensor_range_up")]
    [InlineData("sensor range up", "sensor_range_up")]
    [InlineData("decrease sensor range", "sensor_range_down")]
    [InlineData("sensor range down", "sensor_range_down")]
    [InlineData("recentre the headset", "recentre_headset")]
    [InlineData("recenter the headset", "recentre_headset")]
    [InlineData("reset the headset", "recentre_headset")]
    [InlineData("recentre the view", "recentre_headset")]
    [InlineData("reset vr", "recentre_headset")]
    [InlineData("play galnet", "galnet_play_pause")]
    [InlineData("pause galnet", "galnet_play_pause")]
    [InlineData("galnet play", "galnet_play_pause")]
    [InlineData("galnet pause", "galnet_play_pause")]
    [InlineData("next galnet story", "galnet_next")]
    [InlineData("skip galnet", "galnet_next")]
    [InlineData("previous galnet story", "galnet_previous")]
    [InlineData("clear galnet", "galnet_clear")]
    [InlineData("clear the galnet queue", "galnet_clear")]
    public void EachPhraseRoutesWithoutTheModel(string phrase, string id)
    {
        var match = Build(GuiFocus.None).Router.MatchToolCommand(phrase);

        Assert.NotNull(match);
        Assert.Equal("control_interface", match.ToolName);
        Assert.True(match.Arguments.TryGetString("action", out var action));
        Assert.Equal(id, action);
    }

    [Fact]
    public async Task GalaxyMapHomeOutsideTheGalaxyMapSaysItIsNotOpenAndSendsNothing()
    {
        var rig = Build(GuiFocus.None);
        var match = rig.Router.MatchToolCommand("galaxy map home");
        Assert.NotNull(match);

        var result = await rig.Registry.InvokeAsync(match.ToolName, match.Arguments, TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("The galaxy map is not open.", result.Content);
        Assert.Empty(rig.Input.Steps);
    }

    [Fact]
    public async Task GalaxyMapHomeInsideTheGalaxyMapPressesItsKey()
    {
        var rig = Build(GuiFocus.GalaxyMap);
        var match = rig.Router.MatchToolCommand("galaxy map home");
        Assert.NotNull(match);

        var result = await rig.Registry.InvokeAsync(match.ToolName, match.Arguments, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.NotEmpty(rig.Input.Steps);
    }

    [Fact]
    public async Task RecentringTheHeadsetPressesItsKey()
    {
        var rig = Build(GuiFocus.None);
        var match = rig.Router.MatchToolCommand("recentre the headset");
        Assert.NotNull(match);

        var result = await rig.Registry.InvokeAsync(match.ToolName, match.Arguments, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.NotEmpty(rig.Input.Steps);
    }
}
