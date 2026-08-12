using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class JournalCapabilityTests
{
    private static void Apply(GameStateStore gameState, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        gameState.Apply(parsed!);
    }

    [Fact]
    public async Task WithNoJournalDetectedTheAnswerSaysSoRatherThanGuessing()
    {
        var registry = CapabilityRegistry.Build([JournalCapability.Create(new GameStateStore())]);

        var result = await registry.InvokeAsync("get_location", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("No Elite Dangerous journal", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsTheActiveCommandersRealLocation()
    {
        var gameState = new GameStateStore();
        Apply(gameState, """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""");
        Apply(
            gameState,
            """{"timestamp":"2026-01-01T00:00:01Z","event":"FSDJump","StarSystem":"Fixture Nebula Point","Body":"Fixture Nebula Point A"}""");

        var registry = CapabilityRegistry.Build([JournalCapability.Create(gameState)]);
        var result = await registry.InvokeAsync("get_location", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("Fixture is in Fixture Nebula Point, near Fixture Nebula Point A.", result.Content);
    }

    [Fact]
    public async Task ReportsDockedState()
    {
        var gameState = new GameStateStore();
        Apply(gameState, """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""");
        Apply(
            gameState,
            """{"timestamp":"2026-01-01T00:00:01Z","event":"Docked","StationName":"Fixture Outpost","StarSystem":"Fixture Nebula Point"}""");

        var registry = CapabilityRegistry.Build([JournalCapability.Create(gameState)]);
        var result = await registry.InvokeAsync("get_location", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Equal("Fixture is in Fixture Nebula Point, docked at Fixture Outpost.", result.Content);
    }
}
