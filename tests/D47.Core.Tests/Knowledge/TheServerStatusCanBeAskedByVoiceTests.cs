using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class TheServerStatusCanBeAskedByVoiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private sealed class Source(GameServerState state) : IServerStatusSource
    {
        public Task<GameServerState> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(state);
    }

    private static ServerStatusWatch Watch(GameServerState state, bool enabled = true)
    {
        var watch = new ServerStatusWatch(new Source(state), () => enabled, NullLogger<ServerStatusWatch>.Instance)
        {
            Dispatch = work => work().GetAwaiter().GetResult(),
        };
        watch.Poll(Start);
        watch.Poll(Start);
        return watch;
    }

    private static async Task<string> Ask(ServerStatusWatch? watch, bool enabled, DateTimeOffset now)
    {
        var registry = CapabilityRegistry.Build([ServerStatusCapability.Create(() => watch, () => enabled, () => now)]);
        var result = await registry.InvokeAsync("get_server_status", new ToolArguments(new Dictionary<string, string>()));
        Assert.False(result.IsError);
        return result.Content;
    }

    [Theory]
    [InlineData(GameServerState.Online, "Online")]
    [InlineData(GameServerState.Maintenance, "In maintenance")]
    [InlineData(GameServerState.Offline, "Offline")]
    [InlineData(GameServerState.Unknown, "Unknown")]
    public async Task TheStateIsSaidWithHowOldItIs(GameServerState state, string word)
    {
        var said = await Ask(Watch(state), true, Start.AddMinutes(3));

        Assert.Equal($"{word}. Checked 3 minutes ago.", said);
    }

    [Fact]
    public async Task AFreshAnswerIsLessThanAMinuteOld()
    {
        Assert.Equal("Online. Checked less than a minute ago.", await Ask(Watch(GameServerState.Online), true, Start.AddSeconds(20)));
    }

    [Fact]
    public async Task WithTheSettingOffItSaysSo()
    {
        var said = await Ask(Watch(GameServerState.Online, enabled: false), false, Start);

        Assert.StartsWith("Server status is off.", said);
    }

    [Fact]
    public async Task BeforeTheFirstAnswerItSaysNothingHasBeenChecked()
    {
        var watch = new ServerStatusWatch(new Source(GameServerState.Online), () => true, NullLogger<ServerStatusWatch>.Instance)
        {
            Dispatch = _ => { },
        };
        watch.Poll(Start);

        Assert.Equal("Server status has not been checked yet.", await Ask(watch, true, Start));
        Assert.Equal("Server status has not been checked yet.", await Ask(null, true, Start));
    }

    [Fact]
    public void TheQuestionNeedsNoModelAndMakesNoRequest()
    {
        var tool = ServerStatusCapability.Create(() => null, () => true, () => Start).Tools.Single();

        Assert.Contains(tool.Commands, c => c.Phrase == "are the servers up");
        Assert.Empty(tool.Parameters);
    }
}
