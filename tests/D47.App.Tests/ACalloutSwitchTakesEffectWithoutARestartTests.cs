using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A lambda installed on a callout in <c>BuildCallouts</c> or <c>ApplyCalloutSettings</c> reads the
/// live <c>SettingsService</c> rather than a <c>D47Settings</c> snapshot, so a switch in the panel
/// takes effect on the next tick instead of the next launch (#139).
/// </summary>
public class ACalloutSwitchTakesEffectWithoutARestartTests
{
    [Fact]
    public void BuildCalloutsTakesTheLiveSettingsServiceRatherThanAStartupSnapshot()
    {
        Assert.Equal("SettingsService settings", AppSource.Method("BuildCallouts").Parameters[0]);
    }

    [Fact]
    public void ApplyCalloutSettingsTakesTheLiveSettingsServiceRatherThanAStartupSnapshot()
    {
        Assert.Equal(
            ["CalloutEngine engine", "SettingsService settings"],
            AppSource.Method("ApplyCalloutSettings").Parameters);
    }

    /// <summary>
    /// The one caller outside composition: a callout-settings change re-applies against the live
    /// service, not a snapshot taken when the change fired.
    /// </summary>
    [Fact]
    public void TheSettingsChangedHandlerPassesTheLiveServiceToApplyCalloutSettings()
    {
        Assert.Contains(AppSource.CodeLines("ApplyCalloutSettings("), line => line.Text == "ApplyCalloutSettings(Callouts, Settings);");
        Assert.Empty(AppSource.CodeLines("ApplyCalloutSettings(Callouts, Settings.Current)"));
    }

    /// <summary>
    /// Every settings-reading lambda between the two methods reads through <c>settings.Current</c>.
    /// A bare <c>settings.Callouts.X</c> or <c>settings.Speech.X</c> would mean <c>settings</c> is
    /// once again a <c>D47Settings</c> snapshot rather than the live service — the exact shape of
    /// the defect this guards.
    /// </summary>
    [Fact]
    public void NoLambdaBetweenTheTwoMethodsReadsSettingsWithoutGoingThroughCurrent()
    {
        var bareReads = CalloutBuilderLines()
            .Where(line => line.Contains("settings.", StringComparison.Ordinal))
            .Where(line => !line.Contains("settings.Current.", StringComparison.Ordinal))
            .Where(line => !line.Contains("SettingsService settings", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(bareReads);
    }

    /// <summary>The seven rows that must be live: the six read on <c>IncomingMessages</c>, plus the
    /// personality half of the two chatter gates.</summary>
    [Fact]
    public void EveryRowTheIssueNamedReadsLiveSettings()
    {
        var region = CalloutBuilderLines();

        Assert.Contains("Enabled = () => settings.Current.Speech.SpeakIncomingMessages,", region);
        Assert.Contains("IncludeNpcs = () => settings.Current.Speech.SpeakNpcMessages,", region);
        Assert.Contains(region, line => line.Contains("\"starsystem\" => settings.Current.Speech.SpeakSystemChat,"));
        Assert.Contains(region, line => line.Contains("\"local\" => settings.Current.Speech.SpeakLocalChat,"));
        Assert.Contains(region, line => line.Contains("\"wing\" => settings.Current.Speech.SpeakWingChat,"));
        Assert.Contains(
            region,
            line => line.Contains("\"squadron\" or \"squadleaders\" => settings.Current.Speech.SpeakSquadronChat,"));
        Assert.Contains(region, line => line.Contains("\"player\" => settings.Current.Speech.SpeakDirectMessages,"));

        Assert.Contains(
            "ambient.Enabled = () => settings.Current.Callouts.Ambient && settings.Current.Llm.PersonalityEnabled;",
            region);
        Assert.Contains(
            "chatter.Enabled = () => settings.Current.Callouts.NpcChatter && settings.Current.Llm.PersonalityEnabled;",
            region);
    }

    /// <summary>The code lines of <c>BuildCallouts</c> and <c>ApplyCalloutSettings</c>, trimmed.</summary>
    private static List<string> CalloutBuilderLines() =>
        [.. AppSource.Method("BuildCallouts").CodeLines().Concat(AppSource.Method("ApplyCalloutSettings").CodeLines())
            .Select(line => line.Text)];
}
