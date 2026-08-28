using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// Reading a system name back to the Commander (Phase 18, "Read a system name") — including
/// the two refusals that are the point of the item: no payout claim, and no star class for a system
/// d47 never watched them arrive at.
/// </summary>
public class SystemNameCapabilityTests
{
    private static GameStateStore Store(params string[] lines)
    {
        var gameState = new GameStateStore();

        gameState.Apply(Parse(
            """{"timestamp":"2026-08-16T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        foreach (var line in lines)
        {
            gameState.Apply(Parse(line));
        }

        return gameState;
    }

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static Task<ToolResult> Ask(GameStateStore gameState, string json = "{}") =>
        CapabilityRegistry
            .Build([SystemNameCapability.Create(() => gameState.Active)])
            .InvokeAsync("read_system_name", ToolArguments.FromJson(json), TestContext.Current.CancellationToken);

    /// <summary>
    /// The case the whole item exists for: a name the Commander has read off the Galaxy Map and has
    /// never been to, decoded with no journal, no index and no network.
    /// </summary>
    [Fact]
    public async Task ANameTheCommanderHasNeverVisitedIsStillReadable()
    {
        var result = await Ask(Store(), """{"name":"Dryafea PO-X d2-0"}""");

        Assert.False(result.IsError);
        Assert.Contains("Sector: Dryafea", result.Content, StringComparison.Ordinal);
        Assert.Contains("PO-X d2", result.Content, StringComparison.Ordinal);
        Assert.Contains("Mass code d: fourth of eight", result.Content, StringComparison.Ordinal);
        Assert.Contains("80 light years across", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// The refusal. This is the sentence a Commander who has heard the folklore needs, and it is why
    /// the item ships as a decoder rather than as a strategy.
    /// </summary>
    [Fact]
    public async Task ItDeclinesToSayTheMassCodePredictsPayout()
    {
        var result = await Ask(Store(), """{"name":"Dryafea PO-X d2-0"}""");

        Assert.Contains("not established to predict", result.Content, StringComparison.Ordinal);
    }

    /// <summary>Which rungs are measured and which follow the doubling is said in the answer.</summary>
    [Fact]
    public async Task AnInferredBoxSizeSaysThatItIsInferred()
    {
        var measured = await Ask(Store(), """{"name":"Fixture AA-A c1-2"}""");
        Assert.Contains("measured against real coordinates", measured.Content, StringComparison.Ordinal);

        var inferred = await Ask(Store(), """{"name":"Fixture AA-A g1-2"}""");
        Assert.Contains("not measured here", inferred.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoNameItReadsTheSystemTheCommanderIsIn()
    {
        var result = await Ask(Store(
            """{"timestamp":"2026-08-16T09:10:00Z","event":"FSDTarget","Name":"Dryafea PO-X d2-0","StarClass":"M"}""",
            """{"timestamp":"2026-08-16T09:11:00Z","event":"FSDJump","StarSystem":"Dryafea PO-X d2-0"}"""));

        Assert.Contains("Sector: Dryafea", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// The star class comes from the <c>FSDTarget</c> that preceded the jump — measured at 99.7% of
    /// arrivals against 28.6% for the arrival auto-scan, which is the source that looks obvious and
    /// usually is not there.
    /// </summary>
    [Fact]
    public async Task TheStarClassComesFromTheRouteRatherThanFromAScan()
    {
        var result = await Ask(Store(
            """{"timestamp":"2026-08-16T09:10:00Z","event":"FSDTarget","Name":"Dryafea PO-X d2-0","StarClass":"M"}""",
            """{"timestamp":"2026-08-16T09:11:00Z","event":"FSDJump","StarSystem":"Dryafea PO-X d2-0"}"""));

        Assert.Contains("Main star:", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// A jump that ended somewhere other than the plotted target must carry no class rather than the
    /// target's — the one way this could report a star the Commander is not looking at.
    /// </summary>
    [Fact]
    public async Task ArrivingSomewhereOtherThanTheTargetCarriesNoStarClass()
    {
        var result = await Ask(Store(
            """{"timestamp":"2026-08-16T09:10:00Z","event":"FSDTarget","Name":"Dryafea PO-X d2-0","StarClass":"M"}""",
            """{"timestamp":"2026-08-16T09:11:00Z","event":"FSDJump","StarSystem":"Fixture AA-A b1-2"}"""));

        Assert.DoesNotContain("Main star:", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// A name merely read aloud carries no star, even when d47 knows the class of the system the
    /// Commander is actually sitting in.
    /// </summary>
    [Fact]
    public async Task ANameReadAloudBorrowsNoStarFromWhereTheCommanderIsStanding()
    {
        var gameState = Store(
            """{"timestamp":"2026-08-16T09:10:00Z","event":"FSDTarget","Name":"Dryafea PO-X d2-0","StarClass":"M"}""",
            """{"timestamp":"2026-08-16T09:11:00Z","event":"FSDJump","StarSystem":"Dryafea PO-X d2-0"}""");

        var elsewhere = await Ask(gameState, """{"name":"Fixture AA-A b1-2"}""");
        Assert.DoesNotContain("Main star:", elsewhere.Content, StringComparison.Ordinal);

        // And it is still there for the system they are in, so the guard is about the name rather
        // than about having lost the class.
        var here = await Ask(gameState);
        Assert.Contains("Main star:", here.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AHandNamedSystemIsToldPlainlyThatThereIsNoCodeInIt()
    {
        var result = await Ask(Store(), """{"name":"Colonia"}""");

        Assert.Contains("hand-named system", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Mass code", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// No journal and no name is not an error — it is a prompt, because this is the one capability
    /// that can answer without ever having seen the system.
    /// </summary>
    [Fact]
    public async Task WithNeitherAJournalNorANameItAsksForOne()
    {
        var result = await Ask(new GameStateStore());

        Assert.False(result.IsError);
        Assert.Contains("Say a name", result.Content, StringComparison.Ordinal);
    }
}
