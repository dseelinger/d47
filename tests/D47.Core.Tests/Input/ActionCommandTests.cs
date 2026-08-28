using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>
/// Acting on the game end to end, with nothing sent: the declared phrase, the mode check, the
/// state check, and the refusals (Phase 10, items 1, 4 and 6 to 9).
/// </summary>
public class ActionCommandTests
{
    private static EliteBinds Binds(params (string Action, string Key)[] entries) => new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings = [.. entries.Select(e => new EliteBinding(e.Action, "Primary", "Keyboard", e.Key))],
    };

    private static GameStatus Flying(StatusFlags extra = StatusFlags.None) => new()
    {
        Flags = StatusFlags.InMainShip | extra,
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    private sealed record Fixture(CapabilityRegistry Registry, RecordingGameInput Input, KeywordRouter Router);

    private static Fixture Build(EliteBinds binds, GameStatus status, bool enabled = true)
    {
        var input = new RecordingGameInput();

        var surface = new ActionSurface
        {
            Binds = () => binds,
            Status = () => status,
            Input = input,
            Enabled = () => enabled,
        };

        var registry = CapabilityRegistry.Build(ActionCapabilities.All(surface));
        return new Fixture(registry, input, new KeywordRouter(registry));
    }

    private static async Task<ToolResult> Say(Fixture fixture, string utterance)
    {
        var match = fixture.Router.MatchToolCommand(utterance);
        Assert.NotNull(match);

        return await fixture.Registry.InvokeAsync(
            match.ToolName, match.Arguments, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ADeclaredPhrasePressesTheCommandersOwnKeyWithNoModelInThePath()
    {
        var fixture = Build(Binds(("LandingGearToggle", "Key_L")), Flying());

        var result = await Say(fixture, "gear down");

        Assert.False(result.IsError);
        Assert.Equal(
            [
                new InputStep(InputStepKind.KeyDown, 0x4C),
                new InputStep(InputStepKind.KeyUp, 0x4C),
            ],
            fixture.Input.Steps.Where(step => step.Kind != InputStepKind.Delay));
    }

    [Fact]
    public async Task GearDownWhenTheGearIsAlreadyDownPressesNothing()
    {
        // Elite binds one toggle, so pressing it here would raise the gear — the opposite of
        // what was asked, at the moment it matters most.
        var fixture = Build(
            Binds(("LandingGearToggle", "Key_L")),
            Flying(StatusFlags.LandingGearDown));

        var result = await Say(fixture, "gear down");

        Assert.False(result.IsError);
        Assert.Contains("already", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Input.Steps);
    }

    [Fact]
    public async Task GearUpWhenTheGearIsDownDoesPressIt()
    {
        var fixture = Build(
            Binds(("LandingGearToggle", "Key_L")),
            Flying(StatusFlags.LandingGearDown));

        await Say(fixture, "gear up");

        Assert.NotEmpty(fixture.Input.Steps);
    }

    [Fact]
    public async Task AToggleWithNoStateAskedForAlwaysPresses()
    {
        var fixture = Build(
            Binds(("LandingGearToggle", "Key_L")),
            Flying(StatusFlags.LandingGearDown));

        await Say(fixture, "landing gear");

        Assert.NotEmpty(fixture.Input.Steps);
    }

    [Fact]
    public async Task FlightAssistReadsBackwardsBecauseEliteReportsTheNegative()
    {
        // Elite reports FlightAssistOff, so the flag being set means the feature is off and
        // "flight assist off" is already satisfied.
        var fixture = Build(
            Binds(("ToggleFlightAssist", "Key_Z")),
            Flying(StatusFlags.FlightAssistOff));

        var result = await Say(fixture, "flight assist off");

        Assert.Contains("already", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Input.Steps);
    }

    [Fact]
    public async Task TheWrongModeRefusesWithTheModeAsTheReasonAndPressesNothing()
    {
        var fixture = Build(
            Binds(("LandingGearToggle", "Key_L")),
            Flying(StatusFlags.Supercruise));

        var result = await Say(fixture, "gear down");

        Assert.True(result.IsError);
        Assert.Contains("supercruise", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Input.Steps);
    }

    [Fact]
    public async Task NothingIsPressedWhileTheSettingIsOff()
    {
        var fixture = Build(Binds(("LandingGearToggle", "Key_L")), Flying(), enabled: false);

        var result = await Say(fixture, "gear down");

        Assert.True(result.IsError);
        Assert.Empty(fixture.Input.Steps);
    }

    [Fact]
    public void AQuestionAboutAnActionIsNotAnInstructionToPerformIt()
    {
        // Whole-utterance matching. "Can you put the gear down while docked" is a question, and
        // treating it as a command flies the ship.
        var fixture = Build(Binds(("LandingGearToggle", "Key_L")), Flying());

        Assert.Null(fixture.Router.MatchToolCommand("can you put the gear down while docked"));
        Assert.NotNull(fixture.Router.MatchToolCommand("put the gear down"));
    }

    [Fact]
    public void StopIsNotAnActionPhraseBecauseItHasToStayTheInterrupt()
    {
        var fixture = Build(Binds(("SetSpeedZero", "Key_X")), Flying());

        Assert.Null(fixture.Router.MatchToolCommand("stop"));
        Assert.NotNull(fixture.Router.MatchToolCommand("all stop"));
    }

    [Fact]
    public void FiringWeaponsIsReachableFromNoPhraseAndNoTool()
    {
        // The catalogue carries them for the honk. Nothing the Commander or the model says
        // should get to them.
        var fixture = Build(Binds(("PrimaryFire", "Key_Space")), Flying());

        Assert.Empty(GameActions.All.Where(a => a.Group == GameActions.Weapons).SelectMany(a => a.Phrases));

        Assert.DoesNotContain(
            fixture.Registry.All.SelectMany(c => c.Descriptor.Tools).SelectMany(t => t.Parameters),
            parameter => parameter.AllowedValues.Contains("primary_fire"));
    }

    [Fact]
    public void EveryDeclaredPhraseRoutesToAToolThatAcceptsItsArguments()
    {
        // A phrase naming an action that is not in that tool's vocabulary would fail validation
        // at the moment the Commander says it, which is the worst time to find out.
        var fixture = Build(Binds(), Flying());

        foreach (var capability in fixture.Registry.All)
        {
            foreach (var tool in capability.Descriptor.Tools)
            {
                foreach (var command in tool.Commands)
                {
                    var match = fixture.Router.MatchToolCommand(command.Phrase);

                    Assert.NotNull(match);
                    Assert.Equal(tool.Name, match.ToolName);

                    foreach (var (name, value) in command.Arguments)
                    {
                        var parameter = tool.Parameters.Single(p => p.Name == name);
                        Assert.Contains(value, parameter.AllowedValues);
                    }
                }
            }
        }
    }

    [Fact]
    public void NoTwoPhrasesMeanTwoDifferentThings()
    {
        var fixture = Build(Binds(), Flying());

        var phrases = fixture.Registry.All
            .SelectMany(c => c.Descriptor.Tools)
            .SelectMany(t => t.Commands)
            .Select(command => command.Phrase)
            .ToArray();

        Assert.Equal(phrases.Length, phrases.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void TheLiveBlockNamesOnlyWhatIsReachableRightNow()
    {
        var surface = new ActionSurface
        {
            Binds = () => Binds(("LandingGearToggle", "Key_L"), ("DeployHardpointToggle", "Joy_3")),
            Status = () => Flying(),
            Input = new RecordingGameInput(),
            Enabled = () => true,
        };

        var described = ActionCapabilities.Describe(surface);

        Assert.NotNull(described);
        Assert.Contains("landing_gear", described);
        Assert.DoesNotContain("hardpoints", described);
    }

    [Fact]
    public void TheLiveBlockSaysNothingAtAllWhenTheSettingIsOff()
    {
        // Not billed for a list of things that cannot happen, on every turn.
        var surface = new ActionSurface
        {
            Binds = () => Binds(("LandingGearToggle", "Key_L")),
            Status = () => Flying(),
            Input = new RecordingGameInput(),
            Enabled = () => false,
        };

        Assert.Null(ActionCapabilities.Describe(surface));
    }

    [Fact]
    public void TheSchemaDoesNotMoveWhenTheModeDoes()
    {
        // The whole reason availability lives in the game-state block: a schema that changed
        // with the mode would invalidate the entire cached prefix several times a minute.
        var docked = CapabilityRegistry.Build(ActionCapabilities.All(ActionSurface.Inert with
        {
            Status = () => new GameStatus { Flags = StatusFlags.Docked, ReadAt = DateTimeOffset.UnixEpoch },
        }));

        var supercruise = CapabilityRegistry.Build(ActionCapabilities.All(ActionSurface.Inert with
        {
            Status = () => new GameStatus { Flags = StatusFlags.Supercruise, ReadAt = DateTimeOffset.UnixEpoch },
        }));

        foreach (var capability in docked.All)
        {
            Assert.Equal(
                capability.ToolSchemas,
                supercruise.Find(capability.Descriptor.Id)!.ToolSchemas);
        }
    }
}
