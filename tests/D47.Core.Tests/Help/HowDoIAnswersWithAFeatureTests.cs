using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Help;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Help;

/// <summary>"How do I ..." lands on a feature by the words that reach it, model-free (#170).</summary>
public class HowDoIAnswersWithAFeatureTests
{
    private static TurnLoop Build(TestSurface surface, ILlmProvider? provider = null)
    {
        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(provider is not null),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            settings: surface.Settings,
            clock: new InstantClock(),
            offers: surface.Offers);

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

    [Fact]
    public async Task ASingleMatchAnswersWithTheLeaf()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var loop = Build(surface);

        var result = await RunAsync(loop, "how do I plot a course");

        Assert.Equal(TurnRoute.KeywordRouter, result.Route);
        Assert.StartsWith(
            "Put a system name on your clipboard, and try to plot a course to it.",
            result.Text,
            StringComparison.Ordinal);
        Assert.False(loop.Offers.IsStanding);
    }

    [Fact]
    public async Task TwoOrThreeMatchesOfferThemAndAnAnswerPicksOne()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var loop = Build(surface);

        var offered = await RunAsync(loop, "how do I see my journal session");

        Assert.Equal(TurnRoute.Offer, offered.Route);
        Assert.Equal("Two ways: Journal, or Commander's log. Which one?", offered.Text);
        Assert.True(loop.Offers.IsStanding);

        var picked = await RunAsync(loop, "Journal");

        Assert.Equal(TurnRoute.Offer, picked.Route);
        Assert.StartsWith(
            "Report where the Commander is",
            picked.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoMatchReachesTheModelWhenOneIsConfigured()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("From the model.");
        var loop = Build(TestSurface.For(install), provider);

        var result = await RunAsync(loop, "how do I get rich quick");

        Assert.Equal(TurnRoute.Model, result.Route);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task NoMatchWithNoModelAnswersWithTheTopLevel()
    {
        using var install = new TempInstall();
        var loop = Build(TestSurface.For(install));

        var result = await RunAsync(loop, "how do I get rich quick");

        Assert.Equal(TurnRoute.KeywordRouter, result.Route);
        Assert.EndsWith("Which one?", result.Text, StringComparison.Ordinal);
        Assert.True(loop.Offers.IsStanding);

        var count = int.Parse(result.Text.Split(' ')[0]);
        Assert.InRange(count, 1, HelpTaxonomy.MostAtOnce);
    }

    [Fact]
    public void MoreThanThreeSharedWordsDoesNotNarrowToAFewLeaves()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;

        var matched = HowDoI.Match("ship", registry);

        Assert.True(matched.Count > 3, $"Expected more than 3 leaves to mention \"ship\", got {matched.Count}.");
    }

    [Fact]
    public async Task AQuestionAboutEliteRankIsNotTakenByThisStep()
    {
        using var install = new TempInstall();
        var provider = FakeLlmProvider.Answering("From the model.");
        var loop = Build(TestSurface.For(install), provider);

        var result = await RunAsync(loop, "what is my elite rank in combat");

        Assert.Equal(TurnRoute.Model, result.Route);
        Assert.Equal(1, provider.CallCount);
    }
}
