using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

public class AShuttlePilotAnswersOnTheShuttleChannelTests
{
    private static ShuttleRide Ride(ShuttleKind kind, bool retreat = false) => new(kind, "Sol", "Abraham Lincoln", retreat);

    private static CommsChannels Channels(ShuttleRide? ride)
    {
        var channels = new CommsChannels();
        channels.Update(new CommsInputs { Shuttle = ride });
        return channels;
    }

    private static Speaker Taken(LineDecision decision) => Assert.IsType<LineDecision.Taken>(decision).Speaker;

    [Fact]
    public async Task TheApexPilotIsACivilianPassengerPilotWhoKnowsTheRoute()
    {
        var ride = Ride(ShuttleKind.Apex);
        var channels = Channels(ride);
        var line = new ShuttleLine(() => ride, () => "Sol");

        var speaker = Taken(await line.RouteAsync(channels.Selected!, "how long", TestContext.Current.CancellationToken));

        Assert.Equal("Apex pilot", speaker.Name);
        Assert.Contains("Apex Interstellar", speaker.Brief, StringComparison.Ordinal);
        Assert.False(speaker.OffersTools);
        Assert.Equal(1, speaker.Signal);
        Assert.Contains("Abraham Lincoln", speaker.LiveState!(), StringComparison.Ordinal);
        Assert.Contains("Current system: Sol", speaker.LiveState!(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheFrontlinePilotFliesIntoOrOutOfAConflictZone()
    {
        var current = Ride(ShuttleKind.Frontline);
        var line = new ShuttleLine(() => current, () => null);
        var channels = Channels(current);

        var into = Taken(await line.RouteAsync(channels.Selected!, "hi", TestContext.Current.CancellationToken));
        current = Ride(ShuttleKind.Frontline, retreat: true);
        var outOf = Taken(await line.RouteAsync(channels.Selected!, "hi", TestContext.Current.CancellationToken));

        Assert.Equal("Frontline pilot", into.Name);
        Assert.Contains("into a ground conflict zone", into.Brief, StringComparison.Ordinal);
        Assert.Contains("out of a ground conflict zone", outOf.Brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyThePilotPartyIsTakenAndOnlyDuringARide()
    {
        var ride = Ride(ShuttleKind.Apex);
        var channels = Channels(ride);
        var line = new ShuttleLine(() => ride, () => null);

        Assert.IsType<LineDecision.NotMine>(
            await line.RouteAsync(new CommsSelection(CommsChannel.Shuttle, new CommsSubChannel("other", "Other")), "hi", TestContext.Current.CancellationToken));
        Assert.IsType<LineDecision.NotMine>(
            await line.RouteAsync(new CommsSelection(CommsChannel.Crew, new CommsSubChannel(CommsChannels.PilotKey, "Apex pilot")), "hi", TestContext.Current.CancellationToken));

        var ended = new ShuttleLine(() => null, () => null);
        Assert.IsType<LineDecision.NotMine>(await ended.RouteAsync(channels.Selected!, "hi", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ANewRideStartsAnEmptyTranscriptAndANewPersonality()
    {
        var current = Ride(ShuttleKind.Apex);
        var written = 0;
        var line = new ShuttleLine(
            () => current,
            () => null,
            writePersonality: (_, _) => Task.FromResult<string?>("Chatty number " + ++written));
        var channels = Channels(current);

        var one = Taken(await line.RouteAsync(channels.Selected!, "hi", TestContext.Current.CancellationToken));
        one.Transcript.Add(new ConversationMessage(ConversationRole.User, "hi"));
        var again = Taken(await line.RouteAsync(channels.Selected!, "hi", TestContext.Current.CancellationToken));
        current = Ride(ShuttleKind.Apex);
        var next = Taken(await line.RouteAsync(channels.Selected!, "hi", TestContext.Current.CancellationToken));

        Assert.Single(again.Transcript);
        Assert.Empty(next.Transcript);
        Assert.Contains("Chatty number 1", again.Brief, StringComparison.Ordinal);
        Assert.Contains("Chatty number 2", next.Brief, StringComparison.Ordinal);
    }
}
