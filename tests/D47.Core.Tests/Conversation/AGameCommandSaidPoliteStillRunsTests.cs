using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

public class AGameCommandSaidPoliteStillRunsTests
{
    private static KeywordRouter Router(TempInstall install, DynamicCommand[]? taught = null) =>
        new(TestSurface.For(install).Registry, taught is null ? null : () => taught);

    [Theory]
    [InlineData("gear down please", "gear down")]
    [InlineData("lights off now", "lights off")]
    [InlineData("engage supercruise for me", "engage supercruise")]
    public void ATailOnAGameCommandIsIgnored(string said, string phrase)
    {
        using var install = new TempInstall();
        var router = Router(install);

        var bare = router.MatchToolCommand(phrase);
        var polite = router.MatchToolCommand(said);

        Assert.NotNull(bare);
        Assert.Equal(bare.ToolName, polite?.ToolName);
        Assert.Equal(bare.Phrase, polite?.Phrase);
    }

    [Fact]
    public void OpeningTheCargoScoopPleaseOpensIt()
    {
        using var install = new TempInstall();

        var match = Router(install).MatchToolCommand("open the cargo scoop please");

        Assert.True(match!.Arguments.TryGetString("action", out var action));
        Assert.Equal("cargo_scoop", action);
        Assert.True(match!.Arguments.TryGetString("state", out var state));
        Assert.Equal("on", state);
    }

    [Fact]
    public void AnUtteranceThatMatchesAsSaidIsNeverReadThroughAStrippedReading()
    {
        using var install = new TempInstall();

        var match = Router(install).MatchToolCommand("open the galaxy map");

        Assert.True(match!.Arguments.TryGetString("action", out var action));
        Assert.Equal("galaxy_map", action);
    }

    [Fact]
    public void ATaughtPhraseFollowedByPleaseRunsTheTaughtCommand()
    {
        using var install = new TempInstall();
        DynamicCommand[] taught =
        [
            new("run the docking checklist", "checklists", "get_checklist", new Dictionary<string, string>()),
        ];

        var match = Router(install, taught).MatchToolCommand("run the docking checklist please");

        Assert.Equal("get_checklist", match?.ToolName);
    }
}
