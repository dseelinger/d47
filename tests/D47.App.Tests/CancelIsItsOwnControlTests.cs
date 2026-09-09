using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Hotas;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class CancelIsItsOwnControlTests
{
    private const string Stick = "NonRoamable+Id/One=";

    private static (SettingsService Settings, SettingsHost Host) Open()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        return (settings, SettingsHost.Open(settings, viewState, paths));
    }

    private static Grid? Row(SettingsHost host, string label) =>
        host.View.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.ColumnDefinitions.Count == 3 && grid.IsEffectivelyVisible)
            .FirstOrDefault(grid => grid.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == label));

    /// <summary>One stick with eight buttons, and whichever of them is held.</summary>
    private static HotasReading Reading(int? held = null)
    {
        var state = new bool[8];

        if (held is { } button)
        {
            state[button] = true;
        }

        return new HotasReading { Id = Stick, Buttons = state };
    }

    private static Button Bind(Grid row) =>
        row.GetVisualDescendants().OfType<Button>()
            .First(button => !SettingsView.IsRowChrome(button)
                             && button.Content as string != "Unbind");

    /// <summary>A press opens the microphone and does nothing else.</summary>
    [Fact]
    public void PressingPushToTalkNoLongerSilencesAnything()
    {
        var sources = new PushToTalkSources();
        var seen = new List<string>();

        sources.Pressed += () => seen.Add("opened");
        sources.Released += () => seen.Add("closed");

        sources.KeyPressed();
        sources.KeyReleased();
        sources.ButtonPressed();
        sources.ButtonReleased();

        Assert.Equal(["opened", "closed", "opened", "closed"], seen);

        // And there is no seam left to hang one on.
        Assert.Null(typeof(PushToTalkSources).GetProperty("Barge"));
    }

    /// <summary>
    /// The row is on the page under its own name, for every Commander. #218 had hidden it behind an
    /// <c>AppliesWhen</c> while push-to-talk was bound, which was almost everybody.
    /// </summary>
    [AvaloniaFact]
    public void CancelIsARowEveryCommanderCanSee()
    {
        var (settings, host) = Open();

        Assert.NotNull(Row(host, "Cancel"));
        Assert.Null(Row(host, "Stop speaking"));

        // Bound out of the box, which the row now says.
        var row = settings.Find(ListeningCapability.CancelHotkeyKey)!;

        Assert.Equal("Ctrl+Alt+X", settings.Current.Speech.ShutUpHotkey);
        Assert.Equal("Ctrl+Alt+X", row.DefaultDisplay);
        Assert.True(row.Applies(settings.Current));

        host.Close();
    }

    /// <summary>
    /// It takes a stick button, which is the ask, through the one bind control #217 built: the key row
    /// names the button row and the pair is drawn once.
    /// </summary>
    [AvaloniaFact]
    public void OneRowHoldsTheKeyAndTheStickButton()
    {
        var (settings, host) = Open();

        settings.Apply(ListeningCapability.CancelButtonKey, $"{Stick}#7", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        var row = Row(host, "Cancel")!;

        Assert.Equal("Ctrl+Alt+X, button 8", Bind(row).Content as string);

        // And the button half is not a second row on the page.
        Assert.Null(Row(host, "Cancel button"));

        host.Close();
    }

    /// <summary>Both halves are <see cref="SettingRow.Protected"/>.</summary>
    [Theory]
    [InlineData(ListeningCapability.CancelHotkeyKey)]
    [InlineData(ListeningCapability.CancelButtonKey)]
    public void NeitherHalfIsReachableFromTheModel(string key)
    {
        var settings = TestSurface.Settings();

        Assert.True(settings.Find(key)!.Protected);
        Assert.Equal(
            SettingApplyStatus.Refused,
            settings.Apply(key, null, SettingsCaller.Model).Status);
    }

    /// <summary>
    /// A key claimed from the whole system still cannot be a bare one, and Cancel is claimed from the
    /// whole system because the moment it is wanted is the moment Elite is in front.
    /// </summary>
    [Fact]
    public void TheKeyHalfIsStillSystemWide()
    {
        var settings = TestSurface.Settings();

        Assert.True(settings.Find(ListeningCapability.CancelHotkeyKey)!.SystemWide);
        Assert.Equal(
            SettingApplyStatus.Rejected,
            settings.Apply(ListeningCapability.CancelHotkeyKey, "F9", SettingsCaller.Panel).Status);
    }

    [Fact]
    public void TheStickButtonCancelsOnceOnThePress()
    {
        var button = new BoundButton();
        var cancels = 0;

        button.Pressed += () => cancels++;
        button.Bind(HotasButton.Parse($"{Stick}#7"));

        var down = new[] { Reading(held: 7) };
        var up = new[] { Reading() };

        button.Poll(down);
        button.Poll(down);
        button.Poll(down);

        Assert.Equal(1, cancels);

        button.Poll(up);
        Assert.Equal(1, cancels);

        button.Poll(down);
        Assert.Equal(2, cancels);
    }

    /// <summary>
    /// A binding a Commander already had is untouched by the row growing a second job, because the
    /// property behind it never moved — <c>settings.json</c> is append-only, and the older spelling is
    /// why it is still called <c>shutUpHotkey</c>.
    /// </summary>
    [Fact]
    public void AKeyAlreadyBoundSurvivesTheWidening()
    {
        var settings = TestSurface.Settings();

        settings.Apply(ListeningCapability.CancelHotkeyKey, "Ctrl+Alt+Q", SettingsCaller.Panel);

        Assert.Equal("Ctrl+Alt+Q", settings.Current.Speech.ShutUpHotkey);
        Assert.Equal("Ctrl+Alt+Q", settings.Read(ListeningCapability.CancelHotkeyKey));
    }

    /// <summary>Cancel is drawn directly under push-to-talk: they are the two controls a Commander binds together.</summary>
    [AvaloniaFact]
    public void CancelSitsDirectlyBelowPushToTalk()
    {
        var (_, host) = Open();

        var labels = host.View.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass) && grid.IsEffectivelyVisible)
            .Select(grid => grid.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        var at = labels.IndexOf("Push-to-talk");

        Assert.True(at >= 0, "the push-to-talk row is not on the page");
        Assert.Equal("Cancel", labels[at + 1]);

        host.Close();
    }

    /// <summary>
    /// And the key it is stored under decides which subsystem re-applies it, which is the half of the
    /// move that was not about layout.
    /// </summary>
    [Fact]
    public void BothHalvesRouteToTheListeningSubsystem()
    {
        Assert.Equal(
            SettingsSubsystem.Listening,
            SettingsFanout.For(ListeningCapability.CancelHotkeyKey).Subsystem);

        Assert.Equal(
            SettingsSubsystem.Listening,
            SettingsFanout.For(ListeningCapability.CancelButtonKey).Subsystem);
    }
}
