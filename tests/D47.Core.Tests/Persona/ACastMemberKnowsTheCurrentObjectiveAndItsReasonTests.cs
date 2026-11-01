using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Persona;

/// <summary>A cast member of the running story is told the chapter's current objective and its reason.</summary>
public class ACastMemberKnowsTheCurrentObjectiveAndItsReasonTests
{
    private const string Reason = "A comms specialist there reads signals like the burst.";

    private static readonly AdventureSpeaker Mara = new("mara", "Mara Quell", "A salvager.");

    private static AdventureStanding Standing(string? storyId)
    {
        var route = LanternRoute(Accepted);
        var adventure = route with
        {
            StoryId = storyId,
            Beats = [.. route.Beats.Select((beat, index) => index == 2 ? beat with { Reason = Reason } : beat)],
        };

        return WholeRoute(Accepted).Take(2).Aggregate(AdventureFold.Start(adventure), AdventureFold.Apply);
    }

    private static async Task<Speaker> Asked(CastLine line)
    {
        var decision = await line.RouteAsync(OnComms.Cast(Mara.Id, Mara.Name), "Why am I going there?", TestContext.Current.CancellationToken);

        return Assert.IsType<LineDecision.Taken>(decision).Speaker;
    }

    [Fact]
    public async Task AnOffShipCastMemberIsToldTheCurrentObjectiveAndItsReason()
    {
        var standing = Standing("a-stock-story");
        var line = new CastLine(_ => Mara, () => null, inCockpit: _ => false, objective: () => AdventureContext.StoryNowLine([standing]));

        var speaker = await Asked(line);

        var state = Assert.IsType<Func<string?>>(speaker.LiveState)();
        Assert.Contains($"Reason: {Reason}", state, StringComparison.Ordinal);
        Assert.Contains("waiting to dock at Maren Anchorage", state, StringComparison.Ordinal);
        Assert.Contains("answer from the reason on the Now line", speaker.Brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACastMemberIsToldNoObjectiveWhenNoStoryChapterIsRunning()
    {
        var line = new CastLine(_ => Mara, () => null, inCockpit: _ => false, objective: () => AdventureContext.StoryNowLine([Standing(null)]));

        var state = Assert.IsType<Func<string?>>((await Asked(line)).LiveState)();

        Assert.DoesNotContain("Now:", state, StringComparison.Ordinal);
        Assert.DoesNotContain(Reason, state, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACastMemberInTheCockpitIsNotGivenTheObjectiveTwice()
    {
        var standing = Standing("a-stock-story");
        var line = new CastLine(_ => Mara, () => null, inCockpit: _ => true, objective: () => AdventureContext.StoryNowLine([standing]));

        Assert.Null((await Asked(line)).LiveState);
    }

    [Fact]
    public void TheStoryBlockCarriesTheSameNowLineTheCastMemberIsGiven()
    {
        var standing = Standing("a-stock-story");

        var block = AdventureContext.Describe([standing], _ => null, Accepted.AddHours(1))!;

        Assert.Contains("  " + AdventureContext.NowLine(standing), block, StringComparison.Ordinal);
        Assert.Equal(AdventureContext.NowLine(standing), AdventureContext.StoryNowLine([standing]));
    }
}
