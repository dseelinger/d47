using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// An instruction out-matching a topic (change-requests.md 31).
/// <para>
/// Reported verbatim: <i>"Set course for my carrier"</i> answered with <i>"JOHN DEPARAGON is in
/// Scorpii Sector BB-O a6-2. Currently in normal space."</i> — a report, where an instruction was
/// given. <c>my carrier</c> is a keyword on Journal as well as a whole phrase, keywords match
/// anywhere in the input, and the router answered before the model saw it.
/// </para>
/// <para>
/// <b>The keywords are untouched</b>, because narrowing them was ruled against when remediation 16
/// fixed the same hijack for <i>"where is my fleet carrier"</i>: they are what makes a capability
/// reachable with no model at all.
/// </para>
/// </summary>
public class SetCourseForMyCarrierTests
{
    private const string Where = "Scorpii Sector BB-O a6-2";

    private static CarrierState Parked => new() { Name = "JOHN DEPARAGON", StarSystem = Where };

    private static DynamicCommand[] Commands(CarrierState? carrier) =>
        [.. CarrierCourse.Phrases(() => carrier)];

    [Fact]
    public void ItPlotsToWhereTheCarrierIs()
    {
        var command = Assert.Single(
            Commands(Parked),
            candidate => candidate.Phrase == "set course for my carrier");

        Assert.Equal(RouteCapability.Id, command.CapabilityId);
        Assert.Equal("plot_route", command.ToolName);
        Assert.Equal(Where, command.Arguments["to"]);
    }

    /// <summary>
    /// <b>With no carrier there is no phrase at all</b>, so the sentence falls through to the
    /// model — which says it does not know, rather than plotting a course to nowhere.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithNowhereToGoThereIsNoCommand(string? system)
    {
        Assert.Empty(Commands(system is null ? null : new CarrierState { StarSystem = system }));
    }

    /// <summary>
    /// <b>And it beats the keyword, which is the whole point.</b> Dynamic commands are matched
    /// first and against the whole utterance, so the sentence reaches the planner rather than
    /// Journal's position tool — driven against the real registry, because the hijack was a
    /// property of the whole vocabulary rather than of one capability.
    /// </summary>
    [Fact]
    public void TheRouterTakesItAheadOfTheCarrierKeyword()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var router = new KeywordRouter(surface.Registry, () => Commands(Parked));

        var match = router.MatchToolCommand("set course for my carrier");

        Assert.NotNull(match);
        Assert.Equal(RouteCapability.Id, match.CapabilityId);
        Assert.Equal("plot_route", match.ToolName);
        Assert.True(match.Arguments.TryGetString("to", out var to));
        Assert.Equal(Where, to);
    }

    /// <summary>
    /// And the question it used to be confused with still reaches the capability that answers it.
    /// A fix that silenced <i>"where is my carrier"</i> would have traded one wrong answer for a
    /// silence, which is the trade remediation 16 refused.
    /// </summary>
    [Fact]
    public void AskingWhereItIsStillReachesJournal()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var router = new KeywordRouter(surface.Registry, () => Commands(Parked));

        // Still Journal, and still by the whole-phrase route remediation 16 built for it rather
        // than by the keyword that used to swallow it.
        var match = router.MatchToolCommand("where is my fleet carrier");

        Assert.NotNull(match);
        Assert.Equal(JournalCapability.Id, match.CapabilityId);
    }
}
