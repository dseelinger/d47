using D47.Core.Actions;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>Requesting docking: a blind walk through the contacts panel, judged by the journal (#150).</summary>
public class RequestDockingWalksThePanelAndReadsTheJournalTests
{
    private const uint Comms = 0x33;
    private const uint Left = 0x31;
    private const uint Next = 0x4E;
    private const uint UiRight = 0x44;
    private const uint Select = 0x5A;

    /// <summary>The eight presses in the order the Commander gave them.</summary>
    private static readonly uint[] TheWalk = [Comms, Left, Next, Next, Select, UiRight, Select, Left];

    private const string Requested =
        """{"timestamp":"2026-09-11T18:03:12Z","event":"DockingRequested","StationName":"Patterson Enterprise","StationType":"Coriolis"}""";

    private static EliteBinds PanelBinds(params string[] without)
    {
        (string Action, string Key)[] all =
        [
            ("FocusCommsPanel", "Key_3"),
            ("FocusLeftPanel", "Key_1"),
            ("CycleNextPanel", "Key_N"),
            ("UI_Right", "Key_D"),
            ("UI_Select", "Key_Z"),
        ];

        return new EliteBinds
        {
            PresetName = "Test",
            SourceFile = "Test.binds",
            Bindings =
            [
                .. all
                    .Where(entry => !without.Contains(entry.Action, StringComparer.Ordinal))
                    .Select(entry => new EliteBinding(entry.Action, "Primary", "Keyboard", entry.Key)),
            ],
        };
    }

    /// <summary>Flying in normal space with a station selected, which is the one state the walk allows.</summary>
    private static GameStatus Approaching(StatusFlags flags = StatusFlags.InMainShip, long body = 12) => new()
    {
        Flags = flags,
        Destination = new StatusDestination(7780433924826, body, "Patterson Enterprise"),
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    private static ActionSurface Surface(
        RecordingGameInput input,
        GameStatus status,
        EliteBinds? binds = null,
        Action<string>? acknowledge = null) =>
        new()
        {
            Binds = () => binds ?? PanelBinds(),
            Status = () => status,
            Input = input,
            Enabled = () => true,
            Acknowledge = acknowledge ?? (_ => { }),
        };

    /// <summary>The keys a run actually pressed, ignoring the waits.</summary>
    private static uint[] Pressed(RecordingGameInput input) =>
    [
        .. input.Steps
            .Where(step => step.Kind == InputStepKind.KeyDown)
            .Select(step => step.Code),
    ];

    /// <summary>
    /// The journal as the walk sees it: the lines Elite writes once the keys have landed, read the way
    /// the app reads them.
    /// </summary>
    private sealed class FedJournal(params string[] written) : IDockingWatch
    {
        private readonly List<JournalEvent> _log = [];

        public Task<bool?> ConfirmAsync(CancellationToken cancellationToken)
        {
            foreach (var line in written)
            {
                if (JournalEvent.TryParse(line, NullLogger.Instance, out var parsed) && parsed is { } arrived)
                {
                    _log.Add(arrived);
                }
            }

            return Task.FromResult<bool?>(_log.Any(journalEvent => journalEvent.Kind == "DockingRequested"));
        }
    }

    private static Task<DockingOutcome> Run(
        RecordingGameInput input,
        GameStatus status,
        IDockingWatch? watch = null,
        EliteBinds? binds = null,
        Func<bool?>? dockingComputer = null,
        Action<string>? acknowledge = null) =>
        Docking.RunAsync(
            Surface(input, status, binds, acknowledge),
            () => watch ?? new FedJournal(Requested),
            dockingComputer,
            TestContext.Current.CancellationToken);

    // ---- The walk ----------------------------------------------------------

    [Fact]
    public async Task TheWholeWalkGoesInTheOrderTheCommanderGaveIt()
    {
        var input = new RecordingGameInput();

        var outcome = await Run(input, Approaching());

        Assert.Equal(DockingEnding.Requested, outcome.Ending);
        Assert.Equal(TheWalk, Pressed(input));
    }

    /// <summary>
    /// The panel animates, so a press arriving before it has settled goes nowhere: the gaps are part of
    /// the sequence rather than an implementation detail.
    /// </summary>
    [Fact]
    public async Task EveryPressIsHeldAndFollowedByAGap()
    {
        var input = new RecordingGameInput();

        await Run(input, Approaching());

        var holds = input.Steps.Count(step => step.Kind == InputStepKind.Delay
                                              && step.Delay == TimeSpan.FromMilliseconds(120));

        var gaps = input.Steps.Count(step => step.Kind == InputStepKind.Delay
                                             && step.Delay > TimeSpan.FromMilliseconds(120));

        Assert.Equal(8, holds);
        Assert.Equal(7, gaps);
    }

    [Fact]
    public void EveryStepOfTheWalkIsARealActionThatWorksInNormalSpace()
    {
        foreach (var step in Docking.Walk)
        {
            var action = GameActions.Find(step.Action);

            Assert.NotNull(action);
            Assert.NotNull(action.For(ControlContext.NormalSpace));
        }
    }

    /// <summary>The panel closes on the last press whether or not the request went in.</summary>
    [Fact]
    public async Task TheWalkClosesThePanelEvenWhenNoRequestWentIn()
    {
        var input = new RecordingGameInput();

        var outcome = await Run(input, Approaching(), new FedJournal());

        Assert.Equal(DockingEnding.NoRequest, outcome.Ending);
        Assert.Equal(TheWalk, Pressed(input));
        Assert.Equal(Left, Pressed(input)[^1]);
    }

    /// <summary>The acknowledgement comes before the keys and the verdict after them (#158).</summary>
    [Fact]
    public async Task ItSaysItIsAskingBeforeItPressesAnything()
    {
        var said = new List<string>();

        var outcome = await Run(new RecordingGameInput(), Approaching(), acknowledge: said.Add);

        Assert.Equal(["Requesting docking."], said);
        Assert.Equal("Docking requested.", outcome.Message);
    }

    // ---- What the journal says ---------------------------------------------

    [Fact]
    public async Task ARequestInTheJournalIsWhatSaysItWorked()
    {
        var outcome = await Run(new RecordingGameInput(), Approaching(), new FedJournal(Requested));

        Assert.True(outcome.Ok);
    }

    /// <summary>
    /// The walk assumes the station is the first contact, and nothing it can see would say otherwise —
    /// so the missing event is reported as the request not going in, not as nothing being pressed.
    /// </summary>
    [Fact]
    public async Task NothingInTheJournalMeansTheRequestDidNotGoIn()
    {
        var otherTraffic =
            """{"timestamp":"2026-09-11T18:03:12Z","event":"ReceiveText","Channel":"npc","Message":"$STATION_NoFireZone_entered;"}""";

        var outcome = await Run(new RecordingGameInput(), Approaching(), new FedJournal(otherTraffic));

        Assert.Equal(DockingEnding.NoRequest, outcome.Ending);
        Assert.Contains("no docking request went in", outcome.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("pressed nothing", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AJournalThatCannotAnswerIsNotReportedAsAFailure()
    {
        var outcome = await Run(new RecordingGameInput(), Approaching(), new FixedDockingWatch(null));

        Assert.Equal(DockingEnding.Unknown, outcome.Ending);
    }

    // ---- The gates ---------------------------------------------------------

    public static TheoryData<GameStatus, string> WrongSituations => new()
    {
        { Approaching(StatusFlags.InMainShip | StatusFlags.Docked), "already docked" },
        { Approaching(StatusFlags.InMainShip | StatusFlags.Supercruise), "in supercruise" },
        { Approaching(StatusFlags.InSrv), "in the SRV" },
        { Approaching(StatusFlags.InMainShip | StatusFlags.Landed), "landed" },
        { Approaching(body: 0), "Nothing is selected to dock at" },
        { new GameStatus(), "cannot see the ship's status" },
    };

    [Theory]
    [MemberData(nameof(WrongSituations))]
    public async Task TheWrongSituationRefusesAndNamesIt(GameStatus status, string expected)
    {
        var input = new RecordingGameInput();

        var outcome = await Run(input, status);

        Assert.Equal(DockingEnding.Refused, outcome.Ending);
        Assert.Empty(Pressed(input));
        Assert.Contains(expected, outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The walk sends direction keys, which a map or a scanner mode takes for its own — and the journal
    /// would then report only that no request went in, saying nothing about where the keys went.
    /// </summary>
    [Theory]
    [InlineData(GuiFocus.GalaxyMap, "galaxy map")]
    [InlineData(GuiFocus.SystemMap, "system map")]
    [InlineData(GuiFocus.FssMode, "full spectrum scanner")]
    [InlineData(GuiFocus.SaaMode, "surface scanner")]
    [InlineData(GuiFocus.Orrery, "orrery")]
    [InlineData(GuiFocus.Codex, "codex")]
    public async Task AFullScreenViewInTheWayRefusesAndNamesIt(GuiFocus focus, string expected)
    {
        var input = new RecordingGameInput();

        var outcome = await Run(input, Approaching() with { GuiFocus = focus });

        Assert.Equal(DockingEnding.Refused, outcome.Ending);
        Assert.Empty(Pressed(input));
        Assert.Contains(expected, outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A cockpit panel already showing is not in the way: the walk starts by switching panels.</summary>
    [Theory]
    [InlineData(GuiFocus.None)]
    [InlineData(GuiFocus.InternalPanel)]
    [InlineData(GuiFocus.ExternalPanel)]
    [InlineData(GuiFocus.CommsPanel)]
    [InlineData(GuiFocus.RolePanel)]
    public async Task ACockpitPanelAlreadyShowingDoesNotStopTheWalk(GuiFocus focus)
    {
        var input = new RecordingGameInput();

        var outcome = await Run(input, Approaching() with { GuiFocus = focus });

        Assert.True(outcome.Ok);
        Assert.Equal(TheWalk, Pressed(input));
    }

    [Fact]
    public async Task OnFootRefusesAndNamesIt()
    {
        var input = new RecordingGameInput();

        var onFoot = Approaching() with { Flags2 = (uint)StatusFlags2.OnFoot };

        var outcome = await Run(input, onFoot);

        Assert.Equal(DockingEnding.Refused, outcome.Ending);
        Assert.Empty(Pressed(input));
        Assert.Contains("on foot", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>All eight or none: a walk that stops half way leaves a panel open over the cockpit.</summary>
    [Theory]
    [InlineData("FocusCommsPanel")]
    [InlineData("FocusLeftPanel")]
    [InlineData("CycleNextPanel")]
    [InlineData("UI_Right")]
    [InlineData("UI_Select")]
    public async Task OneUnboundStepStopsTheWholeWalk(string missing)
    {
        var input = new RecordingGameInput();

        var outcome = await Run(input, Approaching(), binds: PanelBinds(missing));

        Assert.Equal(DockingEnding.Refused, outcome.Ending);
        Assert.Empty(Pressed(input));
    }

    // ---- Take us in --------------------------------------------------------

    [Fact]
    public async Task TakeUsInRefusesWithNoDockingComputerAndNamesTheModule()
    {
        var input = new RecordingGameInput();

        var outcome = await Run(input, Approaching(), dockingComputer: () => false);

        Assert.Equal(DockingEnding.Refused, outcome.Ending);
        Assert.Empty(Pressed(input));
        Assert.Contains("docking computer", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A loadout that cannot answer is treated as fitted: refusing a phrase because no Loadout has been
    /// read yet would refuse it for the whole of a session that started in flight.
    /// </summary>
    [Fact]
    public async Task TakeUsInRunsWhenTheLoadoutCannotSay()
    {
        var input = new RecordingGameInput();

        var outcome = await Run(input, Approaching(), dockingComputer: () => null);

        Assert.True(outcome.Ok);
        Assert.Equal(TheWalk, Pressed(input));
    }

    [Fact]
    public async Task TheOtherPhrasesRunWithNoDockingComputerFitted()
    {
        var input = new RecordingGameInput();

        var outcome = await Run(input, Approaching());

        Assert.True(outcome.Ok);
        Assert.Equal(TheWalk, Pressed(input));
    }

    // ---- The command as the Commander reaches it ---------------------------

    private static CapabilityRegistry Commands(
        RecordingGameInput input,
        GameStatus status,
        bool keyboard = true,
        bool permitted = true,
        bool? dockingComputer = null) =>
        CapabilityRegistry.Build(ActionCapabilities.All(
            new ActionSurface
            {
                Binds = () => PanelBinds(),
                Status = () => status,
                Input = input,
                Enabled = () => keyboard,
            },
            new ShipCommandSurface
            {
                Enabled = _ => permitted,
                AwaitLeftPanel = (_, _) => Task.FromResult<bool?>(true),
                AwaitUndocked = _ => Task.FromResult<bool?>(true),
                NextStatus = _ => Task.FromResult(status),
                Now = () => DateTimeOffset.UnixEpoch,
                WatchDockingRequest = () => new FedJournal(Requested),
                DockingComputerFitted = () => dockingComputer,
            }));

    [Theory]
    [InlineData("request docking", ShipCommands.RequestDocking)]
    [InlineData("request permission to dock", ShipCommands.RequestDocking)]
    [InlineData("permission to dock", ShipCommands.RequestDocking)]
    [InlineData("ask for docking", ShipCommands.RequestDocking)]
    [InlineData("take us in", ShipCommands.TakeUsIn)]
    public void EveryPhraseHasAModelFreeRouteIn(string utterance, string command)
    {
        var registry = Commands(new RecordingGameInput(), Approaching());
        var match = new KeywordRouter(registry).MatchToolCommand(utterance);

        Assert.NotNull(match);
        Assert.Equal("ship_command", match.ToolName);
        Assert.True(match.Arguments.TryGetString("command", out var named));
        Assert.Equal(command, named);
    }

    [Theory]
    [InlineData(ShipCommands.RequestDocking)]
    [InlineData(ShipCommands.TakeUsIn)]
    public async Task ASwitchedOnCommandWalksTheContactsPanel(string command)
    {
        var input = new RecordingGameInput();
        var registry = Commands(input, Approaching());

        var result = await registry.InvokeAsync(
            "ship_command",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["command"] = command }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(TheWalk, Pressed(input));
    }

    /// <summary>Its own row, refused in the same words the other three use.</summary>
    [Theory]
    [InlineData(false, true, "switched off")]
    [InlineData(true, false, "its own row")]
    public async Task ACommandThatIsSwitchedOffPressesNothingAndSaysWhichSwitch(
        bool keyboard, bool permitted, string expected)
    {
        var input = new RecordingGameInput();
        var registry = Commands(input, Approaching(), keyboard, permitted);

        var result = await registry.InvokeAsync(
            "ship_command",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["command"] = ShipCommands.RequestDocking,
            }),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Empty(Pressed(input));
        Assert.Contains(expected, result.Content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Both phrases read one switch, because both are the one walk.</summary>
    [Theory]
    [InlineData(ShipCommands.RequestDocking)]
    [InlineData(ShipCommands.TakeUsIn)]
    public void BothPhrasesReadTheSameSettingsRow(string command)
    {
        var settings = new D47Settings();

        Assert.True(ShipCommands.IsEnabled(settings, command));

        var off = settings with { Actions = settings.Actions with { RequestDocking = false } };

        Assert.False(ShipCommands.IsEnabled(off, command));
    }

    /// <summary>The docking computer gate belongs to "take us in" and to nothing else.</summary>
    [Fact]
    public async Task OnlyTakeUsInAsksForADockingComputer()
    {
        var input = new RecordingGameInput();
        var registry = Commands(input, Approaching(), dockingComputer: false);

        var asked = await registry.InvokeAsync(
            "ship_command",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["command"] = ShipCommands.RequestDocking,
            }),
            TestContext.Current.CancellationToken);

        Assert.False(asked.IsError);
        Assert.Equal(TheWalk, Pressed(input));

        input.Clear();

        var refused = await registry.InvokeAsync(
            "ship_command",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["command"] = ShipCommands.TakeUsIn,
            }),
            TestContext.Current.CancellationToken);

        Assert.True(refused.IsError);
        Assert.Empty(Pressed(input));
    }
}
