using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// The two kinds of unprompted chatter are named for <em>who is speaking</em> rather than for the
/// occasion (asked for 2026-09-02).
/// <para>
/// <b>"Ambient" described when the line arrives and left a Commander to work out whose voice it
/// was</b>, and "invented" described how it was made, which is d47's problem rather than theirs.
/// The pair reads as a boundary now — <b>In Ship chatter</b> is your own AI and crew, <b>NPC
/// chatter</b> is everybody outside the ship — and it borrows the wording the Aboard voice slot
/// already uses, so one vocabulary covers the callout rows and the voice rows both.
/// </para>
/// <para>
/// <b>The keys did not move and never can.</b> <c>settings.json</c> is append-only, and
/// <c>callouts.ambientSeconds</c> already carries one retired minutes-era sibling for exactly that
/// reason. A rename that reached the keys would be a rename that lost every Commander's settings.
/// </para>
/// </summary>
public class TheTwoChattersAreNamedForWhoSpeaksTests
{
    private static IReadOnlyList<SettingRow> Rows()
    {
        using var install = new TempInstall();

        return TestSurface.For(install).Registry.All
            .SelectMany(capability => capability.Descriptor.Settings)
            .Where(row => row.Key.StartsWith("callouts.", StringComparison.Ordinal))
            .ToList();
    }

    private static SettingRow Row(string key) => Rows().Single(row => row.Key == key);

    [Theory]
    [InlineData(CalloutCapability.AmbientKey, "In Ship chatter")]
    [InlineData(CalloutCapability.AmbientSecondsKey, "The least time between In Ship chatter")]
    [InlineData(CalloutCapability.AmbientMaxSecondsKey, "The most time between In Ship chatter")]
    [InlineData(CalloutCapability.NpcChatterKey, "NPC chatter")]
    [InlineData(CalloutCapability.NpcChatterSecondsKey, "The least time between NPC chatter")]
    [InlineData(CalloutCapability.NpcChatterMaxSecondsKey, "The most time between NPC chatter")]
    public void EachRowIsDrawnUnderTheNewName(string key, string label) =>
        Assert.Equal(label, Row(key).Label);

    /// <summary>
    /// And the old words reach no Commander through any of these six rows. Both halves of a
    /// rename matter: a page carrying the new name in the label and the old one in the help is a
    /// page that has two names for one thing.
    /// </summary>
    [Theory]
    [InlineData(CalloutCapability.AmbientKey)]
    [InlineData(CalloutCapability.AmbientSecondsKey)]
    [InlineData(CalloutCapability.AmbientMaxSecondsKey)]
    [InlineData(CalloutCapability.NpcChatterKey)]
    [InlineData(CalloutCapability.NpcChatterSecondsKey)]
    [InlineData(CalloutCapability.NpcChatterMaxSecondsKey)]
    public void TheOldWordsAreGoneFromWhatIsDrawn(string key)
    {
        var row = Row(key);
        var drawn = $"{row.Label} {row.Help}";

        Assert.DoesNotContain("ambient", drawn, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("invented exchange", drawn, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <b>Every one of the six says who it is about.</b> That is the whole of the rename's
    /// purpose: the label names the boundary and the help says which voices fall each side of it,
    /// so a Commander deciding between the two rows never has to guess.
    /// </summary>
    [Theory]
    [InlineData(CalloutCapability.AmbientKey, "crew")]
    [InlineData(CalloutCapability.AmbientSecondsKey, "crew")]
    [InlineData(CalloutCapability.AmbientMaxSecondsKey, "crew")]
    [InlineData(CalloutCapability.NpcChatterKey, "outside your ship")]
    [InlineData(CalloutCapability.NpcChatterSecondsKey, "outside your ship")]
    [InlineData(CalloutCapability.NpcChatterMaxSecondsKey, "outside your ship")]
    public void EachHelpSaysWhoItIsAbout(string key, string who) =>
        Assert.Contains(who, Row(key).Help, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <b>Only the AI speaks unasked, and the help says so.</b> Naming the crew without that
    /// clause would have the page promise chatter from people who have never once spoken first —
    /// which is the display telling a Commander something the data does not support.
    /// </summary>
    [Fact]
    public void TheCrewHalfIsNotOversold() =>
        Assert.Contains("unasked", Row(CalloutCapability.AmbientKey).Help, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The spoken route moved with the drawn one. These rows are protected, so a phrase here is
    /// the <em>only</em> way to set them by voice — a rename that left the phrases behind would
    /// leave a Commander saying a name the page no longer uses.
    /// </summary>
    [Theory]
    [InlineData(CalloutCapability.AmbientKey, "stop calling out in ship chatter")]
    [InlineData(CalloutCapability.NpcChatterKey, "stop calling out npc chatter")]
    public void TheSpokenRouteMovedWithIt(string key, string phrase) =>
        Assert.Contains(Row(key).Commands, command => command.Phrase == phrase);

    /// <summary>
    /// The keys are untouched, which is what keeps every settings file already on disk meaning
    /// what it meant.
    /// </summary>
    [Fact]
    public void NoKeyMoved()
    {
        Assert.Equal("callouts.ambient", CalloutCapability.AmbientKey);
        Assert.Equal("callouts.ambientSeconds", CalloutCapability.AmbientSecondsKey);
        Assert.Equal("callouts.ambientMaxSeconds", CalloutCapability.AmbientMaxSecondsKey);
        Assert.Equal("callouts.npcChatter", CalloutCapability.NpcChatterKey);
        Assert.Equal("callouts.npcChatterSeconds", CalloutCapability.NpcChatterSecondsKey);
        Assert.Equal("callouts.npcChatterMaxSeconds", CalloutCapability.NpcChatterMaxSecondsKey);
    }

    /// <summary>
    /// The two pairs carry the same numbers out of the box, and the rows say the same numbers the
    /// record holds. A <c>DefaultDisplay</c> that disagreed with the default would show a reset
    /// glyph on a row nobody had touched.
    /// </summary>
    [Fact]
    public void BothPairsOfferFiveToTenMinutes()
    {
        var fresh = new CalloutSettings();

        Assert.Equal(300, fresh.AmbientSeconds);
        Assert.Equal(600, fresh.AmbientMaxSeconds);
        Assert.Equal(300, fresh.NpcChatterSeconds);
        Assert.Equal(600, fresh.NpcChatterMaxSeconds);

        Assert.Equal("300", Row(CalloutCapability.AmbientSecondsKey).DefaultDisplay);
        Assert.Equal("600", Row(CalloutCapability.AmbientMaxSecondsKey).DefaultDisplay);
        Assert.Equal("300", Row(CalloutCapability.NpcChatterSecondsKey).DefaultDisplay);
        Assert.Equal("600", Row(CalloutCapability.NpcChatterMaxSecondsKey).DefaultDisplay);
    }
}
