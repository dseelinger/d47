using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A story may open with fixed lines, said once per pick before its scan line and chapter one, each by its own speaker.
/// The lines may name the Commander and follow their gender, resolved when the line is said.
/// </summary>
public sealed class AStoryCanOpenWithFixedLinesTests
{
    private static readonly IReadOnlyList<StoryLine> Opening =
    [
        new("Mayday, mayday. This is {commander}.", "dock-hand"),
        new("{commander} was dumbfounded at hearing {their} own voice call out a mayday {they} had never experienced.", StorySpeaker.Narrator),
    ];

    private static readonly StorySecret WithOpening = Secret with { Opening = Opening };

    private static RoundScriptedLlmProvider Picks(int picks) => new(
        [.. Enumerable.Range(0, picks).SelectMany(_ => new[] { RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsElsewhere) })]);

    [Fact]
    public async Task ThePickLeavesTheOpeningForTheTickOnce()
    {
        using var fixtures = new StoryFixtures(Picks(1), WithOpening);

        await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.True(fixtures.Director.OpeningWaits("F1"));

        var due = fixtures.Director.TakeOpening("F1")!;

        Assert.Equal(Id, due.StoryId);
        Assert.Equal(Card.Title, due.Title);
        Assert.Equal(Opening, due.Lines);
        Assert.False(fixtures.Director.OpeningWaits("F1"));
        Assert.Null(fixtures.Director.TakeOpening("F1"));
    }

    [Fact]
    public async Task AStoryWithoutAnOpeningLeavesNone()
    {
        using var fixtures = new StoryFixtures(Picks(1));

        await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.False(fixtures.Director.OpeningWaits("F1"));
        Assert.Null(fixtures.Director.TakeOpening("F1"));
    }

    [Fact]
    public async Task AWeekLeavesItsOpeningAndItsScanLine()
    {
        var scan = new StoryLine("The data link completes.", StorySpeaker.Narrator);
        var week = WithOpening with
        {
            Scan = scan,
            Clues = [.. Secret.Clues.Take(2)],
            Finale = [.. Secret.Finale.Take(2)],
        };

        using var fixtures = new StoryFixtures(Picks(1), week, Card with { Length = StoryPacing.OneWeek.Key });

        await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.Equal(Opening, fixtures.Director.TakeOpening("F1")!.Lines);
        Assert.Equal(scan, fixtures.Director.TakeNarratedScan("F1")!.Line);
    }

    [Fact]
    public async Task ResumingAPausedStoryDoesNotOpenAgain()
    {
        using var fixtures = new StoryFixtures(Picks(1), WithOpening);

        await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);
        Assert.NotNull(fixtures.Director.TakeOpening("F1"));

        Assert.Null(fixtures.Director.SetOn("F1", on: false, Now.AddHours(1)));
        Assert.Null(fixtures.Director.SetOn("F1", on: true, Now.AddHours(2)));

        Assert.False(fixtures.Director.OpeningWaits("F1"));
        Assert.Null(fixtures.Director.TakeOpening("F1"));
    }

    [Fact]
    public async Task PickingTheStoryAgainOpensItAgain()
    {
        using var fixtures = new StoryFixtures(Picks(3), WithOpening);

        await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);
        Assert.NotNull(fixtures.Director.TakeOpening("F1"));

        await fixtures.Director.SwitchAsync("F1", Other.Id, Now.AddDays(1), CancellationToken.None);
        Assert.Null(fixtures.Director.TakeOpening("F1"));

        await fixtures.Director.SwitchAsync("F1", Id, Now.AddDays(2), CancellationToken.None);
        Assert.Equal(Opening, fixtures.Director.TakeOpening("F1")!.Lines);
    }

    [Fact]
    public async Task AnAbandonedPickSaysNoOpening()
    {
        using var fixtures = new StoryFixtures(Picks(1), WithOpening);

        await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);
        Assert.Null(fixtures.Director.Abandon("F1", Now.AddMinutes(1)));

        Assert.Null(fixtures.Director.TakeOpening("F1"));
    }

    [Fact]
    public async Task TheOpeningNamesAVersionedSpeakerForTheCommandersGender()
    {
        using var fixtures = new StoryFixtures(Picks(1), Versioned with { Opening = [new("{name:cray} is calling.", "cray")] });
        fixtures.Director.Gender = () => CommanderGender.Man;

        await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.Equal("Ellie is calling.", Assert.Single(fixtures.Director.TakeOpening("F1")!.Lines).Text);
    }

    [Theory]
    [InlineData("Jameson", CommanderGender.Man, "Commander Jameson was dumbfounded at hearing his own voice call out a mayday he had never experienced.")]
    [InlineData("Jameson", CommanderGender.Woman, "Commander Jameson was dumbfounded at hearing her own voice call out a mayday she had never experienced.")]
    [InlineData("Jameson", null, "Commander Jameson was dumbfounded at hearing their own voice call out a mayday they had never experienced.")]
    [InlineData(null, CommanderGender.Man, "Commander was dumbfounded at hearing his own voice call out a mayday he had never experienced.")]
    public void TheCommanderTokensFollowTheNameAndGender(string? name, string? gender, string spoken) =>
        Assert.Equal(spoken, StorySecret.ForCommander(Opening[1].Text, name, gender));

    [Fact]
    public void TheOpeningLinesAreHiddenText()
    {
        Assert.Equal(["opening[0]", "opening[1]"], WithOpening.Lines().Take(2).Select(line => line.Field));
        Assert.Contains(WithOpening.Texts(), text => text == ("opening[1]", Opening[1].Text));
    }

    [Fact]
    public void ASealedFileCarriesTheOpening()
    {
        using var folder = new DownloadedStoryFolder();
        folder.WriteIndex(Card);
        folder.WriteSealed(WithOpening);

        Assert.Equal(Opening, StoryCatalog.Load(folder.Path).Secret(Id)!.Opening);
    }

    [Fact]
    public void ASealedFileWithoutAnOpeningHasNone()
    {
        using var folder = new DownloadedStoryFolder();
        folder.WriteIndex(Card);
        folder.WriteSealed(Secret);

        Assert.Null(StoryCatalog.Load(folder.Path).Secret(Id)!.Opening);
    }
}
