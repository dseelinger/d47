using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>"Vance, ..." is answered by the hired pilot on the intercom, who keeps the line until dismissed.</summary>
public class TheCrewAnswersOnTheIntercomTests
{
    private const string ShipAi = "Warden";

    private const string ShipAiBrief = "You are the ship's AI aboard this vessel.";

    private const string VanceBrief = "You are Vance";

    private static readonly ShipCrew Roster = new()
    {
        Members = [new CrewMember("Vance", CrewId: 1, CombatRank: "Dangerous", Active: true)],
    };

    private static (TurnLoop Loop, CrewLine Line) Build(
        TestSurface surface,
        ILlmProvider provider,
        ShipCrew? roster = null)
    {
        var line = new CrewLine(() => roster ?? Roster, () => "Warden's Reach", () => ShipAi);

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
    public async Task ThePilotByNameIsAnsweredWithNoTools()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Holding steady, Commander.");
        var (loop, line) = Build(TestSurface.For(install), provider);

        var events = await RunAsync(loop, "Vance, how is the fighter");

        var addressed = Assert.Single(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(VoiceRole.Crew, addressed.Role);
        Assert.Equal("Vance", addressed.Name);

        var prompt = provider.LastRequest!.Prompt;
        Assert.Contains(VanceBrief, prompt.Persona, StringComparison.Ordinal);
        Assert.Equal("how is the fighter", Text(Assert.Single(prompt.History)));
        Assert.Empty(prompt.Tools);
        Assert.True(line.IsOpen);
    }

    [Fact]
    public async Task TheNextTurnWithNoNameReachesThePilotToo()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var (loop, _) = Build(TestSurface.For(install), provider);

        await RunAsync(loop, "Vance, how is the fighter");
        var next = await RunAsync(loop, "and are you low on ammo");

        Assert.Equal(VoiceRole.Crew, Assert.Single(next.OfType<TurnEvent.Addressed>()).Role);
        Assert.Contains(VanceBrief, provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NamingTheCaptainClosesThePilotsLineAndOpensHers()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var surface = TestSurface.For(install);
        var (loop, crewLine) = Build(surface, provider);

        loop.Lines.Add(new CaptainLine(
            () => new CarrierState { CallSign = "BNH-T2F", Name = "Sacred Fire", StarSystem = "Sol" },
            () => "Sol",
            () => ShipAi));

        await RunAsync(loop, "Vance, how is the fighter");
        var next = await RunAsync(loop, "Captain, fuel");

        var addressed = Assert.Single(next.OfType<TurnEvent.Addressed>());
        Assert.Equal(VoiceRole.CarrierCaptain, addressed.Role);
        Assert.False(crewLine.IsOpen);
    }

    [Fact]
    public async Task ThatsAllReturnsTheTurnToTheShipAi()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var (loop, line) = Build(TestSurface.For(install), provider);

        await RunAsync(loop, "Vance, how is the fighter");
        await RunAsync(loop, "That's all");

        Assert.False(line.IsOpen);

        var after = await RunAsync(loop, $"{ShipAi}, summarise that exchange for me");

        Assert.Empty(after.OfType<TurnEvent.Addressed>());
    }

    [Fact]
    public async Task TheShipAiOverhearsThePilotWithoutTakingTheAnswerAsItsOwn()
    {
        using var install = new TempInstall();
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying("Holding steady, Commander."),
            RoundScriptedLlmProvider.Saying("Noted."));
        var (loop, _) = Build(TestSurface.For(install), provider);

        await RunAsync(loop, "Vance, how is the fighter");

        Assert.Empty(loop.History);

        await RunAsync(loop, $"{ShipAi}, summarise that exchange for me");

        var prompt = provider.Requests[1].Prompt;
        var carried = Text(prompt.History[0]);

        Assert.Equal(ShipAiBrief, prompt.Persona);
        Assert.Contains("The Commander said \"Vance, how is the fighter\"", carried, StringComparison.Ordinal);
        Assert.Contains("Vance answered \"Holding steady, Commander.\"", carried, StringComparison.Ordinal);

        Assert.DoesNotContain(
            loop.History,
            message => message.Role == ConversationRole.Assistant
                       && Text(message).Contains("Holding steady", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ANameOffTheRosterReachesTheShipAi()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("I don't have a crew member by that name aboard.");
        var (loop, line) = Build(TestSurface.For(install), provider);

        var events = await RunAsync(loop, "Jameson, how is the fighter");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.False(line.IsOpen);
        Assert.Equal(ShipAiBrief, provider.LastRequest!.Prompt.Persona);
    }
}
