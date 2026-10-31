using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Characters;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>A speaker who is not aboard the Commander's ship is told the system and the station, and nothing of the ship.</summary>
public class ASpeakerOffTheShipKnowsOnlyWhereTheCommanderIsTests
{
    private const string System = "Anlave";

    private const string Station = "Suri Park";

    private static readonly DateTimeOffset Now = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] ShipDetails = ["fuel", "cargo", "hull", "tonnes"];

    private static Speaker Taken(LineDecision decision) => Assert.IsType<LineDecision.Taken>(decision).Speaker;

    private static void KnowsOnlyWhereTheCommanderIs(Speaker speaker)
    {
        var state = Assert.IsType<Func<string?>>(speaker.LiveState)();

        Assert.NotNull(state);
        Assert.Contains(System, state, StringComparison.Ordinal);
        Assert.Contains(Station, state, StringComparison.Ordinal);

        foreach (var detail in ShipDetails)
        {
            Assert.DoesNotContain(detail, state, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task AnInventedStrangerKnowsOnlyWhereTheCommanderIs()
    {
        var line = new ChatterLine(() => System, station: () => Station);
        line.Heard(new NpcChatterLine("Rook", "Anyone near the slot?"), 1);

        var speaker = Taken(await line.RouteAsync(OnComms.System("Rook"), "hello", TestContext.Current.CancellationToken));

        KnowsOnlyWhereTheCommanderIs(speaker);
    }

    [Fact]
    public async Task ARealCommsNpcKnowsOnlyWhereTheCommanderIsAndHasJustCalledOut()
    {
        var roster = new NpcsHeardHere();
        const string theirWords = "Hand over the cargo or burn, Commander.";
        roster.Heard(new Announcement(IncomingMessages.CannedKeyPrefix + "Pirate", theirWords) { Speaker = "Vik", CommsChannel = "npc" }, null, Now);
        var line = new ChatterLine(() => System, roster: () => roster, station: () => Station);

        var speaker = Taken(await line.RouteAsync(OnComms.System("Vik"), "what do you want", TestContext.Current.CancellationToken));

        KnowsOnlyWhereTheCommanderIs(speaker);
        Assert.Contains("just called out on the open channel", speaker.Brief, StringComparison.Ordinal);
        Assert.DoesNotContain("burn", speaker.Brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATargetedPilotKnowsOnlyWhereTheCommanderIs()
    {
        var pilot = new TargetedShip("Cobra Mk III", "Jessa Corvane", "Dangerous", "Federation", "Wanted");
        var line = new TargetLine(() => pilot, system: () => System, station: () => Station);

        var speaker = Taken(await line.RouteAsync(OnComms.Target("Jessa Corvane"), "who are you", TestContext.Current.CancellationToken));

        KnowsOnlyWhereTheCommanderIs(speaker);
    }

    [Fact]
    public async Task ACharacterNotAboardKnowsOnlyWhereTheCommanderIs()
    {
        var remote = new CustomCharacter("bbbb0002", "Juno", null, "warm", false, "Colonia");
        var line = new CharacterLine(() => [remote], () => System, station: () => Station);

        var speaker = Taken(await line.RouteAsync(OnComms.Character(remote.Id, remote.Name), "hi", TestContext.Current.CancellationToken));

        KnowsOnlyWhereTheCommanderIs(speaker);
    }

    [Fact]
    public async Task ACharacterAboardReadsTheShipsLiveState()
    {
        var aboard = new CustomCharacter("aaaa0001", "Mara", null, "gruff", true, null);
        var line = new CharacterLine(() => [aboard], () => System, station: () => Station);

        var speaker = Taken(await line.RouteAsync(OnComms.Character(aboard.Id, aboard.Name), "hi", TestContext.Current.CancellationToken));

        Assert.Null(speaker.LiveState);
    }

    [Fact]
    public async Task ACastMemberOutsideTheCockpitKnowsOnlyWhereTheCommanderIs()
    {
        var mara = new AdventureSpeaker("mara", "Mara Quell", "A salvager.");
        var line = new CastLine(id => mara, () => null, inCockpit: _ => false, system: () => System, station: () => Station);

        var speaker = Taken(await line.RouteAsync(OnComms.Cast(mara.Id, mara.Name), "hi", TestContext.Current.CancellationToken));

        KnowsOnlyWhereTheCommanderIs(speaker);
    }

    [Fact]
    public async Task ACastMemberInTheCockpitReadsTheShipsLiveState()
    {
        var mara = new AdventureSpeaker("mara", "Mara Quell", "A salvager.");
        var line = new CastLine(id => mara, () => null, inCockpit: id => id == mara.Id, system: () => System, station: () => Station);

        var speaker = Taken(await line.RouteAsync(OnComms.Cast(mara.Id, mara.Name), "hi", TestContext.Current.CancellationToken));

        Assert.Null(speaker.LiveState);
    }
}
