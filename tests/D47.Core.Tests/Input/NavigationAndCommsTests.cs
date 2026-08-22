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

    private static GameStatus FlyingWithTheMapOpen => Flying with { GuiFocus = GuiFocus.GalaxyMap };

    private static ActionSurface Actions(
        EliteBinds binds,
        RecordingGameInput input,
        bool enabled = true,
        GameStatus? status = null) => new()
    {
        Binds = () => binds,
        Status = () => status ?? Flying,
        Input = input,
        Enabled = () => enabled,
    };

    /// <summary>
    /// Every binding the galaxy-map macro presses, on the keyboard. The Commander's own file binds
    /// exactly these five, and on these symbols.
    /// </summary>
    private static EliteBinds MapBinds(params string[] without) => Binds(
        [.. new[]
        {
            ("GalaxyMapOpen", "Keyboard", "Key_M"),
            ("UI_Up", "Keyboard", "Key_W"),
            ("UI_Select", "Keyboard", "Key_Space"),
            ("CamTranslateRight", "Keyboard", "Key_R"),
            ("CamTranslateLeft", "Keyboard", "Key_L"),
        }.Where(entry => !without.Contains(entry.Item1))]);

    private static uint Code(string symbol) => EliteKeys.Resolve(symbol).Code;

    private static IReadOnlyList<uint> KeysPressed(IReadOnlyList<InputStep> steps) =>
        [.. steps.Where(step => step.Kind == InputStepKind.KeyDown).Select(step => step.Code)];

    /// <summary>How long each press of a key was held for, in the order they were made.</summary>
    private static IReadOnlyList<TimeSpan> Holds(IReadOnlyList<InputStep> steps, uint code)
    {
        var holds = new List<TimeSpan>();

        for (var index = 0; index < steps.Count; index++)
        {
            if (steps[index] is not { Kind: InputStepKind.KeyDown } down || down.Code != code)
            {
                continue;
            }

            var held = TimeSpan.Zero;

            for (var after = index + 1; after < steps.Count; after++)
            {
                if (steps[after].Kind == InputStepKind.Delay)
                {
                    held += steps[after].Delay;
                }
                else if (steps[after].Kind == InputStepKind.KeyUp && steps[after].Code == code)
                {
                    break;
                }
            }

            holds.Add(held);
        }

        return holds;
    }

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
        bool? confirm,
        bool? mapOpens = true,
        bool? mapCloses = true) => new()
    {
        Clipboard = clipboard,
        Actions = actions,
        AutoPlotEnabled = () => autoPlot,
        WatchRoute = () => new FixedPlotWatch(confirm),
        AwaitGalaxyMap = (open, _) => Task.FromResult(open ? mapOpens : mapCloses),
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
                Actions(MapBinds(), input),
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
                Actions(MapBinds(), new RecordingGameInput()),
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
                Actions(MapBinds(), new RecordingGameInput()),
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

    /// <summary>
    /// The Commander's own sequence (2026-08-21, third cut): map, up, select, paste, return,
    /// three seconds for the camera, a brush of sideways camera right and then left for the same
    /// time, select held for 1.2 seconds, map again to close. The figures are theirs and are
    /// asserted as such. Return rather than "down" because the first cut's down key typed an S
    /// into the search box — the text field keeps focus after the paste. The brush is what takes
    /// focus out of the box and arms the selector: without it the second cut's held select did
    /// nothing. Right then left so the selector ends up back on the star.
    /// </summary>
    [Fact]
    public async Task TheMacroIsTheCommandersOwnSequence()
    {
        var input = new RecordingGameInput();

        await Invoke(
            NavigationCapability.Create(Navigation(new RecordingClipboard(), Actions(MapBinds(), input), autoPlot: true, confirm: true)),
            "plot_course",
            ("system", "Colonia"));

        var steps = input.Steps;

        Assert.Equal(
            [Code("Key_M"), Code("Key_W"), Code("Key_Space"), 0xA2, 0x56, 0x0D, Code("Key_R"), Code("Key_L"), Code("Key_Space"), Code("Key_M")],
            KeysPressed(steps));

        // The camera gets its three seconds, then the brush — right and left for exactly the
        // same time, so it nets to nothing — then the hold that plots.
        Assert.Contains(steps, step => step.Kind == InputStepKind.Delay && step.Delay == TimeSpan.FromSeconds(3));
        Assert.Equal([TimeSpan.FromMilliseconds(30)], Holds(steps, Code("Key_R")));
        Assert.Equal(Holds(steps, Code("Key_R")), Holds(steps, Code("Key_L")));
        Assert.Equal(TimeSpan.FromMilliseconds(1200), Holds(steps, Code("Key_Space"))[^1]);
    }

    /// <summary>
    /// The interface keys are a W and a space bar if they reach the cockpit instead of the map,
    /// so nothing after the map key is sent until Status.json says the map is showing.
    /// </summary>
    [Fact]
    public async Task AMapThatDoesNotOpenGetsNothingTypedIntoTheCockpit()
    {
        var input = new RecordingGameInput();
        var clipboard = new RecordingClipboard();

        var result = await Invoke(
            NavigationCapability.Create(Navigation(clipboard, Actions(MapBinds(), input), autoPlot: true, confirm: true, mapOpens: false)),
            "plot_course",
            ("system", "Colonia"));

        Assert.Equal([Code("Key_M")], KeysPressed(input.Steps));
        Assert.Contains("did not open", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Colonia", clipboard.Last);
    }

    /// <summary>The map key is a toggle, so a map already showing is not shut by the first press.</summary>
    [Fact]
    public async Task AMapAlreadyOpenIsNotToggledShut()
    {
        var input = new RecordingGameInput();

        await Invoke(
            NavigationCapability.Create(Navigation(
                new RecordingClipboard(),
                Actions(MapBinds(), input, status: FlyingWithTheMapOpen),
                autoPlot: true,
                confirm: true)),
            "plot_course",
            ("system", "Colonia"));

        // The map key is pressed once — at the end, to close — and not first.
        Assert.Equal(Code("Key_W"), KeysPressed(input.Steps)[0]);
        Assert.Single(KeysPressed(input.Steps), Code("Key_M"));
    }

    /// <summary>
    /// All three keys or none. A macro that opens the map and then has no "select" leaves the map
    /// open over the cockpit, which is worse than the clipboard alone.
    /// </summary>
    [Fact]
    public async Task OneMissingInterfaceKeyStopsTheWholeAttemptBeforeAnyKeyIsSent()
    {
        var input = new RecordingGameInput();
        var clipboard = new RecordingClipboard();

        var result = await Invoke(
            NavigationCapability.Create(Navigation(clipboard, Actions(MapBinds(without: "UI_Select"), input), autoPlot: true, confirm: true)),
            "plot_course",
            ("system", "Colonia"));

        Assert.Empty(input.Steps);
        Assert.Equal("Colonia", clipboard.Last);
        Assert.Contains("no binding for select", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AMapLeftOpenIsSaidSo()
    {
        var result = await Invoke(
            NavigationCapability.Create(Navigation(
                new RecordingClipboard(),
                Actions(MapBinds(), new RecordingGameInput()),
                autoPlot: true,
                confirm: true,
                mapCloses: false)),
            "plot_course",
            ("system", "Colonia"));

        Assert.Contains("Course plotted to Colonia", result.Content, StringComparison.Ordinal);
        Assert.Contains("still open", result.Content, StringComparison.OrdinalIgnoreCase);
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
