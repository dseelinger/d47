using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Headset;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>A place in the panel named in a message is a link to it, in the window and in the headset (#951).</summary>
public sealed class APlaceNamedInAMessageIsALinkTests
{
    private const string NoVoice = "No Chatterbox voice has been chosen. Pick one in Settings.";
    private const string Loadout = "The full loadout is on Asset Mgmt › Ships.";

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public void TheBannerLinksSettingsAndOpensItClosingADialogPage()
    {
        using var look = AppLook.Put();
        var (view, window, model) = Shown();

        Assert.True(view.Nav.Select(PanelTab.Commander));
        Assert.True(view.Nav.Take(new NavCrumb("pick", "Pick")));

        model.ErrorText = NoVoice;
        Jobs();

        var block = BannerText(view);

        Assert.Equal(Cyan(), Ink(Run(block, "Settings")));
        Assert.NotEqual(Cyan(), Ink(block.Inlines!.OfType<Run>().First()));

        Click(window, block, "Settings");

        Assert.False(view.Nav.Modal);
        Assert.Equal(PanelTab.Settings, view.Nav.Tab);

        window.Close();
    }

    [AvaloniaFact]
    public void ANoticeOnAPageLinksSettings()
    {
        using var look = AppLook.Put();
        var (view, window, _) = Shown();
        const string text = "Looking markets up is switched off. Turn it on in Settings to search.";

        view.Furnish(PanelTab.Navigation, _ => new Notice(NoticeLevel.Warning) { Text = text }, new NavCrumb("plan", "Plan"));
        Assert.True(view.Nav.Select(PanelTab.Navigation));
        Jobs();

        var block = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(candidate => candidate.Inlines?.Text == text);

        Assert.Equal(Cyan(), Ink(Run(block, "Settings")));

        Click(window, block, "Settings");

        Assert.Equal(PanelTab.Settings, view.Nav.Tab);

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ATurnLinksAPathAndOpensItsRoot(bool failed)
    {
        using var look = AppLook.Put();
        var (view, window, model) = Shown();

        if (failed)
        {
            model.AppendError(Loadout);
        }
        else
        {
            model.Append(Loadout);
        }

        Jobs();

        var block = view.TranscriptBlocks.Single();

        Assert.Equal(Cyan(), Ink(Run(block, "Asset Mgmt › Ships")));
        Assert.Equal("carrier", view.Nav.RootKeyOf(PanelTab.Assets));

        Click(window, block, "Asset Mgmt › Ships");

        Assert.Equal(PanelTab.Assets, view.Nav.Tab);
        Assert.Equal("ships", view.Nav.RootKeyOf(PanelTab.Assets));

        window.Close();
    }

    [AvaloniaFact]
    public void ATabNameOnItsOwnIsNotALink()
    {
        using var look = AppLook.Put();
        var (view, window, model) = Shown();

        model.Append("Commander, the route is plotted.");
        Jobs();

        var block = view.TranscriptBlocks.Single();

        Assert.All(block.Inlines!.OfType<Run>(), run => Assert.NotEqual(Cyan(), Ink(run)));

        window.Close();
    }

    [AvaloniaFact]
    public void CopyingAcrossALinkReturnsThePlainText()
    {
        using var look = AppLook.Put();
        var (view, window, model) = Shown();

        model.Append(Loadout);
        Jobs();

        var block = view.TranscriptBlocks.Single();

        Assert.True(block.Inlines!.Count > 1);

        block.SelectAll();

        Assert.Equal(Loadout, block.SelectedText);

        window.Close();
    }

    [AvaloniaFact]
    public void HoveringALinkUnderlinesItAndDraggingAcrossItSelects()
    {
        using var look = AppLook.Put();
        var (view, window, model) = Shown();

        model.Append(Loadout);
        Jobs();

        var block = view.TranscriptBlocks.Single();
        var link = Centre(block, "Asset Mgmt › Ships", window);
        var before = Centre(block, "loadout", window);

        window.MouseMove(link);
        Jobs();

        Assert.Equal(TextDecorations.Underline, Run(block, "Asset Mgmt › Ships").TextDecorations);

        window.MouseDown(link, MouseButton.Left);
        window.MouseMove(before, RawInputModifiers.LeftMouseButton);
        window.MouseMove(link, RawInputModifiers.LeftMouseButton);
        window.MouseUp(link, MouseButton.Left);
        Jobs();

        Assert.NotEmpty(block.SelectedText);
        Assert.Equal(PanelTab.Transcript, view.Nav.Tab);

        window.Close();
    }

    [AvaloniaFact]
    public void ASearchMatchInsideALinkIsStillMarked()
    {
        using var look = AppLook.Put();
        var (view, window, model) = Shown();

        view.EnableSearch();
        model.Append(Loadout);
        Jobs();

        view.GetControl<TextBox>("SearchInput").Text = "Mgmt";
        Jobs();

        var block = view.TranscriptBlocks.Single();
        var match = Run(block, "Mgmt");

        Assert.NotNull(match.Background);
        Assert.Equal("Asset ", Run(block, "Asset ").Text);
        Assert.Equal(Cyan(), Ink(Run(block, "Asset ")));

        window.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void TheHeadsetBannerOpensSettingsInTheHeadsetOnly()
    {
        using var look = AppLook.Put();
        var (desk, shown, _) = Shown();
        var model = new PanelViewModel { ErrorText = NoVoice };

        var headset = new VrPanelSurface(
            model,
            TestSurface.Settings(),
            _ => null,
            settingsPage: () => new TextBlock { Text = "Settings page" });

        var mirror = new TranscriptMirror();
        mirror.Lead(desk.Nav);
        mirror.Add(headset.Nav);
        Jobs();

        var (width, height) = headset.Size;
        var pixels = new VrPixels(width, height);
        headset.Draw(pixels.Address, pixels.RowBytes);

        var view = headset.Board.View.GetVisualDescendants().OfType<PanelView>().Single();
        var block = BannerText(view);

        Assert.Equal(Cyan(), Ink(Run(block, "Settings")));
        Assert.True(headset.Board.Click(Centre(block, "Settings", headset.Board.View)));
        Jobs();

        Assert.Equal(PanelTab.Settings, headset.Nav.Tab);
        Assert.Equal(PanelTab.Transcript, desk.Nav.Tab);

        headset.Dispose();
        shown.Close();
    }

    private static (PanelView View, Window Window, PanelViewModel Model) Shown()
    {
        var model = new PanelViewModel();
        var view = new PanelView { DataContext = model };

        view.EnableSettings(() => new TextBlock { Text = "Settings page" });
        view.Furnish(PanelTab.Commander, crumb => new TextBlock { Text = crumb.Word }, new NavCrumb("checklist", "Checklist"));
        view.Furnish(
            PanelTab.Assets,
            crumb => new TextBlock { Text = crumb.Word },
            new NavCrumb("carrier", "Carrier"),
            new NavCrumb("ships", "Ships"));

        var window = new Window { Content = view, Width = 1000, Height = 700 };
        window.Show();
        Jobs();

        return (view, window, model);
    }

    private static TextBlock BannerText(PanelView view) =>
        view.GetVisualDescendants()
            .OfType<Notice>()
            .Single(notice => notice.Name == "ErrorBanner")
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(block => block.Inlines?.Text == NoVoice);

    private static Run Run(TextBlock block, string text) =>
        block.Inlines!.OfType<Run>().Single(run => run.Text == text);

    private static Color Cyan() => ((ISolidColorBrush)Application.Current!.Resources[ThemeManager.CyanKey]!).Color;

    private static Color? Ink(Run run) => (run.Foreground as ISolidColorBrush)?.Color;

    /// <summary>The middle of <paramref name="text"/> where <paramref name="block"/> draws it, in <paramref name="to"/>'s coordinates.</summary>
    private static Point Centre(TextBlock block, string text, Visual to)
    {
        var start = block.Inlines!.Text!.IndexOf(text, StringComparison.Ordinal);
        var rect = block.TextLayout.HitTestTextRange(start, text.Length).First();

        return block.TranslatePoint(rect.Center + new Point(block.Padding.Left, block.Padding.Top), to)!.Value;
    }

    private static void Click(Window window, TextBlock block, string text)
    {
        var at = Centre(block, text, window);

        window.MouseMove(at);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Jobs();
    }
}
