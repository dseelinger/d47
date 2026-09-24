using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.App.Tests.SettingsPageReading;

namespace D47.App.Tests;

/// <summary>
/// The provider rows name the provider alone, and say under it where it runs and whether its key is
/// stored (#438).
/// </summary>
public class AProviderSaysWhereItRunsAndWhetherItHasItsKeyTests
{
    [AvaloniaFact]
    public void TheHearingProviderStepperShowsWhisperFirstOfFiveOnThisComputer()
    {
        var (host, _) = OpenOnHearing();

        var stepper = StepperFor(host, ListeningCapability.ProviderKey);

        Assert.Equal("Whisper", Text(stepper, "StepperValue"));
        Assert.Equal("1 / 5", Text(stepper, "StepperPosition"));
        Assert.Equal("THIS COMPUTER · FREE", Text(stepper, "StepperStatus"));

        host.Close();
    }

    [AvaloniaFact]
    public void DeepgramWithItsKeyStoredSaysSoInYellow()
    {
        var (host, stepper) = StepTo(SttProviderCatalog.DeepgramId, store: SttProviderCatalog.Deepgram.KeySecretName);

        Assert.Equal("Deepgram", Text(stepper, "StepperValue"));
        Assert.Equal("PAID · KEY STORED", Text(stepper, "StepperStatus"));
        Assert.Equal(Resource(ThemeManager.YellowKey), Ink(stepper));

        host.Close();
    }

    [AvaloniaFact]
    public void GroqWithNoKeySaysItNeedsOneInGrey()
    {
        var (host, stepper) = StepTo(SttProviderCatalog.GroqId, store: null);

        Assert.Equal("Groq", Text(stepper, "StepperValue"));
        Assert.Equal("PAID · NEEDS KEY", Text(stepper, "StepperStatus"));
        Assert.Equal(Resource(ThemeManager.GreyKey), Ink(stepper));

        host.Close();
    }

    [AvaloniaFact]
    public void TheVoiceProviderStepperNamesEachOfItsSixAndSaysWhatEachIs()
    {
        var (host, _) = OpenOnHearing();
        host.View.Reveal(SpeechCapability.Id);
        Dispatcher.UIThread.RunJobs();

        var stepper = StepperFor(host, SpeechCapability.ProviderKey);

        Assert.Equal(6, stepper.ItemsSource.Count);
        Assert.Equal(TtsProviderCatalog.All.Select(provider => provider.Name), stepper.ItemsSource);

        var seen = new List<string?>();
        for (var i = 0; i < stepper.ItemsSource.Count; i++)
        {
            stepper.SelectedIndex = i;
            Dispatcher.UIThread.RunJobs();
            seen.Add(Visible(stepper, "StepperStatus") ? Text(stepper, "StepperStatus") : null);
        }

        Assert.Equal(
            TtsProviderCatalog.All.Select(provider => provider.Id switch
            {
                TtsProviderCatalog.NoneId => null,
                TtsProviderCatalog.EdgeId => "FREE",
                TtsProviderCatalog.KokoroId => "THIS COMPUTER · FREE",
                _ => "PAID · NEEDS KEY",
            }),
            seen);

        host.Close();
    }

    /// <summary>A key stored while the page is open shows at the next refresh, without a rebuild.</summary>
    [AvaloniaFact]
    public void StoringAKeyChangesTheStatusAtTheNextRefresh()
    {
        var (host, secrets) = OpenOnHearing();
        var stepper = StepperFor(host, ListeningCapability.ProviderKey);

        secrets.Set(SttProviderCatalog.Groq.KeySecretName!, "gsk-test");
        host.View.Refresh();
        stepper.SelectedIndex = SttProviderCatalog.Ids.ToList().IndexOf(SttProviderCatalog.GroqId);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("PAID · KEY STORED", Text(stepper, "StepperStatus"));

        host.Close();
    }

    [AvaloniaTheory]
    [InlineData(1280, 860)]
    [InlineData(924, 640)]
    public void TheProviderRowsAreCaptured(double width, double height)
    {
        using var look = AppLook.Put();

        var (settings, viewState, paths, _, secrets) = TestSurface.CreateFull();
        secrets.Set(SttProviderCatalog.Deepgram.KeySecretName!, "dg-test");
        new ThemeManager(Avalonia.Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);
        var host = SettingsHost.Open(settings, viewState, paths, width: width, height: height);

        foreach (var (placeId, key) in new[]
                 {
                     ("voice-input", ListeningCapability.ModeKey),
                     ("voice-input", ListeningCapability.ProviderKey),
                     ("voice", SpeechCapability.ProviderKey),
                 })
        {
            Open(host.View, placeId);

            if (key == ListeningCapability.ProviderKey)
            {
                StepperFor(host, key).SelectedIndex = SttProviderCatalog.Ids.ToList().IndexOf(SttProviderCatalog.DeepgramId);
            }

            host.View.ControlFor(key)?.BringIntoView();
            Dispatcher.UIThread.RunJobs();

            var path = Path.Combine(TestSurface.CaptureDirectory, $"provider-rows-{key}-{width}x{height}.png");

            using (var frame = host.Window.CaptureRenderedFrame()!)
            {
                frame.Save(path, new PngBitmapEncoderOptions());
            }

            Assert.True(File.Exists(path));
        }

        host.Close();
    }

    private static (SettingsHost Host, D47.Core.Configuration.SecretStore Secrets) OpenOnHearing(string? store = null)
    {
        var (settings, viewState, paths, _, secrets) = TestSurface.CreateFull();

        if (store is not null)
        {
            secrets.Set(store, "test-key");
        }

        new ThemeManager(Avalonia.Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);
        host.View.Reveal(ListeningCapability.Id);
        Dispatcher.UIThread.RunJobs();

        return (host, secrets);
    }

    private static (SettingsHost Host, Stepper Stepper) StepTo(string providerId, string? store)
    {
        var (host, _) = OpenOnHearing(store);
        var stepper = StepperFor(host, ListeningCapability.ProviderKey);

        stepper.SelectedIndex = SttProviderCatalog.Ids.ToList().IndexOf(providerId);
        Dispatcher.UIThread.RunJobs();

        return (host, stepper);
    }

    private static Stepper StepperFor(SettingsHost host, string key)
    {
        var control = host.View.ControlFor(key);

        Assert.NotNull(control);

        return control as Stepper ?? control.GetVisualDescendants().OfType<Stepper>().First();
    }

    private static TextBlock Named(Stepper stepper, string name) =>
        stepper.GetVisualDescendants().OfType<TextBlock>().First(text => text.Name == name);

    private static string? Text(Stepper stepper, string name) => Named(stepper, name).Text;

    private static bool Visible(Stepper stepper, string name) => Named(stepper, name).IsVisible;

    private static Color? Ink(Stepper stepper) =>
        (Named(stepper, "StepperStatus").Foreground as ISolidColorBrush)?.Color;

    private static Color? Resource(string key) =>
        Avalonia.Application.Current!.FindResource(key) is ISolidColorBrush brush ? brush.Color : null;
}
