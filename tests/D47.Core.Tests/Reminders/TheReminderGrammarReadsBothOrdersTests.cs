using D47.Core.Reminders;
using Xunit;

namespace D47.Core.Tests.Reminders;

/// <summary>The reminder grammar takes the trigger after the sentence or before it (#643).</summary>
public class TheReminderGrammarReadsBothOrdersTests
{
    [Theory]
    [InlineData("remind me to buy limpets when I next dock", "buy limpets", JournalTrigger.NextDocking, null)]
    [InlineData("Remind me to buy limpets next time I dock.", "buy limpets", JournalTrigger.NextDocking, null)]
    [InlineData("remind me to refuel when I dock at Jameson Memorial", "refuel", JournalTrigger.DockingAt, "Jameson Memorial")]
    [InlineData("remind me to sell data when I get to Sol", "sell data", JournalTrigger.ArrivalIn, "Sol")]
    [InlineData("remind me to sell data when I arrive in the Shinrarta Dezhra system", "sell data", JournalTrigger.ArrivalIn, "Shinrarta Dezhra")]
    [InlineData("remind me to fit shields when I’m back at my carrier", "fit shields", JournalTrigger.OwnCarrier, null)]
    [InlineData("remind me to fit shields when I get back to my carrier", "fit shields", JournalTrigger.OwnCarrier, null)]
    [InlineData("remind me to sell the gold when my hold is full", "sell the gold", JournalTrigger.HoldFull, null)]
    [InlineData("remind me to buy cargo when my hold is empty", "buy cargo", JournalTrigger.HoldEmpty, null)]
    [InlineData("remind me to trade it in when arsenic is full", "trade it in", JournalTrigger.MaterialFull, "arsenic")]
    [InlineData("remind me to check the galnet next session", "check the galnet", JournalTrigger.NextSession, null)]
    [InlineData("remind me to check the galnet next time I play", "check the galnet", JournalTrigger.NextSession, null)]
    [InlineData("when I next dock, remind me to buy limpets", "buy limpets", JournalTrigger.NextDocking, null)]
    [InlineData("When I arrive in Sol, remind me to sell data", "sell data", JournalTrigger.ArrivalIn, "Sol")]
    [InlineData("next session remind me to check the galnet", "check the galnet", JournalTrigger.NextSession, null)]
    [InlineData("remind me about the limpets when I next dock", "about the limpets", JournalTrigger.NextDocking, null)]
    public void BothOrdersAreRead(string said, string sentence, JournalTrigger trigger, string? argument)
    {
        var set = Assert.IsType<JournalReminderReading.Set>(JournalReminderPhrase.Read(said, timersRegistered: false));

        Assert.Equal(sentence, set.Sentence);
        Assert.Equal(trigger, set.Trigger);
        Assert.Equal(argument, set.Argument);
    }

    [Theory]
    [InlineData("remind me what my rank is")]
    [InlineData("set a timer for twenty minutes")]
    [InlineData("cancel the timer")]
    public void WhatIsNotAReminderIsNotRead(string said) =>
        Assert.Null(JournalReminderPhrase.Read(said, timersRegistered: false));

    [Theory]
    [InlineData("remind me at seven to log off")]
    [InlineData("remind me to log off in an hour")]
    public void ATimeIsLeftToTheTimersWhenTheyAreRegistered(string said)
    {
        Assert.Null(JournalReminderPhrase.Read(said, timersRegistered: true));
        Assert.Equal(
            JournalReminderPhrase.NoClock,
            Assert.IsType<JournalReminderReading.Declined>(JournalReminderPhrase.Read(said, timersRegistered: false)).Reply);
    }

    [Fact]
    public void ACancelCarriesTheWords() =>
        Assert.Equal(
            "buy limpets",
            Assert.IsType<JournalReminderReading.Cancel>(
                JournalReminderPhrase.Read("forget the reminder to buy limpets", timersRegistered: false)).Words);
}
