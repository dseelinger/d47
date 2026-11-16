using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>Invented speakers heard in one exchange share it, so an All message and its replies are recorded in it once.</summary>
public class AnAllMessageIsRecordedOnceInASharedChatterExchangeTests
{
    private static int Count(string text, string part) =>
        (text.Length - text.Replace(part, string.Empty, StringComparison.Ordinal).Length) / part.Length;

    private static ChatterLine Chatter(params (string Name, int Exchange)[] heard)
    {
        var chatter = new ChatterLine(() => "Sol");

        foreach (var (name, exchange) in heard)
        {
            chatter.Heard(new NpcChatterLine(name, $"{name} talking.", null, "v1"), exchange);
        }

        return chatter;
    }

    private static async Task<LineDecision.Taken> RouteAsync(ChatterLine chatter, string name, string input) =>
        Assert.IsType<LineDecision.Taken>(await chatter.RouteAsync(OnComms.System(name), input, TestContext.Current.CancellationToken));

    private static CommsExchange Exchange(string asked, params (string Name, string Said)[] replies) =>
        new(CommsChannel.System, asked, [.. replies.Select(reply => new CommsReply(reply.Name, reply.Said))]);

    [Fact]
    public async Task TheCommandersLineIsInEachBriefOnceWhenSeveralSpeakersAreRouted()
    {
        var chatter = Chatter(("Mira Kessel", 1), ("Vik Rane", 1), ("Jon Alder", 1));

        foreach (var name in new[] { "Mira Kessel", "Vik Rane", "Jon Alder" })
        {
            var taken = await RouteAsync(chatter, name, "anyone there");

            Assert.Equal(1, Count(taken.Speaker.Brief, "Commander: anyone there"));
        }
    }

    [Fact]
    public async Task EachReplyIsInTheSharedExchangeOnceAfterTheExchange()
    {
        var chatter = Chatter(("Mira Kessel", 1), ("Vik Rane", 1), ("Jon Alder", 1));
        var mira = await RouteAsync(chatter, "Mira Kessel", "anyone there");
        await RouteAsync(chatter, "Vik Rane", "anyone there");
        await RouteAsync(chatter, "Jon Alder", "anyone there");

        mira.Speaker.Screen!("Here.");

        var all = Exchange("anyone there", ("Mira Kessel", "Here."), ("Vik Rane", "Me too."));
        chatter.Overhear(OnComms.System("Mira Kessel"), all, 0);
        chatter.Overhear(OnComms.System("Vik Rane"), all, 1);
        chatter.Overhear(OnComms.System("Jon Alder"), all, null);

        var brief = (await RouteAsync(chatter, "Jon Alder", "and now")).Speaker.Brief;

        Assert.Equal(1, Count(brief, "Commander: anyone there"));
        Assert.Equal(1, Count(brief, "Mira Kessel: Here."));
        Assert.Equal(1, Count(brief, "Vik Rane: Me too."));
    }

    [Fact]
    public async Task SpeakersInDifferentExchangesEachHoldTheMessageAndRepliesOnce()
    {
        var chatter = Chatter(("Mira Kessel", 1), ("Vik Rane", 2));
        (await RouteAsync(chatter, "Mira Kessel", "anyone there")).Speaker.Screen!("Here.");
        (await RouteAsync(chatter, "Vik Rane", "anyone there")).Speaker.Screen!("Me too.");

        var all = Exchange("anyone there", ("Mira Kessel", "Here."), ("Vik Rane", "Me too."));
        chatter.Overhear(OnComms.System("Mira Kessel"), all, 0);
        chatter.Overhear(OnComms.System("Vik Rane"), all, 1);

        foreach (var name in new[] { "Mira Kessel", "Vik Rane" })
        {
            var brief = (await RouteAsync(chatter, name, "and now")).Speaker.Brief;

            Assert.Equal(1, Count(brief, "Commander: anyone there"));
            Assert.Equal(1, Count(brief, "Mira Kessel: Here."));
            Assert.Equal(1, Count(brief, "Vik Rane: Me too."));
        }
    }

    [Fact]
    public async Task ASinglePartyTurnRecordsTheCommandersLineAndReplyAsBefore()
    {
        var chatter = Chatter(("Mira Kessel", 1));

        for (var turn = 0; turn < 2; turn++)
        {
            var taken = await RouteAsync(chatter, "Mira Kessel", "status");
            taken.Speaker.Screen!("Fine.");
        }

        var brief = (await RouteAsync(chatter, "Mira Kessel", "again")).Speaker.Brief;

        Assert.Equal(2, Count(brief, "Commander: status"));
        Assert.Equal(2, Count(brief, "Mira Kessel: Fine."));
    }
}
