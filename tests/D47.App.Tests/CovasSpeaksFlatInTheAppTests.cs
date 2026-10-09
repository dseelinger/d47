using Xunit;

namespace D47.App.Tests;

/// <summary>The composition root keeps a stock core flat: no delivery notes, no humor, no model for its own lines.</summary>
[Trait("Category", "Gate")]
public class CovasSpeaksFlatInTheAppTests
{
    [Fact]
    public void ATurnWithCovasAboardIsNeverToldItMayWriteDeliveryNotes()
    {
        Assert.True(InTheTree("Turns.CanBeDirected = () => !Personas.Current.Stock && Speech.DirectableIn(VoiceGroup.Aboard);"));
    }

    [Fact]
    public void TheCoreHumorDialIsReadWithTheStockCoreAboardFlag()
    {
        Assert.True(InTheTree("Humor.DialFor(Settings.Current.Persona, group, Personas.Current.Stock)"));
    }

    [Fact]
    public void CovasIntroAndReturnAreSaidAsWrittenWithNoModelCall()
    {
        Assert.True(InTheTree("change.Current.Stock ? null : await AskAsync(instruction)"));
        Assert.True(InTheTree("!change.Current.Stock\n"));
    }

    [Fact]
    public void RewordingIsToldWhetherACoreIsStock()
    {
        Assert.True(InTheTree("stockCoreAboard: () => Personas.Current.Stock"));
    }

    private static bool InTheTree(string fragment) =>
        AppSource.Files.Any(file => file.Text.ReplaceLineEndings("\n").Contains(fragment, StringComparison.Ordinal));
}
