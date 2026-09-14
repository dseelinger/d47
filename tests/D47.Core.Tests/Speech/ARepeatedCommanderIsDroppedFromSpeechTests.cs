using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Speech;

public class ARepeatedCommanderIsDroppedFromSpeechTests
{
    private sealed class FakeClock : IWallClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 14, 14, 47, 0, TimeSpan.Zero);
    }

    private static SpokenAddress Address(FakeClock clock, string? commander = "Doug DEPARAGON") =>
        new(clock) { CommanderName = commander };

    [Fact]
    public void TheFirstAddressOfASessionIsSpokenUnchanged()
    {
        var clock = new FakeClock();
        var address = Address(clock);

        Assert.Equal(
            "welcome home, Commander DEPARAGON.",
            address.Rewrite("welcome home, Commander DEPARAGON."));
    }

    [Fact]
    public void ATrailingAddressWithinTheWindowIsDropped()
    {
        var clock = new FakeClock();
        var address = Address(clock);

        address.Rewrite("welcome home, Commander DEPARAGON.");
        clock.UtcNow = clock.UtcNow.AddSeconds(9);

        Assert.Equal(
            "Standard starport protocol applies as always.",
            address.Rewrite("Standard starport protocol applies as always, Commander."));
    }

    [Fact]
    public void ALeadingAddressBeforeADashIsDroppedAndWhatFollowsIsCapitalised()
    {
        var clock = new FakeClock();
        var address = Address(clock);

        address.Rewrite("welcome home, Commander DEPARAGON.");
        clock.UtcNow = clock.UtcNow.AddSeconds(9);

        Assert.Equal(
            "You're clear, no fire zone active on this deck.",
            address.Rewrite("Commander — you're clear, no fire zone active on this deck."));
    }

    [Fact]
    public void AMidSentenceAddressFlankedByCommasCollapsesToOneComma()
    {
        var clock = new FakeClock();
        var address = Address(clock);

        address.Rewrite("welcome home, Commander DEPARAGON.");
        clock.UtcNow = clock.UtcNow.AddSeconds(9);

        Assert.Equal(
            "I will reconcile it, because someone has to.",
            address.Rewrite("I will reconcile it, Commander, because someone has to."));
    }

    [Fact]
    public void ADroppedAddressDoesNotRestartTheWindow()
    {
        var clock = new FakeClock();
        var address = Address(clock);

        address.Rewrite("welcome home, Commander DEPARAGON.");

        clock.UtcNow = clock.UtcNow.AddSeconds(9);
        address.Rewrite("Standard starport protocol applies as always, Commander.");

        // 35 seconds after the address that was actually kept, not 26 seconds after the one dropped.
        clock.UtcNow = clock.UtcNow.AddSeconds(26);

        Assert.Equal(
            "Ship secured aboard Sacred Fire, Commander DEPARAGON.",
            address.Rewrite("Ship secured aboard Sacred Fire, Commander DEPARAGON."));
    }

    [Fact]
    public void AnAddressMoreThanThirtySecondsAfterAKeptOneIsSpokenUnchanged()
    {
        var clock = new FakeClock();
        var address = Address(clock);

        address.Rewrite("welcome home, Commander DEPARAGON.");
        clock.UtcNow = clock.UtcNow.AddSeconds(31);

        Assert.Equal(
            "Ship secured aboard Sacred Fire, Commander DEPARAGON.",
            address.Rewrite("Ship secured aboard Sacred Fire, Commander DEPARAGON."));
    }

    [Theory]
    [InlineData("the Commander's own")]
    [InlineData("Lieutenant Commander Frost is on the line.")]
    [InlineData("Commander's log, supplemental.")]
    [InlineData("Ask another Commander to confirm.")]
    public void CommanderIsLeftAloneWhereItIsNotAnAddress(string line)
    {
        var clock = new FakeClock();
        var address = Address(clock);

        // Establish a kept address so a real one would be dropped, to prove these do not count as one.
        address.Rewrite("welcome home, Commander DEPARAGON.");

        Assert.Equal(line, address.Rewrite(line));
    }

    [Fact]
    public void AWordThatIsNotTheKnownSurnameIsNotTakenForTheAddress()
    {
        var clock = new FakeClock();
        var address = Address(clock);

        address.Rewrite("welcome home, Commander DEPARAGON.");
        clock.UtcNow = clock.UtcNow.AddSeconds(9);

        Assert.Equal(
            "Commander Vance reporting in.",
            address.Rewrite("Commander Vance reporting in."));
    }

    [Fact]
    public void ASentenceThatIsOnlyTheAddressBecomesEmptyWhenDropped()
    {
        var clock = new FakeClock();
        var address = Address(clock);

        address.Rewrite("welcome home, Commander DEPARAGON.");
        clock.UtcNow = clock.UtcNow.AddSeconds(9);

        // The address is gone; the sentence's own terminal punctuation is left for the caller to judge
        // as having nothing left to say (SpeechPipeline does, by letter or digit).
        Assert.Equal(".", address.Rewrite("Commander."));
    }

    [Fact]
    public void WithNoCommanderNameKnownOnlyTheBareAddressIsRecognised()
    {
        var clock = new FakeClock();
        var address = Address(clock, commander: null);

        address.Rewrite("Standard starport protocol applies as always, Commander.");
        clock.UtcNow = clock.UtcNow.AddSeconds(9);

        Assert.Equal(
            "Docking granted on your own deck.",
            address.Rewrite("Docking granted on your own deck, Commander."));
    }
}
