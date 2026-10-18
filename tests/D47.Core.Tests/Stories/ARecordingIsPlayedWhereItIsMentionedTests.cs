using D47.Core.Adventures;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A story chapter that says a recording, call or message is played or heard gives that person's words a line in the same place.</summary>
[Trait("Category", "Integration")]
public sealed class ARecordingIsPlayedWhereItIsMentionedTests
{
    private const string Beats = """
        {"opening": "OPENING", "openingSpeaker": "narrator", "reply": "Here it is.", "beats": [
          {"title": "Yard Paint", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "lines": [
            {"text": "NARRATED", "speaker": "narrator"},
            {"text": "Nobody. Go.", "speaker": "dock-hand"}
          ]},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "lines": [{"text": "ALONE", "speaker": "narrator"}]},
          {"title": "The Beacon", "function": "resolution", "kind": "beacon", "system": "Ossen's Lantern", "line": "Scan it."}
        ]}
        """;

    private static string Written(
        string opening = "A stock Sidewinder sits on a small pad.",
        string narrated = "The yard lights stay on.",
        string alone = "The anchorage is quiet.") =>
        Beats
            .Replace("OPENING", opening, StringComparison.Ordinal)
            .Replace("NARRATED", narrated, StringComparison.Ordinal)
            .Replace("ALONE", alone, StringComparison.Ordinal);

    private static RoundScriptedLlmProvider Scripted(params string[] answers) =>
        new([RoundScriptedLlmProvider.Saying(Spine), .. answers.Select(answer => RoundScriptedLlmProvider.Saying(answer))]);

    private static async Task<(StoryFixtures Fixtures, string? Refusal)> PickAsync(RoundScriptedLlmProvider provider)
    {
        var fixtures = new StoryFixtures(provider);

        return (fixtures, await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
    }

    private static Adventure Chapter(StoryFixtures fixtures) =>
        fixtures.Book.Store.Find("F1", fixtures.Stories.Current("F1")!.CurrentChapter!)!;

    [Fact]
    public async Task TheChapterWriterIsToldToPlayARecordingItMentions()
    {
        var (fixtures, refusal) = await PickAsync(Scripted(Written()));
        using var _ = fixtures;

        Assert.Null(refusal);
        Assert.Contains(
            "never say one was played, or played before the story began, without them",
            fixtures.Provider.Requests[1].Prompt.History[0].Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARecordingPlayedWithNobodySpeakingIsRewrittenAndTheRewriteIsKept()
    {
        var (fixtures, refusal) = await PickAsync(Scripted(
            Written(alone: "The first recording has been played twice, and stopped halfway both times."),
            Repairs.Replacing(Written(alone: "The anchorage is quiet."), 2)));
        using var _ = fixtures;

        Assert.Null(refusal);
        Assert.Equal(3, fixtures.Provider.Requests.Count);
        Assert.Contains(
            "A recording is mentioned and not played: Objective 2",
            fixtures.Provider.Requests[2].Prompt.History[0].Text,
            StringComparison.Ordinal);
        Assert.Equal("The anchorage is quiet.", Chapter(fixtures).Beats[1].Lines[0].Text);
    }

    [Fact]
    public async Task AnOpeningThatPlaysARecordingIsRefusedAsTheOpening()
    {
        var (fixtures, refusal) = await PickAsync(Scripted(Written(opening: "The first recording has been played twice."), Repairs.ReplacingOpening(Written())));
        using var _ = fixtures;

        Assert.Null(refusal);
        Assert.Contains("A recording is mentioned and not played: The opening", fixtures.Provider.Requests[2].Prompt.History[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARecordingPlayedBesideTheSpeakersOwnLineIsNotRefused()
    {
        var (fixtures, refusal) = await PickAsync(Scripted(Written(narrated: "A recording plays from the chip.")));
        using var _ = fixtures;

        Assert.Null(refusal);
        Assert.Equal(2, fixtures.Provider.Requests.Count);
    }

    [Fact]
    public async Task ARecordingMentionedButNotPlayedIsNotRefused()
    {
        var (fixtures, refusal) = await PickAsync(Scripted(Written(alone: "Nobody can say when the recordings were made. The Commander plays the long game.")));
        using var _ = fixtures;

        Assert.Null(refusal);
        Assert.Equal(2, fixtures.Provider.Requests.Count);
    }

    [Fact]
    public async Task HearingTheShipsVoiceIsNotRefused()
    {
        var (fixtures, refusal) = await PickAsync(Scripted(Written(alone: "The Commander hears the ship's voice confirm the jump.")));
        using var _ = fixtures;

        Assert.Null(refusal);
        Assert.Equal(2, fixtures.Provider.Requests.Count);
    }

    [Fact]
    public async Task ARecordingStillUnplayedAfterTheRewriteStillYieldsAChapter()
    {
        var stubborn = Written(alone: "A message is heard over comms.");
        var (fixtures, refusal) = await PickAsync(Scripted(stubborn, Repairs.Replacing(stubborn, 2), Repairs.Replacing(stubborn, 2)));
        using var _ = fixtures;

        Assert.Null(refusal);
        Assert.Equal(2 + AdventureGenerator.RepairRounds, fixtures.Provider.Requests.Count);
        Assert.Equal("A message is heard over comms.", Chapter(fixtures).Beats[1].Lines[0].Text);
    }
}
