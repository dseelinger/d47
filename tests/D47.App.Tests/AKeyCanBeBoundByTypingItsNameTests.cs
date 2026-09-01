using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using D47.App.Input;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using D47.Core.Capabilities.Builtin;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Binding a key by typing its name
/// (<a href="https://github.com/dseelinger/d47/issues/221">#221</a>).
/// <para>
/// <b>Typing is the only road to twelve of these keys.</b> F13 upward are what HOTAS software
/// hands out — VoiceAttack, TARGET, VIRPIL — precisely because no keyboard has them and nothing
/// else binds them. A Commander cannot press F23 to bind it; there is no F23 to press. The
/// support was already there and only the entry was missing: <c>VirtualKeys</c> has mapped
/// F1–F24 all along, and push-to-talk polls exactly that code.
/// </para>
/// </summary>
public class AKeyCanBeBoundByTypingItsNameTests
{
    /// <summary>
    /// <b>What is typed must store what a press would store.</b> The capture writes
    /// <c>new KeyGesture(key, modifiers).ToString()</c>; so does this. Anything else and the row
    /// would show one thing and a later read find another.
    /// </summary>
    [Theory]
    [InlineData("F13")]
    [InlineData("F17")]
    [InlineData("F23")]
    [InlineData("F24")]
    public void EveryKeyOfTheHotasRangeRoundTrips(string name)
    {
        Assert.True(Gestures.TryType(name, out var stored, out var refusal));
        Assert.Null(refusal);

        // The same bytes a press would have written.
        var key = Enum.Parse<Key>(name);
        Assert.Equal(new KeyGesture(key, KeyModifiers.None).ToString(), stored);

        // And the row will show back exactly what was typed.
        Assert.Equal(name, Gestures.Describe(stored));
    }

    /// <summary>Case and spacing are not the Commander's problem: f23, F23 and " F23 " are one key.</summary>
    [Theory]
    [InlineData("f23")]
    [InlineData(" F23 ")]
    [InlineData("F23")]
    public void CaseAndSpacingDoNotMatter(string typed)
    {
        Assert.True(Gestures.TryType(typed, out var stored, out _));
        Assert.Equal(new KeyGesture(Key.F23, KeyModifiers.None).ToString(), stored);
    }

    /// <summary>
    /// <b>F25 is refused, and the refusal names the ceiling.</b> Win32 defines VK_F1 through
    /// VK_F24 and stops, so Avalonia's enum does too — software that appears to send F25 on
    /// Windows is sending something else. A Commander told "not a key I know" would go looking
    /// for the fault in d47.
    /// </summary>
    [Theory]
    [InlineData("F25")]
    [InlineData("F26")]
    [InlineData("f30")]
    public void PastTheEndOfTheRangeIsRefusedBySayingWhereTheEndIs(string typed)
    {
        Assert.False(Gestures.TryType(typed, out var stored, out var refusal));

        Assert.Null(stored);
        Assert.NotNull(refusal);
        Assert.Contains("F1 to F24", refusal, StringComparison.Ordinal);
    }

    /// <summary>
    /// A typo is refused rather than stored. A gesture no key can ever match is a push-to-talk
    /// that silently never opens the microphone, which is the worst failure this row has.
    /// </summary>
    [Theory]
    [InlineData("Fn23")]
    [InlineData("banana")]
    [InlineData("23")]
    [InlineData("")]
    [InlineData("   ")]
    public void AnythingUnrecognisedIsRefusedRatherThanStored(string typed)
    {
        Assert.False(Gestures.TryType(typed, out var stored, out var refusal));

        Assert.Null(stored);
        Assert.False(string.IsNullOrWhiteSpace(refusal));
    }

    /// <summary>Modifiers still parse, since the same box serves the rows that need them.</summary>
    [Fact]
    public void ModifiersParseAndAnUnknownOneSaysWhichWordItWas()
    {
        Assert.True(Gestures.TryType("Ctrl+F13", out var stored, out _));
        Assert.Equal(new KeyGesture(Key.F13, KeyModifiers.Control).ToString(), stored);

        Assert.True(Gestures.TryType("ctrl+shift+alt+F24", out var all, out _));
        Assert.Equal(
            new KeyGesture(Key.F24, KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt).ToString(),
            all);

        Assert.False(Gestures.TryType("Hyper+F13", out _, out var refusal));
        Assert.Contains("Hyper", refusal!, StringComparison.Ordinal);
    }

    /// <summary>
    /// What the row prints can be typed back into it. "Esc" and "Page Up" are this table's own
    /// words rather than the enum's, so a box that only read the enum would refuse the very
    /// spelling the row had just shown.
    /// </summary>
    [Theory]
    [InlineData("Esc", Key.Escape)]
    [InlineData("Page Up", Key.Prior)]
    [InlineData("pageup", Key.Prior)]
    [InlineData("Enter", Key.Return)]
    [InlineData(",", Key.OemComma)]
    public void WhatTheRowPrintsIsWhatCanBeTyped(string typed, Key expected)
    {
        Assert.True(Gestures.TryType(typed, out var stored, out _));
        Assert.Equal(new KeyGesture(expected, KeyModifiers.None).ToString(), stored);
    }

    /// <summary>
    /// <b>A bare F23 is accepted on a system-wide row and a bare R is still refused</b> (#221).
    /// <para>
    /// The modifier rule exists to stop a Commander claiming a key the game needs — press R
    /// system-wide and Elite never sees it again. F13–F24 are the exact keys nothing else binds,
    /// which is why stick software emits them, so requiring a modifier there protects a key that
    /// needs no protection.
    /// </para>
    /// </summary>
    [Fact]
    public void ASystemWideRowTakesABareHotasKeyAndStillRefusesABareLetter()
    {
        var (settings, _, _) = TestSurface.Create();

        var row = settings.Sections
            .SelectMany(section => section.Rows)
            .First(candidate => candidate.Kind == SettingKind.Hotkey && candidate.SystemWide);

        var letter = settings.Apply(row.Key, "R", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Rejected, letter.Status);
        Assert.Contains("needs a modifier", letter.Message, StringComparison.OrdinalIgnoreCase);

        foreach (var name in new[] { "F13", "F23", "F24" })
        {
            var high = settings.Apply(row.Key, name, SettingsCaller.Panel);

            Assert.True(high.Ok, $"{name} was refused on a system-wide row: {high.Message}");
            Assert.Equal(name, settings.Read(row.Key));
        }

        // And the exemption is exactly twelve keys wide at both ends.
        foreach (var outside in new[] { "F12", "F1" })
        {
            Assert.Equal(
                SettingApplyStatus.Rejected,
                settings.Apply(row.Key, outside, SettingsCaller.Panel).Status);
        }
    }

    /// <summary>
    /// The bind row with its third route on it, for a human to look at. A box, a capture button
    /// and Unbind have to share a column that is two-fifths of the row with a 190-pixel floor —
    /// which is the one thing about this change a test cannot tell you is wrong.
    /// </summary>
    [AvaloniaFact]
    public void TheBindRowIsDrawnForLookingAt()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);
        settings.Apply(InterfaceCapability.ShowEverySettingKey, "true", SettingsCaller.Panel);

        var view = new SettingsView();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableSettings(() =>
        {
            view.Attach(settings, viewState, paths);
            return view;
        });

        var window = new Window { Content = panel, Width = 1180, Height = 880 };

        ZoomHost.Attach(window, settings);
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        panel.Tab = PanelTab.Settings;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Down to the card that carries push-to-talk, which is the bind row a Commander with a
        // stick actually types into.
        var cards = (StackPanel)view.FindControl<Control>("Cards")!;
        var scroller = view.FindControl<ScrollViewer>("Scroller")!;
        var listening = cards.Children.OfType<Border>().Skip(2).First();

        scroller.Offset = new Avalonia.Vector(0, listening.Bounds.Y);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "bind-row-typed.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }

    /// <summary>
    /// Push-to-talk was already taking bare keys — its own default is RightShift — so nothing
    /// about that row changes. Asserted because the exemption above is written in the same
    /// method, and a change there that reached this row would be silent.
    /// </summary>
    [Fact]
    public void ThePolledRowStillTakesWhateverItAlwaysDid()
    {
        var (settings, _, _) = TestSurface.Create();

        Assert.True(settings.Apply(ListeningCapability.PushToTalkKeyKey, "F23", SettingsCaller.Panel).Ok);
        Assert.Equal("F23", settings.Read(ListeningCapability.PushToTalkKeyKey));

        Assert.True(settings.Apply(ListeningCapability.PushToTalkKeyKey, "RightShift", SettingsCaller.Panel).Ok);
    }
}
