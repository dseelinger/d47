using D47.Core.Capabilities;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// What a keyword reaches when its capability has more than one answer to give (#161).
/// <para>
/// The report was <i>"what's the Cobra Mk III's jump range?"</i>, asked out loud three times and
/// answered each time with <i>"JOHN DEPARAGON is in Kamitra, near Hammel Terminal, docked at
/// Hammel Terminal."</i> — the Commander's own position, offered with total confidence as the jump
/// range of a hull they were asking about in the abstract. The model never saw the question.
/// </para>
/// <para>
/// <b>The mechanism is general and the issue said so.</b> A keyword reached a capability and the
/// router then took its first tool with no required parameters — a <em>positional</em> pick, out
/// of the order somebody happened to declare them in. Twelve capabilities had more than one
/// eligible tool when this was measured, so twelve had the same trapdoor, and one had already
/// fallen through it unreported: every phrase Conversation declares cancelled the running turn.
/// </para>
/// <para>
/// <b>The decision, of the three the issue put up:</b> a keyword names its tool, and a keyword
/// that names none on a capability with several eligible tools <em>declines</em> rather than
/// guessing. Narrowing keywords was rejected as fixing only the sentences somebody thinks of;
/// letting a keyword name a tool loses no reachability, because the phrases already do exactly
/// this. Falling through to the model is what already happens to every phrasing nobody wrote
/// down, so the decline costs a slower answer where the guess cost a confident wrong one.
/// </para>
/// </summary>
public class AKeywordThatCouldMeanSeveralToolsTests
{
    private static KeywordRouter Router(TempInstall install) =>
        new(TestSurface.For(install).Registry);

    /// <summary>
    /// The reported sentence, by name, and the second one the issue listed beside it.
    /// <para>
    /// Neither reaches the router at all now, which is the point: they are questions about a hull
    /// rather than about the Commander, and the model has the specification tables.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("what's the Cobra Mk III's jump range?")]
    [InlineData("is the jump range on this thing any good")]
    [InlineData("what is a Python Mk II's jump range")]
    public void AJumpRangeQuestionAboutAShipIsNotAnsweredWithTheCommandersLocation(string asked)
    {
        using var install = new TempInstall();
        var router = Router(install);

        var tool = router.MatchToolCommand(asked)?.ToolName ?? router.Match(asked)?.ToolName;

        Assert.True(
            tool is null,
            $"\"{asked}\" was answered by the router with {tool}, and it is a question for the model.");
    }

    /// <summary>
    /// And the right answer is not traded away for the wrong one: the possessive phrasings still
    /// reach the ship, by the keyword route as well as by the declared phrases.
    /// </summary>
    [Theory]
    [InlineData("what's my jump range")]
    [InlineData("my jump range")]
    [InlineData("so what is my jump range these days")]
    public void AskingYourOwnJumpRangeStillReachesTheShip(string asked)
    {
        using var install = new TempInstall();
        var router = Router(install);

        var tool = router.MatchToolCommand(asked)?.ToolName ?? router.Match(asked)?.ToolName;

        Assert.Equal("get_ship", tool);
    }

    /// <summary>
    /// The one that was already broken and had not been reported: asking which model is running
    /// reached <c>cancel_turn</c>, because Conversation declares it first.
    /// </summary>
    [Theory]
    [InlineData("which model")]
    [InlineData("what model are you")]
    [InlineData("what have you cost me this session")]
    public void AskingAboutTheModelDoesNotCancelTheTurn(string asked)
    {
        using var install = new TempInstall();

        var match = Router(install).Match(asked);

        Assert.NotNull(match);
        Assert.Equal("get_model_status", match!.ToolName);
    }

    /// <summary>
    /// The general rule, asserted against the registry rather than against a list: every declared
    /// keyword reaches a tool, and where its capability has several the keyword said which.
    /// <para>
    /// This is the forcing function. A new capability that declares a keyword and a second
    /// argument-free tool fails here rather than silently answering with whichever it wrote
    /// first — which is how <c>which model</c> came to cancel turns for as long as it did.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryKeywordOnACapabilityWithSeveralAnswersNamesTheOneItMeans()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;

        var unnamed = (from capability in registry.All
                       let eligible = capability.Descriptor.Tools
                           .Count(tool => !tool.Parameters.Any(parameter => parameter.Required))
                       where eligible > 1
                       from keyword in capability.Descriptor.Keywords.Concat(capability.Descriptor.SpokenKeywords)
                       where keyword.ToolName is null
                       select $"{capability.Descriptor.Id}: '{keyword.Phrase}'").ToArray();

        Assert.True(
            unnamed.Length == 0,
            "These capabilities offer several tools the router could call, so a keyword that names "
            + "none of them is answered by whichever was declared first — or, since #161, not at "
            + $"all. Name the tool each phrase means: {string.Join(", ", unnamed)}");
    }

    /// <summary>
    /// And a keyword naming a tool that does not exist — or one the router could not call anyway —
    /// is caught rather than silently unreachable.
    /// </summary>
    [Fact]
    public void EveryNamedToolIsOneTheRouterCouldActuallyCall()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;

        var wrong = (from capability in registry.All
                     let eligible = capability.Descriptor.Tools
                         .Where(tool => !tool.Parameters.Any(parameter => parameter.Required))
                         .Select(tool => tool.Name)
                         .ToHashSet(StringComparer.Ordinal)
                     from keyword in capability.Descriptor.Keywords.Concat(capability.Descriptor.SpokenKeywords)
                     where keyword.ToolName is { Length: > 0 } named && !eligible.Contains(named)
                     select $"{capability.Descriptor.Id}: '{keyword.Phrase}' → {keyword.ToolName}").ToArray();

        Assert.True(
            wrong.Length == 0,
            $"A keyword names a tool its capability cannot answer with: {string.Join(", ", wrong)}");
    }

    /// <summary>
    /// The decline itself, against a descriptor built here rather than against a shipped one — so
    /// the rule stays asserted after the last capability in the registry has been annotated.
    /// </summary>
    [Fact]
    public void AnUnnamedKeywordOnACapabilityWithSeveralAnswersReachesNothing()
    {
        var registry = CapabilityRegistry.Build([new CapabilityDescriptor
        {
            Id = "two-answers",
            Group = "Test",
            Name = "Two answers",
            Summary = "A capability with two tools the router could call and a keyword naming neither.",
            Keywords = ["tell me something"],
            Tools =
            [
                new ToolDefinition
                {
                    Name = "first",
                    Description = "The one that happens to be declared first.",
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok("first")),
                },
                new ToolDefinition
                {
                    Name = "second",
                    Description = "The one a positional pick would never reach.",
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok("second")),
                },
            ],
        }]);

        Assert.Null(new KeywordRouter(registry).Match("tell me something"));
    }

    /// <summary>
    /// And one answer needs no ceremony: a capability offering a single callable tool is still
    /// reached by a bare string, which is how most of the registry declares its vocabulary.
    /// </summary>
    [Fact]
    public void AnUnnamedKeywordOnACapabilityWithOneAnswerStillReachesIt()
    {
        var registry = CapabilityRegistry.Build([new CapabilityDescriptor
        {
            Id = "one-answer",
            Group = "Test",
            Name = "One answer",
            Summary = "A capability with one tool the router could call.",
            Keywords = ["tell me something"],
            Tools =
            [
                new ToolDefinition
                {
                    Name = "only",
                    Description = "The only thing it does.",
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok("only")),
                },
            ],
        }]);

        Assert.Equal("only", new KeywordRouter(registry).Match("tell me something")?.ToolName);
    }
}
