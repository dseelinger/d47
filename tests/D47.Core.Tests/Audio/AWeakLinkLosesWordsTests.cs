using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Words lost on a weak link are dropped whole and marked, the same ones every time.</summary>
public class AWeakLinkLosesWordsTests
{
    private const string Said =
        "Seven hundred ninety-two tonnes of tritium in the depot, Commander, and the market is buying at a "
        + "good price this week, so we can sell some if you like.";

    private static string Thin(double strength, IEnumerable<string> deltas, string seed = "how much fuel")
    {
        var thinner = LinkSignal.Thin(strength, seed);

        return string.Concat(deltas.Select(thinner.Push)) + thinner.Flush();
    }

    /// <summary>The text cut into pieces that end mid-word, the way a stream arrives.</summary>
    private static IEnumerable<string> Pieces(string text, int size) =>
        Enumerable.Range(0, (text.Length + size - 1) / size)
            .Select(index => text.Substring(index * size, Math.Min(size, text.Length - (index * size))));

    [Fact]
    public void AClearLinkLosesNothing() => Assert.Equal(Said, Thin(1, Pieces(Said, 7)));

    [Fact]
    public void AWeakLinkLosesWordsAndMarksWhereTheyWent()
    {
        var heard = Thin(0.2, [Said]);

        Assert.True(heard.Length < Said.Length, $"\"{heard}\" is no shorter than what was said");
        Assert.Contains(LinkSignal.Lost, heard, StringComparison.Ordinal);
        Assert.DoesNotContain(LinkSignal.Lost + " " + LinkSignal.Lost, heard, StringComparison.Ordinal);
    }

    [Fact]
    public void AWordSplitAcrossDeltasIsKeptOrLostWhole() =>
        Assert.Equal(Thin(0.2, [Said]), Thin(0.2, Pieces(Said, 3)));

    [Fact]
    public void TheSameTurnLosesTheSameWords() => Assert.Equal(Thin(0.3, [Said]), Thin(0.3, [Said]));

    [Fact]
    public void EveryWordThatComesThroughIsOneThatWasSaid()
    {
        var said = Said.Split(' ');

        foreach (var word in Thin(0.2, [Said]).Split(' ').Where(word => word != LinkSignal.Lost))
        {
            Assert.Contains(word, said);
        }
    }
}
