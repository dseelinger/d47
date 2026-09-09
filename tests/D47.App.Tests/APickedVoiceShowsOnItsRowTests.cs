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
        var (settings, host) = Open();

        // Whatever ends a session's subscription — the page leaving the visual tree and coming back.
        DetachAndReattach(host);

        settings.Apply(
            SpeechCapability.CarrierCaptainVoiceKey, "U5UjeJMsOvyhYhXfZdvZ", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            "Adam - Classic Scottish Storyteller",
            DrawnValue(host, "Carrier captain voice"),
            StringComparison.Ordinal);

        host.Close();
    }

    /// <summary>
    /// And the tower row beside it, because both were reported and they are two rows rather than one
    /// drawn twice.
    /// </summary>
    [AvaloniaFact]
    public void AndSoDoesTheTowerRow()
    {
        var (settings, host) = Open();

        settings.Apply(SpeechCapability.TowerVoiceKey, "mZ8K1MPRiT5wDQaasg3i", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(
            "Alexander Kensington - Studio Quality",
            DrawnValue(host, "Carrier tower voice"),
            StringComparison.Ordinal);

        host.Close();
    }

    /// <summary>
    /// The subscription itself, asserted as a property rather than through a symptom: a page that has
    /// been detached and re-attached is still listening, and is listening exactly once.
    /// </summary>
    [AvaloniaFact]
    public void ThePageIsListeningExactlyOnceAfterAnyNumberOfDetaches()
    {
        var (settings, host) = Open();

        for (var i = 0; i < 3; i++)
        {
            DetachAndReattach(host);
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
            DrawnValue(host, "Carrier tower voice"),
            StringComparison.Ordinal);

        host.Close();
    }

    /// <summary>
    /// The other half of the fix, isolated: applying through the page redraws it, without depending on
    /// the settings subscription existing.
    /// </summary>
    [AvaloniaFact]
    public void ApplyingThroughThePageRedrawsItWithoutTheSubscription()
    {
        var (settings, host) = Open();

        var row = host.View.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.ColumnDefinitions.Count == 3)
            .FirstOrDefault(grid => grid.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == "Push-to-talk"));

        Assert.True(row is not null, "the push-to-talk row is not on the page");

        var bind = row!.GetVisualDescendants().OfType<Button>()
            .First(button => !SettingsView.IsRowChrome(button)
                             && button.Content as string != "Unbind");

        var unbind = row.GetVisualDescendants().OfType<Button>()
            .First(button => button.Content as string == "Unbind");

        // Bound, so that clearing it has something visible to undo.
        var key = ListeningCapability.PushToTalkKeyKey;

        settings.Apply(key, "Ctrl+Shift+D", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        Assert.NotEqual("Press to bind", bind.Content as string);

        // The app's own unsubscribe.
        DetachOnly(host);

        unbind.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(settings.Read(key));
        Assert.Equal("Press to bind", bind.Content as string);

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

    /// <summary>Takes the page out of the visual tree and puts it back — whatever holds it.</summary>
    private static void DetachAndReattach(SettingsHost host)
    {
        switch (host.View.GetVisualParent())
        {
            case Border border:
                border.Child = null;
                Dispatcher.UIThread.RunJobs();
                border.Child = host.View;
                break;

            case Avalonia.Controls.Panel panel:
                panel.Children.Remove(host.View);
                Dispatcher.UIThread.RunJobs();
                panel.Children.Add(host.View);
                break;

            case ContentControl content:
                content.Content = null;
                Dispatcher.UIThread.RunJobs();
                content.Content = host.View;
                break;

            case var other:
                Assert.Fail($"the page is held by a {other?.GetType().Name ?? "nothing"}, "
                            + "which this helper cannot detach");
                break;
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Everything the named row draws in its control column, joined.</summary>
    private static string DrawnValue(SettingsHost host, string label)
    {
        var row = host.View.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.ColumnDefinitions.Count == 3)
            .FirstOrDefault(grid => grid.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == label));

        Assert.True(row is not null, $"the \"{label}\" row is not on the page");

        return string.Join(
            " | ",
            row!.GetVisualDescendants().OfType<TextBlock>()
                .Select(text => text.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static (SettingsService Settings, SettingsHost Host) Open()
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

        var host = SettingsHost.Open(settings, viewState, paths);
        Dispatcher.UIThread.RunJobs();

        return (settings, host);
    }
}
