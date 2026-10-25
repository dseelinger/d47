using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Input;
using D47.App.Windowing;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>The keys dialog lists the fixed keys and the bound ones, opens on a typed question mark and closes on it or Esc (#1531).</summary>
[Trait("Category", "Integration")]
public sealed class TheKeysDialogListsEveryKeyTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static (KeysDialog Dialog, Window Window, Func<int> Changed) Shown(Action<D47.Core.Configuration.SettingsService>? bind = null)
    {
        new D47.App.Theming.ThemeManager(Application.Current!, Microsoft.Extensions.Logging.Abstractions.NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var settings = TestSurface.Settings();
        bind?.Invoke(settings);

        var window = new MainWindow(host: null);
        window.Show();
        Jobs();

        var changed = 0;
        var dialog = new KeysDialog(KeyList.Build(settings, row => Gestures.BoundAs(settings, row)), () => changed++);
        _ = dialog.Over(window);
        Jobs();

        return (dialog, window, () => changed);
    }

    private static IEnumerable<string?> Texts(Control root) => root.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text);

    [AvaloniaFact]
    public void EveryGroupIsDrawnInOrder()
    {
        var (dialog, _, _) = Shown();

        var names = dialog.GetVisualDescendants().OfType<StackPanel>()
            .Where(p => p.Name?.StartsWith(KeysDialog.GroupPrefix, StringComparison.Ordinal) == true)
            .Select(p => p.Name![KeysDialog.GroupPrefix.Length..])
            .ToArray();

        Assert.Equal(
            ["Getting around", "Look and keys", "On the page", "In the ask box", "Microphone", "Comms channel keys", "Overlay", "Headset overlay", "Galaxy map", "Hull viewer"],
            names);
    }

    [AvaloniaFact]
    public void ARebindingShowsTheNextTimeItOpensAndAClearedKeyReadsUnbound()
    {
        var (dialog, _, _) = Shown(settings =>
        {
            settings.Apply("hotkeys.find", "F23", D47.Core.Configuration.SettingsCaller.Panel);
            settings.Apply("hotkeys.focusAsk", string.Empty, D47.Core.Configuration.SettingsCaller.Panel);
        });

        var texts = Texts(dialog).ToList();

        Assert.Contains("F23", texts);
        Assert.Contains(KeysDialog.UnboundText, texts);
    }

    [AvaloniaFact]
    public void ChangeKeysClosesTheListAndAsksForSettings()
    {
        var (dialog, window, changed) = Shown();

        dialog.GetVisualDescendants().OfType<Button>().Single(b => b.Name == KeysDialog.ChangeKeysName)
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Jobs();

        Assert.Equal(1, changed());
        Assert.Null(ModalHost.For(window).Top);
    }

    [AvaloniaFact]
    public void EscapeClosesTheList()
    {
        var (_, window, _) = Shown();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Jobs();

        Assert.Null(ModalHost.For(window).Top);
    }

    [AvaloniaFact]
    public void AQuestionMarkOpensItOnlyWhereNoTextBoxHasTheCursor()
    {
        var typed = new KeyEventArgs { Key = Key.OemQuestion, KeyModifiers = KeyModifiers.Shift, KeySymbol = "?" };

        Assert.True(MainWindow.IsKeysKey(typed, new Button()));
        Assert.True(MainWindow.IsKeysKey(typed, null));
        Assert.False(MainWindow.IsKeysKey(typed, new TextBox()));
        Assert.False(MainWindow.IsKeysKey(new KeyEventArgs { Key = Key.OemQuestion, KeySymbol = "/" }, null));
    }

    [AvaloniaFact]
    public void TheListIsCaptured()
    {
        var (dialog, window, _) = Shown();
        window.Width = 900;
        window.Height = 1000;
        Jobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("keys-dialog.png");

        Assert.NotNull(dialog);
    }
}
