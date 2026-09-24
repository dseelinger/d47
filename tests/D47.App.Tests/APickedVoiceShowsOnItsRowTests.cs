using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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

/// <summary>A voice chosen for the carrier shows up on the carrier's row.</summary>
public class APickedVoiceShowsOnItsRowTests
{
    /// <summary>Two voices with opaque ids, the shape an ElevenLabs account actually has.</summary>
    private static IReadOnlyList<VoiceInfo> Voices() =>
    [
        new("U5UjeJMsOvyhYhXfZdvZ", "Adam - Classic Scottish Storyteller", "scottish", "male"),
        new("mZ8K1MPRiT5wDQaasg3i", "Alexander Kensington - Studio Quality", "british", "male"),
    ];

    /// <summary>Choose a voice, and read the row.</summary>
    [AvaloniaFact]
    public void TheRowShowsTheVoiceEvenAfterThePageHasBeenDetachedOnce()
    {
        var (settings, view, window) = OpenCarrierStrip();

        // Whatever ends a session's subscription — the page leaving the visual tree and coming back.
        DetachAndReattach(view);

        settings.Apply(
            SpeechCapability.CarrierCaptainVoiceKey, "U5UjeJMsOvyhYhXfZdvZ", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            "Adam - Classic Scottish Storyteller",
            DrawnValue(view, "Carrier captain voice"),
            StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    /// And the tower row beside it, because both were reported and they are two rows rather than one
    /// drawn twice.
    /// </summary>
    [AvaloniaFact]
    public void AndSoDoesTheTowerRow()
    {
        var (settings, view, window) = OpenCarrierStrip();

        settings.Apply(SpeechCapability.TowerVoiceKey, "mZ8K1MPRiT5wDQaasg3i", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            "Alexander Kensington - Studio Quality",
            DrawnValue(view, "Carrier tower voice"),
            StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    /// The subscription itself, asserted as a property rather than through a symptom: a page that has
    /// been detached and re-attached is still listening, and is listening exactly once.
    /// </summary>
    [AvaloniaFact]
    public void ThePageIsListeningExactlyOnceAfterAnyNumberOfDetaches()
    {
        var (settings, view, window) = OpenCarrierStrip();

        for (var i = 0; i < 3; i++)
        {
            DetachAndReattach(view);
        }

        var redraws = 0;
        void Count(SettingsChanged _) => redraws++;

        settings.Changed += Count;
        settings.Apply(SpeechCapability.TowerVoiceKey, "mZ8K1MPRiT5wDQaasg3i", SettingsCaller.Panel);
        settings.Changed -= Count;

        Dispatcher.UIThread.RunJobs();

        // One change raised once, and the row drew it.
        Assert.Equal(1, redraws);

        Assert.Contains(
            "Alexander Kensington - Studio Quality",
            DrawnValue(view, "Carrier tower voice"),
            StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    /// The other half of the fix, isolated: applying through the page redraws it, without depending on
    /// the settings subscription existing.
    /// </summary>
    [AvaloniaFact]
    public void ApplyingThroughThePageRedrawsItWithoutTheSubscription()
    {
        // The full settings page, not the carrier strip: push-to-talk lives there.
        var (settings, viewState, paths) = TestSurface.Create(voices: Voices());
        var host = SettingsHost.Open(settings, viewState, paths);
        Dispatcher.UIThread.RunJobs();

        var row = host.View.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass))
            .FirstOrDefault(grid => grid.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == "Push-to-talk"));

        Assert.True(row is not null, "the push-to-talk row is not on the page");

        string? Bound() => row!.GetVisualDescendants().OfType<Border>()
            .Where(chip => chip.Classes.Contains(SettingsView.BindingChipClass) && chip.IsEffectivelyVisible)
            .Select(chip => (chip.Child as TextBlock)?.Text)
            .First();

        Button Unbind() => row!.GetVisualDescendants().OfType<Button>()
            .First(button => button.Content as string == "CLEAR" && button.IsEffectivelyVisible);

        // Bound, so that clearing it has something visible to undo.
        var key = ListeningCapability.PushToTalkKeyKey;

        settings.Apply(key, "Ctrl+Shift+D", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        Assert.NotEqual("NONE", Bound());

        // The app's own unsubscribe.
        DetachOnly(host);

        Unbind().RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(settings.Read(key));
        Assert.Equal("NONE", Bound());

        host.Close();
    }

    /// <summary>Detaches the page and leaves it detached, which is what drops the subscription.</summary>
    private static void DetachOnly(SettingsHost host)
    {
        if (host.View.GetVisualParent() is Border border)
        {
            border.Child = null;
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Takes the strip out of the visual tree and puts it back.</summary>
    private static void DetachAndReattach(SettingsView view)
    {
        var border = (Border)view.GetVisualParent()!;

        border.Child = null;
        Dispatcher.UIThread.RunJobs();
        border.Child = view;
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Everything the named row draws in its control column, joined.</summary>
    private static string DrawnValue(SettingsView view, string label)
    {
        var row = view.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass))
            .FirstOrDefault(grid => grid.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == label));

        Assert.True(row is not null, $"the \"{label}\" row is not on the page");

        return string.Join(
            " | ",
            row!.GetVisualDescendants().OfType<TextBlock>()
                .Select(text => text.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    /// <summary>
    /// Both carrier voice rows are on Fleet › Carrier now, not the settings page (#305) — drawn as a
    /// standalone strip the way <c>MainWindow.BuildSettingsStrip</c> actually builds one, not through the
    /// full settings page.
    /// </summary>
    private static (SettingsService Settings, SettingsView View, Window Window) OpenCarrierStrip()
    {
        var (settings, viewState, paths) = TestSurface.Create(voices: Voices());

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        // The Commander's own shape: the ship on Edge, the carrier on ElevenLabs.
        settings.Apply(SpeechCapability.ProviderKey, TtsProviderCatalog.EdgeId, SettingsCaller.Panel);
        settings.Apply(
            SpeechCapability.SlotProviderKey(VoiceGroups.Carrier),
            TtsProviderCatalog.ElevenLabsId,
            SettingsCaller.Panel);

        // Expanded, so the picker buttons' own value labels are actually realized — a collapsed strip
        // never lays out what it is hiding, and a headless test never triggers the layout pass a click
        // would.
        viewState.Save(viewState.Load().With("fleet-carrier", expanded: true));

        var view = new SettingsView();
        view.Attach(settings, viewState, paths, tabPlaceId: "fleet-carrier");

        var border = new Border { Child = view };
        var window = new Window { Content = border, Width = 900, Height = 700 };
        window.Show();

        Dispatcher.UIThread.RunJobs();

        return (settings, view, window);
    }
}
