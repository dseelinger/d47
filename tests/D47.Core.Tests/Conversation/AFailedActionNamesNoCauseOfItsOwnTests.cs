using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// A failed plot was reported as "plotter's not finding it as spelled" thirty seconds after the
/// Commander spelled the system out and was told "Noted".
/// </summary>
public class AFailedActionNamesNoCauseOfItsOwnTests
{
    [Fact]
    public void TheGuardrailsForbidNamingACauseTheToolDidNotName()
    {
        Assert.Contains(
            "name only the causes the tool result itself named",
            Guardrails.Text,
            StringComparison.Ordinal);

        Assert.Contains("Do not turn a hedge into a diagnosis", Guardrails.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGuardrailsNameTheCommandersOwnWordsAsTheWorstThingToBlame()
    {
        Assert.Contains("a name they spelled out", Guardrails.Text, StringComparison.Ordinal);
    }
}
