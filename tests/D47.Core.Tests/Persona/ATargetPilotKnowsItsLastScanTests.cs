using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

public class ATargetPilotKnowsItsLastScanTests
{
    private static readonly DateTimeOffset ScannedAt = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Now = ScannedAt.AddSeconds(40);

    private static readonly TargetedShip Full = new(
        "Cobra Mk III",
        "Jessa Corvane",
        "Expert",
        "Corvid Syndicate",
        "Wanted",
        Bounty: 120000,
        ShieldHealth: 62,
        HullHealth: 88,
        Subsystem: "Power Plant",
        SubsystemHealth: 75,
        ScannedAt: ScannedAt);

    private static async Task<string> BriefAsync(TargetedShip pilot, DateTimeOffset? now)
    {
        var channels = new CommsChannels();
        channels.Update(new CommsInputs { Target = pilot });
        var line = new TargetLine(() => pilot, now: now is { } at ? () => at : null);

        var decision = await line.RouteAsync(channels.Selected!, "who are you", TestContext.Current.CancellationToken);

        return Assert.IsType<LineDecision.Taken>(decision).Speaker.Brief;
    }

    [Fact]
    public async Task TheBriefCarriesRankFactionAndBounty()
    {
        var brief = await BriefAsync(Full, Now);

        Assert.Contains("Your combat rank is Expert.", brief, StringComparison.Ordinal);
        Assert.Contains("You fly for Corvid Syndicate.", brief, StringComparison.Ordinal);
        Assert.Contains("There is a bounty of 120,000 credits on you.", brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheBriefLeavesOutWhatIsNotHeld()
    {
        var brief = await BriefAsync(new TargetedShip("Cobra Mk III", "Jessa Corvane"), Now);

        Assert.DoesNotContain("combat rank", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("You fly for", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("bounty of", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("shields", brief, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hull at", brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheBriefDatesTheScanAndStatesTheHullAsACeiling()
    {
        var brief = await BriefAsync(Full, Now);

        Assert.Contains(
            "When the Commander last scanned you, 40 seconds ago, your shields were at 62% and your hull at 88%, and their sights were on your Power Plant, at 75%.",
            brief,
            StringComparison.Ordinal);
        Assert.Contains("Shields recharge, so yours may have changed since; your hull is no better than that now.", brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AScanOverAMinuteOldIsDatedInMinutes()
    {
        var brief = await BriefAsync(Full, ScannedAt.AddSeconds(190));

        Assert.Contains("3 minutes ago", brief, StringComparison.Ordinal);
    }
}
