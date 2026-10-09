using D47.Core.Adventures;
using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A line the narrator reads in a story chapter is never in the first person and never offers, asks or waits.</summary>
[Trait("Category", "Integration")]
public sealed class TheNarratorNeverSpeaksAsItselfTests
{
    private const string FirstPersonOpening = "Sidewinder, still in yard paint, and nobody aboard to talk to but me.";

    private const string Beats = """
        {"opening": "A voice on the open channel.", "openingSpeaker": "narrator", "reply": "Here it is.", "beats": [
          {"title": "Yard Paint", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "lines": [
            {"text": "NARRATED", "speaker": "narrator"},
            {"text": "CAST", "speaker": "dock-hand"}
          ]},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "Someone there knows about the burst.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "lines": [{"text": "SHIP", "speaker": "ship"}]},
          {"title": "The Beacon", "function": "resolution", "kind": "beacon", "system": "Ossen's Lantern", "line": "Scan it."}
        ]}
        """;

    private static string Written(string narrated = "The yard lights stay on.", string cast = "Nobody. Go.", string ship = "To one name.", string opening = "A voice on the open channel.") =>
        Beats
            .Replace("NARRATED", narrated, StringComparison.Ordinal)
            .Replace("CAST", cast, StringComparison.Ordinal)
            .Replace("SHIP", ship, StringComparison.Ordinal)
            .Replace("A voice on the open channel.", opening, StringComparison.Ordinal);

    private static RoundScriptedLlmProvider Scripted(params string[] answers) =>
        new([RoundScriptedLlmProvider.Saying(Spine), .. answers.Select(answer => RoundScriptedLlmProvider.Saying(answer))]);

    private static async Task<(StoryFixtures Fixtures, string? Refusal)> PickAsync(RoundScriptedLlmProvider provider, bool stock = false)
    {
        var fixtures = new StoryFixtures(provider);

        if (stock)
        {
            fixtures.Director.Aboard = () => PersonaCatalog.Covas;
        }

        return (fixtures, await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
    }

    private static Adventure Chapter(StoryFixtures fixtures) =>
        fixtures.Book.Store.Find("F1", fixtures.Stories.Current("F1")!.CurrentChapter!)!;

    [Fact]
    public async Task TheChapterWriterIsToldTheNarratorNeverSpeaksAsItself()
    {
        var (fixtures, refusal) = await PickAsync(Scripted(Written()));
        using var _ = fixtures;

        Assert.Null(refusal);

        var prompt = fixtures.Provider.Requests[1].Prompt.History[0].Text;

        Assert.Contains("who tells the story in the third person from outside the cockpit, never says \"I\", \"me\" or \"we\"", prompt, StringComparison.Ordinal);
        Assert.Contains("never speaks to the Commander, and never offers, asks or waits for anything", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStockCoreMakesTheShipsLineTheNarratorsAndHoldsItToTheSameRule()
    {
        var (fixtures, refusal) = await PickAsync(Scripted(Written()), stock: true);
        using var _ = fixtures;

        Assert.Null(refusal);
        Assert.Contains(
            "it is the narrator's line and follows the same rule",
            fixtures.Provider.Requests[1].Prompt.History[0].Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANarratorLineInTheFirstPersonIsRewrittenAndTheRewriteIsKept()
    {
        var (fixtures, refusal) = await PickAsync(Scripted(
            Written(narrated: "Nobody aboard but me, and I'll say so."),
            Written(narrated: "Nobody is aboard but the Commander.")));
        using var _ = fixtures;

        Assert.Null(refusal);
        Assert.Equal(3, fixtures.Provider.Requests.Count);
        Assert.Contains(
            "The narrator speaks as itself: Objective 1 is read by the narrator",
            fixtures.Provider.Requests[2].Prompt.History[0].Text,
            StringComparison.Ordinal);
        Assert.Equal("Nobody is aboard but the Commander.", Chapter(fixtures).Beats[0].Lines[0].Text);
    }

    [Fact]
    public async Task ANarratorOpeningInTheFirstPersonIsRefusedAsTheOpening()
    {
        var (fixtures, refusal) = await PickAsync(Scripted(Written(opening: FirstPersonOpening), Written()));
        using var _ = fixtures;

        Assert.Null(refusal);
        Assert.Contains("The narrator speaks as itself: The opening is read", fixtures.Provider.Requests[2].Prompt.History[0].Text, StringComparison.Ordinal);
        Assert.Equal("A voice on the open channel.", Chapter(fixtures).Opening);
    }

    [Fact]
    public async Task AnOpeningWithNoSpeakerIsTheShipsSoItIsTheNarratorsOnlyWithAStockCore()
    {
        var noSpeaker = Written(opening: FirstPersonOpening).Replace("\"openingSpeaker\": \"narrator\", ", string.Empty, StringComparison.Ordinal);

        var (own, _) = await PickAsync(Scripted(noSpeaker));
        using var ownFixtures = own;
        var (stock, _) = await PickAsync(Scripted(noSpeaker, Written()), stock: true);
        using var stockFixtures = stock;

        Assert.Equal(2, ownFixtures.Provider.Requests.Count);
        Assert.Equal(3, stockFixtures.Provider.Requests.Count);
    }

    [Fact]
    public async Task AFirstPersonShipLineIsRefusedWithAStockCoreAndNotWithAnother()
    {
        var ship = Written(ship: "We should get to the anchorage.");

        var (stock, stockRefusal) = await PickAsync(Scripted(ship, Written()), stock: true);
        using var stockFixtures = stock;
        var (own, ownRefusal) = await PickAsync(Scripted(ship));
        using var ownFixtures = own;

        Assert.Null(stockRefusal);
        Assert.Null(ownRefusal);
        Assert.Equal(3, stockFixtures.Provider.Requests.Count);
        Assert.Contains("The narrator speaks as itself: Objective 2 is read", stockFixtures.Provider.Requests[2].Prompt.History[0].Text, StringComparison.Ordinal);
        Assert.Equal(2, ownFixtures.Provider.Requests.Count);
    }

    [Fact]
    public async Task AFirstPersonCastLineIsNotRefused()
    {
        var (fixtures, refusal) = await PickAsync(Scripted(Written(cast: "I'm Ren. Nobody told me you were coming. We close at six.")));
        using var _ = fixtures;

        Assert.Null(refusal);
        Assert.Equal(2, fixtures.Provider.Requests.Count);
        Assert.Equal("dock-hand", Chapter(fixtures).Beats[0].Lines[1].Speaker);
    }

    [Fact]
    public async Task AFirstPersonWordInsideAnotherWordOrAnotherCaseIsNotRefused()
    {
        var (fixtures, refusal) = await PickAsync(Scripted(Written(narrated: "The mist rises. i is a letter. Meet Misha at the museum, and the USS Meridian waits.")));
        using var _ = fixtures;

        Assert.Null(refusal);
        Assert.Equal(2, fixtures.Provider.Requests.Count);
    }

    [Fact]
    public async Task ACurlyApostropheDoesNotHideTheFirstPerson()
    {
        var (fixtures, refusal) = await PickAsync(Scripted(Written(narrated: "Standing by. I’ll play it."), Written()));
        using var _ = fixtures;

        Assert.Null(refusal);
        Assert.Equal(3, fixtures.Provider.Requests.Count);
    }

    [Fact]
    public async Task ANarratorLineStillInTheFirstPersonAfterTheRewriteStillYieldsAChapter()
    {
        var stubborn = Written(narrated: "Nobody aboard but me.");
        var (fixtures, refusal) = await PickAsync(Scripted(stubborn, stubborn));
        using var _ = fixtures;

        Assert.Null(refusal);
        Assert.Equal(3, fixtures.Provider.Requests.Count);
        Assert.Equal("Nobody aboard but me.", Chapter(fixtures).Beats[0].Lines[0].Text);
        Assert.Equal(StorySpeaker.Narrator, Chapter(fixtures).Beats[0].Lines[0].Speaker);
    }
}
