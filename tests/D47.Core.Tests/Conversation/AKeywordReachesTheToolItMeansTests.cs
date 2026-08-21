using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// Which tool a declared keyword actually reaches (reported 2026-08-21).
/// <para>
/// The report was one exchange: <i>"Where is my fleet carrier?"</i> answered with
/// <i>"JOHN DEPARAGON is in Oppi, near Reiter City, docked at Reiter City. Next jump… 2 jumps left
/// on the route."</i> — which is the Commander's own position, their own route, and their own name,
/// offered as an answer about a carrier that was somewhere else entirely.
/// </para>
/// <para>
/// <b>The carrier was the symptom.</b> A capability keyword names a <em>capability</em>, and the
/// router then takes that capability's first tool with no required parameters. For Journal that is
/// <c>get_location</c> — so every one of its two dozen keywords answered with where the Commander
/// was standing, whatever it had been asked about.
/// </para>
/// </summary>
public class AKeywordReachesTheToolItMeansTests
{
    private static KeywordRouter Router(TempInstall install) =>
        new(TestSurface.For(install).Registry);

    /// <summary>
    /// The reported sentence, and the three beside it that were broken the same way and were
    /// never reported because nobody had asked them yet.
    /// </summary>
    [Theory]
    [InlineData("where is my fleet carrier", "get_fleet")]
    [InlineData("where is my carrier", "get_fleet")]
    [InlineData("what ships do i own", "get_fleet")]
    [InlineData("what materials am i carrying", "get_materials")]
    [InlineData("how have i done this session", "get_session_summary")]
    [InlineData("what am i flying", "get_ship")]
    public void AQuestionReachesTheToolThatAnswersIt(string asked, string tool)
    {
        using var install = new TempInstall();

        var match = Router(install).MatchToolCommand(asked)
            ?? (ToolCommandMatch?)null;

        Assert.NotNull(match);
        Assert.Equal(tool, match!.ToolName);
    }

    /// <summary>
    /// The defect itself, pinned before it was fixed: the capability-keyword route takes the
    /// first tool with no required parameters, which for Journal is <c>get_location</c>. Kept as a
    /// test of the <em>router</em> rather than of Journal — the rule is general, and the next
    /// capability to declare a keyword and a second tool inherits it.
    /// </summary>
    [Fact]
    public void ACapabilityKeywordStillTakesTheFirstToolWithNoRequiredArguments()
    {
        using var install = new TempInstall();

        // "my fleet" is a Journal keyword and "my fleet carrier" contains it, which is how the
        // reported question reached the wrong answer. The keyword route still behaves this way on
        // purpose — it names a capability and cannot name a tool — so the fix is the phrases
        // declared on the tools above, which are matched first.
        var match = Router(install).Match("where is my fleet carrier");

        Assert.NotNull(match);
        Assert.Equal("get_location", match!.ToolName);
    }

    /// <summary>
    /// And the answer it now reaches is the Commander's own carrier, not a squadron's.
    /// <para>
    /// Elite writes both to the same journal seconds apart, and the state kept whichever came
    /// last — which across the corpus is the squadron one in 152 of the 173 journals that carry
    /// both. Routing the question correctly would have swapped one wrong answer for another.
    /// </para>
    /// </summary>
    [Fact]
    public void ASquadronCarrierDoesNotMoveTheCommandersOwn()
    {
        var store = new D47.Core.Journal.GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-21T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     """{ "timestamp":"2026-08-21T09:00:00Z", "event":"CarrierStats", "CarrierID":3715429376, "Callsign":"KLM-31F", "Name":"JOHN DEPARAGON", "FuelLevel":800, "DockingAccess":"all" }""",
                     """{ "timestamp":"2026-08-21T09:33:30Z", "event":"CarrierLocation", "CarrierType":"FleetCarrier", "CarrierID":3715429376, "StarSystem":"Wyrd" }""",

                     // Three seconds later, and it is not the Commander's.
                     """{ "timestamp":"2026-08-21T09:33:33Z", "event":"CarrierLocation", "CarrierType":"SquadronCarrier", "CarrierID":3713474048, "StarSystem":"Col 285 Sector GT-G c11-9" }""",
                 })
        {
            Assert.True(D47.Core.Journal.JournalEvent.TryParse(
                line, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, out var parsed));

            store.Apply(parsed!);
        }

        Assert.Equal("Wyrd", store.Active!.Carrier.StarSystem);
        Assert.Equal("JOHN DEPARAGON", store.Active!.Carrier.Name);
    }

    /// <summary>
    /// A journal from before Frontier added <c>CarrierType</c> still reports a position. All 223
    /// such events in the corpus name one carrier, so there is nothing to tell apart.
    /// </summary>
    [Fact]
    public void AnEventWithNoCarrierTypeIsStillTheCommandersOwn()
    {
        var store = new D47.Core.Journal.GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-21T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     """{ "timestamp":"2026-08-21T09:00:00Z", "event":"CarrierLocation", "CarrierID":3712682240, "Callsign":"X9K-B1T", "StarSystem":"Deciat" }""",
                 })
        {
            Assert.True(D47.Core.Journal.JournalEvent.TryParse(
                line, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, out var parsed));

            store.Apply(parsed!);
        }

        Assert.Equal("Deciat", store.Active!.Carrier.StarSystem);
    }

    /// <summary>
    /// And the one that was always right stays right: asking where you are is the question
    /// <c>get_location</c> is for.
    /// </summary>
    [Theory]
    [InlineData("where am i")]
    [InlineData("am i docked")]
    public void AndAskingWhereYouAreStillReachesTheLocation(string asked)
    {
        using var install = new TempInstall();

        var tool = Router(install).MatchToolCommand(asked)?.ToolName
                   ?? Router(install).Match(asked)?.ToolName;

        Assert.Equal("get_location", tool);
    }
}
