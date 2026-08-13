using D47.App.Input;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// What a bound key is called on screen. Several Key values carry two names in the enum —
/// OemOpenBrackets and Oem4 are the same number — and ToString picks whichever it finds first,
/// so binding a bracket bound correctly and then displayed itself as "Oem4".
/// </summary>
public class GestureDisplayTests
{
    [Theory]
    // The keys a Commander is most likely to spare for push-to-talk, and the ones whose enum
    // name is least like what is printed on them.
    [InlineData("OemOpenBrackets", "[")]
    [InlineData("Oem4", "[")]
    [InlineData("OemCloseBrackets", "]")]
    [InlineData("Oem6", "]")]
    [InlineData("OemPipe", "\\")]
    [InlineData("OemTilde", "`")]
    [InlineData("OemComma", ",")]
    [InlineData("OemQuestion", "/")]
    [InlineData("Return", "Enter")]
    [InlineData("Escape", "Esc")]
    public void AKeyIsShownAsWhatIsPrintedOnIt(string stored, string shown)
    {
        Assert.Equal(shown, Gestures.Describe(stored));
    }

    [Theory]
    [InlineData("Ctrl+Alt+X", "Ctrl+Alt+X")]
    [InlineData("Ctrl+Oem4", "Ctrl+[")]
    [InlineData("Ctrl+Shift+OemComma", "Ctrl+Shift+,")]
    public void ModifiersAreNamedAsPeopleSayThem(string stored, string shown)
    {
        Assert.Equal(shown, Gestures.Describe(stored));
    }

    [Fact]
    public void NothingBoundSaysSo()
    {
        Assert.Equal("unbound", Gestures.Describe(null));
        Assert.Equal("unbound", Gestures.Describe("   "));
    }

    /// <summary>
    /// A hand-edited settings file can hold anything. Showing it back is more use than showing
    /// nothing, and the row still offers to rebind.
    /// </summary>
    [Fact]
    public void SomethingUnparseableIsShownBackRatherThanSwallowed()
    {
        Assert.Equal("Ctrl+Telepathy", Gestures.Describe("Ctrl+Telepathy"));
    }
}
