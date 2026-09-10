using D47.Core.Actions;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>
/// What the Commander hears when a command works, and what the tool result and the log keep saying
/// underneath it (#66).
/// </summary>
public class ACommandIsAcknowledgedNotNarratedTests
{
    /// <summary>The gear key, and the gesture the tool result names it by.</summary>
    private const string GearKey = "Key_L";
    private const string GearGesture = "L";

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

    private sealed record Fixture(CapabilityRegistry Registry, KeywordRouter Router);

    private static Fixture Build(EliteBinds binds, GameStatus status, bool enabled = true)
    {
        var surface = new ActionSurface
        {
            Binds = () => binds,
            Status = () => status,
            Input = new RecordingGameInput(),
            Enabled = () => enabled,
        };

        var registry = CapabilityRegistry.Build(ActionCapabilities.All(surface));
        return new Fixture(registry, new KeywordRouter(registry));
    }

    private static async Task<ToolResult> Say(Fixture fixture, string utterance)
    {
        var match = fixture.Router.MatchToolCommand(utterance);
        Assert.NotNull(match);

        return await fixture.Registry.InvokeAsync(
            match.ToolName, match.Arguments, TestContext.Current.CancellationToken);
    }

    private static int Words(string sentence) =>
        sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    [Fact]
    public async Task GearDownIsAcknowledgedInAtMostFourWordsAndNamesNoKey()
    {
        var fixture = Build(Binds(("LandingGearToggle", GearKey)), Flying());

        var result = await Say(fixture, "gear down");

        Assert.False(result.IsError);
        Assert.True(Words(result.Spoken) <= 4, result.Spoken);
        Assert.DoesNotContain(GearGesture, result.Spoken, StringComparison.Ordinal);
        Assert.DoesNotContain("Pressed", result.Spoken, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheToolResultAndTheLogStillNameTheBindingThatFired()
    {
        // Which binding actually fired is the first question on any "it did not do it" report.
        var fixture = Build(Binds(("LandingGearToggle", GearKey)), Flying());

        var result = await Say(fixture, "gear down");

        Assert.Contains($"Pressed {GearGesture}", result.Content, StringComparison.Ordinal);
        Assert.Contains("the landing gear", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnboundActionIsRefusedInTheSameWordsItIsRecordedIn()
    {
        var fixture = Build(Binds(), Flying());

        var result = await Say(fixture, "gear down");

        Assert.True(result.IsError);
        Assert.Equal(result.Content, result.Spoken);
    }

    [Fact]
    public async Task KeyboardActionsSwitchedOffIsRefusedInTheSameWordsItIsRecordedIn()
    {
        var fixture = Build(Binds(("LandingGearToggle", GearKey)), Flying(), enabled: false);

        var result = await Say(fixture, "gear down");

        Assert.True(result.IsError);
        Assert.Equal(result.Content, result.Spoken);
    }

    [Fact]
    public async Task AlreadyInThatStateIsStillASentenceRatherThanAnAcknowledgement()
    {
        // A correction, not an acknowledgement: nothing was done, and answering "Aye" would say it was.
        var fixture = Build(Binds(("LandingGearToggle", GearKey)), Flying(StatusFlags.LandingGearDown));

        var result = await Say(fixture, "gear down");

        Assert.Equal("The landing gear is already on.", result.Spoken);
    }

    [Fact]
    public async Task AcknowledgementsVaryAcrossARunOfCommands()
    {
        var fixture = Build(Binds(("LandingGearToggle", GearKey)), Flying());

        var heard = new List<string>();

        for (var command = 0; command < 12; command++)
        {
            heard.Add((await Say(fixture, "landing gear")).Spoken);
        }

        Assert.True(heard.Distinct(StringComparer.Ordinal).Count() > 1, string.Join(" ", heard));

        // Two the same in a row is the repetition a Commander actually hears.
        Assert.DoesNotContain(
            heard.Zip(heard.Skip(1)),
            pair => string.Equals(pair.First, pair.Second, StringComparison.Ordinal));
    }

    [Fact]
    public void NoAcknowledgementAnyActionCanProduceRunsLongerThanFourWords()
    {
        foreach (var action in GameActions.All)
        {
            foreach (var state in Enum.GetValues<DesiredState>())
            {
                foreach (var form in Acknowledgements.Forms(action, state))
                {
                    Assert.True(Words(form) <= 4, $"{action.Id} {state}: {form}");
                }
            }
        }
    }

    [Fact]
    public void TheGuardrailsSayTheSameThingToTheModel()
    {
        // The router performs some of these with no model turn at all, so the two paths have to agree.
        Assert.Contains("acknowledge it rather than narrating it", Guardrails.Text, StringComparison.Ordinal);
        Assert.Contains("is never said aloud", Guardrails.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AQuestionIsAnsweredRatherThanAcknowledged()
    {
        var registry = CapabilityRegistry.Build([SpecificationCapability.Create(() => null)]);

        var answer = await registry.InvokeAsync(
            "get_ship_specification",
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["ship"] = "Python" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(answer.Content, answer.Spoken);
    }

    [Fact]
    public async Task TheCommanderHearsTheAcknowledgementRatherThanTheToolResult()
    {
        var fixture = Build(Binds(("LandingGearToggle", GearKey)), Flying());

        var loop = new TurnLoop(
            fixture.Registry,
            fixture.Router,
            new LlmAvailabilityState(providerConfigured: false),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance);

        var spoken = new System.Text.StringBuilder();
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(
            "gear down", cancellationToken: TestContext.Current.CancellationToken))
        {
            switch (turnEvent)
            {
                case TurnEvent.TextDelta delta:
                    spoken.Append(delta.Text);
                    break;
                case TurnEvent.Completed completed:
                    result = completed.Result;
                    break;
            }
        }

        Assert.NotNull(result);
        Assert.Equal(TurnRoute.ActionCommand, result.Route);
        Assert.DoesNotContain("Pressed", spoken.ToString(), StringComparison.Ordinal);
        Assert.True(Words(spoken.ToString()) <= 4, spoken.ToString());
    }
}
