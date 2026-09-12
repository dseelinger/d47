using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// #152: asked how many jumps were left, d47 answered with how many had been made. On a plotted route
/// Elite writes the target for the next hop about seven seconds into the tunnel, so the arrival threw
/// away a target the game had already set and the route line then said nothing.
/// </summary>
public class TheJumpsLeftComeFromTheRouteFileTests
{
    /// <summary>
    /// The ordering the game actually writes, taken from <c>Journal.2026-09-07T135316.01.log</c>.
    /// </summary>
    private static readonly string[] ACrossingOnARoute =
    [
        """{"timestamp":"3311-01-01T00:04:31Z","event":"StartJump","JumpType":"Hyperspace","StarSystem":"Eta Crucis","StarClass":"K"}""",
        """{"timestamp":"3311-01-01T00:04:38Z","event":"FSDTarget","Name":"Andceeth","StarClass":"M","RemainingJumpsInRoute":4}""",
        """{"timestamp":"3311-01-01T00:04:49Z","event":"FSDJump","StarSystem":"Eta Crucis"}""",
    ];

    /// <summary>Five hops, so four are left once Eta Crucis is reached.</summary>
    private static NavRoute Plotted => new()
    {
        Hops =
        [
            new RouteHop("Eta Crucis", "K"),
            new RouteHop("Andceeth", "M"),
            new RouteHop("Gaes", "G"),
            new RouteHop("Lasata", "F"),
            new RouteHop("Bestii", "M"),
        ],
    };

    private static CommanderGameState State(params string[] lines) => Store(lines).Active!;

    private static GameStateStore Store(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    private static async Task<string> LocationAsync(GameStateStore gameState, NavRoute route)
    {
        var registry = CapabilityRegistry.Build([JournalCapability.Create(gameState, route: () => route)]);

        var result = await registry.InvokeAsync(
            "get_location", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        return result.Content;
    }

    private static readonly string[] Commander =
        ["""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""];

    private static string[] Fixture(params string[] lines) => [.. Commander, .. lines];

    [Fact]
    public async Task TheSystemJustReachedStillHasTheRestOfTheRouteAheadOfIt()
    {
        var answer = await LocationAsync(Store(Fixture(ACrossingOnARoute)), Plotted);

        Assert.Contains("Next jump: Andceeth (class M).", answer, StringComparison.Ordinal);
        Assert.Contains("4 jumps left on the route.", answer, StringComparison.Ordinal);
    }

    [Fact]
    public void TheModelIsToldTheSameThingFromTheSameFile()
    {
        var block = Situation.Describe(State(Fixture(ACrossingOnARoute)), route: Plotted);

        Assert.NotNull(block);
        Assert.Contains("Next jump: Andceeth, star class M", block, StringComparison.Ordinal);
        Assert.Contains("4 jumps left on the route", block, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OffThePlottedRouteNeitherSurfaceSpeaksAJumpCount()
    {
        // The whole route counts as ahead when the Commander is not on it, which is how a jump count read
        // without checking would come back larger than the route itself.
        var wandered = Fixture(
            """{"timestamp":"3311-01-01T00:05:00Z","event":"FSDJump","StarSystem":"Kanates"}""");

        var answer = await LocationAsync(Store(wandered), Plotted);

        Assert.Contains("not on it", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("jumps left", answer, StringComparison.Ordinal);

        var block = Situation.Describe(State(wandered), route: Plotted);

        Assert.NotNull(block);
        Assert.Contains("not on it", block, StringComparison.Ordinal);
        Assert.DoesNotContain("jumps left", block, StringComparison.Ordinal);
    }

    /// <summary>
    /// Elite rewrites <c>NavRoute.json</c> with an empty route on <c>NavRouteClear</c>, so a cleared
    /// route reaches both surfaces as nothing plotted — including after a target the Commander never flew.
    /// </summary>
    [Fact]
    public async Task WithNoRoutePlottedNeitherSurfaceMentionsARouteAtAll()
    {
        var targeted = Fixture(
            """{"timestamp":"3311-01-01T00:02:00Z","event":"FSDTarget","Name":"Andceeth","StarClass":"M","RemainingJumpsInRoute":4}""",
            """{"timestamp":"3311-01-01T00:03:00Z","event":"Location","StarSystem":"Eta Crucis","Docked":false}""");

        var answer = await LocationAsync(Store(targeted), NavRoute.None);

        Assert.DoesNotContain("route", answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Andceeth", answer, StringComparison.Ordinal);

        var block = Situation.Describe(State(targeted), route: NavRoute.None);

        Assert.NotNull(block);
        Assert.DoesNotContain("Route", block, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Andceeth", block, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheLastSystemOnTheRouteIsNotAJumpCountOfItsOwn()
    {
        var arrived = Fixture(
            """{"timestamp":"3311-01-01T00:09:00Z","event":"FSDJump","StarSystem":"Bestii"}""");

        var answer = await LocationAsync(Store(arrived), Plotted);

        Assert.Contains("last system on the plotted route", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("Next jump", answer, StringComparison.Ordinal);
    }
}
