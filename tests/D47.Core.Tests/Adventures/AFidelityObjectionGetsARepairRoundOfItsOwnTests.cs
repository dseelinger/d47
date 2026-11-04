using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>Fidelity objections are repaired in a round counted apart from the place rounds, and a sightseeing hand-in confirms the visit.</summary>
public sealed class AFidelityObjectionGetsARepairRoundOfItsOwnTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private const string NothingObjected = """{"objections": []}""";

    private const string SecondObjected = """{"objections": [{"objective": 2, "quote": "one name", "kind": "history", "reason": "it says the Commander has docked there before."}]}""";

    private const string QuietDock = """
        {"replacements": [
          {"objective": 2, "title": "The Quiet Dock", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "One name, at a dock."}
        ]}
        """;

    private const string LostHop = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Lost Hop", "function": "turn", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Nowhere Reached", "line": "To one name."},
          {"title": "The Freight Delivered", "function": "resolution", "kind": "rank", "reason": "Someone there knows about the burst.", "career": "Trader", "rank": 8, "line": "The freight is delivered."}
        ]}
        """;

    private const string StillLost = """
        {"replacements": [
          {"objective": 2, "title": "The Lost Hop", "function": "turn", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Nowhere Reached", "line": "To one name."}
        ]}
        """;

    private const string Sightseeing = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Tour", "function": "turn", "kind": "mission", "reason": "Someone there knows about the burst.", "count": 1, "mission": "{{family}}", "line": "Take the tourists round."},
          {"title": "The Freight Delivered", "function": "resolution", "kind": "rank", "reason": "Someone there knows about the burst.", "career": "Trader", "rank": 8, "line": "The freight is delivered."}
        ]}
        """;

    private const string SightsConfirmed = "a sightseeing mission was handed in, so its passengers were taken to the sights it listed — not what they made of them";

    private static Task<AdventureOutcome> Write(RoundScriptedLlmProvider provider) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

    private static RoundScriptedLlmProvider Scripted(params string[] after) =>
        new([
            RoundScriptedLlmProvider.Saying(AdventureGeneratorTests.Spine),
            RoundScriptedLlmProvider.Saying(LostHop),
            .. after.Select(reply => RoundScriptedLlmProvider.Saying(reply)),
        ]);

    [Fact]
    public async Task AnObjectionAfterTwoPlaceRoundsIsRepairedOnceMore()
    {
        var provider = Scripted(StillLost, QuietDock, SecondObjected, QuietDock, NothingObjected);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(9, provider.CallCount);
        Assert.Contains("Write a replacement for objective 2, and for no other.", provider.Requests[6].Prompt.History[0].Text);
        Assert.Equal(["The Lantern", "The Quiet Dock", "The Freight Delivered"], outcome.Draft!.Beats.Select(beat => beat.Title));
    }

    [Fact]
    public async Task AnObjectionThatStandsAfterItsRoundFailsTheChapter()
    {
        var provider = Scripted(StillLost, QuietDock, SecondObjected, QuietDock, SecondObjected);

        var outcome = await Write(provider);

        Assert.False(outcome.Succeeded);
        Assert.Equal(9, provider.CallCount);
        Assert.StartsWith("The story states what its objectives do not confirm:", outcome.Refusal);
    }

    private const string TwoObjected = """
        {"objections": [
          {"objective": 2, "quote": "one name", "kind": "history", "reason": "it says the Commander has docked there before."},
          {"objective": 2, "quote": "at a dock", "kind": "event", "reason": "it says how the hop went."}
        ]}
        """;

    private const string DockObjected = """{"objections": [{"objective": 2, "quote": "at a dock", "kind": "action", "reason": "it says the Commander moored."}]}""";

    [Fact]
    public async Task AnObjectionToTheRepairGetsASecondRepairRoundAndIsWrittenWhenItPasses()
    {
        var provider = Scripted(StillLost, QuietDock, SecondObjected, QuietDock, DockObjected, QuietDock, NothingObjected);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(11, provider.CallCount);
    }

    [Fact]
    public async Task TwoObjectionsToOneObjectiveBothReachTheRepairPrompt()
    {
        var provider = Scripted(StillLost, QuietDock, TwoObjected, QuietDock, NothingObjected);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var repair = provider.Requests[6].Prompt.History[0].Text;

        Assert.Contains("it says the Commander has docked there before.", repair);
        Assert.Contains("it says how the hop went.", repair);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(repair, @"- .*Objective 2 [(]"));
    }

    [Fact]
    public async Task AnObjectionFromAnEarlierRoundIsListedAgainInTheNextRepairPrompt()
    {
        var provider = Scripted(StillLost, QuietDock, SecondObjected, QuietDock, DockObjected, QuietDock, NothingObjected);

        await Write(provider);

        var second = provider.Requests[8].Prompt.History[0].Text;

        Assert.Contains("it says the Commander moored.", second);
        Assert.Contains("Earlier rounds also objected: it says the Commander has docked there before.", second);
    }

    [Fact]
    public async Task APlaceRefusalStillGetsOnlyThePlaceRounds()
    {
        var provider = Scripted(StillLost, StillLost);

        var outcome = await Write(provider);

        Assert.False(outcome.Succeeded);
        Assert.Equal(5, provider.CallCount);
        Assert.StartsWith("The story names places that cannot stand:", outcome.Refusal);
    }

    [Fact]
    public void ASightseeingMissionConfirmsTheVisitAndOtherFamiliesKeepTheirText()
    {
        Assert.Equal(SightsConfirmed, TriggerConfirms.Of(TriggerKind.Mission, null, false, null, "Mission_Sightseeing_Fed"));
        Assert.Equal(TriggerConfirms.Of(TriggerKind.Mission), TriggerConfirms.Of(TriggerKind.Mission, null, false, null, "Mission_Courier"));
        Assert.Equal(TriggerConfirms.Of(TriggerKind.Mission), TriggerConfirms.Of(TriggerKind.Mission, null, false, null, null));
    }

    [Theory]
    [InlineData("Mission_Sightseeing", true)]
    [InlineData("Mission_Courier", false)]
    public async Task TheFidelityPromptShowsTheTextForTheObjectivesFamily(string family, bool sightseeing)
    {
        var provider = new RoundScriptedLlmProvider([
            RoundScriptedLlmProvider.Saying(AdventureGeneratorTests.Spine),
            RoundScriptedLlmProvider.Saying(Sightseeing.Replace("{{family}}", family, StringComparison.Ordinal)),
            RoundScriptedLlmProvider.Saying(NothingObjected),
        ]);

        await Write(provider);

        var fidelity = provider.Requests.Select(request => request.Prompt.History[0].Text).First(text => text.Contains("Confirms:", StringComparison.Ordinal));

        Assert.Equal(sightseeing, fidelity.Contains($"Confirms: {SightsConfirmed}", StringComparison.Ordinal));
        Assert.Equal(!sightseeing, fidelity.Contains($"Confirms: {TriggerConfirms.Of(TriggerKind.Mission)}", StringComparison.Ordinal));
    }
}
