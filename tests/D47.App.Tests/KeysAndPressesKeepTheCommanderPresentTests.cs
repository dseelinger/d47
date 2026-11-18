using D47.App.Input;
using D47.Core.Hotas;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Commander's keyboard, mouse, controllers and headset presses keep paid calls going; a key d47 sent does
/// not, and an away time of zero never stops them.
/// </summary>
public class KeysAndPressesKeepTheCommanderPresentTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 18, 20, 0, 0, TimeSpan.Zero);

    private uint? _lastInput = 1_000;
    private uint? _lastSent;

    private CommanderPresence Presence() =>
        new(() => _lastInput, () => _lastSent, NullLogger<CommanderPresence>.Instance);

    [Fact]
    public void NothingForTheAwayMinutesStopsPaidCalls()
    {
        var presence = Presence();
        presence.Tick(Start, [], [], 5);

        presence.Tick(Start.AddMinutes(5).AddSeconds(-1), [], [], 5);
        Assert.False(presence.PaidCallsStopped);

        presence.Tick(Start.AddMinutes(5), [], [], 5);
        Assert.True(presence.PaidCallsStopped);
    }

    [Fact]
    public void AKeyOrMouseMoveStirsTheClock()
    {
        var presence = Presence();
        presence.Tick(Start, [], [], 5);

        _lastInput = 200_000;
        presence.Tick(Start.AddMinutes(4), [], [], 5);
        presence.Tick(Start.AddMinutes(8), [], [], 5);

        Assert.False(presence.PaidCallsStopped);
        presence.Tick(Start.AddMinutes(9), [], [], 5);
        Assert.True(presence.PaidCallsStopped);

        _lastInput = 600_000;
        presence.Tick(Start.AddMinutes(10), [], [], 5);
        Assert.False(presence.PaidCallsStopped);
    }

    [Theory]
    [InlineData(200_000u, 200_000u)]
    [InlineData(200_300u, 200_000u)]
    [InlineData(199_700u, 200_000u)]
    public void AKeyD47SentDoesNot(uint input, uint sent)
    {
        var presence = Presence();
        presence.Tick(Start, [], [], 5);

        _lastSent = sent;
        _lastInput = input;
        presence.Tick(Start.AddMinutes(4), [], [], 5);
        presence.Tick(Start.AddMinutes(5), [], [], 5);

        Assert.True(presence.PaidCallsStopped);
    }

    [Fact]
    public void D47sOwnSendIsToldApartAcrossTheTickCountWrapping()
    {
        Assert.True(CommanderPresence.IsOwn(100, uint.MaxValue - 100));
        Assert.False(CommanderPresence.IsOwn(100_000, uint.MaxValue - 100));
        Assert.False(CommanderPresence.IsOwn(100, null));
    }

    [Fact]
    public void ARiseInAPressCountStirsTheClock()
    {
        var presence = Presence();
        presence.Tick(Start, [], [Reading(presses: 3, hat: 0)], 5);

        presence.Tick(Start.AddMinutes(4), [], [Reading(presses: 4, hat: 0)], 5);
        presence.Tick(Start.AddMinutes(8), [], [Reading(presses: 4, hat: 0)], 5);

        Assert.False(presence.PaidCallsStopped);
    }

    [Fact]
    public void AChangedHatStirsTheClock()
    {
        var presence = Presence();
        presence.Tick(Start, [], [Reading(presses: 3, hat: 0)], 5);

        presence.Tick(Start.AddMinutes(4), [], [Reading(presses: 3, hat: 2)], 5);
        presence.Tick(Start.AddMinutes(8), [], [Reading(presses: 3, hat: 2)], 5);

        Assert.False(presence.PaidCallsStopped);
    }

    [Fact]
    public void APressOnTheHeadsetPanelStirsTheClock()
    {
        var presence = Presence();
        presence.Tick(Start, [], [], 5);

        presence.PanelPressed();
        presence.Tick(Start.AddMinutes(4), [], [], 5);
        presence.Tick(Start.AddMinutes(8), [], [], 5);

        Assert.False(presence.PaidCallsStopped);
    }

    [Fact]
    public void ZeroMinutesNeverStopsPaidCalls()
    {
        var presence = Presence();
        presence.Tick(Start, [], [], 0);

        presence.Tick(Start.AddHours(10), [], [], 0);

        Assert.False(presence.PaidCallsStopped);
    }

    private static HotasReading Reading(int presses, int hat) => new()
    {
        Id = "stick",
        Buttons = [false],
        Presses = [presses],
        Hats = [hat],
    };
}
