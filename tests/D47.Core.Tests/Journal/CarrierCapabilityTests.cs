using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class CarrierCapabilityTests
{
    private const string CarrierStats =
        """
        {"timestamp":"2026-09-05T00:00:00Z","event":"CarrierStats","CarrierID":3715429376,
         "CarrierType":"FleetCarrier","Callsign":"BNH-T2F","Name":"Sacred Fire","DockingAccess":"all",
         "AllowNotorious":false,"FuelLevel":792,"JumpRangeCurr":500.0,"PendingDecommission":false,
         "SpaceUsage":{"TotalCapacity":25000,"Cargo":540,"FreeSpace":23530},
         "Finance":{"CarrierBalance":750352669},
         "Crew":[{"CrewRole":"BlackMarket","Activated":false},
                 {"CrewRole":"Refuel","Activated":true,"Enabled":true,"CrewName":"Rosa Guthrie"},
                 {"CrewRole":"Repair","Activated":true,"Enabled":false,"CrewName":"Ev Chang"}]}
        """;

    private static void Apply(GameStateStore gameState, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        gameState.Apply(parsed!);
    }

    /// <summary><c>describe_carrier</c> names the fuel, the balance, the cargo and every service.</summary>
    [Fact]
    public async Task NamesTheFiguresAndEveryServiceWithItsCrew()
    {
        var gameState = new GameStateStore();
        Apply(gameState, """{"timestamp":"2026-09-05T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""");
        Apply(gameState, CarrierStats);

        var registry = CapabilityRegistry.Build([CarrierCapability.Create(() => gameState.Active)]);
        var result = await registry.InvokeAsync(
            "describe_carrier", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("792", result.Content, StringComparison.Ordinal);
        Assert.Contains("750,352,669", result.Content, StringComparison.Ordinal);
        Assert.Contains("540/25000", result.Content, StringComparison.Ordinal);
        Assert.Contains("Rosa Guthrie", result.Content, StringComparison.Ordinal);
        Assert.Contains("Ev Chang", result.Content, StringComparison.Ordinal);
        Assert.Contains("BlackMarket", result.Content, StringComparison.Ordinal);
    }

    /// <summary>An unseen carrier is reported as unseen, never as owning none.</summary>
    [Fact]
    public async Task AnUnseenCarrierIsNotAnErrorAndDoesNotClaimThereIsNone()
    {
        var registry = CapabilityRegistry.Build([CarrierCapability.Create(() => null)]);
        var result = await registry.InvokeAsync(
            "describe_carrier", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.DoesNotContain("you have no carrier", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>"carrier report" reaches the tool through the keyword router with no model.</summary>
    [Fact]
    public void CarrierReportReachesTheToolThroughTheKeywordRouter()
    {
        using var install = new TempInstall();

        var match = new KeywordRouter(TestSurface.For(install).Registry).Match("carrier report");

        Assert.NotNull(match);
        Assert.Equal("describe_carrier", match!.ToolName);
    }
}
