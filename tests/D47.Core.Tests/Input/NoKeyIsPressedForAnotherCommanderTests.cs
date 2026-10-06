using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>While d47 shows a Commander other than the one Elite runs, no key reaches the game (#893).</summary>
public class NoKeyIsPressedForAnotherCommanderTests
{
    private static readonly EliteBinds GearKey = new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings = [new EliteBinding("LandingGearToggle", "Primary", "Keyboard", "Key_L")],
    };

    private static readonly GameStatus Flying = new()
    {
        Flags = StatusFlags.InMainShip,
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    private static GameStateStore AliceInGame()
    {
        var store = new GameStateStore();
        Assert.True(JournalEvent.TryParse(
            """{"timestamp":"2026-01-01T00:00:00Z","event":"LoadGame","FID":"F1","Commander":"Alice"}""",
            NullLogger.Instance,
            out var login));
        store.Apply(login!);
        return store;
    }

    private static async Task<ToolResult> GearDown(GameStateStore store, RecordingGameInput keys)
    {
        var surface = new ActionSurface
        {
            Binds = () => GearKey,
            Status = () => Flying,
            Input = new OffDutyGameInput(keys, store),
            Enabled = () => true,
        };

        var registry = CapabilityRegistry.Build(ActionCapabilities.All(surface));
        var match = new KeywordRouter(registry).MatchToolCommand("gear down");
        Assert.NotNull(match);

        return await registry.InvokeAsync(match.ToolName, match.Arguments, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AnActionForTheCommanderInTheGameIsPressed()
    {
        var keys = new RecordingGameInput();

        var result = await GearDown(AliceInGame(), keys);

        Assert.False(result.IsError);
        Assert.NotEmpty(keys.Steps);
    }

    [Fact]
    public async Task AnActionWhileAnotherCommanderIsShownIsRefusedNamingTheOneInTheGame()
    {
        var store = AliceInGame();
        store.Pick(new CommanderIdentity("F2", "Bob"));
        var keys = new RecordingGameInput();

        var result = await GearDown(store, keys);

        Assert.True(result.IsError);
        Assert.Contains("Commander Alice", result.Content, StringComparison.Ordinal);
        Assert.Empty(keys.Steps);
    }
}
