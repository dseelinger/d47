using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// An absence and an answer that has not arrived are two different things, and until the walk over older
/// journals has read one the fleet questions can only report the second (#148).
/// </summary>
public class AFleetAnswerSaysWhenItIsStillReadingTests
{
    [Fact]
    public async Task TheCarrierQuestionSaysTheHistoryIsStillBeingRead()
    {
        var result = await Registry(HistoryState.Running)
            .InvokeAsync("get_fleet", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Contains("not finished reading", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// A walk that threw read no older journal either, so the answer is still that d47 does not know
    /// rather than that there is nothing to know.
    /// </summary>
    [Fact]
    public async Task TheCarrierQuestionSaysSoWhenTheWalkDidNotFinish()
    {
        var result = await Registry(HistoryState.Failed)
            .InvokeAsync("get_fleet", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Contains("did not finish", result.Content, StringComparison.Ordinal);

        Assert.DoesNotContain(
            "No fleet carrier appears in any journal read", result.Content, StringComparison.Ordinal);
    }

    /// <summary>And the same for a walk stopped part-way, which read no more than a failed one did.</summary>
    [Fact]
    public async Task TheLoadoutsSaySoWhenTheWalkWasStopped()
    {
        var result = await Registry(HistoryState.Stopped)
            .InvokeAsync("get_fleet_loadouts", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Contains("did not finish", result.Content, StringComparison.Ordinal);
    }

    /// <summary>And once it has been read, the absence is a real one and says so.</summary>
    [Fact]
    public async Task TheCarrierQuestionReportsTheAbsenceOnceTheWalkIsDone()
    {
        var result = await Registry(HistoryState.Done)
            .InvokeAsync("get_fleet", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Contains("No fleet carrier appears in any journal read", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("not finished reading", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheShipListSaysTheHistoryIsStillBeingRead()
    {
        var result = await Registry(HistoryState.Running).InvokeAsync(
            "get_fleet",
            ToolArguments.FromJson("""{"ships":true}"""),
            TestContext.Current.CancellationToken);

        Assert.Contains("do not have your ship list", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheLoadoutsSayTheHistoryIsStillBeingRead()
    {
        var result = await Registry(HistoryState.Running)
            .InvokeAsync("get_fleet_loadouts", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Contains("not finished reading", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// With no Commander identified the answer used to be that no journal had been detected, which is the
    /// same claim of absence as the others and reached before any of them.
    /// </summary>
    [Fact]
    public async Task TheCarrierQuestionSaysSoBeforeAnyCommanderHasBeenIdentified()
    {
        var registry = CapabilityRegistry.Build(
            [JournalCapability.Create(new GameStateStore(), () => HistoryState.Running)]);

        var result = await registry.InvokeAsync(
            "get_fleet", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Contains("not finished reading", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("No Elite Dangerous journal", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheLoadoutsSaySoBeforeAnyCommanderHasBeenIdentified()
    {
        var registry = CapabilityRegistry.Build(
            [JournalCapability.Create(new GameStateStore(), () => HistoryState.Running)]);

        var result = await registry.InvokeAsync(
            "get_fleet_loadouts", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Contains("not finished reading", result.Content, StringComparison.Ordinal);
    }

    /// <summary>And once the walk is done, the no-journal answer is the true one again.</summary>
    [Fact]
    public async Task TheNoJournalAnswerSurvivesOnceTheWalkIsDone()
    {
        var registry = CapabilityRegistry.Build(
            [JournalCapability.Create(new GameStateStore(), () => HistoryState.Done)]);

        var result = await registry.InvokeAsync(
            "get_fleet", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Contains("No Elite Dangerous journal", result.Content, StringComparison.Ordinal);
    }

    /// <summary>The status report names the walk, so a slow start can be diagnosed by asking.</summary>
    [Fact]
    public async Task TheStatusReportNamesTheWalkAndItsState()
    {
        using var install = new TempInstall();

        var history = new HistoryBackfill
        {
            Directory = install.Root,
            Loggers = NullLoggerFactory.Instance,
        };

        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance,
            loadFailed: false);

        var registry = CapabilityRegistry.Build(
        [
            DiagnosticsCapability.Create(
                install.Paths,
                new FakeVerbosityControl(),
                settings,
                TestSurface.Version,
                coverage: null,
                history),
        ]);

        var result = await registry.InvokeAsync(
            "get_app_status", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Contains("Journal history: Pending", result.Content, StringComparison.Ordinal);
    }

    private static CapabilityRegistry Registry(HistoryState state)
    {
        var gameState = new GameStateStore();

        Assert.True(JournalEvent.TryParse(
            """{"timestamp":"2026-09-05T10:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            NullLogger.Instance,
            out var identified));

        gameState.Apply(identified!);

        return CapabilityRegistry.Build([JournalCapability.Create(gameState, () => state)]);
    }
}
