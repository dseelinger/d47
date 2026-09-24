using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Help;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>The clock registers on every run; timers and alarms only with the switch (#90, #458).</summary>
public class TheClockAnswersWithoutTheSwitchTests
{
    private static readonly string[] TimerTools = ["set_timer", "set_alarm", "cancel_reminder"];

    private static readonly string[] ClockPhrases =
    [
        "what is the date", "what's the date", "what time is it", "what is the time", "what's the time",
        "what day is it",
    ];

    private static readonly string[] TimerPhrases =
    [
        "cancel the timer", "cancel my timer", "cancel the alarm", "cancel my alarm", "stop the timer",
    ];

    [Fact]
    public void OffTheClockIsRegisteredAndNoTimerOrAlarmToolIs()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install, timersAndAlarms: false).Registry;

        Assert.NotNull(registry.Find(ClockCapability.Id));
        Assert.Contains("say_the_time", registry.ToolNames);
        Assert.Null(registry.Find(UtilitiesCapability.Id));

        foreach (var tool in TimerTools)
        {
            Assert.DoesNotContain(tool, registry.ToolNames);
        }
    }

    [Fact]
    public void OnTheClockAndEveryTimerToolAreRegistered()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install, timersAndAlarms: true).Registry;

        Assert.NotNull(registry.Find(ClockCapability.Id));
        Assert.NotNull(registry.Find(UtilitiesCapability.Id));

        foreach (var tool in TimerTools.Append("say_the_time"))
        {
            Assert.Contains(tool, registry.ToolNames);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryClockPhraseReachesSayTheTime(bool timersAndAlarms)
    {
        using var install = new TempInstall();
        var router = new KeywordRouter(TestSurface.For(install, timersAndAlarms: timersAndAlarms).Registry);

        foreach (var phrase in ClockPhrases)
        {
            var tool = router.MatchToolCommand(phrase)?.ToolName ?? router.Match(phrase)?.ToolName;

            Assert.True(tool == "say_the_time", $"\"{phrase}\" reached {tool ?? "nothing"}.");
        }
    }

    [Fact]
    public void OffNoTimerPhraseMatches()
    {
        using var install = new TempInstall();
        var router = new KeywordRouter(TestSurface.For(install, timersAndAlarms: false).Registry);

        foreach (var phrase in TimerPhrases)
        {
            Assert.Null(router.Match(phrase));
            Assert.Null(router.MatchToolCommand(phrase));
        }
    }

    [Fact]
    public async Task WhatTimeIsItIsAnsweredWithBothDates()
    {
        var now = new DateTimeOffset(2026, 8, 17, 21, 4, 0, TimeSpan.Zero);
        var registry = CapabilityRegistry.Build([ClockCapability.Create(() => now, () => TimeZoneInfo.Utc)]);
        var match = new KeywordRouter(registry).MatchToolCommand("what time is it");

        Assert.NotNull(match);

        var result = await registry.Invoke(match.ToolName, ToolArguments.Empty);

        Assert.False(result.IsError);
        Assert.Contains("3312", result.Content, StringComparison.Ordinal);
        Assert.Contains("2026", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpForTheClockOpens()
    {
        Assert.NotNull(HelpLibrary.For(ClockCapability.Id));
    }
}
