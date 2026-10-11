using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
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

/// <summary>While the chosen Chatterbox voice is not on this PC, the Voice row says who speaks meanwhile.</summary>
[Trait("Category", "Integration")]
public class AVoiceRowNamesItsStandInTests
{
    private static readonly VoiceInfo[] Voices =
    [
        new("marlow", "Marlow", "en", "female") { Pitch = "high", Pace = "slow" },
        new("lucie", "Lucie", "en", "female") { Pitch = "mid", Pace = "even" },
        new("af_heart", "Heart", "en-US", "female"),
    ];

    private static readonly ChatterboxVoice[] Catalogue =
    [
        new(Voices[0], VoiceRole.ShipAi, "a test clip", "marlow.wav") { Pitch = "high", Pace = "slow" },
        new(Voices[1], null, "a test clip", "lucie.wav") { Shipped = false, Pitch = "mid", Pace = "even", Bytes = 247 * 1024 },
    ];

    private sealed class Clips
    {
        public Dictionary<string, ChatterboxClipState> State { get; } = new()
        {
            ["marlow"] = new(ChatterboxClip.Shipped, 0),
            ["lucie"] = new(ChatterboxClip.NotFetched, 247 * 1024),
        };

        public List<Action<string>> Watchers { get; } = [];

        public List<string> Fetches { get; } = [];

        /// <summary>The voices the slot lists.</summary>
        public string[] Listed { get; init; } = [.. Voices.Select(voice => voice.Id)];

        public void Changed(string id)
        {
            foreach (var watcher in Watchers.ToArray())
            {
                watcher(id);
            }

            Jobs();
        }

        public SpeechCapability.SpeechSurface Wire(SpeechCapability.SpeechSurface surface) => surface with
        {
            Voices = _ => Listed,
            VoiceName = (_, id) => Voices.FirstOrDefault(voice => voice.Id == id)?.Name,
            VoicePitch = (_, id) => Voices.FirstOrDefault(voice => voice.Id == id)?.Pitch,
            VoicePace = (_, id) => Voices.FirstOrDefault(voice => voice.Id == id)?.Pace,
            VoiceClip = (_, id) => State.TryGetValue(id, out var state) ? state : null,
            WatchVoiceClips = refresh =>
            {
                Watchers.Add(refresh);
                return () => Watchers.Remove(refresh);
            },
            FetchVoice = (_, id) => Fetches.Add(id),
            ChatterboxCatalogue = _ => Catalogue,
        };
    }

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static SettingsHost Open(Clips clips, string provider = TtsProviderCatalog.ChatterboxId, string voice = "lucie")
    {
        var (settings, viewState, paths, _, _) = TestSurface.CreateFull(voices: Voices, speech: clips.Wire);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        settings.Apply(SpeechCapability.ProviderKey, provider, SettingsCaller.Panel);
        settings.Apply(SpeechCapability.VoiceKey, voice, SettingsCaller.Panel);

        var host = SettingsHost.Open(settings, viewState, paths, width: 1180, height: 900);

        host.View.ShowPlaceOf(SpeechCapability.VoiceKey);
        Jobs();

        return host;
    }

    private static StackPanel VoiceRow(SettingsView view) =>
        view.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass))
            .First(grid => grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Voice"))
            .FindAncestorOfType<StackPanel>()!;

    private static Button Tile(SettingsView view) =>
        VoiceRow(view).GetVisualDescendants().OfType<Button>().First(button => button.Name == "DropdownTile");

    private static string[] TileWords(SettingsView view) =>
        [.. Tile(view).GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text))
            .Select(text => text.Text!)];

    private static Border? Line(SettingsView view) =>
        VoiceRow(view).GetVisualDescendants().OfType<Border>().SingleOrDefault(border => border.Name == "VoiceAway");

    private static string[] LineWords(SettingsView view) =>
        Line(view) is { IsVisible: true } line
            ? [.. line.GetVisualDescendants().OfType<TextBlock>()
                .Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text))
                .Select(text => text.Text!)]
            : [];

    private static Button LineButton(SettingsView view, string says) =>
        Line(view)!.GetVisualDescendants().OfType<Button>().Single(button => button.IsEffectivelyVisible && button.Content as string == says);

    [AvaloniaFact]
    public void TheTileShowsTheNameTheBandsAndTheState()
    {
        var host = Open(new Clips());

        Same(["Lucie", "FEMALE · MID PITCH · EVEN PACE", "NOT FETCHED", "▼"], TileWords(host.View));

        host.Close();
    }

    [AvaloniaFact]
    public void AFetchHeldOpenShowsFetchingWithTheLineAndTheLineGoesWhenTheClipArrives()
    {
        var clips = new Clips();
        clips.State["lucie"] = new(ChatterboxClip.Fetching, 247 * 1024);

        var host = Open(clips);

        Assert.Contains("FETCHING", TileWords(host.View));
        Same(["FETCHING", "247 KB · GITHUB.COM", "Until Lucie arrives, D47 speaks in Marlow."], LineWords(host.View));

        Capture(host, "voice-row-fetching");

        clips.State["lucie"] = new(ChatterboxClip.Fetched, 247 * 1024);
        clips.Changed("lucie");

        Assert.False(Line(host.View)!.IsVisible);
        Assert.Contains("FETCHED", TileWords(host.View));

        host.Close();
    }

    [AvaloniaFact]
    public void ANotFetchedVoiceOffersFetchNow()
    {
        var clips = new Clips();
        var host = Open(clips);

        Same(
            ["NOT ON THIS PC", "247 KB", "D47 fetches Lucie before its next line, and speaks in Marlow if that takes more than 3 seconds.", "Fetch now"],
            LineWords(host.View));

        Capture(host, "voice-row-not-fetched");

        LineButton(host.View, "Fetch now").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(["lucie"], clips.Fetches);

        host.Close();
    }

    [AvaloniaFact]
    public void AFailedFetchShowsTheNoticeWithItsReasonAndRetryStartsOneFetch()
    {
        var clips = new Clips();
        clips.State["lucie"] = new(ChatterboxClip.Failed, 247 * 1024) { Reason = "The release answered 503 Service Unavailable" };

        var host = Open(clips);

        // Refreshed more than once, so a tile wired again on each refresh would fetch more than once.
        clips.Changed("lucie");
        clips.Changed("lucie");

        var notice = Line(host.View)!.GetVisualDescendants().OfType<Notice>().Single();

        Assert.True(notice.IsEffectivelyVisible);
        Assert.Equal("Lucie could not be fetched", notice.Label);
        Assert.Equal("D47 speaks in Marlow until it arrives. Retry asks github.com again.", notice.Text);
        Assert.Equal("THE RELEASE ANSWERED 503 SERVICE UNAVAILABLE", notice.Detail);
        Assert.True(LineButton(host.View, "Choose another").IsEffectivelyVisible);

        Capture(host, "voice-row-fetch-failed");

        LineButton(host.View, "Retry").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(["lucie"], clips.Fetches);
        Assert.Single(clips.Watchers);

        host.Close();
    }

    [AvaloniaFact]
    public void ThePickerSaysWhatChoosingTheHighlightedVoiceFetches()
    {
        var host = Open(new Clips { Listed = ["marlow", "lucie"] });

        Tile(host.View).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();

        var page = host.Panel.GetVisualDescendants().OfType<PickerPage>().Single();
        var line = page.GetControl<TextBlock>("ChoosingLine");

        Assert.True(line.IsVisible);
        Assert.Equal("Choosing Lucie fetches 247 KB from github.com. Until it arrives, D47 speaks in Marlow.", line.Text);

        host.Close();
    }

    [AvaloniaFact]
    public void AKokoroVoiceRowShowsItsTileAsTodayWithNoLine()
    {
        var clips = new Clips();
        var host = Open(clips, TtsProviderCatalog.KokoroId, "af_heart");

        Same([Voices[2].Label, "▼"], TileWords(host.View));
        Assert.False(Line(host.View)!.IsVisible);
        Assert.Empty(clips.Watchers);

        host.Close();
    }

    /// <summary>The same words, in whatever order the tree holds them.</summary>
    private static void Same(string[] expected, string[] drawn) =>
        Assert.Equal(expected.Order(StringComparer.Ordinal), drawn.Order(StringComparer.Ordinal));

    private static void Capture(SettingsHost host, string name)
    {
        Jobs();

        host.Window.CaptureRenderedFrame()!.SaveCapture($"{name}.png");
    }
}
