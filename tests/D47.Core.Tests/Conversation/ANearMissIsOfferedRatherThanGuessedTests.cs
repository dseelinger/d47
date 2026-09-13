using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>An utterance that misses a model-free phrase by a word is put to the Commander, not to the model.</summary>
public class ANearMissIsOfferedRatherThanGuessedTests
{
    private static TurnLoop Build(
        CapabilityRegistry registry,
        ILlmProvider? provider = null,
        SettingsService? settings = null)
    {
        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(provider is not null),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            settings: settings,
            clock: new InstantClock());

        loop.Retry = RetryPolicy.Default with { Attempts = 1 };
        return loop;
    }

    private static async Task<TurnResult> RunAsync(TurnLoop loop, string input)
    {
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.Completed completed)
            {
                result = completed.Result;
            }
        }

        Assert.NotNull(result);
        return result;
    }

    private sealed record Flight(CapabilityRegistry Registry, RecordingGameInput Input);

    private static Flight Ship(GameStatus status)
    {
        var input = new RecordingGameInput();

        var surface = new ActionSurface
        {
            Binds = () => new EliteBinds
            {
                PresetName = "Test",
                SourceFile = "Test.binds",
                Bindings = [new EliteBinding("LandingGearToggle", "Primary", "Keyboard", "Key_L")],
            },
            Status = () => status,
            Input = input,
            Enabled = () => true,
        };

        return new Flight(CapabilityRegistry.Build(ActionCapabilities.All(surface)), input);
    }

    private static GameStatus Flying() => new() { Flags = StatusFlags.InMainShip, ReadAt = DateTimeOffset.UnixEpoch };

    private static GameStatus OnFoot() => new() { Flags2 = (uint)StatusFlags2.OnFoot, ReadAt = DateTimeOffset.UnixEpoch };

    [Fact]
    public async Task SetFocusOnEliteIsOfferedAndYesRunsIt()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("From the model.");
        var loop = Build(TestSurface.For(install).Registry, provider);

        var offered = await RunAsync(loop, "set focus on elite");

        Assert.Equal(TurnRoute.Offer, offered.Route);
        Assert.Equal("Did you mean 'set focus to elite'?", offered.Text);

        var ran = await RunAsync(loop, "yes");

        Assert.Equal(TurnRoute.KeywordRouter, ran.Route);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task AnUnguardedSettingSaidWithToForOnRunsWithNoOffer()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var provider = FakeLlmProvider.Answering("From the model.");
        var loop = Build(surface.Registry, provider, surface.Settings);

        var result = await RunAsync(loop, "humor to");

        Assert.Equal(TurnRoute.SettingCommand, result.Route);
        Assert.Equal(TurnOutcome.Answered, result.Outcome);
        Assert.False(loop.Offers.IsStanding);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task PutTheGearDawnIsOfferedAndAnUnrelatedSentencePressesNothing()
    {
        var ship = Ship(Flying());
        var provider = FakeLlmProvider.Answering("From the model.");
        var loop = Build(ship.Registry, provider);

        var offered = await RunAsync(loop, "put the gear dawn");

        Assert.Equal(TurnRoute.Offer, offered.Route);
        Assert.Contains("'put the gear down'", offered.Text, StringComparison.Ordinal);
        Assert.Empty(ship.Input.Steps);

        var next = await RunAsync(loop, "how far is it to Colonia");

        Assert.Equal(TurnRoute.Model, next.Route);
        Assert.Empty(ship.Input.Steps);
    }

    [Fact]
    public async Task NearTwoPhrasesOffersBothAndTheFirstRunsTheFirst()
    {
        var ran = new List<string>();

        ToolDefinition Tool(string name, string phrase) => new()
        {
            Name = name,
            Description = "Records that it ran.",
            Commands = [new ToolCommandPhrase(phrase, new Dictionary<string, string>())],
            Handler = (_, _) =>
            {
                ran.Add(name);
                return Task.FromResult(ToolResult.Ok($"Ran {name}."));
            },
        };

        var registry = CapabilityRegistry.Build(
        [
            new CapabilityDescriptor
            {
                Id = "spy",
                Group = "Test",
                Name = "Spy",
                Summary = "Records what ran.",
                Tools = [Tool("first_thing", "run the first thing"), Tool("second_thing", "run the second thing")],
            },
        ]);

        var loop = Build(registry);

        var offered = await RunAsync(loop, "run the thing");

        Assert.Equal("Did you mean 'run the first thing', or 'run the second thing'?", offered.Text);
        Assert.Empty(ran);

        await RunAsync(loop, "the first");

        Assert.Equal(["first_thing"], ran);
    }

    [Fact]
    public async Task AnOfferedFlightActionPickedOnFootIsRefusedWithTheMode()
    {
        var ship = Ship(OnFoot());
        var loop = Build(ship.Registry);

        await RunAsync(loop, "put the gear dawn");
        var picked = await RunAsync(loop, "yes");

        Assert.Equal(TurnRoute.ActionCommand, picked.Route);
        Assert.Contains("on foot", picked.Text, StringComparison.Ordinal);
        Assert.Empty(ship.Input.Steps);
    }

    [Fact]
    public async Task AQuestionAboutEliteRankReachesTheModel()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("From the model.");
        var loop = Build(TestSurface.For(install).Registry, provider);

        var result = await RunAsync(loop, "what is my elite rank in combat");

        Assert.Equal(TurnRoute.Model, result.Route);
        Assert.Equal(1, provider.CallCount);
    }
}
