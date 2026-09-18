using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// <c>get_engineer_unlock_requirements</c> answers a question across every engineer at once,
/// from <see cref="D47.Core.Engineers.EngineerAccess.CriteriaFor"/> for all 39 of them (#272).
/// </summary>
public class WhoWantsWhatAcrossEngineersTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CapabilityRegistry BuildRegistry(GameStateStore gameState) =>
        CapabilityRegistry.Build([EngineerCapability.Create(() => gameState.Active)]);

    [Fact]
    public async Task BothEngineersWantingSensorFragmentsAppearWithTheQuantity()
    {
        var gameState = new GameStateStore();
        var registry = BuildRegistry(gameState);

        var result = await registry.InvokeAsync(
            "get_engineer_unlock_requirements", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("Chloe Sedesi", result.Content, StringComparison.Ordinal);
        Assert.Contains("Professor Palin", result.Content, StringComparison.Ordinal);
        Assert.Contains("25", result.Content, StringComparison.Ordinal);

        var palin = EngineerDirectory.ByName("Professor Palin");
        Assert.NotNull(palin);
        Assert.IsType<UnlockTest.Contribution>(palin.UnlockTest);
        Assert.Equal(25, ((UnlockTest.Contribution)palin.UnlockTest!).Quantity);
    }

    [Fact]
    public async Task EveryEngineerInTheDirectoryAppearsInOneCall()
    {
        var gameState = new GameStateStore();
        var registry = BuildRegistry(gameState);

        var result = await registry.InvokeAsync(
            "get_engineer_unlock_requirements", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        foreach (var engineer in EngineerDirectory.All)
        {
            Assert.Contains(engineer.Name, result.Content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AnEngineerWithNoParsedUnlockTestStillCarriesItsProse()
    {
        var gameState = new GameStateStore();
        var registry = BuildRegistry(gameState);

        var domino = EngineerDirectory.ByName("Domino Green");
        Assert.NotNull(domino);
        Assert.Null(domino.UnlockTest);
        Assert.NotNull(domino.Unlock);

        var result = await registry.InvokeAsync(
            "get_engineer_unlock_requirements", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Contains(domino.Unlock!, result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoJournalEveryCriterionReadsUndeterminedRatherThanRefusing()
    {
        var gameState = new GameStateStore();
        var registry = BuildRegistry(gameState);

        var result = await registry.InvokeAsync(
            "get_engineer_unlock_requirements", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("undetermined", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("not in the journal", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheWholeReportStaysUnderEightThousandCharacters()
    {
        var gameState = new GameStateStore();
        var registry = BuildRegistry(gameState);

        var result = await registry.InvokeAsync(
            "get_engineer_unlock_requirements", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.True(result.Content.Length < 8000, $"Report is {result.Content.Length} characters.");
    }

    [Fact]
    public void ItIsNotProtected()
    {
        var registry = CapabilityRegistry.Build([EngineerCapability.Create(() => null)]);

        Assert.False(registry.Find(EngineerCapability.Id)!.Descriptor.Tools
            .First(tool => tool.Name == "get_engineer_unlock_requirements").Protected);
    }
}
