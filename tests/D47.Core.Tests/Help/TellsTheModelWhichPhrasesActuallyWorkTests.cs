using D47.Core.Capabilities;
using Xunit;

namespace D47.Core.Tests.Help;

/// <summary>
/// find_phrase answers "what do I say" from the real phrase book instead of the model's memory, and
/// never says a goal cannot be done just because nothing matched it (#229).
/// </summary>
public class TellsTheModelWhichPhrasesActuallyWorkTests
{
    private static async Task<string> Answer(string goal)
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;

        var result = await registry.InvokeAsync(
            "find_phrase", new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["goal"] = goal }));

        Assert.False(result.IsError);
        return result.Content;
    }

    [Fact]
    public async Task NamesBothPhrasesForChoosingTheNextRouteSystem()
    {
        var answer = await Answer("choose the next system in the neutron jump route");

        Assert.Contains("'plot next neutron jump'", answer, StringComparison.Ordinal);
        Assert.Contains("'next system'", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaysNoPhraseMatchedRatherThanThatNothingCanBeDone()
    {
        var answer = await Answer("juggle flaming Thargoid eggs");

        Assert.Contains("No phrase matched", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("cannot", answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("can't", answer, StringComparison.OrdinalIgnoreCase);
    }
}
