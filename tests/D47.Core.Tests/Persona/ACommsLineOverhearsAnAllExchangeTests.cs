using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Characters;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>Each comms line keeps what was said to everyone on its channel, without a second copy of what its own memory holds.</summary>
public class ACommsLineOverhearsAnAllExchangeTests
{
    private const string Heading = "Said to everyone on the";

    private static readonly DateTimeOffset Now = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly CarrierState Owned = new() { CallSign = "BNH-T2F", Name = "Sacred Fire", StarSystem = "Wolf 359" };

    private static readonly CustomCharacter Mara = new("aaaa0001", "Mara", null, "gruff", true, null);

    private static readonly AdventureSpeaker Quell = new("mara", "Mara Quell", "A salvager.");

    private static CommsExchange Exchange(CommsChannel channel, string asked, params (string Name, string Said)[] replies) =>
        new(channel, asked, [.. replies.Select(reply => new CommsReply(reply.Name, reply.Said))]);

    private static CaptainLine Captain() =>
        new(() => new CommanderGameState(new CommanderIdentity("F1", "Tester")) { Carrier = Owned }, () => "Sol", () => Now);

    private static TowerLine Tower() =>
        new(() => new CommanderGameState(new CommanderIdentity("F1", "Tester")) { Carrier = Owned }, () => "Sol", () => Now);

    private static CharacterLine Characters() => new(() => [Mara], () => "Sol");

    private static CastLine Cast() => new(id => id == Quell.Id ? Quell : null, () => null);

    private static async Task<string> BriefAsync(ILine line, CommsSelection party, string input = "hello")
    {
        var decision = await line.RouteAsync(party, input, TestContext.Current.CancellationToken);

        return Assert.IsType<LineDecision.Taken>(decision).Speaker.Brief;
    }

    private static string Block(string brief) => brief[brief.IndexOf(Heading, StringComparison.Ordinal)..];

    private static int Count(string text, string part) =>
        (text.Length - text.Replace(part, string.Empty, StringComparison.Ordinal).Length) / part.Length;

    [Fact]
    public async Task APartyThatDidNotAnswerHoldsTheCommandersWordsAndEveryReply()
    {
        var all = Exchange(CommsChannel.Carrier, "status report", ("Tower", "Pad one is clear."), ("Captain", "All quiet."));

        foreach (var (line, party) in new (ILine, CommsSelection)[] { (Captain(), OnComms.Captain), (Tower(), OnComms.Tower) })
        {
            line.Overhear(party, all, ownReply: null);

            Assert.Equal(
                "Said to everyone on the carrier channel, oldest first:\nCommander: status report\nTower: Pad one is clear.\nCaptain: All quiet.",
                Block(await BriefAsync(line, party)));
        }

        var characters = Characters();
        var character = OnComms.Character(Mara.Id, Mara.Name);
        characters.Overhear(character, Exchange(CommsChannel.Characters, "who is there", ("Juno", "Here.")), null);

        Assert.Equal(
            "Said to everyone on the characters channel, oldest first:\nCommander: who is there\nJuno: Here.",
            Block(await BriefAsync(characters, character)));
    }

    [Fact]
    public async Task APartyThatAnsweredHoldsOnlyTheRepliesAfterItsOwn()
    {
        var all = Exchange(CommsChannel.Carrier, "status report", ("Tower", "Pad one is clear."), ("Captain", "All quiet."), ("Dock", "Fuel is in."));
        var captain = Captain();

        captain.Overhear(OnComms.Captain, all, ownReply: 1);

        Assert.Equal(
            "Said to everyone on the carrier channel, oldest first:\nDock: Fuel is in.",
            Block(await BriefAsync(captain, OnComms.Captain)));
    }

    [Fact]
    public async Task APartyThatAnsweredLastHoldsNothing()
    {
        var captain = Captain();

        captain.Overhear(OnComms.Captain, Exchange(CommsChannel.Carrier, "report", ("Tower", "Clear."), ("Captain", "Quiet.")), ownReply: 1);

        Assert.DoesNotContain(Heading, await BriefAsync(captain, OnComms.Captain));
    }

    [Fact]
    public async Task CastThatAnsweredHoldsEveryOtherReplyAndNoSecondCopyOfTheCommandersLine()
    {
        var cast = Cast();
        var party = OnComms.Cast(Quell.Id, Quell.Name);

        var routed = await BriefAsync(cast, party, "where are we");
        Assert.Contains("Commander: where are we", routed);

        cast.Overhear(
            party,
            Exchange(CommsChannel.Story, "where are we", ("Mara Quell", "Near the beacon."), ("Narrator", "The beacon hums.")),
            ownReply: 0);

        var brief = await BriefAsync(cast, party, "and then");

        Assert.Equal(1, Count(brief, "Commander: where are we"));
        Assert.Equal(0, Count(brief, "Mara Quell: Near the beacon."));
        Assert.Equal(1, Count(brief, "Narrator: The beacon hums."));
    }

    [Fact]
    public async Task CastThatDidNotAnswerHoldsEveryReplyAndNotTheCommandersLineAgain()
    {
        var cast = Cast();
        var party = OnComms.Cast(Quell.Id, Quell.Name);

        await BriefAsync(cast, party, "where are we");

        cast.Overhear(party, Exchange(CommsChannel.Story, "where are we", ("Jonas", "Lost."), ("Narrator", "Dark.")), ownReply: null);

        var brief = await BriefAsync(cast, party, "and then");

        Assert.Equal(1, Count(brief, "Commander: where are we"));
        Assert.Contains("Jonas: Lost.\nNarrator: Dark.", brief);
    }

    [Fact]
    public async Task InventedChatterHoldsNoSecondCopyOfTheCommandersLine()
    {
        var chatter = new ChatterLine(() => "Sol");
        chatter.Heard(new NpcChatterLine("Mira Kessel", "Kettle's on.", null, "v1"), 1);

        var party = OnComms.System("Mira Kessel");
        var routed = await BriefAsync(chatter, party, "who is there");
        Assert.Contains("Commander: who is there", routed);

        chatter.Overhear(
            party,
            Exchange(CommsChannel.System, "who is there", ("Mira Kessel", "Me."), ("Vik Rane", "Likewise.")),
            ownReply: 0);

        var brief = await BriefAsync(chatter, party, "and you");

        Assert.Equal(1, Count(brief, "Commander: who is there"));
        Assert.Equal(0, Count(brief, "Mira Kessel: Me."));
        Assert.Equal(1, Count(brief, "Vik Rane: Likewise."));
    }

    [Fact]
    public async Task ACommsNpcHoldsWhatItLacksAndLosesItOnSignOffAndWhenTheSystemChanges()
    {
        var roster = new NpcsHeardHere();
        var system = "Sol";
        var chatter = new ChatterLine(() => system, roster: () => roster);
        var party = OnComms.System("Vik Rane");
        var all = Exchange(CommsChannel.System, "anyone there", ("Dock", "Hello."));

        void Hear() => roster.Heard(
            new Announcement(IncomingMessages.CannedKeyPrefix + "Pirate", "Their own words.") { Speaker = "Vik Rane", CommsChannel = "npc" },
            null,
            Now);

        Hear();
        chatter.Overhear(party, all, ownReply: null);

        Assert.Equal(
            "Said to everyone on the system channel, oldest first:\nCommander: anyone there\nDock: Hello.",
            Block(await BriefAsync(chatter, party)));

        system = "Colonia";
        Assert.DoesNotContain(Heading, await BriefAsync(chatter, party));

        system = "Sol";
        chatter.Overhear(party, all, ownReply: null);

        var taken = Assert.IsType<LineDecision.Taken>(await chatter.RouteAsync(party, "bye", TestContext.Current.CancellationToken));
        taken.Speaker.Screen!(NpcsHeardHere.SignOff("Vik Rane"));

        Hear();
        Assert.DoesNotContain(Heading, await BriefAsync(chatter, party));
    }

    [Fact]
    public async Task ASelectionNamingAnotherLinesPartyIsIgnored()
    {
        var all = Exchange(CommsChannel.Carrier, "status", ("Tower", "Clear."));
        var captain = Captain();
        var characters = Characters();
        var cast = Cast();
        var chatter = new ChatterLine(() => "Sol");
        chatter.Heard(new NpcChatterLine("Mira Kessel", "Kettle's on.", null, "v1"), 1);

        captain.Overhear(OnComms.Tower, all, null);
        characters.Overhear(OnComms.Character("zzzz9999", "Nobody"), all, null);
        characters.Overhear(OnComms.Captain, all, null);
        cast.Overhear(OnComms.Cast("unknown", "Nobody"), all, null);
        chatter.Overhear(OnComms.System("Nobody"), all, null);

        Assert.DoesNotContain(Heading, await BriefAsync(captain, OnComms.Captain));
        Assert.DoesNotContain(Heading, await BriefAsync(characters, OnComms.Character(Mara.Id, Mara.Name)));
        Assert.DoesNotContain("Tower: Clear.", await BriefAsync(cast, OnComms.Cast(Quell.Id, Quell.Name)));
        Assert.DoesNotContain("Tower: Clear.", await BriefAsync(chatter, OnComms.System("Mira Kessel")));

        var tower = Tower();
        tower.Overhear(OnComms.Captain, all, null);
        Assert.DoesNotContain(Heading, await BriefAsync(tower, OnComms.Tower));
    }

    [Fact]
    public async Task TheBlockHoldsOnlyTheLastTwentyLines()
    {
        var captain = Captain();

        for (var i = 1; i <= 30; i++)
        {
            captain.Overhear(OnComms.Captain, Exchange(CommsChannel.Carrier, "ping", ("Tower", $"reply {i}")), ownReply: null);
        }

        var lines = Block(await BriefAsync(captain, OnComms.Captain)).Split('\n').Skip(1).ToList();

        Assert.Equal(ExchangeLines.Most, lines.Count);
        Assert.Equal("Tower: reply 30", lines[^1]);
        Assert.DoesNotContain("Tower: reply 10", lines);
    }

    [Fact]
    public async Task ALineThatDoesNotOverhearKeepsTheDefault()
    {
        ILine line = new DefaultLine();

        line.Overhear(OnComms.Captain, Exchange(CommsChannel.Carrier, "status", ("Tower", "Clear.")), null);

        Assert.IsType<LineDecision.NotMine>(await line.RouteAsync(OnComms.Captain, "x", TestContext.Current.CancellationToken));
    }

    private sealed class DefaultLine : ILine
    {
        public Task<LineDecision> RouteAsync(CommsSelection selected, string input, CancellationToken cancellationToken) =>
            Task.FromResult<LineDecision>(new LineDecision.NotMine());
    }
}
