using D47.App.Panel;
using D47.App.Voice;
using D47.Core.Callouts;
using D47.Core.Characters;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Each input the host holds opens its comms channel, and losing it closes the channel again (#1610).</summary>
public sealed class TheCommsChannelsFollowTheGameTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    private static readonly string[] Login =
    [
        """{"timestamp":"2026-10-24T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
        """{"timestamp":"2026-10-24T09:00:01Z","event":"Loadout","Ship":"anaconda","Modules":[],"ShipName":"Sacred Wings","ShipIdent":"SW-01"}""",
    ];

    private const string Carrier =
        """{"timestamp":"2026-10-24T09:01:00Z","event":"CarrierStats","CarrierID":3700000001,"Callsign":"BNH-T2F","Name":"GREYBEARD DELTA"}""";

    private const string Target =
        """{"timestamp":"2026-10-24T09:02:00Z","event":"ShipTargeted","TargetLocked":true,"Ship":"cobramkiii","Ship_Localised":"Cobra Mk III","ScanStage":3,"PilotName":"$npc_name_decorate:#name=Mara Okafor;","PilotName_Localised":"Mara Okafor"}""";

    private const string TargetLost =
        """{"timestamp":"2026-10-24T09:03:00Z","event":"ShipTargeted","TargetLocked":false}""";

    private const string Hired =
        """{"timestamp":"2026-10-24T09:04:00Z","event":"CrewHire","Name":"Samira Voss","CrewID":1,"CombatRank":"Dangerous"}""";

    private const string Active =
        """{"timestamp":"2026-10-24T09:04:01Z","event":"CrewAssign","Name":"Samira Voss","CrewID":1,"Role":"Active"}""";

    private const string ShoreLeave =
        """{"timestamp":"2026-10-24T09:05:00Z","event":"CrewAssign","Name":"Samira Voss","CrewID":1,"Role":"OnShoreLeave"}""";

    private const string OtherCommander =
        """{"timestamp":"2026-10-24T10:00:00Z","event":"Commander","FID":"F2","Name":"Kestrel"}""";

    private static readonly NpcHeard Npc = new("Ida Rook", NpcKind.Invented, null, null, Start);

    private static readonly CustomCharacter Character = new("0a1b2c3d", "Mara Quill", null, null, true, null);

    private static RunningCast Story() => new(
        "salt-road",
        "Salt Road",
        [
            new StoryCastMember(
                "salt-road",
                "Salt Road",
                new StorySpeakerShown("wren", "Wren Calloway", "edge", "voice", "salt-road.wren") { Primary = true },
                new PinnedVoice("edge", "voice"),
                new PinnedVoice("edge", "voice")),
        ]);

    private static GameStateStore Game(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in Login.Concat(lines))
        {
            Apply(store, line);
        }

        return store;
    }

    private static void Apply(GameStateStore store, string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        store.Apply(parsed!);
    }

    private static CommsChannels Fed(GameStateStore game, IReadOnlyList<NpcHeard>? heard = null, RunningCast? story = null, IReadOnlyList<CustomCharacter>? characters = null)
    {
        var channels = new CommsChannels();
        channels.Update(CommsFeed.Inputs(game.Active, heard ?? [], story, characters ?? []));
        return channels;
    }

    [Fact]
    public void NothingHeldOpensNoChannel() => Assert.Empty(Fed(Game()).Available);

    [Fact]
    public void AnOwnedCarrierOpensCarrier() =>
        Assert.Equal([CommsChannel.Carrier], Fed(Game(Carrier)).Available);

    [Fact]
    public void ATargetedNpcOpensTargetUnderTheirName()
    {
        var channels = Fed(Game(Target));

        Assert.Equal([CommsChannel.Target], channels.Available);
        Assert.Equal("Mara Okafor", Assert.Single(channels.SubChannels(CommsChannel.Target)).Name);
    }

    [Fact]
    public void AHeardNpcOpensSystem()
    {
        var channels = Fed(Game(), heard: [Npc]);

        Assert.Equal([CommsChannel.System], channels.Available);
        Assert.Equal("Ida Rook", Assert.Single(channels.SubChannels(CommsChannel.System)).Name);
    }

    [Fact]
    public void ARunningStoryOpensStoryWithItsIntroducedMember()
    {
        var channels = Fed(Game(), story: Story());

        Assert.Equal([CommsChannel.Story], channels.Available);
        Assert.Equal(new CommsSubChannel("wren", "Wren Calloway"), Assert.Single(channels.SubChannels(CommsChannel.Story)));
    }

    [Fact]
    public void AnActiveHiredPilotOpensCrew()
    {
        var channels = Fed(Game(Hired, Active));

        Assert.Equal([CommsChannel.Crew], channels.Available);
        Assert.Equal("Samira Voss", Assert.Single(channels.SubChannels(CommsChannel.Crew)).Name);
    }

    [Fact]
    public void AHiredPilotOnShoreLeaveOpensNothing() => Assert.Empty(Fed(Game(Hired)).Available);

    [Fact]
    public void ACustomCharacterOpensCharacters()
    {
        var channels = Fed(Game(), characters: [Character]);

        Assert.Equal([CommsChannel.Characters], channels.Available);
        Assert.Equal("Mara Quill", Assert.Single(channels.SubChannels(CommsChannel.Characters)).Name);
    }

    [Fact]
    public void LosingEachInputClosesItsChannel()
    {
        var game = Game(Carrier, Target, Hired, Active);
        var channels = new CommsChannels();

        channels.Update(CommsFeed.Inputs(game.Active, [Npc], Story(), [Character]));
        Assert.Equal(Enum.GetValues<CommsChannel>(), channels.Available);

        Apply(game, TargetLost);
        channels.Update(CommsFeed.Inputs(game.Active, [Npc], Story(), [Character]));
        Assert.DoesNotContain(CommsChannel.Target, channels.Available);

        Apply(game, ShoreLeave);
        channels.Update(CommsFeed.Inputs(game.Active, [Npc], Story(), [Character]));
        Assert.DoesNotContain(CommsChannel.Crew, channels.Available);

        channels.Update(CommsFeed.Inputs(game.Active, [], Story(), [Character]));
        Assert.DoesNotContain(CommsChannel.System, channels.Available);

        channels.Update(CommsFeed.Inputs(game.Active, [], null, [Character]));
        Assert.DoesNotContain(CommsChannel.Story, channels.Available);

        channels.Update(CommsFeed.Inputs(game.Active, [], null, []));
        Assert.Equal([CommsChannel.Carrier], channels.Available);

        Apply(game, OtherCommander);
        channels.Update(CommsFeed.Inputs(game.Active, [], null, []));
        Assert.Empty(channels.Available);
    }
}
