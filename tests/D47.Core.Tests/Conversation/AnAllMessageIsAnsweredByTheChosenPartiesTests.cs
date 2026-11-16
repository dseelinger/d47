using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A comms message said to All reaches every party, a routing call chooses who answers, and they answer in turn.</summary>
public sealed class AnAllMessageIsAnsweredByTheChosenPartiesTests
{
    private const string Background = "background-model";

    private sealed class Party(string key, bool refuses = false) : ILine
    {
        public List<ConversationMessage> Transcript { get; } = [];

        public List<(CommsExchange Exchange, int? Own)> Heard { get; } = [];

        public string Brief => $"You are {key}, who handles the {key} desk. You answer briefly.";

        public Task<LineDecision> RouteAsync(CommsSelection selected, string input, CancellationToken cancellationToken)
        {
            if (selected.SubChannel?.Key != key)
            {
                return Task.FromResult<LineDecision>(new LineDecision.NotMine());
            }

            return Task.FromResult<LineDecision>(refuses
                ? new LineDecision.Refused($"{key} is beyond comms range.", TurnRoute.NoOneOnComms)
                : new LineDecision.Taken(new Speaker(VoiceRole.Comms, key, Brief, Transcript, OffersTools: false, Signal: 1), input.Trim()));
        }

        public void Overhear(CommsSelection party, CommsExchange exchange, int? ownReply)
        {
            if (party.SubChannel?.Key == key)
            {
                Heard.Add((exchange, ownReply));
            }
        }
    }

    private static CommsSelection All(CommsChannel channel, params string[] keys) =>
        new(channel, new CommsSubChannel(CommsChannels.AllKey, "All")) { Everyone = [.. keys.Select(key => new CommsSubChannel(key, key))] };

    private static TurnLoop Loop(RoundScriptedLlmProvider provider, Func<CommsSelection?> selected, params ILine[] lines)
    {
        var surface = TestSurface.For(new MemoryInstall());
        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider)
        {
            CommsSelected = selected,
            BackgroundModel = Background,
        };

        loop.Lines.AddRange(lines);
        return loop;
    }

    private static IReadOnlyList<LlmStreamEvent> Say(string text) => RoundScriptedLlmProvider.Saying(text);

    private static IReadOnlyList<LlmStreamEvent> Fail() => [new LlmStreamEvent.Failed("overloaded", Transient: false)];

    private static string Asked(LlmRequest request) => request.Prompt.History[^1].Text;

    private static List<string> Answerers(List<TurnEvent> events) => [.. events.OfType<TurnEvent.Addressed>().Select(addressed => addressed.Name)];

    [Fact]
    public async Task TheChosenPartiesAnswerInTurnAndEachLaterOneIsGivenTheEarlierReplies()
    {
        var provider = new RoundScriptedLlmProvider(
            Say("""{"answer": ["Vance", "Okafor"]}"""),
            Say("Pad two is yours."),
            Say("Fuel is on its way."));
        var loop = Loop(provider, () => All(CommsChannel.System, "Okafor", "Vance", "Reyes"), new Party("Okafor"), new Party("Vance"), new Party("Reyes"));

        var events = await OnComms.SayAsync(loop, "requesting docking");

        Assert.Equal(["Vance", "Okafor"], Answerers(events));
        Assert.Equal(
            ["Pad two is yours.", "Fuel is on its way."],
            events.OfType<TurnEvent.Completed>().Select(completed => completed.Result.Text));

        var routing = provider.Requests[0];
        Assert.Equal(Background, routing.Model);
        Assert.False(routing.ThinkingAllowed);
        Assert.Equal(LlmSampling.CommsRouting, routing.Sampling);
        Assert.NotNull(routing.ResponseSchema);
        Assert.Contains("requesting docking", Asked(routing), StringComparison.Ordinal);
        Assert.Contains("- Reyes: Reyes. You are Reyes, who handles the Reyes desk.", Asked(routing), StringComparison.Ordinal);
        Assert.DoesNotContain("You answer briefly.", Asked(routing), StringComparison.Ordinal);

        Assert.Equal("The Commander, to everyone on the system channel: \"requesting docking\"", Asked(provider.Requests[1]));
        Assert.Equal(
            "The Commander, to everyone on the system channel: \"requesting docking\"\n\nAlready answered:\n- Vance: \"Pad two is yours.\"\n"
            + "Add only what is yours to add; do not repeat them.",
            Asked(provider.Requests[2]));
    }

    [Fact]
    public async Task EveryCandidateHearsTheExchangeAndKnowsWhichReplyWasItsOwn()
    {
        var okafor = new Party("Okafor");
        var vance = new Party("Vance");
        var reyes = new Party("Reyes");
        var provider = new RoundScriptedLlmProvider(Say("""{"answer": ["Vance", "Okafor"]}"""), Say("Pad two."), Say("Fuel."));
        var loop = Loop(provider, () => All(CommsChannel.System, "Okafor", "Vance", "Reyes"), okafor, vance, reyes);

        await OnComms.SayAsync(loop, "requesting docking");

        var heard = Assert.Single(reyes.Heard);
        Assert.Null(heard.Own);
        Assert.Equal("requesting docking", heard.Exchange.Asked);
        Assert.Equal([("Vance", "Pad two."), ("Okafor", "Fuel.")], heard.Exchange.Replies.Select(reply => (reply.Name, reply.Said)));
        Assert.Equal(0, Assert.Single(vance.Heard).Own);
        Assert.Equal(1, Assert.Single(okafor.Heard).Own);
    }

    [Fact]
    public async Task APartyNotChosenRemembersTheMessageWhenAskedAlone()
    {
        var commander = new CommanderGameState(new CommanderIdentity("F1", "Tester"))
        {
            Carrier = new CarrierState { CallSign = "BNH-T2F", Name = "Sacred Fire", StarSystem = "Sol" },
        };
        var selected = All(CommsChannel.Carrier, CommsChannels.CaptainKey, CommsChannels.TowerKey);
        var provider = new RoundScriptedLlmProvider(
            Say($$"""{"answer": ["{{CommsChannels.CaptainKey}}"]}"""),
            Say("Understood, I will have her ready."),
            Say("In about an hour."));
        var loop = Loop(
            provider,
            () => selected,
            new CaptainLine(() => commander, () => "Sol", () => DateTimeOffset.UnixEpoch),
            new TowerLine(() => commander, () => "Sol", () => DateTimeOffset.UnixEpoch));

        var events = await OnComms.SayAsync(loop, "we jump to Colonia in an hour");
        Assert.Equal([NpcChatter.CaptainName], Answerers(events));

        selected = OnComms.Tower;
        await OnComms.SayAsync(loop, "when do we jump?");

        var tower = provider.Requests[^1];
        Assert.Equal("when do we jump?", Asked(tower));
        Assert.Contains("Commander: we jump to Colonia in an hour", tower.Prompt.Persona, StringComparison.Ordinal);
        Assert.Contains($"{NpcChatter.CaptainName}: Understood, I will have her ready.", tower.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReplyOverAWeakLinkIsPassedOnAsItCameThrough()
    {
        var commander = new CommanderGameState(new CommanderIdentity("F1", "Tester"))
        {
            Carrier = new CarrierState { CallSign = "BNH-T2F", Name = "Sacred Fire", StarSystem = "Sol" },
        };
        const string Full = "The carrier is fuelled, the jump is plotted, the crew are aboard and the tritium market is closed for the night.";
        var provider = new RoundScriptedLlmProvider(
            Say($$"""{"answer": ["{{CommsChannels.CaptainKey}}", "{{CommsChannels.TowerKey}}"]}"""),
            Say(Full),
            Say("Pads are clear."));
        Task<double?> Far(string here, string there, CancellationToken cancellationToken) => Task.FromResult<double?>(450);
        var loop = Loop(
            provider,
            () => All(CommsChannel.Carrier, CommsChannels.CaptainKey, CommsChannels.TowerKey),
            new CaptainLine(() => commander, () => "Achenar", () => DateTimeOffset.UnixEpoch, Far),
            new TowerLine(() => commander, () => "Achenar", () => DateTimeOffset.UnixEpoch, Far));

        var events = await OnComms.SayAsync(loop, "status report");

        var cameThrough = events.OfType<TurnEvent.Completed>().First().Result.Text;
        var heard = string.Concat(events.TakeWhile(turnEvent => turnEvent is not TurnEvent.Completed).OfType<TurnEvent.TextDelta>().Select(delta => delta.Text)).Trim();

        Assert.Contains(LinkSignal.Lost, heard, StringComparison.Ordinal);
        Assert.Equal(Full, cameThrough);

        var asked = Asked(provider.Requests[2]);
        Assert.Contains($"- {NpcChatter.CaptainName}: \"{heard}\"", asked, StringComparison.Ordinal);
        Assert.DoesNotContain(Full, asked, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeysThatAreNotCandidatesAreDroppedRepeatsAreDroppedAndAtMostThreeAnswer()
    {
        var provider = new RoundScriptedLlmProvider(
            Say("""{"answer": ["Nobody", "Okafor", "okafor", "Vance", "Reyes", "Kim"]}"""),
            Say("One."),
            Say("Two."),
            Say("Three."));
        var loop = Loop(
            provider,
            () => All(CommsChannel.System, "Kim", "Okafor", "Vance", "Reyes"),
            new Party("Kim"),
            new Party("Okafor"),
            new Party("Vance"),
            new Party("Reyes"));

        var events = await OnComms.SayAsync(loop, "anyone there");

        Assert.Equal(["Okafor", "Vance", "Reyes"], Answerers(events));
        Assert.Equal(4, provider.CallCount);
    }

    [Fact]
    public async Task AnEmptyOrFailedRoutingCallFallsBackToTheFirstCandidate()
    {
        foreach (var routing in new[] { Say("""{"answer": []}"""), Say("""{"answer": ["Nobody"]}"""), Say("Okafor, I think."), Fail() })
        {
            var provider = new RoundScriptedLlmProvider(routing, Say("Okafor here."));
            var loop = Loop(provider, () => All(CommsChannel.System, "Okafor", "Vance"), new Party("Okafor"), new Party("Vance"));

            var events = await OnComms.SayAsync(loop, "anyone there");

            Assert.Equal(["Okafor"], Answerers(events));
            Assert.Equal(2, provider.CallCount);
        }
    }

    [Fact]
    public async Task ARefusingPartyIsLeftOutAndALoneCandidateAnswersWithoutARoutingCall()
    {
        var refusing = new Party("Okafor", refuses: true);
        var provider = new RoundScriptedLlmProvider(Say("Vance here."));
        var loop = Loop(provider, () => All(CommsChannel.System, "Okafor", "Vance"), refusing, new Party("Vance"));

        var events = await OnComms.SayAsync(loop, "anyone there");

        Assert.Equal(["Vance"], Answerers(events));
        Assert.Equal(1, provider.CallCount);
        Assert.Equal("The Commander, to everyone on the system channel: \"anyone there\"", Asked(provider.Requests[0]));
        Assert.Empty(refusing.Heard);
    }

    [Fact]
    public async Task WhenEveryPartyRefusesTheCoreSaysTheFirstRefusal()
    {
        var provider = new RoundScriptedLlmProvider();
        var loop = Loop(provider, () => All(CommsChannel.System, "Okafor", "Vance"), new Party("Okafor", refuses: true), new Party("Vance", refuses: true));

        var events = await OnComms.SayAsync(loop, "anyone there");

        Assert.Empty(Answerers(events));
        Assert.Equal("Okafor is beyond comms range.", Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Text);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task AFailedReplyEndsTheExchange()
    {
        var okafor = new Party("Okafor");
        var vance = new Party("Vance");
        var provider = new RoundScriptedLlmProvider(Say("""{"answer": ["Okafor", "Vance"]}"""), Fail());
        var loop = Loop(provider, () => All(CommsChannel.System, "Okafor", "Vance"), okafor, vance);

        var events = await OnComms.SayAsync(loop, "anyone there");

        Assert.Equal(["Okafor"], Answerers(events));
        Assert.Equal(2, provider.CallCount);
        Assert.Empty(vance.Heard);
    }

    [Fact]
    public async Task TheRoutingCallIsShownTheLastExchangeOnTheChannel()
    {
        var provider = new RoundScriptedLlmProvider(
            Say("""{"answer": ["Okafor"]}"""),
            Say("Pad two is yours."),
            Say("""{"answer": ["Vance"]}"""),
            Say("Fuel is on its way."));
        var loop = Loop(provider, () => All(CommsChannel.System, "Okafor", "Vance"), new Party("Okafor"), new Party("Vance"));

        await OnComms.SayAsync(loop, "requesting docking");

        Assert.DoesNotContain("The last message said to everyone", Asked(provider.Requests[0]), StringComparison.Ordinal);

        await OnComms.SayAsync(loop, "and fuel");

        Assert.Contains(
            "The last message said to everyone on this channel, oldest first:\nCommander: requesting docking\nOkafor: Pad two is yours.",
            Asked(provider.Requests[2]),
            StringComparison.Ordinal);
    }
}
