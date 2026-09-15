using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>"Captain, ..." is answered by the carrier's captain, who keeps the line until it is ended.</summary>
public class TheCaptainAnswersAndKeepsTheLineTests
{
    private const string ShipAi = "Warden";

    private const string ShipAiBrief = "You are the ship's AI aboard this vessel.";

    private const string CaptainBrief = "captain of the Commander's fleet carrier";

    private const string ToShipAi = "Warden, summarise that exchange for me";

    private static readonly CarrierState Owned = new()
    {
        CallSign = "BNH-T2F",
        Name = "Sacred Fire",
        StarSystem = "Wolf 359",
    };

    private static (TurnLoop Loop, CaptainLine Line) Build(
        TestSurface surface,
        ILlmProvider provider,
        Func<string, string, CancellationToken, Task<double?>>? distance = null,
        CarrierState? carrier = null)
    {
        var line = new CaptainLine(
            () => carrier ?? Owned,
            () => "Sol",
            () => ShipAi,
            distance ?? ((_, _, _) => Task.FromResult<double?>(100)));

        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider)
        {
            Persona = ShipAiBrief,
        };

        loop.Lines.Add(line);

        return (loop, line);
    }

    private static async Task<List<TurnEvent>> RunAsync(TurnLoop loop, string input)
    {
        List<TurnEvent> events = [];

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            events.Add(turnEvent);
        }

        return events;
    }

    private static string Text(ConversationMessage message) =>
        string.Join(' ', message.Content.OfType<ConversationContent.Text>().Select(part => part.Value));

    [Fact]
    public async Task CaptainByNameIsAnsweredWithTheCaptainsBrief()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Seven hundred ninety-two tonnes, Commander.");
        var (loop, line) = Build(TestSurface.For(install), provider);

        var events = await RunAsync(loop, "Captain, how much fuel have we got");

        var addressed = Assert.Single(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(VoiceRole.CarrierCaptain, addressed.Role);
        Assert.IsType<TurnEvent.Routed>(events[events.IndexOf(addressed) + 1]);

        var prompt = provider.LastRequest!.Prompt;
        Assert.Contains(CaptainBrief, prompt.Persona, StringComparison.Ordinal);
        Assert.Equal("how much fuel have we got", Text(Assert.Single(prompt.History)));
        Assert.True(line.IsOpen);
    }

    [Fact]
    public async Task TheNextTurnWithNoNameIsTheCaptainsToo()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var (loop, _) = Build(TestSurface.For(install), provider);

        await RunAsync(loop, "Captain, how much fuel have we got");
        var next = await RunAsync(loop, "and how much cargo");

        Assert.Equal(VoiceRole.CarrierCaptain, Assert.Single(next.OfType<TurnEvent.Addressed>()).Role);
        Assert.Contains(CaptainBrief, provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
        Assert.Equal(3, provider.LastRequest.Prompt.History.Count);
    }

    [Fact]
    public async Task ThatsAllCaptainEndsTheLine()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var (loop, line) = Build(TestSurface.For(install), provider);

        await RunAsync(loop, "Captain, how much fuel have we got");
        await RunAsync(loop, "That's all, Captain");

        Assert.False(line.IsOpen);

        var after = await RunAsync(loop, ToShipAi);

        Assert.Empty(after.OfType<TurnEvent.Addressed>());
    }

    [Fact]
    public async Task TheShipAisNameEndsTheLine()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var (loop, line) = Build(TestSurface.For(install), provider);

        await RunAsync(loop, "Captain, how much fuel have we got");
        var named = await RunAsync(loop, ToShipAi);

        Assert.Empty(named.OfType<TurnEvent.Addressed>());
        Assert.False(line.IsOpen);
        Assert.Equal(ShipAiBrief, provider.LastRequest!.Prompt.Persona);

        Assert.Empty((await RunAsync(loop, "and how much cargo")).OfType<TurnEvent.Addressed>());
    }

    [Fact]
    public async Task BeyondRangeTheShipAiSaysSoWithoutAModel()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var (loop, line) = Build(TestSurface.For(install), provider, (_, _, _) => Task.FromResult<double?>(600));

        var events = await RunAsync(loop, "Captain, how much fuel have we got");

        var result = Assert.Single(events.OfType<TurnEvent.Completed>()).Result;

        Assert.Equal(TurnRoute.CarrierOutOfRange, result.Route);
        Assert.Contains("600", result.Text, StringComparison.Ordinal);
        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(0, provider.CallCount);
        Assert.False(line.IsOpen);
    }

    [Fact]
    public async Task ADistanceThatThrowsGivesACaptainsTurn()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var (loop, _) = Build(
            TestSurface.For(install),
            provider,
            (_, _, _) => throw new HttpRequestException("the galaxy service went away"));

        var events = await RunAsync(loop, "Captain, how much fuel have we got");

        Assert.Single(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(TurnRoute.Model, Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Route);
    }

    [Fact]
    public async Task ADistanceNobodyKnowsGivesACaptainsTurn()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var (loop, _) = Build(TestSurface.For(install), provider, (_, _, _) => Task.FromResult<double?>(null));

        var events = await RunAsync(loop, "Captain, how much fuel have we got");

        Assert.Single(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(TurnRoute.Model, Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Route);
    }

    [Fact]
    public async Task WithNoCarrierOwnedTheShipAiAnswers()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var (loop, line) = Build(TestSurface.For(install), provider, carrier: CarrierState.None);

        var events = await RunAsync(loop, "Captain, status");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.False(line.IsOpen);
    }

    [Fact]
    public async Task AProtectedToolTheCaptainCallsIsRefused()
    {
        using var install = new TempInstall();
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling("call-1", "accept_proposal", "{}"),
            RoundScriptedLlmProvider.Saying("I can't do that from here, Commander."));
        var (loop, _) = Build(TestSurface.For(install), provider);

        var events = await RunAsync(loop, "Captain, accept the checklist proposal");

        Assert.Single(events.OfType<TurnEvent.Addressed>());
        Assert.False(Assert.Single(events.OfType<TurnEvent.ToolFinished>()).Succeeded);

        var answered = string.Join(
            ' ',
            provider.Requests[1].Prompt.History.SelectMany(message => message.Content).Select(part => part.ToString()));

        Assert.Contains("not something I can do on my own", answered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheShipAiOverhearsTheCaptainWithoutTakingTheAnswerAsItsOwn()
    {
        using var install = new TempInstall();
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying("Seven hundred ninety-two tonnes, Commander."),
            RoundScriptedLlmProvider.Saying("Noted."));
        var (loop, _) = Build(TestSurface.For(install), provider);

        await RunAsync(loop, "Captain, how much fuel have we got");

        Assert.Empty(loop.History);

        await RunAsync(loop, ToShipAi);

        var prompt = provider.Requests[1].Prompt;
        var carried = Text(prompt.History[0]);

        Assert.Equal(ShipAiBrief, prompt.Persona);
        Assert.Contains("The Commander said \"Captain, how much fuel have we got\"", carried, StringComparison.Ordinal);
        Assert.Contains("Captain answered \"Seven hundred ninety-two tonnes, Commander.\"", carried, StringComparison.Ordinal);

        Assert.DoesNotContain(
            loop.History,
            message => message.Role == ConversationRole.Assistant
                       && Text(message).Contains("Seven hundred", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACalloutSpokenDuringTheCaptainsLineStillReachesTheShipAi()
    {
        using var install = new TempInstall();
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying("Seven hundred ninety-two tonnes, Commander."),
            RoundScriptedLlmProvider.Saying("Five hundred forty tonnes aboard."),
            RoundScriptedLlmProvider.Saying("Noted."));
        var (loop, _) = Build(TestSurface.For(install), provider);

        await RunAsync(loop, "Captain, how much fuel have we got");

        loop.Said("Hostile contact, Commander.");

        await RunAsync(loop, "and how much cargo");
        await RunAsync(loop, ToShipAi);

        Assert.DoesNotContain(
            provider.Requests[1].Prompt.History,
            message => Text(message).Contains("Hostile contact", StringComparison.Ordinal));

        Assert.Contains("Hostile contact, Commander.", Text(provider.Requests[2].Prompt.History[0]), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("That's all, Captain", true)]
    [InlineData("that’ll be all", true)]
    [InlineData("Thank you, Captain.", true)]
    [InlineData("Captain, dismissed", true)]
    [InlineData("carry on", true)]
    [InlineData("that's all the fuel we have?", false)]
    public void TheDismissalsAreReadWhateverThePunctuation(string said, bool dismissal) =>
        Assert.Equal(dismissal, CaptainLine.IsDismissal(said));
}
