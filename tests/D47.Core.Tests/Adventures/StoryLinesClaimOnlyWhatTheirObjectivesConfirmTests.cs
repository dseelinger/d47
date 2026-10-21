using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>A line may state only what its objective's trigger confirms; one call reads the draft against the table and refused objectives are written again.</summary>
public sealed class StoryLinesClaimOnlyWhatTheirObjectivesConfirmTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private const string NothingObjected = """{"objections": []}""";

    private const string SecondObjected = """{"objections": [{"objective": 2, "reason": "it says the Commander has docked there before."}]}""";

    private const string QuietDock = """
        {"replacements": [
          {"objective": 2, "title": "The Quiet Dock", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "A dock."}
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

        Assert.Contains("The ending must be something the last objective confirms.", prompt);

        foreach (var kind in Enum.GetValues<TriggerKind>().Where(kind => kind is not (TriggerKind.Beacon or TriggerKind.Reach)))
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
        Assert.Equal(["The Lantern", "The Quiet Dock", "The Column Will Not Balance"], outcome.Draft!.Beats.Select(beat => beat.Title));
    }

    [Fact]
    public async Task AFidelityReplyThatCannotBeReadObjectsToNothing()
    {
        var outcome = await Write(Scripted("not json"));

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }
}
