using D47.Core.Capabilities;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A declared keyword buried in a long remark must fall through to the model.</summary>
public class AKeywordDoesNotHijackASentenceTests
{
    /// <summary>
    /// Verbatim from the log line at 14:54:11, which is the whole point of asserting it here: the
    /// sentence a Commander actually said, not a shortened stand-in for it.
    /// </summary>
    private const string TheComplaint =
        "I don't care what has happened in this session, you should know where my carrier is and " +
        "be able to use the galaxy map to plot a route there.";

    /// <summary>A word no declared phrase contains, so padding with it adds length and nothing else.</summary>
    private const string Filler = "anyway";

    private static KeywordRouter Router(TempInstall install) =>
        new(TestSurface.For(install).Registry);

    private static IEnumerable<string> EveryKeyword(CapabilityRegistry registry) =>
        from capability in registry.All
        from keyword in capability.Descriptor.Keywords.Concat(capability.Descriptor.SpokenKeywords)
        select keyword.Phrase;

    private static int Words(string phrase) => phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>Buries a phrase in a sentence several times its own length.</summary>
    private static string Buried(string phrase)
    {
        var padding = string.Join(' ', Enumerable.Repeat(Filler, Math.Max(4, Words(phrase))));

        return $"{padding} {phrase} {padding}";
    }

    [Fact]
    public void TheComplaintReachesTheModelRatherThanTheSessionSummary()
    {
        using var install = new TempInstall();

        var match = Router(install).Match(TheComplaint, InputSource.Spoken);

        Assert.True(
            match is null,
            $"A complaint about session scoping was routed to {match?.ToolName} on the strength of " +
            "the words 'this session'.");
    }

    /// <summary>And the line that followed it still stops d47 mid-sentence.</summary>
    [Fact]
    public void TheLineAfterItStillStopsTheSpeaking()
    {
        using var install = new TempInstall();

        Assert.NotNull(Router(install).MatchInterrupting("Oh stop it, you're an idiot."));
    }

    /// <summary>
    /// And so does a long one, whichever list the phrase was declared in. "shut up" is a general Speech
    /// keyword rather than an InterruptKeyword, so exempting only the interrupt-only vocabulary left a
    /// wordy request for silence bounded and refused.
    /// </summary>
    [Theory]
    [InlineData("oh for goodness sake will you just shut up and let me think")]
    [InlineData("I have heard enough of this, please be quiet for a minute")]
    [InlineData("you can stop talking now, I worked it out while you were reading it")]
    public void ALongRequestForSilenceStillStopsTheSpeaking(string said)
    {
        using var install = new TempInstall();

        Assert.NotNull(Router(install).MatchInterrupting(said));
    }

    /// <summary>
    /// The short forms, from the keyword table itself rather than a list beside it, so a keyword added
    /// later is covered without anybody remembering to add it here.
    /// </summary>
    [Fact]
    public void EveryDeclaredKeywordStillRoutesOnItsOwn()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;
        var router = new KeywordRouter(registry);

        var lost = EveryKeyword(registry)
            .Where(phrase => router.Match(phrase, InputSource.Spoken) is null)
            .ToArray();

        Assert.True(lost.Length == 0, $"The length bound took these phrases away: {string.Join(", ", lost)}");
    }

    /// <summary>Named rather than counted: the summary the Commander did not ask for is still reachable by asking for it.</summary>
    [Theory]
    [InlineData("session summary")]
    [InlineData("how have I done this session")]
    [InlineData("what has this session cost")]
    public void AskingForTheSessionSummaryStillGetsIt(string asked)
    {
        using var install = new TempInstall();

        Assert.NotNull(Router(install).Match(asked, InputSource.Spoken));
    }

    /// <summary>The general rule, asserted across the whole keyword set rather than for one example.</summary>
    [Fact]
    public void NoDeclaredKeywordMatchesWhenBuriedInALongSentence()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;
        var router = new KeywordRouter(registry);

        var hijacked = EveryKeyword(registry)
            .Where(phrase => router.Match(Buried(phrase), InputSource.Spoken) is not null)
            .ToArray();

        Assert.True(
            hijacked.Length == 0,
            $"These phrases still answer a sentence that merely mentions them: {string.Join(", ", hijacked)}");
    }

    [Fact]
    public void TheFillerMeansNothing()
    {
        // If a capability ever declares "anyway", the test above would be padding one keyword with another
        // and would stop proving anything.
        using var install = new TempInstall();

        Assert.Null(Router(install).Match(Buried(Filler), InputSource.Spoken));
    }

    /// <summary>The dynamic-command route is untouched.</summary>
    [Fact]
    public void ADynamicCommandStillMatchesTheWholeUtteranceAndOnlyThat()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;
        DynamicCommand[] commands =
        [
            new("run the docking checklist", "checklists", "get_checklist", new Dictionary<string, string>()),
        ];

        var router = new KeywordRouter(registry, () => commands);

        Assert.NotNull(router.MatchToolCommand("run the docking checklist"));
        Assert.NotNull(router.MatchToolCommand("Run the docking checklist."));
        Assert.Null(router.MatchToolCommand("should I run the docking checklist before I dock"));
    }

    /// <summary>
    /// And so are the two other whole-utterance routes: they were already one notch stricter than the
    /// keyword route, and this change did not reach them.
    /// </summary>
    [Fact]
    public void TheSettingAndToolCommandRoutesAreStillWholeUtterance()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;
        var router = new KeywordRouter(registry);

        var setting = (from capability in registry.All
                       from row in capability.Descriptor.Settings
                       from command in row.Commands
                       select command.Phrase).First();

        var tool = (from capability in registry.All
                    from definition in capability.Descriptor.Tools
                    from command in definition.Commands
                    where command.When is null
                    select command.Phrase).First();

        Assert.NotNull(router.MatchSetting(setting));
        Assert.Null(router.MatchSetting(Buried(setting)));

        Assert.NotNull(router.MatchToolCommand(tool));
        Assert.Null(router.MatchToolCommand(Buried(tool)));
    }
}
