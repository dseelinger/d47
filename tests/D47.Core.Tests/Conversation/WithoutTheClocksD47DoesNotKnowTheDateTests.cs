using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Help;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>Timers and alarms left out of the registry take their tools, their phrases and the date with them (#90).</summary>
public class WithoutTheClocksD47DoesNotKnowTheDateTests
{
    private static readonly string[] Tools = ["say_the_time", "set_timer", "set_alarm", "cancel_reminder"];

    private static readonly string[] Phrases =
    [
        "what is the date", "what's the date", "what time is it", "what is the time", "what's the time",
        "what day is it", "cancel the timer", "cancel my timer", "cancel the alarm", "cancel my alarm",
        "stop the timer",
    ];

    [Fact]
    public void LeftOutTheCapabilityAndEveryOneOfItsToolsAreAbsent()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install, timersAndAlarms: false).Registry;

        Assert.Null(registry.Find(UtilitiesCapability.Id));

        foreach (var tool in Tools)
        {
            Assert.DoesNotContain(tool, registry.ToolNames);
        }
    }

    [Fact]
    public void RegisteredTheCapabilityAndEveryOneOfItsToolsArePresent()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install, timersAndAlarms: true).Registry;

        Assert.NotNull(registry.Find(UtilitiesCapability.Id));

        foreach (var tool in Tools)
        {
            Assert.Contains(tool, registry.ToolNames);
        }
    }

    [Fact]
    public void LeftOutNoneOfItsPhrasesMatches()
    {
        using var install = new TempInstall();
        var router = new KeywordRouter(TestSurface.For(install, timersAndAlarms: false).Registry);

        foreach (var phrase in Phrases)
        {
            Assert.Null(router.Match(phrase));
            Assert.Null(router.MatchToolCommand(phrase));
        }
    }

    [Fact]
    public void RegisteredEveryOneOfItsPhrasesMatches()
    {
        using var install = new TempInstall();
        var router = new KeywordRouter(TestSurface.For(install, timersAndAlarms: true).Registry);

        foreach (var phrase in Phrases)
        {
            Assert.True(
                router.Match(phrase) is not null || router.MatchToolCommand(phrase) is not null,
                $"\"{phrase}\" matched nothing.");
        }
    }

    /// <summary>
    /// With no tool and no phrase, the question reaches the model, and the model is told it does not
    /// know the date rather than being left to produce one.
    /// </summary>
    [Fact]
    public async Task AskedTheDateTheModelIsToldToSayItDoesNotKnow()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install, timersAndAlarms: false).Registry;
        var provider = FakeLlmProvider.Answering("I do not know the date.");

        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock())
        {
            Retry = RetryPolicy.Default with { Attempts = 1 },
        };

        var events = new List<TurnEvent>();

        await foreach (var turnEvent in loop.RunAsync(
            "what year is it", cancellationToken: TestContext.Current.CancellationToken))
        {
            events.Add(turnEvent);
        }

        var prompt = Assert.IsType<LlmRequest>(provider.LastRequest).Prompt;

        Assert.Contains(
            "If it is not given, say that you do not know it.",
            prompt.RenderCachedSystemBlock(),
            StringComparison.Ordinal);

        Assert.DoesNotContain("3312", prompt.RenderCachedSystemBlock(), StringComparison.Ordinal);
        Assert.Null(prompt.LiveGameState);
        Assert.Contains(events, turnEvent => turnEvent is TurnEvent.Completed);
    }

    /// <summary>Help resolves against the embedded pages rather than the registry, so it still opens.</summary>
    [Fact]
    public void HelpForTheCapabilityStillOpens()
    {
        Assert.NotNull(HelpLibrary.For(UtilitiesCapability.Id));
    }
}
