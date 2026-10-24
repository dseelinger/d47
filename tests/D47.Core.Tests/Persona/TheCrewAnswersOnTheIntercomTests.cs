using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>A hired pilot answers on the intercom every comms turn while the CREW channel selects them.</summary>
public class TheCrewAnswersOnTheIntercomTests
{
    private const string ShipAi = "Warden";

    private const string ShipAiBrief = "You are the ship's AI aboard this vessel.";

    private const string VanceBrief = "You are Vance";

    private static readonly ShipCrew Roster = new()
    {
        Members = [new CrewMember("Vance", CrewId: 1, CombatRank: "Dangerous", Active: true)],
    };

    private static TurnLoop Build(TestSurface surface, ILlmProvider provider, CommsSelection? selected = null)
    {
        var line = new CrewLine(() => Roster, () => "Warden's Reach");

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
            CommsSelected = () => selected ?? OnComms.Crew(1, "Vance"),
        };

        loop.Lines.Add(line);

        return loop;
    }

    private static string Text(ConversationMessage message) =>
        string.Join(' ', message.Content.OfType<ConversationContent.Text>().Select(part => part.Value));

    [Fact]
    public async Task ThePilotIsAnsweredWithNoTools()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("Holding steady, Commander.");
        var loop = Build(TestSurface.For(install), provider);

        var events = await OnComms.SayAsync(loop, "how is the fighter");

        var addressed = Assert.Single(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(VoiceRole.Crew, addressed.Role);
        Assert.Equal("Vance", addressed.Name);

        var prompt = provider.LastRequest!.Prompt;
        Assert.Contains(VanceBrief, prompt.Persona, StringComparison.Ordinal);
        Assert.Equal("how is the fighter", Text(Assert.Single(prompt.History)));
        Assert.Empty(prompt.Tools);
    }

    [Fact]
    public async Task EveryCommsTurnReachesThePilotWhileSelected()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var loop = Build(TestSurface.For(install), provider);

        await OnComms.SayAsync(loop, "how is the fighter");
        var next = await OnComms.SayAsync(loop, "That's all");

        Assert.Equal(VoiceRole.Crew, Assert.Single(next.OfType<TurnEvent.Addressed>()).Role);
        Assert.Contains(VanceBrief, provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APrimaryTurnNamingThePilotReachesTheShipAi()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var loop = Build(TestSurface.For(install), provider);

        var events = await OnComms.AskAsync(loop, "Vance, how is the fighter");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(ShipAiBrief, provider.LastRequest!.Prompt.Persona);
    }

    [Fact]
    public async Task TheShipAiOverhearsThePilotWithoutTakingTheAnswerAsItsOwn()
    {
        var install = new MemoryInstall();
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying("Holding steady, Commander."),
            RoundScriptedLlmProvider.Saying("Noted."));
        var loop = Build(TestSurface.For(install), provider);

        await OnComms.SayAsync(loop, "how is the fighter");

        Assert.Empty(loop.History);

        await OnComms.AskAsync(loop, $"{ShipAi}, summarise that exchange for me");

        var prompt = provider.Requests[1].Prompt;
        var carried = Text(prompt.History[0]);

        Assert.Equal(ShipAiBrief, prompt.Persona);
        Assert.Contains("The Commander said \"how is the fighter\"", carried, StringComparison.Ordinal);
        Assert.Contains("Vance answered \"Holding steady, Commander.\"", carried, StringComparison.Ordinal);

        Assert.DoesNotContain(
            loop.History,
            message => message.Role == ConversationRole.Assistant
                       && Text(message).Contains("Holding steady", StringComparison.Ordinal));
    }

    [Fact]
    public async Task APilotOffTheRosterIsNoOne()
    {
        var install = new MemoryInstall();
        var provider = FakeLlmProvider.Answering("Aye, Commander.");
        var loop = Build(TestSurface.For(install), provider, OnComms.Crew(2, "Jameson"));

        var events = await OnComms.SayAsync(loop, "how is the fighter");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Equal("No one on Crew.", Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Text);
        Assert.Equal(0, provider.CallCount);
    }
}
