using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every entry opens listening, and the drawn keys, once shown, draw their characters whole (#433).</summary>
public class AnEntryOpensListeningWithLegibleKeysTests
{
    private sealed record Opened(PanelPrompts Prompts, Control Page, Window Window);

    private static Opened Open()
    {
        var nav = new PanelNavigator();

        nav.Register(PanelTab.Transcript, new NavCrumb("root", "Root"));

        var layer = new Avalonia.Controls.Panel();
        var prompts = new PanelPrompts(nav, layer);

        prompts.Enter(
            new EntryRequest("entry", "Minutes", "How long for \"Tea\"?", "Minutes, from now.", string.Empty, EntrySurface.Voice),
            _ => { });

        var page = prompts.Build(nav.Trail[^1])!;

        var window = new Window
        {
            Content = new Avalonia.Controls.Panel { Children = { page, layer } },
            Width = AppLook.CaptureWidth,
            Height = AppLook.CaptureHeight,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return new Opened(prompts, page, window);
    }

    private static CheckBox Swap(Control page) => page.GetVisualDescendants().OfType<CheckBox>().Single();

    /// <summary>The character keys and delete and clear: every button but Back and Done.</summary>
    private static List<Button> Keys(Control page) =>
        [.. page.GetVisualDescendants().OfType<Button>().Where(button => button.Content is string text && text is not ("Done" or "Back"))];

    [Fact]
    public void NoCallSiteOpensAnEntryOnTheKeyboard()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);

        while (root is not null && !File.Exists(Path.Combine(root.FullName, "d47.slnx")))
        {
            root = root.Parent;
        }

        var callers = Directory
            .EnumerateFiles(Path.Combine(root!.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => Path.GetFileName(file) != "PanelPrompts.cs")
            .Where(file => File.ReadAllText(file).Contains("EntrySurface.Keyboard", StringComparison.Ordinal))
            .Select(Path.GetFileName);

        Assert.Empty(callers);
    }

    [AvaloniaFact]
    public void AnEntryOpensWithTheBoxAndAnUntickedKeyboardAndNoKeys()
    {
        using var _ = AppLook.Put();
        var opened = Open();

        Assert.Contains(opened.Page.GetVisualDescendants().OfType<TextBox>(), box => box.IsEffectivelyVisible);
        Assert.False(Swap(opened.Page).IsChecked);
        Assert.NotEmpty(Keys(opened.Page));
        Assert.All(Keys(opened.Page), key => Assert.False(key.IsEffectivelyVisible));
        Assert.True(opened.Prompts.IsListening);

        opened.Window.Close();
    }

    [AvaloniaFact]
    public void NothingHeardBringsTheKeysUpWithTheLineThatSaysSo()
    {
        using var _ = AppLook.Put();
        var opened = Open();

        opened.Prompts.Hear(new Heard(string.Empty, 1, Final: true));
        Dispatcher.UIThread.RunJobs();

        Assert.True(Swap(opened.Page).IsChecked);
        Assert.All(Keys(opened.Page), key => Assert.True(key.IsEffectivelyVisible));

        var explained = TextEntryLoop.Explain(EntryFallback.NothingHeard, null);

        Assert.Contains(
            opened.Page.GetVisualDescendants().OfType<TextBlock>(),
            line => line.Text == explained && line.IsEffectivelyVisible);

        opened.Window.Close();
    }

    /// <summary>Ticks Keyboard, checks every key has room for its label, and saves the board.</summary>
    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Light)]
    public void TickingKeyboardDrawsEveryKeyWhole(string themeId)
    {
        using var _ = AppLook.Put(themeId);
        var opened = Open();

        Swap(opened.Page).IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        var keys = Keys(opened.Page);

        Assert.NotEmpty(keys);

        foreach (var key in keys)
        {
            Assert.True(key.IsEffectivelyVisible);

            var presenter = key.GetVisualDescendants().OfType<ContentPresenter>().First();
            var inside = presenter.Bounds.Deflate(presenter.Padding);
            var label = presenter.GetVisualDescendants().OfType<TextBlock>().Single();

            label.Measure(Size.Infinity);

            Assert.True(
                label.DesiredSize.Width <= inside.Width && label.DesiredSize.Height <= inside.Height,
                $"\"{key.Content}\" needs {label.DesiredSize} and has {inside.Size}");
        }

        var path = Path.Combine(TestSurface.CaptureDirectory, $"entry-keyboard-{themeId}.png");

        using (var frame = opened.Window.CaptureRenderedFrame()!)
        {
            frame.Save(path, new PngBitmapEncoderOptions());
        }

        opened.Window.Close();
    }
}
