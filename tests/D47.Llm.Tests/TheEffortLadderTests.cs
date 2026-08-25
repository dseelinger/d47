using D47.Core.Conversation;
using Xunit;

namespace D47.Llm.Tests;

/// <summary>
/// Five rungs, in order, and what each one becomes on the wire (list.md Phase 54).
/// <para>
/// <b>Declaration order is the ladder.</b> Phase 54's floor and ceiling clamp against these as
/// they are written, so a member inserted in the wrong place silently reorders the rungs and a
/// ceiling of High starts meaning something else. Nothing else depends on the ordinals —
/// settings serialise the enum as camelCase strings, the spend ledger records no effort, and
/// there is no <c>(int)</c> cast on it in <c>src/</c> — which is what made inserting one safe.
/// </para>
/// <para>
/// <b>The compiler cannot catch a missed rung here, and that is the whole reason this file
/// exists.</b> Every <c>Translate</c> ends in a <c>_ =&gt;</c> arm, so adding <c>Xhigh</c> built
/// clean on the first attempt while both OpenAI providers quietly sent <c>"medium"</c> for it —
/// a rung <em>below</em> High for the setting that asks for more than High. A non-exhaustive
/// switch was expected to be the surprise; the absence of one was.
/// </para>
/// </summary>
public class TheEffortLadderTests
{
    /// <summary>
    /// The rungs, low to high. Written out rather than derived from the enum, because deriving
    /// them from the thing under test would assert nothing at all.
    /// </summary>
    [Fact]
    public void TheLadderIsInOrder()
    {
        Assert.Equal(
            [
                ThinkingEffort.Low,
                ThinkingEffort.Medium,
                ThinkingEffort.High,
                ThinkingEffort.Xhigh,
                ThinkingEffort.Max,
            ],
            Enum.GetValues<ThinkingEffort>());
    }

    /// <summary>
    /// <c>Xhigh</c> is between High and Max and not beside them, which is the property the clamp
    /// will read. Stated as the comparison rather than as an ordinal so it survives another
    /// insertion.
    /// </summary>
    [Fact]
    public void XhighSitsBetweenHighAndMax()
    {
        Assert.True(ThinkingEffort.Xhigh > ThinkingEffort.High);
        Assert.True(ThinkingEffort.Xhigh < ThinkingEffort.Max);
    }

    /// <summary>
    /// There is no off. The checklist allows low through max and no "off" unless the LLM is set
    /// to none — and that means no provider to ask, so there is no effort to choose.
    /// </summary>
    [Fact]
    public void ThereIsNoOff()
    {
        Assert.DoesNotContain(
            Enum.GetNames<ThinkingEffort>(),
            name => name is "Off" or "None");
    }
}
