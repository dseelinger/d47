using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>Enter in an entry page's box does what Done does (#1913).</summary>
public class EnterInAnEntryBoxPressesDoneTests
{
    private static (Window Window, TextBox Box) Open(Func<string, EntryVerdict>? validate, Action<string> done)
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Transcript, new NavCrumb("root", "Root"));

        var layer = new Avalonia.Controls.Panel();
        var prompts = new PanelPrompts(nav, layer);

        prompts.Enter(
            new EntryRequest("entry", "Minutes", "How long?", null, string.Empty, EntrySurface.Voice, validate),
            done);

        var page = prompts.Build(nav.Trail[^1])!;
        var window = new Window
        {
            Content = new Avalonia.Controls.Panel { Children = { page, layer } },
            Width = AppLook.CaptureWidth,
            Height = AppLook.CaptureHeight,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page.GetVisualDescendants().OfType<TextBox>().First());
    }

    [AvaloniaFact]
    public void EnterCompletesTheEntryWithTheTypedValue()
    {
        using var _ = AppLook.Put();
        string? received = null;
        var (window, box) = Open(null, value => received = value);

        box.Text = "12";
        box.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("12", received);
        Assert.Equal("12", box.Text);

        window.Close();
    }

    [AvaloniaFact]
    public void EnterOnARefusedValueShowsTheComplaintAndCompletesNothing()
    {
        using var _ = AppLook.Put();
        string? received = null;
        var (window, box) = Open(_ => EntryVerdict.No("Not a number."),value => received = value);

        box.Text = "abc";
        box.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(received);
        Assert.Contains(
            window.GetVisualDescendants().OfType<TextBlock>(),
            line => line.Text?.Contains("Not a number.", StringComparison.Ordinal) == true);

        window.Close();
    }
}
