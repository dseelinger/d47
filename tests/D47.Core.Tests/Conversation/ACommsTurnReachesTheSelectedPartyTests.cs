using D47.Core.Adventures;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Characters;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// With every line registered, a comms turn reaches the party the selection names on each channel, and an empty
/// selection is answered by the ship's AI with a fixed line and no model call.
/// </summary>
public class ACommsTurnReachesTheSelectedPartyTests
{
    private const string ShipAiBrief = "You are the ship's AI aboard this vessel.";

    private static readonly AdventureSpeaker Mara = new("mara", "Mara Quell", "A salvager.");

    private static readonly CustomCharacter Juno = new("bbbb0002", "Juno", null, "warm", true, null);

    private static readonly CommanderGameState Commander = new(new CommanderIdentity("F1", "Tester"))
    {
        Carrier = new CarrierState { CallSign = "BNH-T2F", Name = "Sacred Fire", StarSystem = "Sol" },
    };

    private static readonly ShipCrew Crew = new() { Members = [new CrewMember("Vance", CrewId: 1, CombatRank: 6, Active: true)] };

    private static readonly TargetedShip Target = new("Cobra Mk III", "Jessa Corvane", "Dangerous", "Federation", "Clean");

    private static (TurnLoop Loop, FakeLlmProvider Provider) Build(CommsSelection? selected)
    {
        var surface = TestSurface.For(new MemoryInstall());
        var provider = FakeLlmProvider.Answering("Copy that.");
        var roster = new NpcsHeardHere();
        var said = new NpcChatterLine("Odalys Fenn", "Fine flying, Commander.");

        roster.Heard(
            new Announcement(NpcChatter.LineKey, said.Text) { Speaker = said.Name, Invented = new NpcChatterHeard(said, 7, Hail: true) },
            null,
            DateTimeOffset.UnixEpoch);

        var chatter = new ChatterLine(() => "Sol", roster: () => roster);
        chatter.Heard(said, 7);

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
            ShipAiNames = () => ["Warden"],
            CommsSelected = () => selected,
        };

        loop.Lines.AddRange(
        [
            new CaptainLine(() => Commander, () => "Sol", () => DateTimeOffset.UnixEpoch),
            new TowerLine(() => Commander, () => "Sol", () => DateTimeOffset.UnixEpoch),
            new CharacterLine(() => [Juno], () => "Sol"),
            new CrewLine(() => Crew, () => "Warden's Reach"),
            chatter,
            new TargetLine(() => Target),
            new NarratorLine(() => null),
            new CastLine(castId => castId == Mara.Id ? Mara : null, () => null),
        ]);

        return (loop, provider);
    }

    public static TheoryData<string, VoiceRole, string> Parties => new()
    {
        { "captain", VoiceRole.CarrierCaptain, NpcChatter.CaptainName },
        { "tower", VoiceRole.TowerControl, NpcChatter.TowerName },
        { "target", VoiceRole.Comms, "Jessa Corvane" },
        { "system", VoiceRole.Comms, "Odalys Fenn" },
        { "cast", VoiceRole.Crew, "Mara Quell" },
        { "narrator", VoiceRole.Narrator, NarratorLine.Name },
        { "crew", VoiceRole.Crew, "Vance" },
        { "character", VoiceRole.Crew, "Juno" },
    };

    private static CommsSelection Selection(string party) => party switch
    {
        "captain" => OnComms.Captain,
        "tower" => OnComms.Tower,
        "target" => OnComms.Target("Jessa Corvane"),
        "system" => OnComms.System("Odalys Fenn"),
        "cast" => OnComms.Cast(Mara.Id, Mara.Name),
        "narrator" => OnComms.Narrator,
        "crew" => OnComms.Crew(1, "Vance"),
        "character" => OnComms.Character(Juno.Id, Juno.Name),
        _ => throw new ArgumentOutOfRangeException(nameof(party)),
    };

    [Theory]
    [MemberData(nameof(Parties))]
    public async Task EachChannelsPartyAnswersTheCommsKey(string party, VoiceRole role, string name)
    {
        var (loop, provider) = Build(Selection(party));

        var events = await OnComms.SayAsync(loop, "are you receiving");

        var addressed = Assert.Single(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(role, addressed.Role);
        Assert.Equal(name, addressed.Name);
        Assert.DoesNotContain(ShipAiBrief, provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Parties))]
    public async Task ThePrimaryKeyReachesTheCoreWhoeverIsSelected(string party, VoiceRole role, string name)
    {
        _ = role;
        var (loop, provider) = Build(Selection(party));

        var events = await OnComms.AskAsync(loop, $"{name}, are you receiving");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(ShipAiBrief, provider.LastRequest!.Prompt.Persona);
    }

    [Theory]
    [InlineData(CommsChannel.Carrier, "No one on Carrier.")]
    [InlineData(CommsChannel.System, "No one on System.")]
    [InlineData(CommsChannel.Story, "No one on Story.")]
    public async Task NoOneOnTheChannelIsAnsweredWithNoModelCall(CommsChannel channel, string said)
    {
        var (loop, provider) = Build(OnComms.NoOne(channel));

        var events = await OnComms.SayAsync(loop, "are you receiving");
        var result = Assert.Single(events.OfType<TurnEvent.Completed>()).Result;

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(said, result.Text);
        Assert.Equal(TurnRoute.NoOneOnComms, result.Route);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task WithNoChannelAvailableTheShipSaysSoWithNoModelCall()
    {
        var (loop, provider) = Build(selected: null);

        var result = Assert.Single((await OnComms.SayAsync(loop, "are you receiving")).OfType<TurnEvent.Completed>()).Result;

        Assert.Equal(TurnLoop.NoChannel, result.Text);
        Assert.Equal(0, provider.CallCount);
    }
}
