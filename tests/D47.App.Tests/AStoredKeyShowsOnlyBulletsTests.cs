using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A stored key is a masked block with REPLACE, VERIFY and FORGET KEY and no field; the field comes back on
/// REPLACE, or at once when no key is stored (#442).
/// </summary>
public sealed class AStoredKeyShowsOnlyBulletsTests
{
    private const string DeepgramKey = "dg-not-a-real-key-7Q2F";
    private const string ElevenLabsKey = "el-not-a-real-key-9XK4";

    private static readonly string DeepgramRow = ListeningCapability.KeyRowFor(SttProviderCatalog.Deepgram);
    private static readonly string ElevenLabsRow = SpeechCapability.KeyRowFor(TtsProviderCatalog.ElevenLabs);

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static SettingsHost Open(
        out SettingsService settings,
        bool stored,
        double width = 1280,
        double height = 860,
        Action? onVerify = null)
    {
        (settings, var viewState, var paths) = TestSurface.Create(
            verifyKey: (_, _) =>
            {
                onVerify?.Invoke();
                return Task.FromResult(SecretCheck.Works("ElevenLabs accepted the key."));
            });

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        settings.Apply(ListeningCapability.ProviderKey, SttProviderCatalog.DeepgramId, SettingsCaller.Panel);
        settings.Apply(SpeechCapability.ProviderKey, TtsProviderCatalog.ElevenLabsId, SettingsCaller.Panel);

        if (stored)
        {
            settings.Apply(DeepgramRow, DeepgramKey, SettingsCaller.Panel);
            settings.Apply(ElevenLabsRow, ElevenLabsKey, SettingsCaller.Panel);
        }

        var host = SettingsHost.Open(settings, viewState, paths, width: width, height: height);
        Jobs();

        return host;
    }

    private static void ShowPlace(SettingsHost host, string placeId)
    {
        var index = SettingsLayout.Areas.SelectMany(area => area.Places).ToList()
            .FindIndex(place => place.Id == placeId);

        host.View.ShowPlace(index);
        Jobs();
        Jobs();
    }

    private static SecretEditor Editor(SettingsHost host, string key) =>
        Assert.IsType<SecretEditor>(host.View.ControlFor(key));

    private static Button Press(SecretEditor editor, string label) =>
        editor.GetVisualDescendants().OfType<Button>().Single(button => button.Content as string == label);

    private static bool Shown(Control control) => control.IsEffectivelyVisible;

    private static TextBox Field(SecretEditor editor) => editor.GetVisualDescendants().OfType<TextBox>().Single();

    private static List<string> ShownTexts(SecretEditor editor) =>
    [
        .. editor.GetVisualDescendants().OfType<TextBlock>()
            .Where(Shown)
            .Select(block => block.Text ?? string.Empty)
            .Where(text => text.Length > 0),
    ];

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
    }

    [AvaloniaFact]
    public void AStoredDeepgramKeyIsBulletsAndABadgeWithNoField()
    {
        var host = Open(out _, stored: true);
        ShowPlace(host, "voice-input");

        var editor = Editor(host, DeepgramRow);

        Assert.Equal([SecretEditor.Mask, "KEY STORED"], ShownTexts(editor).Take(2));
        Assert.True(Shown(Press(editor, "REPLACE")));
        Assert.True(Shown(Press(editor, "FORGET KEY")));
        Assert.False(Shown(Field(editor)));
        Assert.False(Shown(Press(editor, "SAVE")));

        // No run of the key's characters, shown or hidden, anywhere under the row: not the whole key, and not
        // the last four the mockup drew.
        var everything = editor.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)
            .Concat(editor.GetVisualDescendants().OfType<TextBox>().Select(box => box.Text ?? string.Empty))
            .ToList();

        var runs = Enumerable.Range(0, DeepgramKey.Length - 3).Select(start => DeepgramKey.Substring(start, 4));

        Assert.All(runs, run => Assert.DoesNotContain(everything, text => text.Contains(run, StringComparison.Ordinal)));

        host.Close();
    }

    [AvaloniaFact]
    public void ReplaceOpensTheFieldAndCancelLeavesTheStoreAlone()
    {
        var host = Open(out var settings, stored: true);
        ShowPlace(host, "voice-input");

        var editor = Editor(host, DeepgramRow);

        Click(Press(editor, "REPLACE"));

        Assert.True(Shown(Field(editor)));
        Assert.True(Shown(Press(editor, "SAVE")));
        Assert.True(Shown(Press(editor, "CANCEL")));
        Assert.False(Shown(Press(editor, "REPLACE")));
        Assert.DoesNotContain(SecretEditor.Mask, ShownTexts(editor));

        Field(editor).Text = "dg-half-typed";
        Click(Press(editor, "CANCEL"));

        Assert.False(Shown(Field(editor)));
        Assert.Empty(Field(editor).Text ?? string.Empty);
        Assert.Contains(SecretEditor.Mask, ShownTexts(editor));
        Assert.True(settings.HasSecret(SttProviderCatalog.Deepgram.KeySecretName));

        host.Close();
    }

    [AvaloniaFact]
    public void SaveOverAStoredKeyReturnsToTheStoredLine()
    {
        var host = Open(out var settings, stored: true);
        ShowPlace(host, "voice-input");

        var editor = Editor(host, DeepgramRow);

        Click(Press(editor, "REPLACE"));
        Field(editor).Text = "dg-another-not-real-key";
        Click(Press(editor, "SAVE"));

        editor = Editor(host, DeepgramRow);

        Assert.False(Shown(Field(editor)));
        Assert.Contains(SecretEditor.Mask, ShownTexts(editor));
        Assert.True(settings.HasSecret(SttProviderCatalog.Deepgram.KeySecretName));

        host.Close();
    }

    [AvaloniaFact]
    public async Task AStoredElevenLabsKeyOffersVerifyAndItRuns()
    {
        var checks = 0;
        var host = Open(out _, stored: true, onVerify: () => checks++);
        ShowPlace(host, "voice");

        var editor = Editor(host, ElevenLabsRow);
        var verify = Press(editor, "VERIFY");

        Assert.True(Shown(verify));
        Assert.True(verify.IsEnabled);

        verify.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Yield();
        Jobs();

        Assert.Equal(1, checks);
        Assert.Contains("ElevenLabs accepted the key.", ShownTexts(Editor(host, ElevenLabsRow)));

        host.Close();
    }

    [AvaloniaFact]
    public void WithNoKeyTheFieldAndSaveShowAtOnce()
    {
        var host = Open(out _, stored: false);
        ShowPlace(host, "voice-input");

        var editor = Editor(host, DeepgramRow);

        Assert.True(Shown(Field(editor)));
        Assert.True(Shown(Press(editor, "SAVE")));
        Assert.Contains("NO KEY", ShownTexts(editor));
        Assert.False(Shown(Press(editor, "REPLACE")));
        Assert.False(Shown(Press(editor, "CANCEL")));
        Assert.False(Shown(Press(editor, "FORGET KEY")));
        Assert.DoesNotContain(SecretEditor.Mask, ShownTexts(editor));

        host.Close();
    }

    [AvaloniaFact]
    public void ForgetKeyAsksAndOnYesDeletesTheKey()
    {
        var host = Open(out var settings, stored: true);
        ShowPlace(host, "voice-input");

        var forget = Press(Editor(host, DeepgramRow), "FORGET KEY");

        Assert.Contains(SettingsView.DestructiveClass, forget.Classes);

        Click(forget);

        var ask = Assert.Single(host.Window.OwnedWindows.OfType<ConfirmWindow>());

        // Asked, and nothing deleted while it waits.
        Assert.True(settings.HasSecret(SttProviderCatalog.Deepgram.KeySecretName));

        Click(ask.GetVisualDescendants().OfType<Button>().Single(button => button.Content as string == "Delete key"));
        Jobs();

        Assert.False(settings.HasSecret(SttProviderCatalog.Deepgram.KeySecretName));

        var editor = Editor(host, DeepgramRow);
        Assert.True(Shown(Field(editor)));
        Assert.Contains("NO KEY", ShownTexts(editor));

        host.Close();
    }

    [AvaloniaTheory]
    [InlineData(1280, 860)]
    [InlineData(924, 640)]
    public void TheKeyRowsAreCaptured(double width, double height)
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        foreach (var stored in new[] { true, false })
        {
            var host = Open(out _, stored, width, height);

            foreach (var (place, key) in new[] { ("voice-input", DeepgramRow), ("voice", ElevenLabsRow) })
            {
                ShowPlace(host, place);
                host.View.ControlFor(key)!.BringIntoView();
                Jobs();

                var path = Path.Combine(
                    TestSurface.CaptureDirectory,
                    $"secret-{place}-{(stored ? "stored" : "none")}-{width}x{height}.png");

                using var frame = host.Window.CaptureRenderedFrame()!;
                frame.Save(path, new PngBitmapEncoderOptions());

                Assert.True(File.Exists(path));
            }

            host.Close();
        }
    }
}
