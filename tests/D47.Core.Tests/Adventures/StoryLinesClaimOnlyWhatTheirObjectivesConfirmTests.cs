using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>A line may state only what its objective's trigger confirms; one call reads the draft against the table and refused objectives are written again.</summary>
public sealed class StoryLinesClaimOnlyWhatTheirObjectivesConfirmTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private const string NothingObjected = """{"objections": []}""";

    private const string SecondObjected = """{"objections": [{"objective": 2, "quote": "one name", "kind": "history", "reason": "it says the Commander has docked there before."}]}""";

    private const string QuietDock = """
        {"replacements": [
          {"objective": 2, "title": "The Quiet Dock", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "One name, at a dock."}
        ]}
        """;

    private const string BeaconBeats = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
          {"title": "The Beacon", "function": "resolution", "kind": "beacon", "system": "Ossen's Lantern", "line": "The cores are aboard."}
        ]}
        """;

    private static RoundScriptedLlmProvider Scripted(params string[] after) =>
        new([
            RoundScriptedLlmProvider.Saying(AdventureGeneratorTests.Spine),
            RoundScriptedLlmProvider.Saying(AdventureGeneratorTests.GoodBeats),
            .. after.Select(reply => RoundScriptedLlmProvider.Saying(reply)),
        ]);

    private static Task<AdventureOutcome> Write(RoundScriptedLlmProvider provider) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

    [Trait("Category", "Gate")]
    [Fact]
    public void EveryTriggerKindSaysWhatItConfirms()
    {
        var missing = Enum.GetValues<TriggerKind>().Where(kind => !TriggerConfirms.Kinds.Contains(kind)).ToList();

        Assert.True(missing.Count == 0, $"Add an entry to TriggerConfirms for: {string.Join(", ", missing)}.");
        Assert.All(Enum.GetValues<TriggerKind>(), kind => Assert.False(string.IsNullOrWhiteSpace(TriggerConfirms.Of(kind))));
    }

    [Fact]
    public async Task TheSpinePromptCarriesTheEntryForEveryKind()
    {
        var provider = Scripted(NothingObjected);

        await Write(provider);

        var prompt = provider.Requests[0].Prompt.History[0].Text;

        Assert.Contains(TriggerConfirms.Rule, prompt);

        foreach (var kind in Enum.GetValues<TriggerKind>().Where(kind => kind is not (TriggerKind.Beacon or TriggerKind.Reach or TriggerKind.Site)))
        {
            Assert.Contains($"- \"{kind.ToString().ToLowerInvariant()}\": {TriggerConfirms.Of(kind)}", prompt);
        }
    }

    [Fact]
    public async Task TheBeatsPromptCarriesTheEntryForEveryKindItOffers()
    {
        var provider = Scripted(NothingObjected);

        await Write(provider);

        var prompt = provider.Requests[1].Prompt.History[0].Text;
        var offered = Enum.GetValues<TriggerKind>()
            .Select(kind => kind.ToString().ToLowerInvariant())
            .Where(word => prompt.Contains($"\n- \"{word}\": the Commander", StringComparison.Ordinal))
            .ToList();

        Assert.Contains(TriggerConfirms.Rule, prompt);
        Assert.True(offered.Count > 20, "The prompt offers few kinds.");
        Assert.All(offered, word => Assert.Contains($"- \"{word}\": {TriggerConfirms.Of(word)}", prompt));
    }

    [Fact]
    public async Task TheFidelityCallShowsEachObjectiveItsEntryAndTheEnding()
    {
        var provider = Scripted(NothingObjected);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(3, provider.CallCount);

        var prompt = provider.Requests[2].Prompt.History[0].Text;

        Assert.Contains(TriggerConfirms.Rule, prompt);
        Assert.Contains($"Confirms: {TriggerConfirms.Of(TriggerKind.Dock)}", prompt);
        Assert.Contains("\"Scoop here.\"", prompt);
        Assert.Contains("(the last objective: its lines are the ending)", prompt);
        Assert.Equal("story_fidelity", provider.Requests[2].ResponseSchema?.Name);
    }

    [Fact]
    public async Task AnObjectiveTheFidelityCallNamesIsWrittenAgainAndTheOthersStand()
    {
        var provider = Scripted(SecondObjected, QuietDock, NothingObjected);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(5, provider.CallCount);

        var repair = provider.Requests[3].Prompt.History[0].Text;

        Assert.Contains("Objective 2 (The Anchorage) states something its trigger does not confirm: it says the Commander has docked there before.", repair);
        Assert.Contains("Write a replacement for objective 2, and for no other.", repair);
        Assert.Contains("delete that statement from the lines rather than rewording it", repair);
        Assert.Equal(["The Lantern", "The Quiet Dock", "The Column Will Not Balance"], outcome.Draft!.Beats.Select(beat => beat.Title));
    }

    [Fact]
    public async Task AnObjectiveTheCheckPassedIsNotRefusedByALaterRound()
    {
        var provider = Scripted(
            SecondObjected,
            QuietDock,
            """{"objections": [{"objective": 1, "quote": "Scoop here", "kind": "action", "reason": "it says the Commander was paid."}, {"objective": 3, "quote": "It balances", "kind": "event", "reason": "it says no beacon was scanned."}]}""");

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(5, provider.CallCount);
        Assert.Equal(["The Lantern", "The Quiet Dock", "The Column Will Not Balance"], outcome.Draft!.Beats.Select(beat => beat.Title));
    }

    [Theory]
    [InlineData("""{"objections": [{"objective": 2, "quote": "To one name", "kind": "fiction", "reason": "a record in the story."}]}""")]
    [InlineData("""{"objections": [{"objective": 2, "quote": "The hold took in the canisters", "kind": "action", "reason": "words the lines do not hold."}]}""")]
    [InlineData("""{"objections": [{"objective": 2, "kind": "action", "reason": "no quote."}]}""")]
    public async Task AnObjectionThatQuotesNoCheckedStatementInTheLinesIsDropped(string objected)
    {
        var provider = Scripted(objected);

        var outcome = await Write(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(3, provider.CallCount);
    }

    [Fact]
    public async Task AnObjectionThatOutlastsTheRepairsFailsAsAFidelityRefusal()
    {
        var outcome = await Write(Scripted(SecondObjected, QuietDock, SecondObjected, QuietDock, SecondObjected));

        Assert.False(outcome.Succeeded);
        Assert.StartsWith("The story states what its objectives do not confirm: Fidelity check: Objective 2 (The Quiet Dock)", outcome.Refusal);
    }

    [Fact]
    public async Task AStockStorysBeaconConfirmsItsCoresComeAboard()
    {
        var provider = new RoundScriptedLlmProvider([
            RoundScriptedLlmProvider.Saying(AdventureGeneratorTests.Spine),
            RoundScriptedLlmProvider.Saying(BeaconBeats),
            RoundScriptedLlmProvider.Saying(NothingObjected),
        ]);
        var story = new AdventureStory("s", "S", "Public", "Hidden", Chapter: 1, DaysRunning: 3, DaysSinceBeacon: null)
        {
            Beacon = new AdventureBeacon(13872878396833, "IC 2391 Sector MX-T b3-6"),
        };

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Story: story), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        Assert.Contains("the story's Guardian cores came aboard", TriggerConfirms.Of(TriggerKind.Beacon, story: true));
        Assert.DoesNotContain("cores", TriggerConfirms.Of(TriggerKind.Beacon));
        Assert.Contains($"- \"beacon\": {TriggerConfirms.Of(TriggerKind.Beacon, story: true)}", provider.Requests[1].Prompt.History[0].Text);
        Assert.Contains($"Confirms: {TriggerConfirms.Of(TriggerKind.Beacon, story: true)}", provider.Requests[2].Prompt.History[0].Text);
    }

    [Fact]
    public async Task AFidelityReplyThatCannotBeReadObjectsToNothing()
    {
        var outcome = await Write(Scripted("not json"));

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }
}
