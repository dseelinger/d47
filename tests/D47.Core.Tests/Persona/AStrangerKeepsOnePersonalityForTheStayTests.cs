using D47.Core.Callouts;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>A stranger on comms is given a personality once, which every later reply is briefed with.</summary>
public class AStrangerKeepsOnePersonalityForTheStayTests
{
    private static readonly DateTimeOffset Now = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private const string Written = "Dry and unhurried. Cares more about her cargo than the traffic.";

    private static (ChatterLine Line, NpcsHeardHere Roster) Build(Func<NpcHeard, CancellationToken, Task<string?>> write)
    {
        var roster = new NpcsHeardHere();
        var line = new ChatterLine(
            () => "Diaguandri",
            roster: () => roster,
            writePersonality: write);

        var hail = new NpcChatterLine("Vance", "Nice ship, Commander.", VoiceId: "bf_emma");
        line.Heard(hail, 3);
        roster.Heard(
            new Announcement(NpcChatter.LineKey, hail.Text) { Speaker = "Vance", Invented = new NpcChatterHeard(hail, 3, Hail: false) },
            "bf_emma",
            Now);

        return (line, roster);
    }

    private static async Task<string> BriefAsync(ChatterLine line, string input)
    {
        var decision = await line.RouteAsync(OnComms.System("Vance"), input, TestContext.Current.CancellationToken);

        return Assert.IsType<LineDecision.Taken>(decision).Speaker.Brief;
    }

    [Fact]
    public async Task ItIsWrittenOnceAndEveryReplyIsBriefedWithIt()
    {
        var calls = 0;
        var (line, roster) = Build((_, _) =>
        {
            calls++;
            return Task.FromResult<string?>(Written);
        });

        var first = await BriefAsync(line, "how's the cargo");
        var second = await BriefAsync(line, "and the traffic");

        Assert.Equal(1, calls);
        Assert.Contains("Your personality: " + Written, first, StringComparison.Ordinal);
        Assert.Contains("Your personality: " + Written, second, StringComparison.Ordinal);
        Assert.Equal(Written, roster.Find("Vance")!.Personality);
    }

    [Fact]
    public async Task AFailureFallsBackToThePlainBriefAndIsNotRetriedForTheStay()
    {
        var calls = 0;
        var (line, _) = Build((_, _) =>
        {
            calls++;
            throw new InvalidOperationException("offline");
        });

        var first = await BriefAsync(line, "how's the cargo");
        var second = await BriefAsync(line, "and the traffic");

        Assert.Equal(1, calls);
        Assert.DoesNotContain("Your personality", first, StringComparison.Ordinal);
        Assert.DoesNotContain("Your personality", second, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAnswerThatTalksAboutItselfIsNotKept()
    {
        var (line, roster) = Build((_, _) => Task.FromResult<string?>("As an AI language model, I am calm."));

        var brief = await BriefAsync(line, "how's the cargo");

        Assert.DoesNotContain("Your personality", brief, StringComparison.Ordinal);
        Assert.Null(roster.Find("Vance")!.Personality);
    }

    [Fact]
    public async Task ItSurvivesHearingTheStrangerAgainAndGoesWithThem()
    {
        var calls = 0;
        var (line, roster) = Build((_, _) =>
        {
            calls++;
            return Task.FromResult<string?>(Written);
        });

        await BriefAsync(line, "how's the cargo");

        var again = new NpcChatterLine("Vance", "Cargo's late again.");
        roster.Heard(
            new Announcement(NpcChatter.LineKey, again.Text) { Speaker = "Vance", Invented = new NpcChatterHeard(again, 3, Hail: false) },
            "bf_emma",
            Now.AddSeconds(5));

        Assert.Equal(Written, roster.Find("Vance")!.Personality);

        roster.Heard(
            new Announcement(NpcChatter.LineKey, NpcsHeardHere.SignOff("Vance")) { Speaker = "Vance", Invented = new NpcChatterHeard(again, 3, Hail: false) },
            "bf_emma",
            Now.AddSeconds(9));

        Assert.Null(roster.Find("Vance"));
        Assert.Equal(1, calls);
    }
}
