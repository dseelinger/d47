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

/// <summary>
/// Push-to-talk is one row that holds a key, a stick button, or both
/// (<a href="https://github.com/dseelinger/d47/issues/217">#217</a>).
/// <para>
/// <b>Two rows over two properties, drawn as one.</b> <c>settings.json</c> is append-only, so
/// neither property is merged away — a build that dropped one would silently discard whichever
/// half of a Commander's binding it dropped. What merges is the question, which was always one.
/// </para>
/// <para>
/// Driven through the real settings surface rather than through a probe of the registry: the claim
/// is about what is on the page and what the controls on it write.
/// </para>
/// </summary>
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
            .First(button => button.Name != SettingsView.RowResetName
                             && button.Content as string != "Unbind");

    private static Button Unbind(Grid row) =>
        row.GetVisualDescendants().OfType<Button>()
            .First(button => button.Content as string == "Unbind");

    /// <summary>
    /// <b>One control says both.</b> The help already promised this — <em>"with both set, either
    /// one opens the microphone"</em> — and until now it took two rows to say it.
    /// </summary>
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

    /// <summary>
    /// And the second row is not on the page beside it. It is still a row — still written, still
    /// validated as a button, still documented — it is simply not a second question.
    /// </summary>
    [AvaloniaFact]
    public void TheButtonHalfIsNotDrawnAsARowOfItsOwn()
    {
        var (_, host) = Open();

        // Showing every setting, which SettingsHost turns on: this is not the fold hiding it.
        Assert.Null(Row(host, "Push-to-talk button"));
        Assert.NotNull(Row(host, "Push-to-talk"));

        host.Close();
    }

    /// <summary>
    /// <b>A Commander who already had both keeps both</b> through a render. The append-only rule
    /// is what this is really asserting: the properties are two and only the drawing merged.
    /// </summary>
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

    /// <summary>
    /// <b>Unbind means unbind</b>, which is the ruling the issue left open. Clearing both is what
    /// the word says; a removable chip per bound gesture would be a second idiom on the page for a
    /// row nobody has both halves of by accident.
    /// </summary>
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

    /// <summary>
    /// <b>A hold row must not quietly be given a fire-once binding.</b> Push-to-talk needs both
    /// edges, which is why it is polled through <c>GetAsyncKeyState</c> rather than registered as a
    /// hotkey — <c>RegisterHotKey</c> has no release edge. The control takes that from the row: a
    /// key bound here lands in <c>listening.pushToTalkKey</c>, the polled property, and in nothing
    /// under <c>hotkeys.</c>, which is where the fire-once binds live.
    /// </summary>
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

    /// <summary>
    /// <b>Which listeners are armed comes from the row.</b> With a controller composed the one
    /// control is waiting for either — the merge the issue asks for — and with none it says so
    /// rather than promising a stick that is not there. Asserted through the caption because the
    /// caption is the promise: a control that said "or button" with nothing polling would be
    /// lying about what pressing one would do.
    /// </summary>
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
    /// taking away the Commander's way of speaking to d47, and merging the rows traded nothing
    /// there. Protected rows cost no tool-surface bytes either, so there was nothing to trade.
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

        // Protected is checked at the service, not at the row: a caller that is not the model
        // still gets through, and the model does not.
        Assert.Equal(
            SettingApplyStatus.Refused,
            settings.Apply(key, null, SettingsCaller.Model).Status);
    }

    /// <summary>
    /// The pair is declared on the rows rather than known by the panel, so the surface holds no
    /// list of which two rows are really one — the fault that list would eventually have.
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

        // And DrawnElsewhere is not AppliesWhen: the row still applies, so it can still be
        // written — which is what happens every time a stick button is bound.
        Assert.True(button.Applies(settings.Current));
    }

    /// <summary>
    /// <b>How you end up with both: bind twice, once per gesture.</b> One capture takes whichever
    /// arrives first and stores it against the half it belongs to, so pressing the control again
    /// and giving it the other kind adds rather than replaces. Reported as a question —
    /// <em>"it says I can bind to both. How?"</em> — which is what sent the sentence into the help.
    /// </summary>
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

        // The stick half first, as the Commander who asked had it: bound to a button and nothing
        // else. The walk itself is ButtonCapture's, tested in Core, so it is not re-run here.
        settings.Apply(ListeningCapability.PushToTalkButtonKey, $"{Stick}#10", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        Press(host, row, Avalonia.Input.Key.F9, Avalonia.Input.PhysicalKey.F9);

        Assert.Equal("F9", settings.Current.Listening.PushToTalkKey);
        Assert.Equal($"{Stick}#10", settings.Current.Listening.PushToTalkButton);
        Assert.Equal("F9, button 11", Bind(row).Content as string);

        host.Close();
    }

    /// <summary>
    /// <b>Right shift is bindable, and it is the default.</b> A bare modifier used to be swallowed
    /// on every bind row as "someone still assembling a chord" — so the one key push-to-talk ships
    /// with could not be put back once it was cleared, which is exactly the corner the Commander
    /// who reported this was in.
    /// <para>
    /// Told apart by the edge rather than refused: pressed, a modifier is still a chord being
    /// assembled; released with nothing else having arrived, it was the binding. The same idiom
    /// the stick walk uses, and for the same reason — it is the edge that answers the question.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// And a chord is still a chord. The modifier is remembered on the way down and forgotten the
    /// moment anything else arrives, so holding Ctrl to reach Ctrl+D cannot bind Ctrl.
    /// </summary>
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

    /// <summary>
    /// <b>And a system-wide row still waits.</b> A key claimed from the whole system cannot be a
    /// bare one — it would stop working in every other application, Elite included — so binding one
    /// silently and having the service refuse it is worse than the capture going on listening.
    /// </summary>
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

    /// <summary>
    /// <b>One of each, and no more.</b> Binding a second key replaces the key and leaves the button
    /// where it is — the two halves are two settings properties holding one value apiece, so there
    /// is no third slot to land in. Worth pinning rather than assuming: "or both" reads to a
    /// Commander as though it might mean <em>any number</em>, and the answer is that it means one
    /// of each.
    /// </summary>
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
    /// construction rather than by the capture being careful. A stick button can only land in the
    /// <see cref="SettingKind.HotasButton"/> half, and a key can only land in the other.
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
    /// The caption reads key first whichever order they were bound in, so the row does not
    /// rearrange itself under a Commander who rebinds one half.
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
