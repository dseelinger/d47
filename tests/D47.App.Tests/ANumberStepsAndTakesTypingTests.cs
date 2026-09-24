using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A number row is a 220-wide ◄ value ► stepper: the arrows move by the row's step and clamp to its range,
/// and the value box takes typing (#440).
/// </summary>
public class ANumberStepsAndTakesTypingTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static SettingsHost Open(out SettingsService settings, double width = 1180)
    {
        (settings, var viewState, var paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        return SettingsHost.Open(settings, viewState, paths, width: width, height: 800);
    }

    private static Amount AmountOn(SettingsHost host, string key, string label)
    {
        host.View.ShowPlaceOf(key);
        Jobs();

        return host.View.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass) && grid.IsEffectivelyVisible)
            .First(grid => grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label))
            .GetVisualDescendants().OfType<Amount>().Single();
    }

    private static string? Shown(Amount amount) =>
        amount.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "AmountValue").Text;

    private static void Type(SettingsHost host, Amount amount, string text)
    {
        amount.BeginEdit();
        Jobs();

        amount.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "AmountEditor").Text = text;
        host.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Jobs();
    }

    [Theory]
    [InlineData(500, 1, 50, 200, 3000, 550)]
    [InlineData(2980, 1, 50, 200, 3000, 3000)]
    [InlineData(220, -1, 50, 200, 3000, 200)]
    [InlineData(0.05, -1, 0.001, 0, 10, 0.049)]
    public void AnArrowMovesByTheStepAndClampsToTheRange(
        double from, int direction, double step, double minimum, double maximum, double expected) =>
        Assert.Equal(
            (decimal)expected,
            Amount.Stepped((decimal)from, null, (decimal)step, direction, (decimal)minimum, (decimal)maximum));

    [Fact]
    public void FromNothingSetAnArrowStepsFromTheDefault() =>
        Assert.Equal(501m, Amount.Stepped(null, 500m, 1m, 1, 0m, null));

    [Theory]
    [InlineData(500, "0", "ms", "500 MS")]
    [InlineData(0.05, "0.###", "$", "$0.05")]
    [InlineData(80, "0", "%", "80%")]
    [InlineData(1.2, "0.##", null, "1.2")]
    public void TheValueCarriesItsUnitInCapitals(double value, string format, string? unit, string expected) =>
        Assert.Equal(expected, Amount.Display((decimal)value, format, unit));

    [AvaloniaFact]
    public void CaptureBeforeTheKeyStepsAndTakesATypedValue()
    {
        using var _ = AppLook.ControlKit();
        var host = Open(out var settings);
        var amount = AmountOn(host, ListeningCapability.PreRollKey, "Capture before the key");

        Assert.Equal("500 MS", Shown(amount));
        Assert.Equal(Amount.CompactWidth, amount.Bounds.Width, 1);

        var next = amount.GetVisualDescendants().OfType<RepeatButton>().Single(button => button.Content as string == "►");
        next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();

        Assert.Equal("501", settings.Read(ListeningCapability.PreRollKey));

        Type(host, amount, "650");

        Assert.Equal("650", settings.Read(ListeningCapability.PreRollKey));
        Assert.Equal("650 MS", Shown(amount));

        host.Close();
    }

    [AvaloniaFact]
    public void ThePricePerThousandCharactersShowsDollarsAndTakesATypedValue()
    {
        using var _ = AppLook.ControlKit();
        var host = Open(out var settings);

        settings.Apply(SpeechCapability.ProviderKey, TtsProviderCatalog.ElevenLabsId, SettingsCaller.Panel);
        Jobs();

        var amount = AmountOn(host, SpeechCapability.CharacterPriceKey, "Price per 1,000 characters");

        Assert.Equal("$0.05", Shown(amount));

        Type(host, amount, "0.07");

        Assert.Equal("0.07", settings.Read(SpeechCapability.CharacterPriceKey));
        Assert.Equal("$0.07", Shown(amount));

        host.Close();
    }

    [AvaloniaTheory]
    [InlineData(1180)]
    [InlineData(924)]
    public void VoiceInputIsCaptured(double width)
    {
        using var _ = AppLook.ControlKit();
        var host = Open(out var settings, width);

        settings.Apply(ListeningCapability.PushToTalkButtonKey, "NonRoamable+Id/One=#10", SettingsCaller.Panel);
        var amount = AmountOn(host, ListeningCapability.PreRollKey, "Capture before the key");

        host.Window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, $"binding-push-to-talk-{width}.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        amount.BringIntoView();
        Jobs();

        host.Window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, $"amount-voice-input-{width}.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        host.Close();
    }
}
