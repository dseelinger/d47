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

/// <summary>
/// A voice chosen for the carrier shows up on the carrier's row
/// (<a href="https://github.com/dseelinger/d47/issues/90">#90</a>).
/// <para>
/// <b>Reported as "I pick 'use this' but the selected voice does not change in the drop downs"</b>,
/// against both carrier rows, with the row going on showing its default wording. Three earlier
/// attempts each named a mechanism and each was ruled out, and the reason they were all ruled out
/// is the same: they were tested against a <em>tab switch</em>, which does not detach this view.
/// </para>
/// <para>
/// <b>What the store said settled where it was not.</b> The Commander's own <c>settings.json</c>
/// held both ids, the log showed both applies landing and nothing rewriting them, and both ids
/// resolve to real names in the provider's fetched list. So the write was never the fault and
/// neither was the lookup: the row simply stopped re-reading.
/// </para>
/// <para>
/// <b>Two faults, each hiding the other.</b> <c>Apply</c> refreshed on failure alone, reasoning
/// that a successful change is visible in the control that made it — true of a toggle, false of a
/// picker button whose caption is derived from a catalogue. That was survivable only while the
/// settings subscription redrew the page, and the subscription was written <c>+=</c> once in the
/// constructor against <c>-=</c> on <em>every</em> detach: one detach and the page never hears a
/// settings change again, for the life of the view.
/// </para>
/// </summary>
public class APickedVoiceShowsOnItsRowTests
{
    /// <summary>Two voices with opaque ids, the shape an ElevenLabs account actually has.</summary>
    private static IReadOnlyList<VoiceInfo> Voices() =>
    [
        new("U5UjeJMsOvyhYhXfZdvZ", "Adam - Classic Scottish Storyteller", "scottish", "male"),
        new("mZ8K1MPRiT5wDQaasg3i", "Alexander Kensington - Studio Quality", "british", "male"),
    ];

    /// <summary>
    /// The reported fault, through the drawn page: choose a voice, and read the row.
    /// <para>
    /// The detach and re-attach in the middle is the whole point. It is what a tab switch was
    /// assumed to do and does not, and what something in a real session plainly does — the store
    /// proves the writes landed while the row stayed stale.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheRowShowsTheVoiceEvenAfterThePageHasBeenDetachedOnce()
    {
        var (settings, host) = Open();

        // Whatever ends a session's subscription — the page leaving the visual tree and coming
        // back. Once, which is all it ever took.
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
    /// And the tower row beside it, because both were reported and they are two rows rather than
    /// one drawn twice.
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
    /// The subscription itself, asserted as a property rather than through a symptom: a page that
    /// has been detached and re-attached is still listening, and is listening exactly once.
    /// <para>
    /// Once matters as much as at-all. Re-subscribing without unsubscribing first would leave two
    /// handlers on the second attach and three on the third, each posting its own redraw of every
    /// row on the page.
    /// </para>
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
    /// The other half of the fix, isolated: <b>applying through the page redraws it, without
    /// depending on the settings subscription existing.</b>
    /// <para>
    /// This is the half that is not reachable through the carrier rows, because the only thing
    /// that drives their apply is a modal picker. The <b>Unbind</b> button on a hotkey row goes
    /// through the same <c>Apply</c>, and its caption is derived the same way a picker button's
    /// is — nothing about clicking Unbind puts "Press to bind" on the button; only a redraw does.
    /// </para>
    /// <para>
    /// The page is detached first, which is the app's own code path for dropping the
    /// subscription. That is what leaves <c>Apply</c> as the only thing that could redraw, and so
    /// what makes this assert the fix rather than the subscription doing it again.
    /// </para>
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
            .First(button => button.Name != SettingsView.RowResetName
                             && button.Content as string != "Unbind");

        var unbind = row.GetVisualDescendants().OfType<Button>()
            .First(button => button.Content as string == "Unbind");

        // Bound, so that clearing it has something visible to undo.
        var key = ListeningCapability.PushToTalkKeyKey;

        settings.Apply(key, "Ctrl+Shift+D", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        Assert.NotEqual("Press to bind", bind.Content as string);

        // The app's own unsubscribe. From here only Apply can redraw the page.
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

    /// <summary>
    /// Takes the page out of the visual tree and puts it back — whatever holds it. Which control
    /// that is, is not the subject: the page is hosted in a <c>Border</c> today and the fault is
    /// about leaving the tree at all, so this handles either shape rather than pinning one.
    /// </summary>
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

        // The Commander's own shape: the ship on Edge, the carrier on ElevenLabs. The carrier's
        // ids are opaque, which is why its row's caption is looked up rather than shown raw.
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
