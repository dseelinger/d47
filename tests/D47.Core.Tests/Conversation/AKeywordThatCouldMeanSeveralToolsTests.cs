using D47.Core.Capabilities;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>What a keyword reaches when its capability has more than one answer to give.</summary>
public class AKeywordThatCouldMeanSeveralToolsTests
{
    private static KeywordRouter Router(TempInstall install) =>
        new(TestSurface.For(install).Registry);

    /// <summary>A jump-range question about a ship, and the second sentence broken the same way.</summary>
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
    /// And the right answer is not traded away for the wrong one: the possessive phrasings still reach
    /// the ship, by the keyword route as well as by the declared phrases.
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

    /// <summary>Asking which model is running reached <c>cancel_turn</c>, because Conversation declares it first.</summary>
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
    /// And a keyword naming a tool that does not exist — or one the router could not call anyway — is
    /// caught rather than silently unreachable.
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
    /// The decline itself, against a descriptor built here rather than against a shipped one — so the
    /// rule stays asserted after the last capability in the registry has been annotated.
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
    /// And one answer needs no ceremony: a capability offering a single callable tool is still reached
    /// by a bare string, which is how most of the registry declares its vocabulary.
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
