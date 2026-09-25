using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Info rows draw their value on a Slab data block with no left bar, the spend rows as one tile per
/// figure, the egress detail behind a SHOW tile, and the two forgetting presses red (#443).
/// </summary>
public sealed class ReadOnlyValuesSitOnGreyDataBlocksTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static SettingsHost Open(
        double width = 1400,
        SpeechSpend? spend = null,
        Action? forgetCorrections = null) => Open(out _, width, spend, forgetCorrections);

    private static SettingsHost Open(
        out SettingsService settings,
        double width = 1400,
        SpeechSpend? spend = null,
        Action? forgetCorrections = null)
    {
        (settings, var viewState, var paths) = TestSurface.Create(
            speechSpend: spend,
            forgetCorrections: forgetCorrections);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths, width: width);
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

    private static Color Slab(SettingsHost host)
    {
        Assert.True(host.View.TryFindResource(ThemeManager.SlabKey, host.View.ActualThemeVariant, out var found));
        return found switch
        {
            ISolidColorBrush brush => brush.Color,
            Color color => color,
            _ => throw new InvalidOperationException($"Slab is a {found?.GetType().Name}"),
        };
    }

    private static List<Border> DataBlocks(SettingsHost host) =>
        host.View.GetVisualDescendants()
            .OfType<Border>()
            .Where(border => border.Classes.Contains(SettingsView.DataBlockClass) && border.IsEffectivelyVisible)
            .ToList();

    private static Button Named(SettingsHost host, string name) =>
        host.View.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
    }

    [AvaloniaFact]
    public void EveryInfoRowInSettingsIsOnSlabWithNoLeftBar()
    {
        var host = Open(out var settings, spend: Spoken(), forgetCorrections: () => { });
        var slab = Slab(host);
        var checkedPlaces = 0;

        foreach (var place in SettingsLayout.Areas.SelectMany(area => area.Places))
        {
            ShowPlace(host, place.Id);

            var infoRows = settings.RowsForPlace(place.Id)
                .Where(row => row.Kind == D47.Core.Capabilities.SettingKind.Info && !row.ValueAsHint)
                .ToList();

            if (infoRows.Count == 0)
            {
                continue;
            }

            var blocks = DataBlocks(host);
            Assert.True(blocks.Count > 0, $"{place.Id} has Info rows but no data block");

            foreach (var block in blocks)
            {
                var ground = Assert.IsAssignableFrom<ISolidColorBrush>(block.Background);
                Assert.Equal(slab, ground.Color);
            }

            var bars = host.View.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.IsEffectivelyVisible && border.BorderThickness == new Thickness(2, 0, 0, 0));
            Assert.Empty(bars);

            checkedPlaces++;
        }

        Assert.True(checkedPlaces > 0);
        host.Close();
    }

    [AvaloniaFact]
    public void WhatItCostsShowsEachFigureAsItsOwnTile()
    {
        var host = Open(spend: Spoken());
        host.View.ShowPlaceOf(SpeechCapability.SpentKey);
        Jobs();

        var tiles = DataBlocks(host)
            .Select(block => block.GetVisualDescendants().OfType<TextBlock>().ToList())
            .Where(texts => texts.Count == 2)
            .ToList();

        var session = Assert.Single(tiles, texts => texts[0].Text == "SPOKEN THIS SESSION");
        Assert.StartsWith("115 chars · ", session[1].Text, StringComparison.Ordinal);
        Assert.Equal(new FontFamily(Fonts.MonoFamily).Name, session[1].FontFamily.Name);

        var aboard = Assert.Single(tiles, texts => texts[0].Text == "ABOARD");
        Assert.StartsWith("115 via ElevenLabs · ", aboard[1].Text, StringComparison.Ordinal);

        host.Close();
    }

    [AvaloniaFact]
    public void TheEgressDetailOpensFromAShowTileThatReadsHide()
    {
        var host = Open();
        var key = $"egress.{EgressDisclosure.Ids.First()}";
        host.View.ShowPlaceOf(key);
        Jobs();

        var toggle = Named(host, $"Disclose_{key.Replace('.', '_')}");
        Assert.Equal("Show", toggle.Content as string);

        Click(toggle);
        Assert.Equal("Hide", toggle.Content as string);

        Click(toggle);
        Assert.Equal("Show", toggle.Content as string);

        host.Close();
    }

    [AvaloniaFact]
    public void TheTwoForgettingPressesAreRed()
    {
        var host = Open(forgetCorrections: () => { });

        foreach (var key in new[] { ListeningCapability.CorrectionsKey, SpeechCapability.ResetVoicesKey })
        {
            host.View.ShowPlaceOf(key);
            Jobs();

            var press = Named(host, $"Press_{key.Replace('.', '_')}");
            Assert.Contains(SettingsView.DestructiveClass, press.Classes);
        }

        host.Close();
    }

    [AvaloniaTheory]
    [InlineData(1400)]
    [InlineData(924)]
    public void CapturesVoiceInputAndItsVoice(int width)
    {
        using var look = AppLook.Put();
        var host = Open(width, Spoken(), () => { });

        foreach (var placeId in new[] { "voice-input", "voice" })
        {
            ShowPlace(host, placeId);
            Capture(host, $"data-blocks-{placeId}-{width}.png");
        }

        host.View.ShowPlaceOf(SpeechCapability.SpentKey);
        Jobs();
        Reveal(host, DataBlocks(host).Last());
        Capture(host, $"data-blocks-voice-cost-{width}.png");

        host.View.ShowPlaceOf(ListeningCapability.CorrectionsKey);
        Jobs();
        Reveal(host, Named(host, $"Press_{ListeningCapability.CorrectionsKey.Replace('.', '_')}"));
        Capture(host, $"data-blocks-voice-input-corrections-{width}.png");

        var egress = $"egress.{EgressDisclosure.Ids.First()}";
        host.View.ShowPlaceOf(egress);
        Jobs();
        var disclose = Named(host, $"Disclose_{egress.Replace('.', '_')}");
        Click(disclose);
        Reveal(host, disclose);
        Capture(host, $"data-blocks-egress-{width}.png");

        host.Close();
    }

    private static void Reveal(SettingsHost host, Control target)
    {
        var scroller = host.View.FindControl<ScrollViewer>("Scroller")!;
        var top = target.TranslatePoint(default, (Visual)scroller.Content!)!.Value.Y;
        scroller.Offset = new Vector(0, Math.Max(0, top - 200));
        Jobs();
        Jobs();
    }

    private static void Capture(SettingsHost host, string name)
    {
        var path = Path.Combine(TestSurface.CaptureDirectory, name);

        using (var frame = host.Window.CaptureRenderedFrame()!)
        {
            frame.Save(path, new PngBitmapEncoderOptions());
        }

        Assert.True(File.Exists(path));
    }

    private static SpeechSpend Spoken()
    {
        var spend = new SpeechSpend();
        spend.Record(TtsProviderCatalog.ElevenLabsId, 115, VoiceGroup.Aboard);
        return spend;
    }
}
