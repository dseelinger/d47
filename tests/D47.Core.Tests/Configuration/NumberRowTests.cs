using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// A number row holds the numbers its own help text offers.
/// <para>
/// This was not true until Phase 9 needed it. <see cref="SettingKind.Number"/> parsed integers
/// only, so the speaking rate row — documented as "1.0 is the voice's natural pace, 1.2 is a
/// fifth faster" — rejected 1.2. It failed quietly, as a rejected value snapping back, which is
/// indistinguishable from a control that did not take the click.
/// </para>
/// </summary>
public class NumberRowTests
{
    [Fact]
    public void ARowThatOffersFifthsAcceptsAFifth()
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;

        var applied = settings.Apply(SpeechCapability.RateKey, "1.2", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Applied, applied.Status);
        Assert.Equal("1.2", settings.Read(SpeechCapability.RateKey));
    }

    /// <summary>
    /// And a row that counts things still counts them. Widening the parse must not turn a jump
    /// count into a fraction of a jump.
    /// </summary>
    [Fact]
    public void ARowThatCountsThingsStillRoundsToWholeOnes()
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;

        settings.Apply(CalloutCapability.RouteEveryKey, "3.7", SettingsCaller.Panel);

        Assert.Equal("4", settings.Read(CalloutCapability.RouteEveryKey));
    }

    [Fact]
    public void SomethingThatIsNotANumberIsStillRefused()
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;

        Assert.Equal(
            SettingApplyStatus.Rejected,
            settings.Apply(SpeechCapability.RateKey, "quickly", SettingsCaller.Panel).Status);
    }

    /// <summary>
    /// The format is derived from the step rather than declared alongside it, because a format
    /// and a step that disagree is a value that changes every time it is read back.
    /// </summary>
    [Theory]
    [InlineData(1, "0")]
    [InlineData(0.5, "0.#")]
    [InlineData(0.05, "0.##")]
    public void HowANumberIsWrittenComesFromHowItSteps(double step, string expected)
    {
        var row = new SettingRow
        {
            Key = "test",
            Label = "Test",
            Help = "Test",
            Kind = SettingKind.Number,
            Step = step,
        };

        Assert.Equal(expected, row.NumberFormat);
    }

    [Fact]
    public void EveryPlacementRowStepsInSomethingSmallerThanAMetre()
    {
        using var install = new TempInstall();
        var settings = TestSurface.For(install).Settings;

        foreach (var slot in new[] { VrCapability.PanelSlot, VrCapability.MiniSlot })
        {
            foreach (var name in new[] { "distance", "size", "curve", "opacity" })
            {
                var row = settings.Find($"vr.{slot}.{name}");

                Assert.NotNull(row);
                Assert.True(
                    row.Step < 1,
                    $"vr.{slot}.{name} steps in whole units, so it cannot hold the value it defaults to");
            }
        }
    }
}
