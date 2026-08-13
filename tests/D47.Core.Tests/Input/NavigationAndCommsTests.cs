using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>
/// Getting a destination into the game and a message out of it (list.md Phase 10, items 10 to
/// 12).
/// </summary>
public class NavigationAndCommsTests
{
    private static EliteBinds Binds(params (string Action, string Device, string Key)[] entries) => new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings = [.. entries.Select(e => new EliteBinding(e.Action, "Primary", e.Device, e.Key))],
    };

    private static GameStatus Flying => new()
    {
        Flags = StatusFlags.InMainShip,
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    private static ActionSurface Actions(EliteBinds binds, RecordingGameInput input, bool enabled = true) => new()
    {
        Binds = () => binds,
        Status = () => Flying,
        Input = input,
        Enabled = () => enabled,
    };

    private static async Task<ToolResult> Invoke(
        CapabilityDescriptor descriptor,
        string tool,
        params (string Name, string Value)[] arguments)
    {
        var registry = CapabilityRegistry.Build([descriptor]);

        return await registry.InvokeAsync(
            tool,
            new ToolArguments(arguments.ToDictionary(a => a.Name, a => a.Value, StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);
    }

    private static NavigationSurface Navigation(
        RecordingClipboard clipboard,
        ActionSurface actions,
        bool autoPlot,
        bool? confirm) => new()
    {
        Clipboard = clipboard,
        Actions = actions,
        AutoPlotEnabled = () => autoPlot,
        ConfirmPlot = (_, _) => Task.FromResult(confirm),
    };

    [Fact]
    public async Task TheClipboardTakesWhateverItWasGiven()
    {
        var clipboard = new RecordingClipboard();

        var result = await Invoke(
            NavigationCapability.Create(Navigation(clipboard, ActionSurface.Inert, false, null)),
            "copy_to_clipboard",
            ("text", "Shinrarta Dezhra"));

        Assert.False(result.IsError);
        Assert.Equal("Shinrarta Dezhra", clipboard.Last);
    }

    [Fact]
    public async Task PlottingCopiesTheNameEvenWithAutoPlotOff()
    {
        // The clipboard is the primary path. Whatever happens to the attempt, the Commander
        // ends up holding the name.
        var clipboard = new RecordingClipboard();

        var result = await Invoke(
            NavigationCapability.Create(Navigation(clipboard, ActionSurface.Inert, autoPlot: false, null)),
            "plot_course",
            ("system", "Colonia"));

        Assert.False(result.IsError);
        Assert.Equal("Colonia", clipboard.Last);
        Assert.Contains("clipboard", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NoRouteAppearingIsReportedAsProbablyNotWorkingRatherThanAsSuccess()
    {
        // The failure this verification exists to prevent: believing a course is set.
        var input = new RecordingGameInput();
        var clipboard = new RecordingClipboard();

        var result = await Invoke(
            NavigationCapability.Create(Navigation(
                clipboard,
                Actions(Binds(("GalaxyMapOpen", "Keyboard", "Key_F6")), input),
                autoPlot: true,
                confirm: false)),
            "plot_course",
            ("system", "Colonia"));

        Assert.Contains("did not work", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(input.Steps);
    }

    [Fact]
    public async Task ARouteAppearingIsReportedPlainly()
    {
        var result = await Invoke(
            NavigationCapability.Create(Navigation(
                new RecordingClipboard(),
                Actions(Binds(("GalaxyMapOpen", "Keyboard", "Key_F6")), new RecordingGameInput()),
                autoPlot: true,
                confirm: true)),
            "plot_course",
            ("system", "Colonia"));

        Assert.Contains("Course plotted to Colonia", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CannotTellIsItsOwnAnswer()
    {
        // "I cannot tell" and "it did not work" send the Commander to different places.
        var result = await Invoke(
            NavigationCapability.Create(Navigation(
                new RecordingClipboard(),
                Actions(Binds(("GalaxyMapOpen", "Keyboard", "Key_F6")), new RecordingGameInput()),
                autoPlot: true,
                confirm: null)),
            "plot_course",
            ("system", "Colonia"));

        Assert.Contains("cannot tell", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnUnboundGalaxyMapStillCopiesAndSaysWhyItCouldNotPlot()
    {
        // GalaxyMapOpen ships unbound in Elite's own default keyboard preset, so this is the
        // out-of-the-box experience rather than an edge case.
        var clipboard = new RecordingClipboard();
        var input = new RecordingGameInput();

        var result = await Invoke(
            NavigationCapability.Create(Navigation(
                clipboard,
                Actions(Binds(("LandingGearToggle", "Keyboard", "Key_L")), input),
                autoPlot: true,
                confirm: true)),
            "plot_course",
            ("system", "Colonia"));

        Assert.False(result.IsError);
        Assert.Equal("Colonia", clipboard.Last);
        Assert.Contains("no binding", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(input.Steps);
    }

    [Fact]
    public async Task AChatMessageIsTypedAsTextWithItsChannelPrefix()
    {
        var input = new RecordingGameInput();
        var actions = Actions(Binds(("FocusCommsPanel", "Keyboard", "Key_Enter")), input);

        var result = await Invoke(
            CommsCapability.Create(actions, () => true),
            "send_chat_message",
            ("message", "o7 Commander"),
            ("channel", "wing"));

        Assert.False(result.IsError);

        var typed = input.Steps.Single(step => step.Kind == InputStepKind.Text);
        Assert.Equal("/w o7 Commander", typed.Text);
    }

    [Fact]
    public async Task TheMessageIsReadBackSoAMisheardOneIsCaughtByTheCommanderFirst()
    {
        var actions = Actions(Binds(("FocusCommsPanel", "Keyboard", "Key_Enter")), new RecordingGameInput());

        var result = await Invoke(
            CommsCapability.Create(actions, () => true),
            "send_chat_message",
            ("message", "docking at Jameson"),
            ("channel", "local"));

        Assert.Contains("docking at Jameson", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANewlineCannotSendHalfAMessageAndTypeTheRestIntoTheCockpit()
    {
        // Every character reaching the cockpit is a keybind.
        var input = new RecordingGameInput();
        var actions = Actions(Binds(("FocusCommsPanel", "Keyboard", "Key_Enter")), input);

        await Invoke(
            CommsCapability.Create(actions, () => true),
            "send_chat_message",
            ("message", "first line\nsecond line"),
            ("channel", "local"));

        var typed = input.Steps.Single(step => step.Kind == InputStepKind.Text);
        Assert.DoesNotContain('\n', typed.Text!);
    }

    [Fact]
    public async Task NoMessageGoesOutWhileTheSettingIsOff()
    {
        var input = new RecordingGameInput();
        var actions = Actions(Binds(("FocusCommsPanel", "Keyboard", "Key_Enter")), input);

        var result = await Invoke(
            CommsCapability.Create(actions, () => false),
            "send_chat_message",
            ("message", "o7"),
            ("channel", "local"));

        Assert.True(result.IsError);
        Assert.Empty(input.Steps);
    }

    [Fact]
    public async Task ChatNeedsKeyPressesToBeAllowedAsWell()
    {
        var input = new RecordingGameInput();
        var actions = Actions(Binds(("FocusCommsPanel", "Keyboard", "Key_Enter")), input, enabled: false);

        var result = await Invoke(
            CommsCapability.Create(actions, () => true),
            "send_chat_message",
            ("message", "o7"),
            ("channel", "local"));

        Assert.True(result.IsError);
        Assert.Empty(input.Steps);
    }

    [Fact]
    public void EverySwitchThatReachesTheGameIsOffAndProtectedInADefaultInstall()
    {
        var defaults = D47.Core.Configuration.D47Settings.Defaults;

        Assert.False(defaults.Actions.Keyboard);
        Assert.False(defaults.Actions.HonkOnArrival);
        Assert.False(defaults.Actions.AutoPlot);
        Assert.False(defaults.Actions.Chat);

        var rows = new[]
        {
            NavigationCapability.Create(NavigationSurface.Inert),
            CommsCapability.Create(ActionSurface.Inert, () => false),
            AutonomousCapability.Create(() => string.Empty),
        }.SelectMany(descriptor => descriptor.Settings);

        Assert.All(rows, row => Assert.True(row.Protected, $"{row.Key} reaches the game and must be protected."));
    }
}
