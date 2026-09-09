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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Push-to-talk is one row that holds a key, a stick button, or both.</summary>
public class OneRowForPushToTalkTests
{
    private const string Stick = "NonRoamable+Id/One=";

    private static (SettingsService Settings, SettingsHost Host) Open()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        return (settings, SettingsHost.Open(settings, viewState, paths));
    }

    /// <summary>A visible row by its label, as a Commander would pick it out.</summary>
    private static Grid? Row(SettingsHost host, string label) =>
        host.View.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.ColumnDefinitions.Count == 3 && grid.IsEffectivelyVisible)
            .FirstOrDefault(grid => grid.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text == label));

    private static Button Bind(Grid row) =>
        row.GetVisualDescendants().OfType<Button>()
            .First(button => !SettingsView.IsRowChrome(button)
                             && button.Content as string != "Unbind");

    private static Button Unbind(Grid row) =>
        row.GetVisualDescendants().OfType<Button>()
            .First(button => button.Content as string == "Unbind");

    /// <summary>One control says both.</summary>
    [AvaloniaFact]
    public void OneRowShowsAKeyAndAButtonTogether()
    {
        var (settings, host) = Open();

        settings.Apply(ListeningCapability.PushToTalkKeyKey, "RightShift", SettingsCaller.Panel);
        settings.Apply(ListeningCapability.PushToTalkButtonKey, $"{Stick}#10", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        var row = Row(host, "Push-to-talk");
        Assert.True(row is not null, "the push-to-talk row is not on the page");

        Assert.Equal("RightShift, button 11", Bind(row!).Content as string);

        host.Close();
    }

    /// <summary>And the second row is not on the page beside it.</summary>
    [AvaloniaFact]
    public void TheButtonHalfIsNotDrawnAsARowOfItsOwn()
    {
        var (_, host) = Open();

        // Showing every setting, which SettingsHost turns on: this is not the fold hiding it.
        Assert.Null(Row(host, "Push-to-talk button"));
        Assert.NotNull(Row(host, "Push-to-talk"));

        host.Close();
    }

    /// <summary>A Commander who already had both keeps both through a render.</summary>
    [AvaloniaFact]
    public void BothHalvesSurviveTheSurfaceRenderingThem()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        settings.Apply(ListeningCapability.PushToTalkKeyKey, "RightShift", SettingsCaller.Panel);
        settings.Apply(ListeningCapability.PushToTalkButtonKey, $"{Stick}#10", SettingsCaller.Panel);

        var host = SettingsHost.Open(settings, viewState, paths);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("RightShift", settings.Current.Listening.PushToTalkKey);
        Assert.Equal($"{Stick}#10", settings.Current.Listening.PushToTalkButton);

        host.Close();
    }

    /// <summary>Unbind means unbind, which is the ruling the issue left open.</summary>
    [AvaloniaFact]
    public void UnbindClearsBothHalves()
    {
        var (settings, host) = Open();

        settings.Apply(ListeningCapability.PushToTalkKeyKey, "RightShift", SettingsCaller.Panel);
        settings.Apply(ListeningCapability.PushToTalkButtonKey, $"{Stick}#10", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        var row = Row(host, "Push-to-talk")!;

        Unbind(row).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(settings.Current.Listening.PushToTalkKey);
        Assert.Null(settings.Current.Listening.PushToTalkButton);
        Assert.Equal("Press to bind", Bind(row).Content as string);

        host.Close();
    }

    /// <summary>A hold row must not quietly be given a fire-once binding.</summary>
    [AvaloniaFact]
    public void AKeyBoundOnThisRowIsAHeldKeyRatherThanAFireOnceHotkey()
    {
        var (settings, host) = Open();

        var row = Row(host, "Push-to-talk")!;

        Bind(row).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        host.Window.KeyPress(Avalonia.Input.Key.F9, Avalonia.Input.RawInputModifiers.None,
            Avalonia.Input.PhysicalKey.F9, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("F9", settings.Current.Listening.PushToTalkKey);

        // And nothing under hotkeys. moved with it, which is the half that would be silent.
        Assert.NotEqual("F9", settings.Current.Hotkeys.ShowOverlay);
        Assert.NotEqual("F9", settings.Current.Speech.ShutUpHotkey);

        host.Close();
    }

    /// <summary>Which listeners are armed comes from the row.</summary>
    [AvaloniaTheory]
    [InlineData(true, "Press a key or button…")]
    [InlineData(false, "Press a key…")]
    public void TheControlArmsTheStickOnlyWhenThereIsOne(bool controllers, string says)
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(
            settings, viewState, paths, switches: controllers ? Editing(paths) : null);

        var row = Row(host, "Push-to-talk")!;
        var bind = Bind(row);

        bind.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(says, bind.Content as string);

        // Escape puts the capture down again, so the page is not left armed behind the test.
        host.Window.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None,
            Avalonia.Input.PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();

        host.Close();
    }

    /// <summary>A controller seam with nothing plugged in — enough to be composed, and it is.</summary>
    private static SwitchEditing Editing(D47.Core.AppPaths paths) => new(
        new D47.Core.Hotas.SwitchStore(
            Path.Combine(paths.Data, "switches.json"),
            NullLogger<D47.Core.Hotas.SwitchStore>.Instance),
        new D47.Core.Hotas.FakeHotasReader(),
        new D47.Core.Hotas.SwitchReconciler(NullLogger<D47.Core.Hotas.SwitchReconciler>.Instance),
        () => DateTimeOffset.UnixEpoch,
        Path.Combine(paths.Data, "switch-capture.txt"),
        () => []);

    /// <summary>
    /// Both halves stay <see cref="SettingRow.Protected"/> — rebinding or clearing push-to-talk is
    /// taking away the Commander's way of speaking to d47, and merging the rows traded nothing there.
    /// </summary>
    [Theory]
    [InlineData(ListeningCapability.PushToTalkKeyKey)]
    [InlineData(ListeningCapability.PushToTalkButtonKey)]
    public void BothHalvesAreStillUnreachableFromTheModel(string key)
    {
        var settings = TestSurface.Settings();
        var row = settings.Find(key);

        Assert.NotNull(row);
        Assert.True(row.Protected);

        // Protected is checked at the service, not at the row: a caller that is not the model still gets
        // through, and the model does not.
        Assert.Equal(
            SettingApplyStatus.Refused,
            settings.Apply(key, null, SettingsCaller.Model).Status);
    }

    /// <summary>
    /// The pair is declared on the rows rather than known by the panel, so the surface holds no list of
    /// which two rows are really one — the fault that list would eventually have.
    /// </summary>
    [Fact]
    public void TheRowsThemselvesSayTheyAreOnePair()
    {
        var settings = TestSurface.Settings();

        var key = settings.Find(ListeningCapability.PushToTalkKeyKey)!;
        var button = settings.Find(ListeningCapability.PushToTalkButtonKey)!;

        Assert.Equal(ListeningCapability.PushToTalkButtonKey, key.AlsoBinds);
        Assert.True(button.DrawnElsewhere);
        Assert.Equal(
            [ListeningCapability.PushToTalkKeyKey, ListeningCapability.PushToTalkButtonKey],
            key.BoundKeys);

        // And DrawnElsewhere is not AppliesWhen: the row still applies, so it can still be written — which is
        // what happens every time a stick button is bound.
        Assert.True(button.Applies(settings.Current));
    }

    /// <summary>How you end up with both: bind twice, once per gesture.</summary>
    [AvaloniaFact]
    public void BindingAgainAddsTheOtherKindRatherThanReplacingTheFirst()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths, switches: Editing(paths));
        var row = Row(host, "Push-to-talk")!;

        Unbind(row).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // The stick half first, as the Commander who asked had it: bound to a button and nothing else.
        settings.Apply(ListeningCapability.PushToTalkButtonKey, $"{Stick}#10", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        Press(host, row, Avalonia.Input.Key.F9, Avalonia.Input.PhysicalKey.F9);

        Assert.Equal("F9", settings.Current.Listening.PushToTalkKey);
        Assert.Equal($"{Stick}#10", settings.Current.Listening.PushToTalkButton);
        Assert.Equal("F9, button 11", Bind(row).Content as string);

        host.Close();
    }

    /// <summary>Right shift is bindable, and it is the default.</summary>
    [AvaloniaTheory]
    [InlineData(Avalonia.Input.Key.RightShift, Avalonia.Input.PhysicalKey.ShiftRight, "RightShift")]
    [InlineData(Avalonia.Input.Key.LeftAlt, Avalonia.Input.PhysicalKey.AltLeft, "LeftAlt")]
    public void ABareModifierBindsOnItsRelease(
        Avalonia.Input.Key key, Avalonia.Input.PhysicalKey physical, string stored)
    {
        var (settings, host) = Open();

        // Cleared first, so RightShift arriving means this capture rather than the default.
        settings.Apply(ListeningCapability.PushToTalkKeyKey, "", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        var row = Row(host, "Push-to-talk")!;

        Bind(row).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        host.Window.KeyPress(key, Avalonia.Input.RawInputModifiers.None, physical, null);
        Dispatcher.UIThread.RunJobs();

        // Still nothing: pressed is not enough, because this is also how a chord starts.
        Assert.NotEqual(stored, settings.Current.Listening.PushToTalkKey);

        host.Window.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, physical, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(stored, settings.Current.Listening.PushToTalkKey);

        host.Close();
    }

    [AvaloniaFact]
    public void AModifierOnTheWayToAChordDoesNotBindItself()
    {
        var (settings, host) = Open();
        var row = Row(host, "Push-to-talk")!;

        Bind(row).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        host.Window.KeyPress(Avalonia.Input.Key.LeftCtrl, Avalonia.Input.RawInputModifiers.None,
            Avalonia.Input.PhysicalKey.ControlLeft, null);
        host.Window.KeyPress(Avalonia.Input.Key.D, Avalonia.Input.RawInputModifiers.Control,
            Avalonia.Input.PhysicalKey.D, null);
        host.Window.KeyRelease(Avalonia.Input.Key.LeftCtrl, Avalonia.Input.RawInputModifiers.None,
            Avalonia.Input.PhysicalKey.ControlLeft, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Ctrl+D", settings.Current.Listening.PushToTalkKey);

        host.Close();
    }

    [AvaloniaFact]
    public void ASystemWideRowStillIgnoresABareModifier()
    {
        var (settings, host) = Open();
        var row = Row(host, "Show or hide the overlay")!;

        Bind(row).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        host.Window.KeyPress(Avalonia.Input.Key.RightShift, Avalonia.Input.RawInputModifiers.None,
            Avalonia.Input.PhysicalKey.ShiftRight, null);
        host.Window.KeyRelease(Avalonia.Input.Key.RightShift, Avalonia.Input.RawInputModifiers.None,
            Avalonia.Input.PhysicalKey.ShiftRight, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Ctrl+Alt+O", settings.Current.Hotkeys.ShowOverlay);

        // Still armed, so the Commander's next attempt is heard.
        Assert.Equal("Press a key…", Bind(row).Content as string);

        host.Window.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None,
            Avalonia.Input.PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();

        host.Close();
    }

    /// <summary>Arm the row's control and give it one whole keystroke.</summary>
    private static void Press(
        SettingsHost host, Grid row, Avalonia.Input.Key key, Avalonia.Input.PhysicalKey physical)
    {
        Bind(row).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        host.Window.KeyPress(key, Avalonia.Input.RawInputModifiers.None, physical, null);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>One of each, and no more.</summary>
    [AvaloniaFact]
    public void ASecondKeyReplacesTheKeyAndLeavesTheButtonAlone()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths, switches: Editing(paths));
        var row = Row(host, "Push-to-talk")!;

        settings.Apply(ListeningCapability.PushToTalkButtonKey, $"{Stick}#10", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        Press(host, row, Avalonia.Input.Key.F9, Avalonia.Input.PhysicalKey.F9);
        Assert.Equal("F9, button 11", Bind(row).Content as string);

        Press(host, row, Avalonia.Input.Key.F10, Avalonia.Input.PhysicalKey.F10);

        Assert.Equal("F10", settings.Current.Listening.PushToTalkKey);
        Assert.Equal($"{Stick}#10", settings.Current.Listening.PushToTalkButton);
        Assert.Equal("F10, button 11", Bind(row).Content as string);

        host.Close();
    }

    /// <summary>
    /// And there are two slots, one per kind — which is what makes the sentence above true by
    /// construction rather than by the capture being careful.
    /// </summary>
    [Fact]
    public void ThereAreTwoSlotsAndTheyAreOnePerKind()
    {
        var settings = TestSurface.Settings();
        var row = settings.Find(ListeningCapability.PushToTalkKeyKey)!;

        Assert.Equal(
            [SettingKind.Hotkey, SettingKind.HotasButton],
            row.BoundKeys.Select(key => settings.Find(key)!.Kind));
    }

    /// <summary>
    /// The caption reads key first whichever order they were bound in, so the row does not rearrange
    /// itself under a Commander who rebinds one half.
    /// </summary>
    [AvaloniaFact]
    public void TheCaptionReadsKeyThenButtonWhicheverWasBoundFirst()
    {
        var (settings, host) = Open();

        settings.Apply(ListeningCapability.PushToTalkButtonKey, $"{Stick}#10", SettingsCaller.Panel);
        settings.Apply(ListeningCapability.PushToTalkKeyKey, "F9", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("F9, button 11", Bind(Row(host, "Push-to-talk")!).Content as string);

        host.Close();
    }
}
