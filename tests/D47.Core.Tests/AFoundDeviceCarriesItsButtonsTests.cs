using D47.Core.Configuration;
using D47.Core.Hotas;
using Xunit;

namespace D47.Core.Tests;

/// <summary>Push-to-talk and cancel follow a re-linked device, and only that device (#1158).</summary>
public class AFoundDeviceCarriesItsButtonsTests
{
    private static readonly FoundDevice Found = new("X", "Y", "stick", "GEAR", Fills: false);

    private static D47Settings With(string? talk, string? cancel) => new()
    {
        Listening = new ListeningSettings { PushToTalkButton = talk },
        Speech = new SpeechSettings { CancelButton = cancel },
    };

    [Fact]
    public void PushToTalkOnTheOldIdKeepsItsButtonNumberOnTheNewOne()
    {
        var next = Found.RepointButtons(With("X#4", "X#7"), out var moved);

        Assert.Equal("Y#4", next.Listening.PushToTalkButton);
        Assert.Equal("Y#7", next.Speech.CancelButton);
        Assert.Equal(["push-to-talk", "cancel"], moved);
    }

    [Fact]
    public void AButtonOnAnotherDeviceIsUntouched()
    {
        var settings = With("Z#4", null);

        var next = Found.RepointButtons(settings, out var moved);

        Assert.Equal(settings, next);
        Assert.Empty(moved);
    }

    [Fact]
    public void ASwitchThatTookItsDeviceTextFromTheFlipMovesNoButtons()
    {
        var filling = Found with { Fills = true };

        var next = filling.RepointButtons(With("X#4", "X#7"), out var moved);

        Assert.Equal("X#4", next.Listening.PushToTalkButton);
        Assert.Empty(moved);
    }

    [Theory]
    [InlineData(4, "Found the device — 4 switches re-pointed.")]
    [InlineData(1, "Found the device — 1 switch re-pointed.")]
    public void TheCardCountsSwitchesAlone(int count, string says) =>
        Assert.Equal(says, Found.Says(count));

    [Fact]
    public void TheCardNamesTheButtonsItMoved() =>
        Assert.Equal(
            "Found the device — 4 switches and push-to-talk re-pointed.",
            Found.Says(4, ["push-to-talk"]));
}
