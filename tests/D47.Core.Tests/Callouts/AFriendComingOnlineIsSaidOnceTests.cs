using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class AFriendComingOnlineIsSaidOnceTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-21T12:00:00Z");

    private static JournalEvent Line(DateTimeOffset at, string json)
    {
        var text = $"{{\"timestamp\":\"{at:yyyy-MM-ddTHH:mm:ssZ}\", {json}}}";
        Assert.True(JournalEvent.TryParse(text, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Online(DateTimeOffset at, string name) =>
        Line(at, $"\"event\":\"Friends\", \"Status\":\"Online\", \"Name\":\"{name}\"");

    private static JournalEvent Offline(DateTimeOffset at, string name) =>
        Line(at, $"\"event\":\"Friends\", \"Status\":\"Offline\", \"Name\":\"{name}\"");

    private static JournalEvent Header(DateTimeOffset at, int part) =>
        Line(at, $"\"event\":\"Fileheader\", \"part\":{part}");

    private sealed class Rig
    {
        public FriendRoster Roster { get; } = new();

        public FriendsCallout Callout { get; }

        public Rig() => Callout = new FriendsCallout(Roster);

        public string[] Tick(DateTimeOffset now, bool priming = false, bool calloutOn = true, params JournalEvent[] events)
        {
            Roster.Apply(events);

            return calloutOn
                ? Callout.Examine(new CalloutContext(now, priming, null, GameStatus.Unknown, NavRoute.None, events))
                    .Select(a => a.Text).ToArray()
                : [];
        }
    }

    [Fact]
    public void TheRestatementAfterLoginIsNotSaidAgain()
    {
        var rig = new Rig();
        string[] names = ["Arcturus", "Vega", "Rigel"];

        Assert.Empty(rig.Tick(Start, events: [Header(Start, 1), .. names.Select(n => Online(Start, n))]));
        Assert.Equal(["Arcturus, Vega and Rigel are online."], rig.Tick(Start.AddSeconds(6)));

        Assert.Empty(rig.Tick(Start.AddMinutes(1), events: [.. names.Select(n => Online(Start.AddMinutes(1), n))]));
        Assert.Empty(rig.Tick(Start.AddMinutes(1).AddSeconds(10)));
    }

    [Theory]
    [InlineData(31, true)]
    [InlineData(29, false)]
    public void ANameReportedAgainIsSaidOnlyAfterThirtyMinutes(int minutes, bool said)
    {
        var rig = new Rig();
        rig.Tick(Start, events: Online(Start, "Vega"));
        rig.Tick(Start.AddSeconds(6));

        var later = Start.AddMinutes(minutes);
        rig.Tick(later, events: Online(later, "Vega"));

        Assert.Equal(said ? ["Vega is online."] : [], rig.Tick(later.AddSeconds(6)));
    }

    [Fact]
    public void OnlineOfflineOnlineIsSaidTwice()
    {
        var rig = new Rig();

        rig.Tick(Start, events: Online(Start, "Vega"));
        Assert.Equal(["Vega is online."], rig.Tick(Start.AddSeconds(6)));

        rig.Tick(Start.AddMinutes(1), events: Offline(Start.AddMinutes(1), "Vega"));
        rig.Tick(Start.AddMinutes(2), events: Online(Start.AddMinutes(2), "Vega"));

        Assert.Equal(["Vega is online."], rig.Tick(Start.AddMinutes(2).AddSeconds(6)));
    }

    [Fact]
    public void FiveArrivalsAreOneLineWithThreeNamesAndACount()
    {
        var rig = new Rig();
        string[] names = ["Arcturus", "Vega", "Rigel", "Deneb", "Altair"];

        rig.Tick(Start, events: [.. names.Select(n => Online(Start, n))]);

        Assert.Equal(["Arcturus, Vega, Rigel and two others are online."], rig.Tick(Start.AddSeconds(6)));
    }

    [Fact]
    public void ANameThatArrivesInsideTheHoldJoinsTheLine()
    {
        var rig = new Rig();

        rig.Tick(Start, events: Online(Start, "Arcturus"));
        Assert.Empty(rig.Tick(Start.AddSeconds(3), events: Online(Start.AddSeconds(3), "Vega")));

        Assert.Equal(["Arcturus and Vega are online."], rig.Tick(Start.AddSeconds(6)));
    }

    [Fact]
    public void NothingIsSaidWhilePrimingAndTheRosterKeepsTheBacklogsNames()
    {
        var rig = new Rig();

        Assert.Empty(rig.Tick(Start, priming: true, events: [Online(Start, "Vega"), Online(Start, "Rigel")]));
        Assert.Empty(rig.Tick(Start.AddSeconds(10)));

        Assert.Equal(["Rigel", "Vega"], rig.Roster.Online.Keys.Order());
    }

    [Fact]
    public void OnlyTheFirstPartOfAFileheaderAndAShutdownEmptyTheRoster()
    {
        var rig = new Rig();
        rig.Tick(Start, events: Online(Start, "Vega"));

        rig.Tick(Start.AddSeconds(1), events: Header(Start.AddSeconds(1), 2));
        Assert.Single(rig.Roster.Online);

        rig.Tick(Start.AddSeconds(2), events: Header(Start.AddSeconds(2), 1));
        Assert.Empty(rig.Roster.Online);

        rig.Tick(Start.AddSeconds(3), events: Online(Start.AddSeconds(3), "Vega"));
        rig.Tick(Start.AddSeconds(4), events: Line(Start.AddSeconds(4), "\"event\":\"Shutdown\""));
        Assert.Empty(rig.Roster.Online);
    }

    [Fact]
    public void WithTheCalloutOffNothingIsSaidAndTheRosterStillFolds()
    {
        var rig = new Rig();

        Assert.Empty(rig.Tick(Start, calloutOn: false, events: Online(Start, "Vega")));
        Assert.Empty(rig.Tick(Start.AddSeconds(6), calloutOn: false));
        Assert.Single(rig.Roster.Online);
    }

    [Fact]
    public void AddedAndRequestedFriendsChangeNothing()
    {
        var rig = new Rig();

        rig.Tick(Start, events: Line(Start, "\"event\":\"Friends\", \"Status\":\"Added\", \"Name\":\"Vega\""));

        Assert.Empty(rig.Roster.Online);
        Assert.Empty(rig.Roster.Arrivals);
    }
}
