using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>An activity refused in a story is a key, and a chapter with a beat for it is refused when it is written.</summary>
public sealed class ARefusedActivityIsNotAskedAgainTests
{
    private const string Spine = """
        {"name": "The Long Way", "premise": "A debt.", "want": "To pay it.", "stake": "Whether it can be.", "turn": "It is owed.", "ending": "Forgiven."}
        """;

    private static string Beats(string kind, string extra) => $$"""
        {"opening": "x", "reply": "ok", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Job", "function": "finale", "kind": "{{kind}}", "count": 2, {{extra}} "line": "Do it."}
        ]}
        """;

    private static async Task<AdventureOutcome> Written(string kind, string extra, params string[] refused)
    {
        var story = new AdventureStory("s", "S", "public", "hidden", 1, 10, null, Refused: refused);
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(Beats(kind, extra)),
            RoundScriptedLlmProvider.Saying(Beats(kind, extra)));

        return await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Story: story), AdventureFixtures.Accepted, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AChapterWithARefusedActivityIsRefused()
    {
        var outcome = await Written("bond", string.Empty, "bond");

        Assert.Null(outcome.Draft);
        Assert.Contains("asks the Commander to earn combat kill bonds, which they refused for this story", outcome.Refusal);
    }

    [Fact]
    public async Task ARefusedMissionFamilyDoesNotRefuseAnother()
    {
        var courier = await Written("mission", "\"mission\": \"Mission_Courier\",", "mission:Mission_Massacre");
        var massacre = await Written("mission", "\"mission\": \"Mission_Massacre\",", "mission:Mission_Massacre");

        Assert.NotNull(courier.Draft);
        Assert.Null(massacre.Draft);
        Assert.Contains("complete massacre missions", massacre.Refusal);
    }

    [Fact]
    public async Task AChapterWithoutTheActivityIsWritten()
    {
        var outcome = await Written("bounty", string.Empty, "bond");

        Assert.NotNull(outcome.Draft);
    }

    [Fact]
    public void APlaceKindHasNoKeyAndAMissionKeyCarriesItsFamily()
    {
        Assert.Null(RefusedActivities.Key(TriggerKind.Dock, null));
        Assert.Null(RefusedActivities.Key(TriggerKind.Arrive, null));
        Assert.Equal("bond", RefusedActivities.Key(TriggerKind.Bond, null));
        Assert.Equal("mission:Mission_Massacre", RefusedActivities.Key(TriggerKind.Mission, "Mission_Massacre"));
        Assert.Equal("mission", RefusedActivities.Key(TriggerKind.Mission, null));
    }

    [Fact]
    public void AMissionRefusedWithoutAFamilyRefusesEveryMission()
    {
        Assert.True(RefusedActivities.Refuses(["mission"], TriggerKind.Mission, "Mission_Courier"));
        Assert.True(RefusedActivities.Refuses(["mission"], TriggerKind.Mission, null));
        Assert.False(RefusedActivities.Refuses(["mission"], TriggerKind.Bounty, null));
    }

    [Fact]
    public void TheComfortZonePickSkipsARefusedActivity()
    {
        var statistics = Stories.StoryFixtures.Loaded(1, statistics: "\"Combat\":{ \"Bounties_Claimed\":0 }").Statistics;

        Assert.Equal("bounty", ChapterFit.LeastDone(statistics)?.Name);
        Assert.Equal("bond", ChapterFit.LeastDone(statistics, ["bounty"])?.Name);
    }
}
