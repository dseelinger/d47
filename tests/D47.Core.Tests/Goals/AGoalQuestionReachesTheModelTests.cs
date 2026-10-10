using D47.Core.Conversation;
using D47.Core.Goals;
using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Goals;

/// <summary>The question an arc's Ask D47 button sends is answered by the model, not taken by a router.</summary>
public class AGoalQuestionReachesTheModelTests
{
    public static TheoryData<string> Asked()
    {
        var data = new TheoryData<string>();

        foreach (var arc in GoalCatalogue.Every)
        {
            if (arc.Ask is { } question)
            {
                data.Add(question);
            }
        }

        return data;
    }

    [Fact]
    public void EveryRankArcAndPowerplayHasAQuestionAndNoOtherArcDoes()
    {
        var asking = GoalCatalogue.Every.Where(arc => arc.Ask is not null).Select(arc => arc.Key).ToList();

        Assert.Equal(
            ["rank.combat", "rank.trade", "rank.explore", "rank.soldier", "rank.exobiologist",
             "rank.empire", "rank.federation", GoalCatalogue.Powerplay],
            asking);
    }

    [Theory]
    [MemberData(nameof(Asked))]
    public void NoRouterTakesTheQuestion(string question)
    {
        var install = new MemoryInstall();
        var router = new KeywordRouter(TestSurface.For(install).Registry);

        Assert.Null(router.MatchSetting(question));
        Assert.Null(router.MatchToolCommand(question));
        Assert.Null(router.Match(question, InputSource.Typed));
    }

    [Theory]
    [MemberData(nameof(Asked))]
    public void ThePanelDoesNotTakeTheQuestion(string question)
    {
        var nav = new PanelNavigator();

        foreach (var tab in Enum.GetValues<PanelTab>())
        {
            nav.Register(tab, new NavCrumb(tab.ToString().ToLowerInvariant(), tab.ToString()));
        }

        nav.Register(PanelTab.Commander, new NavCrumb("checklist", "Checklist"));
        nav.Register(PanelTab.Commander, new NavCrumb("goals", "Goals"));
        nav.Register(PanelTab.Assets, new NavCrumb("fleet", "Ships"));

        Assert.Null(PanelPhrases.Apply(question, nav));
    }

    [Fact]
    public void TheMercenaryQuestionNamesItsLadder() =>
        Assert.Equal(
            "What's the fastest way to gain Mercenary rank from where I stand? Search the web if you can.",
            GoalCatalogue.Every.Single(arc => arc.Key == "rank.soldier").Ask);
}
