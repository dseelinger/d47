using D47.Core.Conversation;
using D47.Core.Tests.Configuration;
using Xunit;

namespace D47.Core.Tests.Conversation;

public class AStrayWordInFrontOfACommandStillReadsAsTheCommandTests
{
    private static KeywordRouter Router(Func<IReadOnlyList<DynamicCommand>>? commands = null) =>
        new(TestSurface.For(new MemoryInstall()).Registry, commands);

    [Fact]
    public void SoSmallPanelSetsTheMiniPanel()
    {
        var match = Router().MatchSetting("So small panel");

        Assert.Equal("vr.mode", match?.Row.Key);
        Assert.Equal("mini", match?.Value);
    }

    [Fact]
    public void AgainCommaSmallPanelSetsTheMiniPanel()
    {
        var match = Router().MatchSetting("Again, small panel");

        Assert.Equal("vr.mode", match?.Row.Key);
        Assert.Equal("mini", match?.Value);
    }

    [Fact]
    public void ALeadingSentenceIsDroppedBeforeBigPanel()
    {
        var match = Router().MatchSetting("Like this. Big panel");

        Assert.Equal("vr.mode", match?.Row.Key);
        Assert.Equal("full", match?.Value);
    }

    [Fact]
    public void ASentenceWithThePhraseInTheMiddleStillMisses()
    {
        Assert.Null(Router().MatchSetting("should I say small panel before I dock"));
    }

    [Fact]
    public void AFillerWordInFrontOfAToolPhraseRoutesIt()
    {
        DynamicCommand[] commands =
        [
            new("run the docking checklist", "checklists", "get_checklist", new Dictionary<string, string>()),
        ];

        Assert.NotNull(Router(() => commands).MatchToolCommand("Um, run the docking checklist"));
    }

    [Fact]
    public void ALastSentenceThatIsAToolPhraseRoutesIt()
    {
        DynamicCommand[] commands =
        [
            new("run the docking checklist", "checklists", "get_checklist", new Dictionary<string, string>()),
        ];

        Assert.NotNull(Router(() => commands).MatchToolCommand("Not that one. Run the docking checklist."));
    }
}
