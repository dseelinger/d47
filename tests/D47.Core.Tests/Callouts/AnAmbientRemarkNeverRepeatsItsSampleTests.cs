using D47.Core.Callouts;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class AnAmbientRemarkNeverRepeatsItsSampleTests
{
    private const string Authored = "Nothing like solid ground under your boots, Commander.";

    private static readonly Announcement Remark = new($"{AmbientCallout.KeyPrefix}onfoot", Authored);

    private static Task<Announcement?> Vary(Announcement announcement, string reply) =>
        new Rewording(new RewordChance(new Random(1)), null).VaryAsync(
            announcement,
            hasModel: true,
            personalityEnabled: true,
            rewordPercent: 100,
            () => ShipFacts.Unknown,
            "Doug",
            (brief, instruction, token) => Task.FromResult(new FlavourReply(reply, FlavourMiss.None, null)));

    [Fact]
    public async Task AReplyEqualToTheSampleReturnsTheInputAnnouncement()
    {
        var said = await Vary(Remark, Authored);

        Assert.Same(Remark, said);
    }

    [Fact]
    public async Task ADifferentCaseAndNoFinalFullStopIsStillTheSample()
    {
        var said = await Vary(Remark, "NOTHING like solid ground under your boots, Commander");

        Assert.Same(Remark, said);
    }

    [Fact]
    public async Task AReplyInDifferentWordingIsReturnedAsVaried()
    {
        const string Reply = "Boots on the ground again, Commander. I almost missed the gravity.";

        var said = await Vary(Remark, Reply);

        Assert.NotSame(Remark, said);
        Assert.Equal(Reply, said?.Text);
    }
}
