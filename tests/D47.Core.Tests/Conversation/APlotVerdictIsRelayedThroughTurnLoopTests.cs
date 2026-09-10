using D47.Core.Actions;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// <c>plot_course</c>'s three verdicts run through the real <see cref="NavigationCapability"/> and reach
/// the Commander as written, on the model route, with no follow-up round asked for (#112).
/// </summary>
public class APlotVerdictIsRelayedThroughTurnLoopTests
{
    private static EliteBinds MapBinds() => new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings =
        [
            new EliteBinding("GalaxyMapOpen", "Primary", "Keyboard", "Key_M"),
            new EliteBinding("UI_Up", "Primary", "Keyboard", "Key_W"),
            new EliteBinding("UI_Select", "Primary", "Keyboard", "Key_Space"),
            new EliteBinding("UI_Down", "Primary", "Keyboard", "Key_S"),
            new EliteBinding("CamTranslateRight", "Primary", "Keyboard", "Key_R"),
            new EliteBinding("CamTranslateLeft", "Primary", "Keyboard", "Key_L"),
        ],
    };

    private static GameStatus Flying => new() { Flags = StatusFlags.InMainShip, ReadAt = DateTimeOffset.UnixEpoch };

    private static CapabilityDescriptor Navigation(bool? confirm) => NavigationCapability.Create(new NavigationSurface
    {
        Clipboard = new RecordingClipboard(),
        Actions = new ActionSurface
        {
            Binds = MapBinds,
            Status = () => Flying,
            Input = new RecordingGameInput(),
            Enabled = () => true,
        },
        AutoPlotEnabled = () => true,
        WatchRoute = () => new FixedPlotWatch(confirm),
        AwaitGalaxyMap = (_, _) => Task.FromResult<bool?>(true),
    });

    private static TurnLoop Build(ILlmProvider provider, CapabilityDescriptor navigation)
    {
        var registry = CapabilityRegistry.Build([navigation]);

        return new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock())
        {
            Retry = RetryPolicy.Default with { Attempts = 1 },
        };
    }

    private static async Task<(TurnResult Result, string Spoken)> RunAsync(TurnLoop loop, string input)
    {
        var spoken = new System.Text.StringBuilder();
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
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
        return (result, spoken.ToString());
    }

    private static readonly string[] SpellingWords = ["spell", "spelt", "as typed", "matched"];

    [Fact]
    public async Task ANoRouteVerdictIsSpokenAsWrittenAndNoFollowUpRoundIsAsked()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling("call_1", "plot_course", """{"system":"Colonia"}"""),
            RoundScriptedLlmProvider.Saying("Confirm the spelling that comes up."));

        var (result, spoken) = await RunAsync(Build(provider, Navigation(confirm: false)), "plot a course to Colonia");

        Assert.Equal(TurnRoute.Model, result.Route);
        Assert.Single(provider.Requests);

        Assert.All(SpellingWords, word => Assert.DoesNotContain(word, spoken, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("did not work", spoken, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ANoRouteVerdictIsRelayedAsTheSecondToolCallOfTheTurn()
    {
        var provider = new RoundScriptedLlmProvider(
            [
                new LlmStreamEvent.TextDelta("Checking first."),
                new LlmStreamEvent.ToolUse("call_1", "copy_to_clipboard", """{"text":"Colonia"}"""),
                new LlmStreamEvent.ToolUse("call_2", "plot_course", """{"system":"Colonia"}"""),
                new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.ToolUse),
            ],
            RoundScriptedLlmProvider.Saying("Confirm the spelling that comes up."));

        var (result, spoken) = await RunAsync(Build(provider, Navigation(confirm: false)), "plot a course to Colonia");

        Assert.Equal(TurnRoute.Model, result.Route);
        Assert.Single(provider.Requests);

        Assert.All(SpellingWords, word => Assert.DoesNotContain(word, spoken, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("did not work", spoken, StringComparison.OrdinalIgnoreCase);
    }
}
