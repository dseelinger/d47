using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>The carrier's captain answers every comms turn while the CARRIER channel selects them, and no primary turn.</summary>
public class TheCaptainAnswersOnTheCarrierChannelTests
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

    private static TurnLoop Build(
        TestSurface surface,
        ILlmProvider provider,
        Func<string, string, CancellationToken, Task<double?>>? distance = null,
        CarrierState? carrier = null)
    {
        var line = new CaptainLine(
            () => new CommanderGameState(new CommanderIdentity("F1", "Tester")) { Carrier = carrier ?? Owned },
            () => "Sol",
            () => DateTimeOffset.UnixEpoch,
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
            ShipAiNames = () => [ShipAi],
            CommsSelected = () => OnComms.Captain,
        };

        loop.Lines.Add(line);

        return loop;
    }

    private static string Text(ConversationMessage message) =>
        string.Join(' ', message.Content.OfType<ConversationContent.Text>().Select(part => part.Value));

    [Fact]
    public async Task ACommsTurnIsAnsweredWithTheCaptainsBrief()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("Seven hundred ninety-two tonnes, Commander.");
        var loop = Build(TestSurface.For(install), provider);

        var events = await OnComms.SayAsync(loop, "how much fuel have we got");

        var addressed = Assert.Single(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(VoiceRole.CarrierCaptain, addressed.Role);
        Assert.IsType<TurnEvent.Routed>(events[events.IndexOf(addressed) + 1]);

        var prompt = provider.LastRequest!.Prompt;
        Assert.Contains(CaptainBrief, prompt.Persona, StringComparison.Ordinal);
        Assert.Equal("how much fuel have we got", Text(Assert.Single(prompt.History)));
    }

    [Fact]
    public async Task EveryCommsTurnIsTheCaptainsWhileSelected()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var loop = Build(TestSurface.For(install), provider);

        await OnComms.SayAsync(loop, "how much fuel have we got");
        var next = await OnComms.SayAsync(loop, "and how much cargo");

        Assert.Equal(VoiceRole.CarrierCaptain, Assert.Single(next.OfType<TurnEvent.Addressed>()).Role);
        Assert.Contains(CaptainBrief, provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
        Assert.Equal(3, provider.LastRequest.Prompt.History.Count);
    }

    [Fact]
    public async Task ADismissalDoesNotCloseTheChannel()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var loop = Build(TestSurface.For(install), provider);

        await OnComms.SayAsync(loop, "That's all, Captain");
        var after = await OnComms.SayAsync(loop, "one more thing");

        Assert.Equal(VoiceRole.CarrierCaptain, Assert.Single(after.OfType<TurnEvent.Addressed>()).Role);
    }

    [Fact]
    public async Task APrimaryTurnNamingTheCaptainReachesTheCore()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var loop = Build(TestSurface.For(install), provider);

        var events = await OnComms.AskAsync(loop, "Captain, how much fuel have we got");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(ShipAiBrief, provider.LastRequest!.Prompt.Persona);
    }

    [Fact]
    public async Task BeyondRangeTheShipAiSaysSoWithoutAModel()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var loop = Build(TestSurface.For(install), provider, (_, _, _) => Task.FromResult<double?>(600));

        var events = await OnComms.SayAsync(loop, "how much fuel have we got");

        var result = Assert.Single(events.OfType<TurnEvent.Completed>()).Result;

        Assert.Equal(TurnRoute.CarrierOutOfRange, result.Route);
        Assert.Contains("600", result.Text, StringComparison.Ordinal);
        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task ADistanceThatThrowsGivesACaptainsTurn()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var loop = Build(
            TestSurface.For(install),
            provider,
            (_, _, _) => throw new HttpRequestException("the galaxy service went away"));

        var events = await OnComms.SayAsync(loop, "how much fuel have we got");

        Assert.Single(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(TurnRoute.Model, Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Route);
    }

    [Fact]
    public async Task ADistanceNobodyKnowsGivesACaptainsTurn()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var loop = Build(TestSurface.For(install), provider, (_, _, _) => Task.FromResult<double?>(null));

        var events = await OnComms.SayAsync(loop, "how much fuel have we got");

        Assert.Single(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(TurnRoute.Model, Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Route);
    }

    [Fact]
    public async Task WithNoCarrierOwnedNoOneIsOnTheChannel()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var loop = Build(TestSurface.For(install), provider, carrier: CarrierState.None);

        var events = await OnComms.SayAsync(loop, "status");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Equal("No one on Carrier.", Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Text);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task AProtectedToolTheCaptainCallsIsRefused()
    {
        var install = new MemoryInstall();
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling("call-1", "accept_proposal", "{}"),
            RoundScriptedLlmProvider.Saying("I can't do that from here, Commander."));
        var loop = Build(TestSurface.For(install), provider);

        var events = await OnComms.SayAsync(loop, "accept the checklist proposal");

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
        var install = new MemoryInstall();
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying("Seven hundred ninety-two tonnes, Commander."),
            RoundScriptedLlmProvider.Saying("Noted."));
        var loop = Build(TestSurface.For(install), provider);

        await OnComms.SayAsync(loop, "how much fuel have we got");

        Assert.Empty(loop.History);

        await OnComms.AskAsync(loop, ToShipAi);

        var prompt = provider.Requests[1].Prompt;
        var carried = Text(prompt.History[0]);

        Assert.Equal(ShipAiBrief, prompt.Persona);
        Assert.Contains("The Commander said \"how much fuel have we got\"", carried, StringComparison.Ordinal);
        Assert.Contains("Captain answered \"Seven hundred ninety-two tonnes, Commander.\"", carried, StringComparison.Ordinal);

        Assert.DoesNotContain(
            loop.History,
            message => message.Role == ConversationRole.Assistant
                       && Text(message).Contains("Seven hundred", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACalloutSpokenDuringTheCaptainsLineStillReachesTheShipAi()
    {
        var install = new MemoryInstall();
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying("Seven hundred ninety-two tonnes, Commander."),
            RoundScriptedLlmProvider.Saying("Five hundred forty tonnes aboard."),
            RoundScriptedLlmProvider.Saying("Noted."));
        var loop = Build(TestSurface.For(install), provider);

        await OnComms.SayAsync(loop, "how much fuel have we got");

        loop.Said("Hostile contact, Commander.");

        await OnComms.SayAsync(loop, "and how much cargo");
        await OnComms.AskAsync(loop, ToShipAi);

        Assert.DoesNotContain(
            provider.Requests[1].Prompt.History,
            message => Text(message).Contains("Hostile contact", StringComparison.Ordinal));

        Assert.Contains("Hostile contact, Commander.", Text(provider.Requests[2].Prompt.History[0]), StringComparison.Ordinal);
    }
}
