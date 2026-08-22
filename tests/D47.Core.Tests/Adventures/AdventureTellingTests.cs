using D47.Core.Adventures;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// What was said about a story, kept — and the wait between a beat firing and it being said
/// (asked for 2026-08-22).
/// </summary>
public class AdventureTellingTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-adventure-telling", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private AdventureBook Wired(bool begun = true)
    {
        var store = new AdventureStore(Path.Combine(_folder, "adventures.json"), NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);
        book.Write("F1", LanternRoute(begun ? Accepted : null));
        book.CatchUp([]);
        return book;
    }

    private static AdventureTold Beat(string text, DateTimeOffset at) => new()
    {
        Kind = AdventureToldKind.Beat,
        Text = text,
        At = at,
        Beat = 0,
        Title = "The Lantern",
        Trigger = "arrive at Ossen's Lantern",
    };

    // ---- the wait ------------------------------------------------------------------------------

    /// <summary>
    /// A beat firing starts the wait, and the line arriving ends it. This is the whole of what the
    /// tab's animation is drawn from: the Commander asked to be shown that d47 is composing rather
    /// than that they had failed to do the thing.
    /// </summary>
    [Fact]
    public void AFiredBeatWaitsUntilItsLineHasBeenSaid()
    {
        var book = Wired();
        var raised = 0;
        book.StirringChanged += () => raised++;

        Assert.False(book.IsStirring("F1", "the-lantern-route"));

        var reached = Accepted.AddMinutes(1);
        book.Observe(Jump(Lantern, reached), "F1");

        Assert.True(book.IsStirring("F1", "the-lantern-route"));
        Assert.True(book.IsStirringAnywhere("F1"));
        Assert.Equal(1, raised);

        book.Told("F1", "the-lantern-route", Beat("Scoop here, in my own words.", reached.AddSeconds(23)));

        Assert.False(book.IsStirring("F1", "the-lantern-route"));
        Assert.False(book.IsStirringAnywhere("F1"));
        Assert.Equal(2, raised);
    }

    /// <summary>
    /// A beat dropped rather than spoken — mid-interdiction — stops the wait too. Without this the
    /// animation runs until something else happens to clear it, which is worse than never having
    /// shown one: it says d47 is about to speak, and d47 has decided not to.
    /// </summary>
    [Fact]
    public void ADroppedBeatStopsTheWaitWithoutRecordingAnything()
    {
        var book = Wired();
        book.Observe(Jump(Lantern, Accepted.AddMinutes(1)), "F1");

        book.Quiet("F1", "the-lantern-route");

        Assert.False(book.IsStirring("F1", "the-lantern-route"));
        Assert.Empty(book.Store.Find("F1", "the-lantern-route")!.Told);
    }

    /// <summary>Abandoning a story stops its wait, exactly as it drops the beat that was settling.</summary>
    [Fact]
    public void AbandoningStopsTheWait()
    {
        var book = Wired();
        book.Observe(Jump(Lantern, Accepted.AddMinutes(1)), "F1");

        Assert.Null(book.Abandon("F1", "the-lantern-route", Accepted.AddMinutes(2)));
        Assert.False(book.IsStirring("F1", "the-lantern-route"));
    }

    // ---- what was said -------------------------------------------------------------------------

    /// <summary>
    /// The feed is what the Commander heard, in order, with the trigger stored beside it — not the
    /// authored line, which the definition already holds.
    /// </summary>
    [Fact]
    public void WhatWasSaidIsKeptInOrderWithItsTrigger()
    {
        var book = Wired();

        book.Told("F1", "the-lantern-route", Beat("The beacon is still turning.", Accepted.AddMinutes(1)));
        book.Told("F1", "the-lantern-route", new AdventureTold
        {
            Kind = AdventureToldKind.Aside,
            Text = "Whoever pays for it has not stopped.",
            Asked = "Who is keeping the lantern lit?",
            At = Accepted.AddMinutes(4),
        });

        var told = book.Store.Find("F1", "the-lantern-route")!.Told;

        Assert.Equal(2, told.Count);
        Assert.Equal(AdventureToldKind.Beat, told[0].Kind);
        Assert.Equal("arrive at Ossen's Lantern", told[0].Trigger);
        Assert.Equal("The Lantern", told[0].Title);

        Assert.Equal(AdventureToldKind.Aside, told[1].Kind);
        Assert.Equal("Who is keeping the lantern lit?", told[1].Asked);
    }

    /// <summary>Bounded, so a file the Commander can open by hand stays one they can read.</summary>
    [Fact]
    public void TheFeedIsCappedAndKeepsTheNewest()
    {
        var book = Wired();

        for (var index = 0; index < AdventureLimits.MaxTold + 12; index++)
        {
            book.Told("F1", "the-lantern-route", Beat($"line {index}", Accepted.AddMinutes(index)));
        }

        var told = book.Store.Find("F1", "the-lantern-route")!.Told;

        Assert.Equal(AdventureLimits.MaxTold, told.Count);
        Assert.Equal($"line {AdventureLimits.MaxTold + 11}", told[^1].Text);
        Assert.Equal("line 12", told[0].Text);
    }

    /// <summary>A story begun again is being told again, so the feed of the last run does not ride along.</summary>
    [Fact]
    public void BeginningAgainClearsTheFeed()
    {
        var book = Wired();
        book.Told("F1", "the-lantern-route", Beat("From the first run.", Accepted.AddMinutes(1)));

        Assert.Null(book.Abandon("F1", "the-lantern-route", Accepted.AddMinutes(2)));
        Assert.Null(book.Begin("F1", "the-lantern-route", Accepted.AddMinutes(3)));

        Assert.Empty(book.Store.Find("F1", "the-lantern-route")!.Told);
    }

    /// <summary>A story that is not on file is not an error: it was removed while a beat was settling.</summary>
    [Fact]
    public void RecordingAgainstAStoryThatIsGoneIsSilent()
    {
        var book = Wired();
        book.Told("F1", "no-such-story", Beat("Nowhere.", Accepted));
    }

    // ---- the step ------------------------------------------------------------------------------

    /// <summary>
    /// The count the Commander asked for on 2026-08-22, and the phase's own rule until that day.
    /// The step is the one being worked on, so a story with nothing reached is on step 1.
    /// </summary>
    [Fact]
    public void TheStepCountsTheBeatBeingWorkedOn()
    {
        var book = Wired();

        Assert.Equal("Step 1 of 5", book.Standing("F1", "the-lantern-route")!.Step());

        book.Observe(Jump(Lantern, Accepted.AddMinutes(1)), "F1");
        Assert.Equal("Step 2 of 5", book.Standing("F1", "the-lantern-route")!.Step());

        foreach (var journalEvent in WholeRoute(Accepted.AddMinutes(1)))
        {
            book.Observe(journalEvent, "F1");
        }

        var done = book.Standing("F1", "the-lantern-route")!;

        Assert.True(done.IsDone);
        Assert.Equal("Step 5 of 5", done.Step());
    }

    /// <summary>A story nobody has agreed to has no step: a count on it would read as one under way.</summary>
    [Fact]
    public void AStoryNotBegunHasNoStep()
    {
        var book = Wired(begun: false);

        Assert.Null(book.Standing("F1", "the-lantern-route")!.Step());
    }

    // ---- the acknowledgements ------------------------------------------------------------------

    /// <summary>
    /// Ten of them, short, and the pool wraps — the whole design is that they cost no model call
    /// and almost no synthesis, because they exist to arrive before anything else does.
    /// </summary>
    [Fact]
    public void TheAcknowledgementsAreTenShortLinesAndTheIndexWraps()
    {
        Assert.Equal(10, AdventureAcks.Count);

        var lines = Enumerable.Range(0, AdventureAcks.Count).Select(AdventureAcks.Pick).ToList();

        Assert.Equal(lines.Count, lines.Distinct(StringComparer.Ordinal).Count());
        Assert.All(lines, line => Assert.InRange(line.Split(' ').Length, 1, 5));
        Assert.Equal(AdventureAcks.Pick(0), AdventureAcks.Pick(AdventureAcks.Count));
        Assert.Equal(AdventureAcks.Pick(3), AdventureAcks.Pick(-3));
    }

    /// <summary>
    /// The acknowledgement's key must not sit under the beat's prefix, or <c>FlavourBriefs</c>
    /// sends it through the model round trip it exists to arrive ahead of.
    /// </summary>
    [Fact]
    public void TheAcknowledgementKeyIsNotABeatKey()
    {
        Assert.False(AdventureCallout.AckPrefix.StartsWith(AdventureCallout.KeyPrefix, StringComparison.Ordinal));
        Assert.Null(AdventureCallout.Reached($"{AdventureCallout.AckPrefix}the-lantern-route.0"));
    }

    /// <summary>The app has only the announcement to go on, so the key has to read back.</summary>
    [Theory]
    [InlineData("adventure.the-lantern-route.0", "the-lantern-route", 0)]
    [InlineData("adventure.the-lantern-route.4", "the-lantern-route", 4)]
    [InlineData("adventure.the-lantern-route.opening", "the-lantern-route", -1)]
    [InlineData("adventure.a.dotted.key.2", "a.dotted.key", 2)]
    public void ABeatKeyReadsBackToItsStoryAndBeat(string key, string story, int beat)
    {
        var read = AdventureCallout.Reached(key);

        Assert.NotNull(read);
        Assert.Equal(story, read!.Value.Key);
        Assert.Equal(beat, read.Value.Beat);
    }

    [Theory]
    [InlineData("ambient.docked.3")]
    [InlineData("adventure.")]
    [InlineData("adventure.nokey")]
    [InlineData(null)]
    public void AnythingElseReadsBackAsNothing(string? key) => Assert.Null(AdventureCallout.Reached(key));

    // ---- what counts as being about the story --------------------------------------------------

    /// <summary>
    /// The heuristic the Commander chose: the story's name, a beat's title, or a place a beat waits
    /// at. Either side of the exchange counts — a Commander who says "what about the anchorage" and
    /// one who says "and now?" and is answered with it are both talking about the story.
    /// </summary>
    [Theory]
    [InlineData("What is the Lantern Route actually about?")]
    [InlineData("tell me about maren anchorage")]
    [InlineData("Why is the survey filed so late?")]
    [InlineData("Anything at Cairn of Veyl worth seeing?")]
    public void AnExchangeNamingTheStoryIsAboutIt(string asked) =>
        Assert.True(AdventureMention.InExchange(LanternRoute(Accepted), asked, "Some answer."));

    [Fact]
    public void EitherSideOfTheExchangeCounts() =>
        Assert.True(AdventureMention.InExchange(
            LanternRoute(Accepted), "and now?", "Maren Anchorage is where the manifest is."));

    /// <summary>
    /// Whole words only, and nothing shorter than four letters. Substring matching on Elite's names
    /// is how a beat called "The Survey" matches "surveying the market" — which would fill an
    /// adventure-only page with conversation about anything.
    /// </summary>
    [Theory]
    [InlineData("How much fuel does a lanterns worth of scooping take?")]
    [InlineData("Where is the nearest material trader?")]
    [InlineData("Set a timer for twenty minutes.")]
    public void AnExchangeAboutSomethingElseIsNot(string asked) =>
        Assert.False(AdventureMention.InExchange(LanternRoute(Accepted), asked, "Some answer."));

    /// <summary>
    /// A title beginning "The " answers to the rest of it as well: a Commander asks about "the
    /// Anchorage" and about "Maren Anchorage", and neither is the string the beat was titled with.
    /// </summary>
    [Fact]
    public void ATitleAnswersToItselfWithoutTheArticle() =>
        Assert.True(AdventureMention.Mentions(LanternRoute(Accepted), "what happened at the anchorage?"));

    /// <summary>A three-letter place would match too much to be evidence of anything.</summary>
    [Fact]
    public void AShortNameIsNotLookedFor()
    {
        var story = LanternRoute(Accepted) with
        {
            Name = "Ash",
            Beats = [AdventureFixtures.Beat("Sol", "setup", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 1, System = "Sol" }, "Home.")],
        };

        Assert.False(AdventureMention.Mentions(story, "the solar wind is a problem here, ash and all"));
    }
}
