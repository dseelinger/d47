using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Characters;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>Strangers, the targeted pilot and custom characters read none of the running story's hidden layer; crew and cast read it.</summary>
public class OnlyThoseInTheStoryReadItsHiddenLayerTests
{
    private const string Layer = "THE-HIDDEN-LAYER";

    private static Speaker Taken(LineDecision decision) => Assert.IsType<LineDecision.Taken>(decision).Speaker;

    [Fact]
    public async Task AnInventedStrangerReadsNone()
    {
        var line = new ChatterLine(() => "Anlave");
        line.Heard(new NpcChatterLine("Rook", "Anyone near the slot?"), 1);

        var speaker = Taken(await line.RouteAsync(OnComms.System("Rook"), "hello", TestContext.Current.CancellationToken));

        Assert.Null(speaker.HiddenStory);
    }

    [Fact]
    public async Task ARealCommsNpcReadsNone()
    {
        var roster = new NpcsHeardHere();
        roster.Heard(
            new Announcement(IncomingMessages.CannedKeyPrefix + "Pirate", "Hand over the cargo.") { Speaker = "Vik", CommsChannel = "npc" },
            null,
            DateTimeOffset.UnixEpoch);
        var line = new ChatterLine(() => "Anlave", roster: () => roster);

        var speaker = Taken(await line.RouteAsync(OnComms.System("Vik"), "what do you want", TestContext.Current.CancellationToken));

        Assert.Null(speaker.HiddenStory);
    }

    [Fact]
    public async Task ATargetedPilotReadsNone()
    {
        var pilot = new TargetedShip("Cobra Mk III", "Jessa Corvane", "Dangerous", "Federation", "Wanted");
        var line = new TargetLine(() => pilot);

        var speaker = Taken(await line.RouteAsync(OnComms.Target("Jessa Corvane"), "who are you", TestContext.Current.CancellationToken));

        Assert.Null(speaker.HiddenStory);
    }

    [Fact]
    public async Task ACustomCharacterReadsNone()
    {
        var juno = new CustomCharacter("bbbb0002", "Juno", null, "warm", true, null);
        var line = new CharacterLine(() => [juno], () => "Anlave");

        var speaker = Taken(await line.RouteAsync(OnComms.Character(juno.Id, juno.Name), "hi", TestContext.Current.CancellationToken));

        Assert.Null(speaker.HiddenStory);
    }

    [Fact]
    public async Task HiredCrewReadItAsGiven()
    {
        var crew = new ShipCrew { Members = [new CrewMember("Vance", CrewId: 1, CombatRank: 6, Active: true)] };
        var line = new CrewLine(() => crew, () => "Warden's Reach", () => Layer);

        var speaker = Taken(await line.RouteAsync(OnComms.Crew(1, "Vance"), "hi", TestContext.Current.CancellationToken));

        Assert.Equal(Layer, speaker.HiddenStory!());
    }

    [Fact]
    public async Task ACastMemberReadsItOnlyAsFarAsTheirCharacterWouldKnow()
    {
        var mara = new AdventureSpeaker("mara", "Mara Quell", "A salvager.");
        var line = new CastLine(_ => mara, () => Layer);

        var speaker = Taken(await line.RouteAsync(OnComms.Cast(mara.Id, mara.Name), "hi", TestContext.Current.CancellationToken));

        var brief = speaker.HiddenStory!();
        Assert.Contains(Layer, brief, StringComparison.Ordinal);
        Assert.Contains(StoryClues.CastKnowledge, brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASpeakerWithNoReadingGetsNoneFromTheShipAi()
    {
        var surface = TestSurface.For(new MemoryInstall());
        var provider = FakeLlmProvider.Answering("Copy that.");
        var pilot = new TargetedShip("Cobra Mk III", "Jessa Corvane", "Dangerous", "Federation", "Wanted");

        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider)
        {
            Persona = "You are the ship's AI.",
            HiddenStory = () => Layer,
            CommsSelected = () => OnComms.Target("Jessa Corvane"),
        };
        loop.Lines.Add(new TargetLine(() => pilot));

        await OnComms.SayAsync(loop, "who are you");

        Assert.Null(provider.LastRequest!.Prompt.HiddenStory);
    }

    [Fact]
    public async Task ATurnWithNoSpeakerGetsTheShipAisReading()
    {
        var surface = TestSurface.For(new MemoryInstall());
        var provider = FakeLlmProvider.Answering("Copy that.");

        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider)
        {
            Persona = "You are the ship's AI.",
            HiddenStory = () => Layer,
        };

        await OnComms.AskAsync(loop, "tell me about the system");

        Assert.Equal(Layer, provider.LastRequest!.Prompt.HiddenStory);
    }
}
