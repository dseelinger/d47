using System.Reflection;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests;

/// <summary>An instruction out-matching a topic.</summary>
public class SetCourseForMyCarrierTests
{
    private const string Where = "Scorpii Sector BB-O a6-2";

    private static CarrierState Parked => new() { Name = "JOHN DEPARAGON", StarSystem = Where };

    private static DynamicCommand[] Commands(CarrierState? carrier) =>
        [.. CarrierCourse.Phrases(() => carrier)];

 /// <summary>It plots in the galaxy map.</summary>
    [Fact]
    public void ItPlotsToWhereTheCarrierIs()
    {
        var command = Assert.Single(
            Commands(Parked),
            candidate => candidate.Phrase == "set course for my carrier");

        Assert.Equal(NavigationCapability.Id, command.CapabilityId);
        Assert.Equal("plot_course", command.ToolName);
        Assert.Equal(Where, command.Arguments["system"]);
    }

    /// <summary>"Route" plots too (#405, second pass).</summary>
    [Theory]
    [InlineData("route to my carrier")]
    [InlineData("plot a route to my carrier")]
    public void AskingForTheRoutePlotsInTheMapToo(string phrase)
    {
        var command = Assert.Single(Commands(Parked), candidate => candidate.Phrase == phrase);

        Assert.Equal(NavigationCapability.Id, command.CapabilityId);
        Assert.Equal("plot_course", command.ToolName);
        Assert.Equal(Where, command.Arguments["system"]);
    }

    /// <summary>
 /// And the planner is reached by saying neutron, which is the one thing it has that the
    /// galaxy map has not.
    /// </summary>
    [Fact]
    public void AskingForANeutronJumpReachesThePlanner()
    {
        var neutron = Commands(Parked)
            .Where(command => command.Phrase.Contains("neutron", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(CarrierCourse.NeutronSpellings().Count(), neutron.Length);

        foreach (var command in neutron)
        {
            Assert.Equal(RouteCapability.Id, command.CapabilityId);
            Assert.Equal("plot_route", command.ToolName);
            Assert.Equal(Where, command.Arguments["to"]);
        }

        // The three shapes the Commander actually said, named rather than left to the product.
        foreach (var phrase in new[]
                 {
                     "neutron jump to my carrier",
                     "plot neutron jumps to my carrier",
                     "plot a neutron route to my fleet carrier",
                 })
        {
            Assert.Contains(neutron, command => command.Phrase == phrase);
        }
    }

    /// <summary>
    /// And nothing the planner answers to says "course" or plain "route" any more, so the two tables
 /// cannot drift back into meaning the same thing.
    /// </summary>
    [Fact]
    public void NothingReachingThePlannerReadsAsACourse()
    {
        var planner = Commands(Parked).Where(command => command.ToolName == "plot_route");

        Assert.All(planner, command =>
            Assert.Contains("neutron", command.Phrase, StringComparison.Ordinal));
    }

    /// <summary>
 /// Over the whole command table, not one phrase: a fifteenth spelling added later cannot
    /// silently go to the planner instead.
    /// </summary>
    [Fact]
    public void EverySetCoursePhraseInTheTablePlotsInTheMap()
    {
        var courses = Table()
            .Where(command => command.Phrase.Contains("set course", StringComparison.Ordinal))
            .ToArray();

        // Not a count: a fifteenth spelling is welcome, and only where it goes is asserted.
        Assert.NotEmpty(courses);

        foreach (var command in courses)
        {
            Assert.Equal(NavigationCapability.Id, command.CapabilityId);
            Assert.Equal("plot_course", command.ToolName);
        }
    }

    /// <summary>
    /// And no phrase table in Core says "set course" outside that table, so the assertion above cannot
    /// be escaped by putting the fifteenth spelling somewhere else.
    /// </summary>
    [Fact]
    public void NoOtherPhraseTableInCoreSaysSetCourse()
    {
        var known = Table().Select(command => command.Phrase).ToHashSet(StringComparer.Ordinal);

        var strays =
            from type in typeof(CarrierCourse).Assembly.GetTypes()
            from field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            where field.FieldType == typeof(string) || field.FieldType == typeof(string[])
            let value = field.GetValue(null)
            from phrase in value is string one ? [one] : (string[]?)value ?? []
            where phrase.Contains("set course", StringComparison.OrdinalIgnoreCase)
            where !known.Contains(phrase)
            select $"{type.Name}.{field.Name}: {phrase}";

        Assert.Empty(strays);
    }

    /// <summary>
    /// Every dynamic command Core can offer with a carrier parked and a system last found — the two
    /// builders that speak of courses.
    /// </summary>
    private static DynamicCommand[] Table()
    {
        var lastFound = new LastFoundSystem();
        lastFound.Remember("HR 6012");

        return [.. Commands(Parked), .. CommunityGoalCourse.Phrases(lastFound)];
    }

    /// <summary>
    /// With no carrier there is no phrase at all, so the sentence falls through to the model — which
    /// says it does not know, rather than plotting a course to nowhere.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithNowhereToGoThereIsNoCommand(string? system)
    {
        Assert.Empty(Commands(system is null ? null : new CarrierState { StarSystem = system }));
    }

    /// <summary>And it beats the keyword, which is the reason for the phrase.</summary>
    [Fact]
    public void TheRouterTakesItAheadOfTheCarrierKeyword()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var router = new KeywordRouter(surface.Registry, () => Commands(Parked));

        var match = router.MatchToolCommand("set course for my carrier");

        Assert.NotNull(match);
        Assert.Equal(NavigationCapability.Id, match.CapabilityId);
        Assert.Equal("plot_course", match.ToolName);
        Assert.True(match.Arguments.TryGetString("system", out var system));
        Assert.Equal(Where, system);
    }

    /// <summary>And the neighbouring question still reaches the capability that answers it.</summary>
    [Fact]
    public void AskingWhereItIsStillReachesJournal()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var router = new KeywordRouter(surface.Registry, () => Commands(Parked));

        // Still Journal, and by the whole-phrase route rather than by a keyword.
        var match = router.MatchToolCommand("where is my fleet carrier");

        Assert.NotNull(match);
        Assert.Equal(JournalCapability.Id, match.CapabilityId);
    }
}
