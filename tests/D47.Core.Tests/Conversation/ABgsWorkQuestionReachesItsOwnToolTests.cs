using D47.Core.Capabilities;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

public class ABgsWorkQuestionReachesItsOwnToolTests
{
    private static KeywordRouter Router() => new(TestSurface.For(new MemoryInstall()).Registry);

    [Theory]
    [InlineData("what's my bgs work", "get_bgs_work")]
    [InlineData("what have i done for the bgs", "get_bgs_work")]
    [InlineData("how have i done this session", "get_session_summary")]
    [InlineData("what have i done this session", "get_session_summary")]
    public void TheQuestionReachesTheToolThatAnswersIt(string asked, string tool)
    {
        var match = Router().Match(asked);

        Assert.NotNull(match);
        Assert.Equal(tool, match!.ToolName);
    }

    [Fact]
    public async Task TheModelMayCallTheTool()
    {
        var registry = TestSurface.For(new MemoryInstall()).Registry;
        var tool = registry.All.SelectMany(capability => capability.Descriptor.Tools).Single(tool => tool.Name == "get_bgs_work");

        Assert.False(tool.Protected);

        var result = await registry.InvokeAsync(
            "get_bgs_work", ToolArguments.FromJson(null), TestContext.Current.CancellationToken, ToolCaller.Model);

        Assert.False(result.IsError, result.Spoken);
    }
}
