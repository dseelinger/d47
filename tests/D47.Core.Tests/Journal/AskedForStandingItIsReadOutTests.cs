using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>Reputation, navy rank and faction standing, from state d47 already holds (#418).</summary>
public class AskedForStandingItIsReadOutTests
{
    private static void Apply(GameStateStore gameState, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        gameState.Apply(parsed!);
    }

    private static GameStateStore Commander()
    {
        var gameState = new GameStateStore();
        Apply(gameState, """{"timestamp":"2026-09-23T10:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""");
        return gameState;
    }

    /// <summary>The journal of 2026-09-23 the issue quotes.</summary>
    private static GameStateStore Standing()
    {
        var gameState = Commander();
        Apply(gameState, """{"timestamp":"2026-09-23T10:00:01Z","event":"Rank","Combat":3,"Trade":4,"Explore":5,"Empire":1,"Federation":2}""");
        Apply(gameState, """{"timestamp":"2026-09-23T10:00:01Z","event":"Progress","Combat":10,"Trade":20,"Explore":30,"Empire":93,"Federation":40}""");
        Apply(gameState, """{"timestamp":"2026-09-23T10:00:02Z","event":"Reputation","Empire":28.347900,"Federation":25.210800,"Independent":0.000000,"Alliance":10.972300}""");
        Apply(
            gameState,
            """
            {"timestamp":"2026-09-20T18:00:00Z","event":"FSDJump","StarSystem":"Fixture Reach","SystemAddress":1,
             "Factions":[{"Name":"Mother Gaia","MyReputation":42.5},{"Name":"Gaia Workers' Union","MyReputation":-40.0},
                         {"Name":"Sirius Corporation","MyReputation":95.0}]}
            """);
        return gameState;
    }

    private static async Task<string> AskAsync(GameStateStore gameState, string? faction = null)
    {
        var arguments = faction is null
            ? ToolArguments.Empty
            : new ToolArguments(new Dictionary<string, string> { ["faction"] = faction });

        var result = await CapabilityRegistry.Build([JournalCapability.Create(gameState)])
            .InvokeAsync("get_standing", arguments, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        return result.Content;
    }

    [Fact]
    public async Task EachSuperpowerIsGivenABandANumberAndItsNavyRank()
    {
        var answer = await AskAsync(Standing());

        Assert.Contains("as of 2026-09-23 10:00 UTC", answer, StringComparison.Ordinal);
        Assert.Contains("Empire: Cordial, 28 of 100. Imperial Navy rank: Outsider, 93% into it.", answer, StringComparison.Ordinal);
        Assert.Contains("Federation: Cordial, 25 of 100. Federal Navy rank:", answer, StringComparison.Ordinal);
        Assert.Contains("Alliance: Cordial, 11 of 100.", answer, StringComparison.Ordinal);
        Assert.Contains("Independent: Neutral, 0 of 100.", answer, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Empire")]
    [InlineData("the Empire")]
    [InlineData("imperial")]
    public async Task OneSuperpowerNamedIsAnsweredAlone(string asked)
    {
        var answer = await AskAsync(Standing(), asked);

        Assert.Contains("Empire: Cordial, 28 of 100. Imperial Navy rank: Outsider, 93% into it.", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("Federation", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoReputationEventItSaysSoRatherThanReportingZero()
    {
        var answer = await AskAsync(Commander());

        Assert.Contains("has not reported your superpower reputation", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("0 of 100", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFactionIsAnsweredWithTheDateItWasRead()
    {
        var answer = await AskAsync(Standing(), "sirius");

        Assert.Equal("Sirius Corporation: Allied, 95 of 100, last read on 2026-09-20.", answer);
    }

    [Fact]
    public async Task AFactionNamedInFullIsAnsweredWhateverTheCase()
    {
        var answer = await AskAsync(Standing(), "mother gaia");

        Assert.Equal("Mother Gaia: Friendly, 43 of 100, last read on 2026-09-20.", answer);
    }

    [Fact]
    public async Task SeveralMatchesAreListedByNameRatherThanGuessed()
    {
        var answer = await AskAsync(Standing(), "gaia");

        Assert.Equal("Several factions match 'gaia': Gaia Workers' Union, Mother Gaia.", answer);
    }

    [Fact]
    public async Task AFactionNeverSeenIsSaidToBeUnseen()
    {
        var answer = await AskAsync(Standing(), "Pilots' Federation Local Branch");

        Assert.Contains("I have not seen your reputation with a faction called", answer, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-100, ReputationBand.Hostile)]
    [InlineData(-90, ReputationBand.Unfriendly)]
    [InlineData(-35.01, ReputationBand.Unfriendly)]
    [InlineData(-35, ReputationBand.Neutral)]
    [InlineData(3.99, ReputationBand.Neutral)]
    [InlineData(4, ReputationBand.Cordial)]
    [InlineData(35, ReputationBand.Friendly)]
    [InlineData(90, ReputationBand.Allied)]
    [InlineData(100, ReputationBand.Allied)]
    public void ABandStartsWhereEngineerAccessSaysItDoes(double reputation, ReputationBand band) =>
        Assert.Equal(band, ReputationBands.Of(reputation));

    [Theory]
    [InlineData("what's my reputation with the Empire", "Empire")]
    [InlineData("my standing with the Federation", "Federation")]
    [InlineData("what's my imperial rank", "Empire")]
    [InlineData("what's my navy rank", null)]
    [InlineData("what's my reputation", null)]
    public void ItIsReachedWithoutTheModel(string said, string? faction)
    {
        using var install = new TempInstall();
        var router = new KeywordRouter(TestSurface.For(install).Registry);

        var (tool, arguments) = router.MatchToolCommand(said) is { } command
            ? (command.ToolName, command.Arguments)
            : router.Match(said) is { } match
                ? (match.ToolName, match.Arguments)
                : (null, ToolArguments.Empty);

        Assert.Equal("get_standing", tool);
        Assert.Equal(faction, arguments.TryGetString("faction", out var named) ? named : null);
    }

    [Fact]
    public void AFactionByNameIsReachedWithoutTheModel()
    {
        using var install = new TempInstall();
        var gameState = Standing();
        var router = new KeywordRouter(
            TestSurface.For(install).Registry, () => JournalCapability.StandingPhrases(() => gameState.Active));

        var command = router.MatchToolCommand("what's my reputation with Mother Gaia");

        Assert.NotNull(command);
        Assert.Equal("get_standing", command.ToolName);
        Assert.True(command.Arguments.TryGetString("faction", out var named));
        Assert.Equal("Mother Gaia", named);
    }
}
