using Xunit;

namespace D47.App.Tests;

public class OverheardChatterIsWrittenFromTheOffShipStateTests
{
    [Fact]
    [Trait("Category", "Gate")]
    public void TheChatterCallPassesTheOffShipStateAndNotTheLiveGameState()
    {
        var compose = AppSource.Method("ComposeNpcChatterAsync").Text;
        var call = compose[compose.IndexOf("FlavourTurn.AskAsync", StringComparison.Ordinal)..];
        call = call[..call.IndexOf(");", StringComparison.Ordinal)];

        Assert.Contains("OffShipKnowledge.Of(location)", call, StringComparison.Ordinal);
        Assert.DoesNotContain("LiveGameState", call, StringComparison.Ordinal);
    }
}
