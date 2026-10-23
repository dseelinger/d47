using Xunit;
using Avalonia.Input;
using D47.App.Settings;

namespace D47.App.Tests;

public class AStickOnlyBindCaptureEndsOnEscTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EscEndsACaptureWhetherOrNotTheKeyboardIsArmed(bool keyboardArmed) =>
        Assert.Equal(BindCapture.KeyDecision.Escape, BindCapture.Decide(keyboardArmed, Key.Escape, KeyModifiers.None));

    [Fact]
    public void AnotherKeyIsLeftAloneWhenOnlyTheStickIsArmed() =>
        Assert.Equal(BindCapture.KeyDecision.Ignore, BindCapture.Decide(false, Key.F5, KeyModifiers.None));

    [Fact]
    public void AnotherKeyIsAGestureWhenTheKeyboardIsArmed() =>
        Assert.Equal(BindCapture.KeyDecision.Gesture, BindCapture.Decide(true, Key.F5, KeyModifiers.Control));

    [Fact]
    public void AModifierAloneIsNotAGesture() =>
        Assert.Equal(BindCapture.KeyDecision.Ignore, BindCapture.Decide(true, Key.LeftCtrl, KeyModifiers.Control));
}
