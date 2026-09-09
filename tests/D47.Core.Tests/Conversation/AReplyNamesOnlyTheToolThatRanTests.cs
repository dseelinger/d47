using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// Told the galaxy map was what he wanted, the Commander was answered with "that's the same tool I
/// used" — of two tools in two files, one of which had not run — and with Lave, a system from an
/// earlier attempt, while the request was his carrier at Meene.
/// </summary>
public class AReplyNamesOnlyTheToolThatRanTests
{
    [Fact]
    public void TheGuardrailsForbidCallingTwoToolsOne()
    {
        Assert.Contains(
            "Name only the tool that actually ran, or name none at all",
            Guardrails.Text,
            StringComparison.Ordinal);

        Assert.Contains("two tools are never", Guardrails.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGuardrailsForbidASubjectCarriedOverFromAnEarlierTurn()
    {
        Assert.Contains(
            "name only the system, station or thing this request was",
            Guardrails.Text,
            StringComparison.Ordinal);

        Assert.Contains("never one carried over from an earlier turn", Guardrails.Text, StringComparison.Ordinal);
    }
}
