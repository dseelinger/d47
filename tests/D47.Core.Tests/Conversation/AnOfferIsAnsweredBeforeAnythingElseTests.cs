using D47.Core.Capabilities;
using D47.Core.Checklists;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A standing offer reads the Commander's next utterance before any other route does.</summary>
public class AnOfferIsAnsweredBeforeAnythingElseTests
{
    private sealed class Spy
    {
        public List<string> Ran { get; } = [];

        public CapabilityRegistry Registry() => CapabilityRegistry.Build(
        [
            new CapabilityDescriptor
            {
                Id = "spy",
                Group = "Test",
                Name = "Spy",
                Summary = "Records what ran.",
                Tools = [Tool("first_thing", "run the first thing"), Tool("second_thing", "run the second thing")],
            },
        ]);

        private ToolDefinition Tool(string name, string phrase) => new()
        {
            Name = name,
            Description = "Records that it ran.",
            Commands = [new ToolCommandPhrase(phrase, new Dictionary<string, string>())],
            Handler = (_, _) =>
            {
                Ran.Add(name);
                return Task.FromResult(ToolResult.Ok($"Ran {name}."));
            },
        };
    }

    private static TurnLoop Build(CapabilityRegistry registry, ILlmProvider? provider = null)
    {
        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(provider is not null),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock());

        loop.Retry = RetryPolicy.Default with { Attempts = 1 };
        return loop;
    }

    private static async Task<TurnResult> RunAsync(TurnLoop loop, string input)
    {
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.Completed completed)
            {
                result = completed.Result;
            }
        }

        Assert.NotNull(result);
        return result;
    }

    private static Offer Routing(params string[] phrases) =>
        new([.. phrases.Select(phrase => new OfferChoice(phrase, new OfferTarget.RoutePhrase(phrase, Guarded: false)))]);

    [Fact]
    public async Task TheSecondRoutesTheSecondPhrase()
    {
        var spy = new Spy();
        var loop = Build(spy.Registry());
        loop.Offers.Open(Routing("run the first thing", "run the second thing"));

        var result = await RunAsync(loop, "the second");

        Assert.Equal(["second_thing"], spy.Ran);
        Assert.Equal(TurnRoute.ActionCommand, result.Route);
    }

    [Fact]
    public async Task GoAheadRunsTheOnlyChoice()
    {
        var spy = new Spy();
        var loop = Build(spy.Registry());
        loop.Offers.Open(Routing("run the first thing"));

        await RunAsync(loop, "go ahead");

        Assert.Equal(["first_thing"], spy.Ran);
    }

    [Fact]
    public async Task YesToTwoChoicesAsksWhichOneAndTheOfferStands()
    {
        var spy = new Spy();
        var loop = Build(spy.Registry());
        loop.Offers.Open(Routing("run the first thing", "run the second thing"));

        var asked = await RunAsync(loop, "yes");

        Assert.Equal(TurnRoute.Offer, asked.Route);
        Assert.Equal("Which one?", asked.Text);
        Assert.Empty(spy.Ran);

        await RunAsync(loop, "the first");

        Assert.Equal(["first_thing"], spy.Ran);
    }

    [Fact]
    public async Task NoRunsNothingAndTheNextUtteranceRoutesNormally()
    {
        var spy = new Spy();
        var provider = FakeLlmProvider.Answering("From the model.");
        var loop = Build(spy.Registry(), provider);
        loop.Offers.Open(Routing("run the first thing", "run the second thing"));

        var dropped = await RunAsync(loop, "no");

        Assert.Equal("Dropped.", dropped.Text);
        Assert.False(loop.Offers.IsStanding);

        var next = await RunAsync(loop, "the first");

        Assert.Equal(TurnRoute.Model, next.Route);
        Assert.Equal(1, provider.CallCount);
        Assert.Empty(spy.Ran);
    }

    [Fact]
    public async Task AnUnrelatedSentenceReachesTheModelInTheSameTurn()
    {
        var spy = new Spy();
        var provider = FakeLlmProvider.Answering("From the model.");
        var loop = Build(spy.Registry(), provider);
        loop.Offers.Open(Routing("run the first thing", "run the second thing"));

        var result = await RunAsync(loop, "how far is it to Colonia");

        Assert.Equal(TurnRoute.Model, result.Route);
        Assert.Equal(1, provider.CallCount);
        Assert.Empty(spy.Ran);
        Assert.False(loop.Offers.IsStanding);
    }

    [Fact]
    public async Task YesAnswersTheOfferBeforeAPendingChecklistProposal()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        surface.ChecklistService.AddNote(ChecklistScope.Universal, "Unlock Lei Cheung");
        surface.ChecklistService.ProposeChange("Unlock Lei Cheung", ProposalKind.Remove);

        var loop = Build(surface.Registry);
        loop.Offers.Open(new Offer([new OfferChoice("the offer", new OfferTarget.Answer(() => new OfferAnswer("Offer taken.")))]));

        var result = await RunAsync(loop, "yes");

        Assert.Equal("Offer taken.", result.Text);
        Assert.Single(surface.ChecklistService.Proposals.Pending);
    }

    [Fact]
    public async Task AnAnswerCanLeaveTheNextOfferStanding()
    {
        var spy = new Spy();
        var loop = Build(spy.Registry());
        var next = Routing("run the second thing");
        loop.Offers.Open(new Offer([new OfferChoice("more", new OfferTarget.Answer(() => new OfferAnswer("Want the second?", next)))]));

        var answered = await RunAsync(loop, "more");

        Assert.Equal("Want the second?", answered.Text);
        Assert.True(loop.Offers.IsStanding);

        await RunAsync(loop, "yeah");

        Assert.Equal(["second_thing"], spy.Ran);
    }

    [Fact]
    public async Task APhraseThatNoLongerRoutesIsNeverHandedToTheModel()
    {
        var spy = new Spy();
        var provider = FakeLlmProvider.Answering("From the model.");
        var loop = Build(spy.Registry(), provider);
        loop.Offers.Open(Routing("run the third thing"));

        var result = await RunAsync(loop, "do it");

        Assert.Equal(TurnRoute.Offer, result.Route);
        Assert.Equal("That isn't available right now.", result.Text);
        Assert.Equal(TurnOutcome.Unsure, result.Outcome);
        Assert.Equal(0, provider.CallCount);
    }

    [Theory]
    [InlineData("first", "alpha route")]
    [InlineData("the second one", "beta route")]
    [InlineData("number three", "gamma route")]
    [InlineData("Number 2.", "beta route")]
    [InlineData("the last", "gamma route")]
    [InlineData("Beta route", "beta route")]
    [InlineData("gamma", "gamma route")]
    public void AnOrdinalOrANamePicks(string said, string choice)
    {
        var window = new OfferWindow();
        window.Open(Routing("alpha route", "beta route", "gamma route"));

        var reading = Assert.IsType<OfferReading.Picked>(window.Read(said));

        Assert.Equal(choice, reading.Choice.Name);
    }

    [Theory]
    [InlineData("no")]
    [InlineData("nope")]
    [InlineData("cancel")]
    [InlineData("never mind")]
    [InlineData("neither")]
    public void ADeclineDeclines(string said)
    {
        var window = new OfferWindow();
        window.Open(Routing("alpha route", "beta route"));

        Assert.IsType<OfferReading.Declined>(window.Read(said));
        Assert.False(window.IsStanding);
    }

    [Fact]
    public void AWordInEveryNameIsNotAPick()
    {
        var window = new OfferWindow();
        window.Open(Routing("alpha route", "beta route"));

        Assert.IsType<OfferReading.Unrelated>(window.Read("route"));
    }

    [Fact]
    public void ASecondUnclearAnswerDropsTheOffer()
    {
        var window = new OfferWindow();
        window.Open(Routing("alpha route", "beta route"));

        Assert.IsType<OfferReading.Unclear>(window.Read("yes"));
        Assert.IsType<OfferReading.Declined>(window.Read("yes"));
        Assert.False(window.IsStanding);
    }
}
