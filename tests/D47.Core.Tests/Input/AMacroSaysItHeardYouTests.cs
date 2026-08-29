using D47.Core.Actions;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>
/// When each macro says it heard you, against when it says how it went (#158).
/// <para>
/// Asked 2026-08-28: <i>"'Take us out' and similar commands should acknowledge the command at the
/// beginning, not only at the end after the attempt is over."</i> Widened the same day to the
/// galaxy-map plot, whose silence is measurably longer: <i>"Also add the acknowledgement to the
/// galaxy map plot macro."</i>
/// </para>
/// <para>
/// <b>The successful launch line was literally "Taking us out."</b>, spoken after the ship had
/// left the pad — the present tense arriving in the past, and the first sound either way. The
/// separations are the same shape: a bounded boost loop with a wall-clock ceiling, and nothing
/// said until it ends. That gap is what produces a repeated command, and a repeated command
/// mid-macro is its own hazard.
/// </para>
/// <para>
/// <b>One rule for all four:</b> acknowledge at accept, per command, in its own words; verdict
/// when known. A refusal stays exactly one line, immediately — which is why the acknowledgement
/// lives inside each macro after its own pre-flight rather than in <see cref="ShipCommands"/>
/// ahead of them.
/// </para>
/// </summary>
public class AMacroSaysItHeardYouTests
{
    private static EliteBinds Binds(params (string Action, string Key)[] entries) => new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings = [.. entries.Select(entry => new EliteBinding(entry.Action, "Primary", "Keyboard", entry.Key))],
    };

    private static EliteBinds PanelBinds() => Binds(
        ("FocusLeftPanel", "Key_1"),
        ("UI_Back", "Key_B"),
        ("UI_Down", "Key_S"),
        ("UI_Select", "Key_Z"));

    private static GameStatus Docked() => new()
    {
        Flags = StatusFlags.InMainShip | StatusFlags.Docked,
        GuiFocus = GuiFocus.None,
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    private static GameStatus Flying() => new()
    {
        Flags = StatusFlags.InMainShip,
        GuiFocus = GuiFocus.None,
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    /// <summary>
    /// An input that records what was said at the moment each key went out, so "before the first
    /// key" is asserted as an ordering rather than as a count of sentences.
    /// </summary>
    private sealed class Listening
    {
        public List<string> Said { get; } = [];

        public List<string> SaidBeforeTheFirstKey { get; } = [];

        public ActionSurface Surface(RecordingGameInput input, GameStatus status, EliteBinds binds) =>
            new()
            {
                Binds = () => binds,
                Status = () => status,
                Input = input,
                Enabled = () => true,
                Acknowledge = line =>
                {
                    Said.Add(line);

                    if (input.Steps.Count == 0)
                    {
                        SaidBeforeTheFirstKey.Add(line);
                    }
                },
            };
    }

    /// <summary>
    /// The reported command: acknowledged before the left-panel key, and the verdict afterwards —
    /// two lines doing two jobs, in that order.
    /// </summary>
    [Fact]
    public async Task TakeUsOutIsAcknowledgedBeforeTheLeftPanelKeyIsPressed()
    {
        var heard = new Listening();
        var input = new RecordingGameInput();

        var outcome = await Launch.RunAsync(
            heard.Surface(input, Docked(), PanelBinds()),
            (_, _) => Task.FromResult<bool?>(true),
            _ => Task.FromResult<bool?>(true),
            TestContext.Current.CancellationToken);

        Assert.Equal(["Taking us out."], heard.SaidBeforeTheFirstKey);
        Assert.Equal(LaunchEnding.Launched, outcome.Ending);

        // And the verdict no longer reads as if the launch were only now starting.
        Assert.NotEqual("Taking us out.", outcome.Message);
        Assert.Equal("We are away.", outcome.Message);
    }

    /// <summary>
    /// A refusal is still exactly one line, immediately: the acknowledgement exists only on the
    /// road where work follows.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARefusalIsStillOneLineAndNothingIsAcknowledged(bool missingBindings)
    {
        var heard = new Listening();
        var input = new RecordingGameInput();

        var outcome = await Launch.RunAsync(
            heard.Surface(
                input,

                // Not docked, or docked with nothing bound to walk the panel with. Both refuse
                // before a key, and neither is an attempt.
                missingBindings ? Docked() : Flying(),
                missingBindings ? Binds(("FocusLeftPanel", "Key_1")) : PanelBinds()),
            (_, _) => Task.FromResult<bool?>(true),
            _ => Task.FromResult<bool?>(true),
            TestContext.Current.CancellationToken);

        Assert.Equal(LaunchEnding.Refused, outcome.Ending);
        Assert.Empty(heard.Said);
        Assert.Empty(input.Steps);
    }

    /// <summary>
    /// Both separations acknowledge before the first boost. They differ in one word — which drive
    /// finishes the manoeuvre — and deliberately in nothing else, so they say the same thing.
    /// </summary>
    [Theory]
    [InlineData("supercruise")]
    [InlineData("hyperspace")]
    public async Task ASeparationIsAcknowledgedBeforeTheFirstBoost(string finisher)
    {
        var heard = new Listening();
        var input = new RecordingGameInput();

        var massLocked = new GameStatus
        {
            Flags = StatusFlags.InMainShip | StatusFlags.FsdMassLocked,
            ReadAt = DateTimeOffset.UnixEpoch,
        };

        var clear = new GameStatus
        {
            Flags = StatusFlags.InMainShip,
            ReadAt = DateTimeOffset.UnixEpoch,
        };

        var outcome = await Separation.RunAsync(
            heard.Surface(
                input,
                massLocked,
                Binds(
                    ("SetSpeed100", "Key_W"),
                    ("UseBoostJuice", "Key_Space"),
                    ("Supercruise", "Key_J"),
                    ("Hyperspace", "Key_E"))),
            finisher,
            _ => Task.FromResult(clear),
            SeparationLimits.Default,
            TestContext.Current.CancellationToken);

        Assert.Equal(["Separating."], heard.SaidBeforeTheFirstKey);
        Assert.Equal(SeparationEnding.Away, outcome.Ending);
    }

    /// <summary>
    /// The plot macro, and it names the system — because the name is the payload and a misheard
    /// one is worth catching before the map has been driven to the wrong place.
    /// </summary>
    [Fact]
    public async Task PlottingACourseIsAcknowledgedWithTheSystemNameBeforeTheMapKey()
    {
        var heard = new Listening();
        var input = new RecordingGameInput();
        var clipboard = new RecordingClipboard();

        var surface = new NavigationSurface
        {
            Clipboard = clipboard,
            Actions = heard.Surface(
                input,
                Flying(),
                Binds(
                    ("GalaxyMapOpen", "Key_M"),
                    ("UI_Select", "Key_Z"),
                    ("UI_Right", "Key_D"),
                    ("UI_Up", "Key_W"),
                    ("UI_Down", "Key_S"),
                    ("UI_Back", "Key_B"),
                    ("CamTranslateRight", "Key_L"),
                    ("CamTranslateLeft", "Key_J"))),
            AutoPlotEnabled = () => true,
            WatchRoute = () => new FixedPlotWatch(true),
            AwaitGalaxyMap = (_, _) => Task.FromResult<bool?>(true),
        };

        var registry = D47.Core.Capabilities.CapabilityRegistry.Build([NavigationCapability.Create(surface)]);

        await registry.InvokeAsync(
            "plot_course",
            new D47.Core.Capabilities.ToolArguments(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = "Shinrarta Dezhra" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(["Plotting the course to Shinrarta Dezhra."], heard.SaidBeforeTheFirstKey);
    }

    /// <summary>
    /// And the clipboard fallback is refusal-shaped: one line, immediately, with nothing
    /// acknowledged — because nothing is about to happen that the Commander would otherwise be
    /// left guessing at.
    /// </summary>
    [Fact]
    public async Task TheClipboardFallbackAcknowledgesNothing()
    {
        var heard = new Listening();
        var input = new RecordingGameInput();

        var surface = new NavigationSurface
        {
            Clipboard = new RecordingClipboard(),
            Actions = heard.Surface(input, Flying(), Binds(("GalaxyMapOpen", "Key_M"))),

            // Switched off, which is the commonest of the three fallbacks and the one a Commander
            // meets first.
            AutoPlotEnabled = () => false,
            WatchRoute = () => new FixedPlotWatch(true),
            AwaitGalaxyMap = (_, _) => Task.FromResult<bool?>(true),
        };

        var registry = D47.Core.Capabilities.CapabilityRegistry.Build([NavigationCapability.Create(surface)]);

        await registry.InvokeAsync(
            "plot_course",
            new D47.Core.Capabilities.ToolArguments(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = "Shinrarta Dezhra" }),
            TestContext.Current.CancellationToken);

        Assert.Empty(heard.Said);
        Assert.Empty(input.Steps);
    }
}
