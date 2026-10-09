using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Persona.TheNarratorAnswersInNarrationTests;

namespace D47.Core.Tests.Persona;

/// <summary>Within a narration's window, a command d47 knows without the model, and the ship AI's name, still reach the ship.</summary>
public class ACommandAfterANarrationReachesTheShipTests
{
    private static (TurnLoop Loop, NarratorLine Line, World World, RecordingGameInput Input) Flying(FakeLlmProvider provider)
    {
        var input = new RecordingGameInput();

        var registry = CapabilityRegistry.Build(ActionCapabilities.All(new ActionSurface
        {
            Binds = () => new EliteBinds
            {
                PresetName = "Test",
                SourceFile = "Test.binds",
                Bindings = [new EliteBinding("LandingGearToggle", "Primary", "Keyboard", "Key_L")],
            },
            Status = () => new GameStatus { Flags = StatusFlags.InMainShip, ReadAt = DateTimeOffset.UnixEpoch },
            Input = input,
            Enabled = () => true,
        }));

        var (loop, line, world) = Build(registry, new KeywordRouter(registry), provider);
        world.Now += TimeSpan.FromSeconds(20);

        return (loop, line, world, input);
    }

    [Fact]
    public async Task GearDownTwentySecondsLaterLowersTheGear()
    {
        var provider = FakeLlmProvider.Answering("The gear came down.");
        var (loop, line, _, input) = Flying(provider);

        var events = await RunAsync(loop, "gear down");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(TurnRoute.ActionCommand, Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Route);
        Assert.NotEmpty(input.Steps);
        Assert.Null(provider.LastRequest);
        Assert.True(line.IsOpen);
    }

    [Fact]
    public async Task LandingGearDownReachesTheShipAsItWouldWithNoNarration()
    {
        var provider = FakeLlmProvider.Answering("The gear came down.");
        var (loop, _, _, _) = Flying(provider);

        var events = await RunAsync(loop, "landing gear down");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        var result = Assert.Single(events.OfType<TurnEvent.Completed>()).Result;
        Assert.Equal(TurnRoute.ActionCommand, result.Route);
        Assert.Null(provider.LastRequest);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheShipAiByNameGoesToTheShipAiAndClosesTheLine()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Noted.");
        var (loop, line, _) = Build(TestSurface.For(install), provider);

        var events = await RunAsync(loop, $"{ShipAi}, what was that about");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Contains(ShipAiBrief, provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
        Assert.False(line.IsOpen);
    }
}
