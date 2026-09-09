using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// "Set a course": acting on the system a nearest-first commodity search last found, rather than
/// the Commander repeating a name they just heard.
/// </summary>
public class SetACourseFromASearchTests
{
    [Fact]
    public void ItPlotsToWhatWasLastFound()
    {
        var lastFound = new LastFoundSystem();
        lastFound.Remember("HR 6012");

        var command = Assert.Single(
            CommunityGoalCourse.Phrases(lastFound), candidate => candidate.Phrase == "set a course");

        Assert.Equal(NavigationCapability.Id, command.CapabilityId);
        Assert.Equal("plot_course", command.ToolName);
        Assert.Equal("HR 6012", command.Arguments["system"]);
    }

    /// <summary>
    /// With nothing found there is no phrase at all, so the sentence falls through to the model rather
    /// than plotting a course to nowhere — the same shape <see cref="CarrierCourse"/> already uses for
    /// "set course for my carrier".
    /// </summary>
    [Fact]
    public void WithNothingFoundYetThereIsNoCommand()
    {
        Assert.Empty(CommunityGoalCourse.Phrases(new LastFoundSystem()));
    }

    [Fact]
    public void TheRouterTakesItAheadOfAnythingElse()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var lastFound = new LastFoundSystem();
        lastFound.Remember("HR 6012");

        var router = new KeywordRouter(surface.Registry, () => CommunityGoalCourse.Phrases(lastFound));

        var match = router.MatchToolCommand("set a course");

        Assert.NotNull(match);
        Assert.Equal(NavigationCapability.Id, match.CapabilityId);
        Assert.Equal("plot_course", match.ToolName);
        Assert.True(match.Arguments.TryGetString("system", out var system));
        Assert.Equal("HR 6012", system);
    }

    [Fact]
    public void ForgettingClearsTheCommand()
    {
        var lastFound = new LastFoundSystem();
        lastFound.Remember("HR 6012");
        lastFound.Remember(null);

        Assert.Empty(CommunityGoalCourse.Phrases(lastFound));
        Assert.Null(lastFound.System);
    }
}
