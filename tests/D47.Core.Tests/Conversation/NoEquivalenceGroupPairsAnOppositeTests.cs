using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>Folding two opposite instructions together would run the wrong one.</summary>
public class NoEquivalenceGroupPairsAnOppositeTests
{
    public static readonly TheoryData<string, string> Opposites = new()
    {
        { "on", "off" },
        { "up", "down" },
        { "in", "out" },
        { "start", "stop" },
        { "open", "close" },
        { "deploy", "retract" },
        { "enable", "disable" },
    };

    [Theory]
    [MemberData(nameof(Opposites))]
    public void NoGroupHoldsBothWordsOfAPair(string first, string second) =>
        Assert.DoesNotContain(
            PhraseScore.EquivalenceTable,
            group => group.Contains(first, StringComparer.OrdinalIgnoreCase)
                && group.Contains(second, StringComparer.OrdinalIgnoreCase));
}
