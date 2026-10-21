using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public sealed class WhosOnlineIsAnsweredFromFriendsSeenTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-21T12:00:00Z");

    private static JournalEvent Online(int minute, string name)
    {
        var at = Start.AddMinutes(minute);
        var text = $"{{\"timestamp\":\"{at:yyyy-MM-ddTHH:mm:ssZ}\", \"event\":\"Friends\", \"Status\":\"Online\", \"Name\":\"{name}\"}}";
        Assert.True(JournalEvent.TryParse(text, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CapabilityRegistry Build(FriendRoster roster, Func<bool> running) =>
        CapabilityRegistry.Build([CommsCapability.Create(ActionSurface.Inert, () => false, null, null, roster, running)]);

    private static async Task<string> Ask(FriendRoster roster, bool running = true)
    {
        var result = await Build(roster, () => running)
            .InvokeAsync("get_friends_online", new ToolArguments(new Dictionary<string, string>()), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        return result.Content;
    }

    private static FriendRoster Roster(params string[] namesOldestFirst)
    {
        var roster = new FriendRoster();
        roster.Apply([.. namesOldestFirst.Select((name, index) => Online(index, name))]);
        return roster;
    }

    [Fact]
    public async Task ThreeFriendsAreNamedNewestFirst() =>
        Assert.Equal(
            "Reported online since Elite started: Rigel, Vega and Arcturus.",
            await Ask(Roster("Arcturus", "Vega", "Rigel")));

    [Fact]
    public async Task MoreThanThreeNameTheNewestThreeAndCountTheRest() =>
        Assert.Equal(
            "Reported online since Elite started: G, F, E and four others.",
            await Ask(Roster("A", "B", "C", "D", "E", "F", "G")));

    [Fact]
    public async Task NoFriendsSaysSo() =>
        Assert.Equal("No friends have been reported online since Elite started.", await Ask(new FriendRoster()));

    [Fact]
    public async Task EliteNotRunningWinsOverNamesStillInTheRoster() =>
        Assert.Equal(
            "Elite isn't running, so no friends are reported online.",
            await Ask(Roster("Arcturus"), running: false));

    [Theory]
    [InlineData("who's online")]
    [InlineData("who is online")]
    [InlineData("which friends are online")]
    [InlineData("any friends online")]
    public void ThePhrasesReachTheToolWithNoModel(string said)
    {
        var router = new KeywordRouter(Build(new FriendRoster(), () => true));

        Assert.Equal("get_friends_online", router.MatchToolCommand(said)?.ToolName);
    }
}
