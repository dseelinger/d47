using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class AWingmateJoiningIsSaidOnceTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-21T12:00:00Z");

    private static JournalEvent Line(DateTimeOffset at, string json)
    {
        var text = $"{{\"timestamp\":\"{at:yyyy-MM-ddTHH:mm:ssZ}\", {json}}}";
        Assert.True(JournalEvent.TryParse(text, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Add(DateTimeOffset at, string name) =>
        Line(at, $"\"event\":\"WingAdd\", \"Name\":\"{name}\"");

    private static JournalEvent Join(DateTimeOffset at, params string[] others) =>
        Line(at, $"\"event\":\"WingJoin\", \"Others\":[{string.Join(",", others.Select(o => $"\"{o}\""))}]");

    private static JournalEvent Load(DateTimeOffset at) =>
        Line(at, "\"event\":\"LoadGame\"");

    private static string[] Tick(WingCallout callout, DateTimeOffset now, bool priming = false, params JournalEvent[] events) =>
        callout.Examine(new CalloutContext(now, priming, null, GameStatus.Unknown, NavRoute.None, events))
            .Select(a => a.Text).ToArray();

    [Fact]
    public void JoiningAWingNamesTheOthers()
    {
        Assert.Equal(["We're in a wing with Arcturus and Vega."], Tick(new WingCallout(), Start, events: Join(Start, "Arcturus", "Vega")));
    }

    [Fact]
    public void JoiningAWingNamesThreeAndCountsTheRest()
    {
        var said = Tick(new WingCallout(), Start, events: Join(Start, "Arcturus", "Vega", "Rigel", "Deneb", "Altair"));

        Assert.Equal(["We're in a wing with Arcturus, Vega, Rigel and two others."], said);
    }

    [Fact]
    public void AWingMateJoiningIsSaidAfterTheHold()
    {
        var callout = new WingCallout();

        Assert.Empty(Tick(callout, Start, events: Add(Start, "Arcturus")));
        Assert.Equal(["Arcturus joined the wing."], Tick(callout, Start.AddSeconds(6)));
        Assert.Empty(Tick(callout, Start.AddSeconds(12)));
    }

    [Fact]
    public void ABurstOfJoinersIsOneLine()
    {
        var callout = new WingCallout();

        Tick(callout, Start, events: Add(Start, "Arcturus"));
        Tick(callout, Start.AddSeconds(3), events: Add(Start.AddSeconds(3), "Vega"));

        Assert.Equal(["Arcturus and Vega joined the wing."], Tick(callout, Start.AddSeconds(6)));
    }

    [Theory]
    [InlineData(59, false)]
    [InlineData(61, true)]
    public void AWingAddJustAfterLoadGameRestoresTheWing(int seconds, bool said)
    {
        var callout = new WingCallout();
        var add = Start.AddSeconds(seconds);

        Tick(callout, Start, events: Load(Start));
        Tick(callout, add, events: Add(add, "Vega"));

        Assert.Equal(said ? ["Vega joined the wing."] : [], Tick(callout, add.AddSeconds(6)));
    }

    [Fact]
    public void LeavingTheWingIsNotSaid()
    {
        var callout = new WingCallout();

        Assert.Empty(Tick(callout, Start, events: Line(Start, "\"event\":\"WingLeave\"")));
        Assert.Empty(Tick(callout, Start.AddSeconds(6)));
    }

    [Fact]
    public void NothingIsSaidWhilePriming()
    {
        var callout = new WingCallout();

        Assert.Empty(Tick(callout, Start, priming: true, events: [Join(Start, "Vega"), Add(Start, "Rigel")]));
        Assert.Empty(Tick(callout, Start.AddSeconds(10)));
    }

    [Fact]
    public void ALoadGameInTheReplayStillMarksTheRestoreWindow()
    {
        var callout = new WingCallout();

        Tick(callout, Start, priming: true, events: Load(Start));
        Tick(callout, Start.AddSeconds(10), events: Add(Start.AddSeconds(10), "Vega"));

        Assert.Empty(Tick(callout, Start.AddSeconds(20)));
    }
}
