using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

public class TheServerStatusAnswerIsParsedTests
{
    // Captured from the live page on 2026-10-21.
    private const string LiveOnline = """{"status": "Good", "message": "", "code": 1, "product": "FORC-FDEV-DO-1000"}""";

    [Fact]
    public void TheLiveOnlineAnswerIsOnline() =>
        Assert.Equal(GameServerState.Online, FrontierServerStatusSource.Parse(LiveOnline));

    [Fact]
    public void MaintenanceIsMaintenance() =>
        Assert.Equal(GameServerState.Maintenance, FrontierServerStatusSource.Parse("""{"status": "Maintenance"}"""));

    [Theory]
    [InlineData("""{"status": "Bad"}""")]
    [InlineData("""{"status": "good"}""")]
    [InlineData("""{"status": ""}""")]
    public void AnyOtherWellFormedStatusIsOffline(string response) =>
        Assert.Equal(GameServerState.Offline, FrontierServerStatusSource.Parse(response));

    [Theory]
    [InlineData("")]
    [InlineData("<html>Bad gateway</html>")]
    [InlineData("[]")]
    [InlineData("""{"message": "x"}""")]
    [InlineData("""{"status": 1}""")]
    [InlineData("""{"status": null}""")]
    public void AnUnreadableAnswerIsUnknown(string response) =>
        Assert.Equal(GameServerState.Unknown, FrontierServerStatusSource.Parse(response));
}
