using D47.Core.Callouts;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// The opening line of a session (list.md Phase 31, "Picking up where you left off"), as amended
/// on 2026-08-21: a greeting on the Commander's own clock and a readiness, and nothing about the
/// list, the gap or the engineer under their feet — those are answered when asked for.
/// </summary>
public class ContinuityCalloutTests
{
    private static readonly DateTimeOffset Evening = new(3311, 4, 8, 19, 0, 0, TimeSpan.Zero);

    private static CalloutContext At(DateTimeOffset now, bool priming = false) =>
        new(now, priming, null, GameStatus.Unknown, NavRoute.None, []);

    [Theory]
    [InlineData(5, "morning")]
    [InlineData(9, "morning")]
    [InlineData(11, "morning")]
    [InlineData(12, "afternoon")]
    [InlineData(17, "afternoon")]
    [InlineData(18, "evening")]
    [InlineData(23, "evening")]
    [InlineData(2, "evening")]
    public void TheGreetingFollowsTheCommandersClock(int hour, string expected)
    {
        var now = new DateTimeOffset(3311, 4, 8, hour, 30, 0, TimeSpan.FromHours(-5));

        Assert.Equal($"Good {expected}, Commander. Ready to go.", new ContinuityCallout().Compose(now));
    }

    /// <summary>
    /// The hour is the offset's hour, not UTC's: a Commander at 7 pm local is wished good evening
    /// whatever the date line says.
    /// </summary>
    [Fact]
    public void TheHourIsLocalRatherThanUniversal()
    {
        // Nine in the morning five hours west of Greenwich is two in the afternoon there. The
        // Commander is wished good morning.
        var nineInTheMorningLocal = new DateTimeOffset(3311, 4, 8, 9, 0, 0, TimeSpan.FromHours(-5));

        Assert.Equal("morning", ContinuityCallout.TimeOfDay(nineInTheMorningLocal));
        Assert.Equal("afternoon", ContinuityCallout.TimeOfDay(nineInTheMorningLocal.ToUniversalTime()));
    }

    /// <summary>
    /// What the line no longer carries (2026-08-21): "Top of your list: Grade 5 Efficient Weapon
    /// on 2F Pulse Laser on Hammer (Type-11 Prospector); then…" was the complaint, word for word.
    /// </summary>
    [Fact]
    public void TheLineCarriesNoListAndNoGap()
    {
        var line = new ContinuityCallout().Compose(Evening);

        Assert.DoesNotContain("Top of your list", line, StringComparison.Ordinal);
        Assert.DoesNotContain("It has been", line, StringComparison.Ordinal);
        Assert.Equal(2, line.Split('.', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void NothingIsSaidWhileTheJournalBacklogIsStillBeingFolded()
    {
        // Priming is the replay of everything before this session, and a greeting produced from
        // the middle of that replay would be about the wrong moment.
        Assert.Empty(new ContinuityCallout().Examine(At(Evening, priming: true)));
    }

    [Fact]
    public void TheLineWaitsForTheSettleWindowAndThenIsSaidExactlyOnce()
    {
        var callout = new ContinuityCallout();

        // The first live tick only starts the clock: the backlog has just been folded and
        // Status.json may not have been read yet.
        Assert.Empty(callout.Examine(At(Evening)));
        Assert.Empty(callout.Examine(At(Evening.Add(callout.Settle / 2))));

        Assert.Single(callout.Examine(At(Evening.Add(callout.Settle))));

        // And never again for the life of the process, whatever else happens.
        Assert.Empty(callout.Examine(At(Evening.Add(callout.Settle).AddHours(3))));
    }

    [Fact]
    public void TheLineStandsDownForAnythingThatFiresOnAnEvent()
    {
        var callout = new ContinuityCallout();
        Assert.Empty(callout.Examine(At(Evening)));

        var announcement = Assert.Single(callout.Examine(At(Evening.Add(callout.Settle))));

        Assert.Equal(CalloutUrgency.Routine, announcement.Urgency);
        Assert.Equal(ContinuityCallout.Key, announcement.Key);
    }

    /// <summary>
    /// It is d47 talking, so it belongs on the conversation page as well as being heard — the
    /// correction remediation.md records for the Phase 8 family, inherited here rather than
    /// rediscovered.
    /// </summary>
    [Fact]
    public void TheLineReachesTheConversationPage()
    {
        var callout = new ContinuityCallout();
        Assert.Empty(callout.Examine(At(Evening)));

        var announcement = Assert.Single(callout.Examine(At(Evening.Add(callout.Settle))));

        Assert.Equal(announcement.Text, announcement.ConversationLine);
    }

    /// <summary>
    /// With a persona on, the core finishes "Ready to …" in its own words and changes nothing else.
    /// The time of day is the one fact in the line and the brief says to keep it.
    /// </summary>
    [Fact]
    public void ACoreFinishesTheReadinessInCharacterAndKeepsTheTimeOfDay()
    {
        var announcement = new Announcement(ContinuityCallout.Key, "Good evening, Commander. Ready to go.");

        var brief = FlavourBriefs.For(announcement, personalityEnabled: true);

        Assert.NotNull(brief);
        Assert.True(brief.NeedsPersona);
        Assert.False(brief.NeedsGameState);

        Assert.Contains(announcement.Text, brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("time of day", brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("Ready to", brief.Instruction, StringComparison.Ordinal);
        Assert.Contains("no facts", brief.Instruction, StringComparison.Ordinal);
    }

    [Fact]
    public void PersonalityOffSaysItPlainly()
    {
        var announcement = new Announcement(ContinuityCallout.Key, "Good evening, Commander. Ready to go.");

        Assert.Null(FlavourBriefs.For(announcement, personalityEnabled: false));
    }
}
