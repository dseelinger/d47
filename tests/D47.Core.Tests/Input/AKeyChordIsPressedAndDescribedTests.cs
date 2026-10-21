using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Input;
using Xunit;

namespace D47.Core.Tests.Input;

public class AKeyChordIsPressedAndDescribedTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Fact]
    public void CtrlVHoldsControlTapsVAndReleasesInReverse()
    {
        var steps = new KeyChord("Key_V", ChordModifiers.Ctrl).Steps();

        Assert.Equal(
            [
                new InputStep(InputStepKind.KeyDown, 0xA2),
                new InputStep(InputStepKind.KeyDown, 0x56),
                InputStep.Wait(InputSequence.TapHold),
                new InputStep(InputStepKind.KeyUp, 0x56),
                new InputStep(InputStepKind.KeyUp, 0xA2),
            ],
            steps);
    }

    [Fact]
    public void ShiftWinDSendsTheWindowsKeyExtended()
    {
        var steps = new KeyChord("Key_D", ChordModifiers.Shift | ChordModifiers.Win).Steps();

        Assert.Equal(
            [
                new InputStep(InputStepKind.KeyDown, 0xA0),
                new InputStep(InputStepKind.KeyDown, 0x5B, true),
                new InputStep(InputStepKind.KeyDown, 0x44),
                InputStep.Wait(InputSequence.TapHold),
                new InputStep(InputStepKind.KeyUp, 0x44),
                new InputStep(InputStepKind.KeyUp, 0x5B, true),
                new InputStep(InputStepKind.KeyUp, 0xA0),
            ],
            steps);
    }

    [Theory]
    [InlineData("Key_V", ChordModifiers.Ctrl, "Ctrl+V")]
    [InlineData("Key_D", ChordModifiers.Win | ChordModifiers.Shift, "Shift+Win+D")]
    [InlineData("Key_F13", ChordModifiers.None, "F13")]
    [InlineData("Key_A", ChordModifiers.Win | ChordModifiers.Alt | ChordModifiers.Shift | ChordModifiers.Ctrl, "Ctrl+Shift+Alt+Win+A")]
    public void DescribeOrdersModifiersAndDropsThePrefix(string key, ChordModifiers modifiers, string expected) =>
        Assert.Equal(expected, new KeyChord(key, modifiers).Describe());

    [Theory]
    [InlineData("Mouse_1", ChordModifiers.None)]
    [InlineData("Joy_3", ChordModifiers.None)]
    [InlineData("Key_NoSuchKey", ChordModifiers.None)]
    [InlineData("Key_LeftShift", ChordModifiers.None)]
    [InlineData("Key_RightWin", ChordModifiers.Ctrl)]
    [InlineData("Key_F4", ChordModifiers.Alt)]
    [InlineData("Key_L", ChordModifiers.Win)]
    public void AChordThatCannotOrShouldNotBeSentIsRefused(string key, ChordModifiers modifiers)
    {
        var chord = new KeyChord(key, modifiers);
        var problem = KeyChord.Problem(chord);

        Assert.NotNull(problem);
        Assert.Contains(chord.Describe(), problem);
        Assert.Empty(chord.Steps());
    }

    [Fact]
    public void AKeyAloneIsAllowed() => Assert.Null(KeyChord.Problem(new KeyChord("Key_F13")));

    [Fact]
    public void AChordSurvivesAJsonRoundTrip()
    {
        var chord = new KeyChord("Key_D", ChordModifiers.Shift | ChordModifiers.Win);

        var text = JsonSerializer.Serialize(chord, Json);

        Assert.Equal(chord, JsonSerializer.Deserialize<KeyChord>(text, Json));
    }
}
