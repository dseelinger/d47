using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Headset;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A "Settings" link in a message that carries a setting's row opens the settings page on the place holding
/// that row, in the window and in the headset; one that carries none opens the Settings tab (#952).
/// </summary>
public sealed class ASettingsLinkOpensTheRowItNamesTests
{
    private const string NoVoice = "No Chatterbox voice has been chosen. Pick one in Settings.";

    private const string Hotkey =
        "The cancel hotkey Ctrl+F12 could not be registered system-wide. Another application is probably holding it — pick another in Settings.";

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public void TheBannerOpensItsVoicesPlaceBeforeSettingsWasEverOpened()
    {
        using var look = AppLook.Put();
        var (view, window, model, page) = Shown();

        Assert.NotEqual(PanelTab.Settings, view.Nav.Tab);

        model.ShowError(NoVoice, SpeechCapability.ChatterboxVoiceKey);
        Jobs();

        Click(window, Banner(view), "Settings");

        Assert.Equal(PanelTab.Settings, view.Nav.Tab);
        Assert.Equal("voice", page.SectionIds[page.ActiveSection]);

        window.Close();
    }

    [AvaloniaFact]
    public void AFailedTurnOpensItsRowWhenTheLinkWraps()
    {
        using var look = AppLook.Put();
        var (view, window, model, page) = Shown();

        model.AppendError(Hotkey, ListeningCapability.CancelHotkeyKey);
        Jobs();

        var block = view.TranscriptBlocks.Single();

        // The link is on the second line, where the text layout's own hit test says nothing is inside.
        Assert.True(block.TextLayout.TextLines.Count > 1);
        Assert.True(block.TextLayout.TextLines[0].Length <= block.Inlines!.Text!.IndexOf("Settings", StringComparison.Ordinal));

        Click(window, block, "Settings");

        Assert.Equal(PanelTab.Settings, view.Nav.Tab);
        Assert.Equal("voice-input", page.SectionIds[page.ActiveSection]);
        Assert.True(page.LabelFor(ListeningCapability.CancelHotkeyKey)?.IsEffectivelyVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void TwoFailedTurnsInARowEachOpenTheirOwnRow()
    {
        using var look = AppLook.Put();
        var (view, window, model, page) = Shown();

        model.AppendError(Hotkey, ListeningCapability.CancelHotkeyKey);
        model.AppendError(NoVoice, SpeechCapability.ChatterboxVoiceKey);
        Jobs();

        var second = view.TranscriptBlocks.Single(block => block.Inlines?.Text == NoVoice);

        Click(window, second, "Settings");

        Assert.Equal(PanelTab.Settings, view.Nav.Tab);
        Assert.Equal("voice", page.SectionIds[page.ActiveSection]);

        window.Close();
    }

    [AvaloniaFact]
    public void ARowUnderTheFoldIsDrawnWhenALinkOpensIt()
    {
        using var look = AppLook.Put();
        var (view, window, model, page) = Shown();

        view.Tab = PanelTab.Settings;
        Jobs();
        page.ShowPlaceOf(ListeningCapability.ModelKey);
        Jobs();

        Assert.False(page.LabelFor(ListeningCapability.ModelKey)?.IsEffectivelyVisible ?? false);

        view.Tab = PanelTab.Transcript;
        model.AppendError("The speech model is missing. Pick one in Settings.", ListeningCapability.ModelKey);
        Jobs();

        Click(window, view.TranscriptBlocks.Single(), "Settings");

        Assert.Equal(PanelTab.Settings, view.Nav.Tab);
        Assert.True(page.LabelFor(ListeningCapability.ModelKey)?.IsEffectivelyVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void ABannerWithNoRowOpensTheSettingsTabWhereItWas()
    {
        using var look = AppLook.Put();
        var (view, window, model, page) = Shown();

        view.Tab = PanelTab.Settings;
        Jobs();
        page.ShowPlaceOf(ListeningCapability.CancelHotkeyKey);
        view.Tab = PanelTab.Transcript;
        Jobs();

        model.ShowError(NoVoice, SpeechCapability.ChatterboxVoiceKey);
        model.ErrorText = NoVoice;
        Jobs();

        Assert.Null(model.ErrorSetting);

        Click(window, Banner(view), "Settings");

        Assert.Equal(PanelTab.Settings, view.Nav.Tab);
        Assert.Equal("voice-input", page.SectionIds[page.ActiveSection]);

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetBannerOpensItsOwnSettingsPage()
    {
        using var look = AppLook.Put();
        var (settings, viewState, paths) = Surface();
        var model = new PanelViewModel();
        var page = new SettingsView();

        var headset = new VrPanelSurface(
            model,
            settings,
            _ => null,
            settingsPage: () =>
            {
                page.Attach(settings, viewState);
                return page;
            });

        model.ShowError(NoVoice, SpeechCapability.ChatterboxVoiceKey);
        Jobs();

        var (width, height) = headset.Size;
        var pixels = new VrPixels(width, height);
        headset.Draw(pixels.Address, pixels.RowBytes);

        var view = headset.Board.View.GetVisualDescendants().OfType<PanelView>().Single();
        var block = Banner(view);

        Assert.True(headset.Board.Click(Centre(block, "Settings", headset.Board.View)));
        Jobs();
        headset.Draw(pixels.Address, pixels.RowBytes);
        Jobs();

        Assert.Equal(PanelTab.Settings, headset.Nav.Tab);
        Assert.Equal("voice", page.SectionIds[page.ActiveSection]);

        headset.Dispose();
    }

    /// <summary>Chatterbox speaks for the ship, so its row is on the page.</summary>
    private static (SettingsService Settings, ViewStateStore ViewState, AppPaths Paths) Surface()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);
        settings.Apply(SpeechCapability.ProviderKey, TtsProviderCatalog.ChatterboxId, SettingsCaller.Panel);

        return (settings, viewState, paths);
    }

    private static (PanelView View, Window Window, PanelViewModel Model, SettingsView Page) Shown()
    {
        var (settings, viewState, paths) = Surface();
        var model = new PanelViewModel();
        var view = new PanelView { DataContext = model };
        var page = new SettingsView();

        view.EnableSettings(() =>
        {
            page.Attach(settings, viewState);
            return page;
        });

        var window = new Window { Content = view, Width = 1180, Height = 880 };
        window.Show();
        Jobs();

        return (view, window, model, page);
    }

    private static TextBlock Banner(PanelView view) =>
        view.GetVisualDescendants()
            .OfType<Notice>()
            .Single(notice => notice.Name == "ErrorBanner")
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(block => block.Inlines?.Text == NoVoice);

    private static Point Centre(TextBlock block, string text, Visual to)
    {
        var start = block.Inlines!.Text!.LastIndexOf(text, StringComparison.Ordinal);
        var rect = block.TextLayout.HitTestTextRange(start, text.Length).First();

        return block.TranslatePoint(rect.Center + new Point(block.Padding.Left, block.Padding.Top), to)!.Value;
    }

    private static void Click(Window window, TextBlock block, string text)
    {
        // Rendered first, so the press is hit-tested against where the block is now drawn.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Jobs();

        var at = Centre(block, text, window);

        window.MouseMove(at);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Jobs();
    }
}
