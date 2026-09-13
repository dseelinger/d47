using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Help;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Help;

/// <summary>"What can you do" walks the spoken map one level at a time (#168).</summary>
public class TheDrillWalksTheSpokenMapTests
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
    public async Task WhatCanYouDoAsksAQuestionOverAtMostSixAreas()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var loop = Build(surface);

        var opened = await RunAsync(loop, "what can you do");

        Assert.Equal(TurnRoute.KeywordRouter, opened.Route);
        Assert.EndsWith("Which one?", opened.Text, StringComparison.Ordinal);
        Assert.True(loop.Offers.IsStanding);

        var count = int.Parse(opened.Text.Split(' ')[0]);
        Assert.InRange(count, 1, HelpTaxonomy.MostAtOnce);
    }

    [Theory]
    [InlineData("the second one")]
    [InlineData("Trading and goals")]
    [InlineData("engineering")]
    public async Task AnOrdinalANameAndAPartialNameEachDescendOneLevel(string reply)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var loop = Build(surface);

        await RunAsync(loop, "what can you do");
        var descended = await RunAsync(loop, reply);

        Assert.Equal(TurnRoute.Offer, descended.Route);
        Assert.NotEqual("Dropped.", descended.Text);
        Assert.NotEqual("Which one?", descended.Text);
    }

    [Fact]
    public async Task AnUnrelatedSentenceAfterALevelDropsTheOfferAndRoutesNormally()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var provider = FakeLlmProvider.Answering("From the model.");
        var loop = Build(surface, provider);

        await RunAsync(loop, "what can you do");
        var result = await RunAsync(loop, "how far is it to Colonia");

        Assert.Equal(TurnRoute.Model, result.Route);
        Assert.False(loop.Offers.IsStanding);
    }

    [Fact]
    public async Task ALeafNamesTheFeatureItsPhrasesAndItsPanelPage_FlightAndNavigation()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var loop = Build(surface);

        await RunAsync(loop, "what can you do");
        await RunAsync(loop, "Flying");
        await RunAsync(loop, "Controls");
        var leaf = await RunAsync(loop, "Flight and navigation");

        Assert.Equal(TurnRoute.Offer, leaf.Route);
        Assert.StartsWith(
            "Operate the landing gear, lights, cargo scoop, hardpoints and the frame shift drive.",
            leaf.Text,
            StringComparison.Ordinal);
        Assert.Contains("Say '", leaf.Text, StringComparison.Ordinal);
        Assert.Contains("It has a page on the panel called Flight and navigation.", leaf.Text, StringComparison.Ordinal);
        Assert.False(loop.Offers.IsStanding);
    }

    [Fact]
    public async Task ALeafNamesTheFeatureItsPhrasesAndItsPanelPage_Speech()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var loop = Build(surface);

        await RunAsync(loop, "what can you do");
        await RunAsync(loop, "Talking and voices");
        await RunAsync(loop, "The voice");
        var leaf = await RunAsync(loop, "Speech");

        Assert.Equal(TurnRoute.Offer, leaf.Route);
        Assert.Contains("Say '", leaf.Text, StringComparison.Ordinal);
        Assert.Contains("It has a page on the panel called Speech.", leaf.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ALeafNamesTheFeatureItsPhrasesAndItsPanelPage_Interface()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var loop = Build(surface);

        await RunAsync(loop, "what can you do");
        await RunAsync(loop, "Settings and safety");
        var leaf = await RunAsync(loop, "Interface");

        Assert.Equal(TurnRoute.Offer, leaf.Route);
        Assert.Contains("Say '", leaf.Text, StringComparison.Ordinal);
        Assert.Contains("It has a page on the panel called Interface.", leaf.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheModelCallingGetCapabilitiesOpensNoOffer()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var result = await surface.Registry.InvokeAsync(
            "get_capabilities",
            ToolArguments.Empty,
            TestContext.Current.CancellationToken,
            ToolCaller.Model);

        Assert.False(result.IsError);
        Assert.False(surface.Offers.IsStanding);
    }

    [Fact]
    public async Task TheModelCannotReachTheDrill()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var result = await surface.Registry.InvokeAsync(
            "drill_capabilities",
            ToolArguments.Empty,
            TestContext.Current.CancellationToken,
            ToolCaller.Model);

        Assert.True(result.IsError);
        Assert.False(surface.Offers.IsStanding);
    }
}
