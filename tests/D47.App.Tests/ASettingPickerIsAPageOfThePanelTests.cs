using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A settings Choice row opens its picker as a page of the panel it is drawn on, with a crumb above it, on
/// the desktop window and in the headset alike (#414).
/// </summary>
public class ASettingPickerIsAPageOfThePanelTests
{
    /// <summary>The smallest panel size a Commander can pick (<see cref="PanelResolution.Steps"/>).</summary>
    private const int Width = 800;

    private const int Height = 500;

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    /// <summary>Enough voices to fill the list, with genders so the facet is drawn.</summary>
    private static IReadOnlyList<VoiceInfo> Voices() =>
    [
        .. Enumerable.Range(1, 24).Select(i => new VoiceInfo(
            $"voice-{i:00}",
            $"Voice number {i}",
            "en-GB",
            i % 3 == 0 ? null : i % 2 == 0 ? "female" : "male")),
    ];

    private static SettingsHost Desktop() => Desktop(out _);

    private static SettingsHost Desktop(out SettingsService settings)
    {
        (settings, var viewState, var paths) = TestSurface.Create(
            voices: Voices(),
            audition: (_, _, _) => Task.CompletedTask);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths, width: Width, height: Height);

        host.View.Reveal(SpeechCapability.Id);
        Jobs();

        return host;
    }

    /// <summary>The row's dropdown tile, found by the row's label.</summary>
    private static Button PickerButton(SettingsView view, string label) =>
        view.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass))
            .First(grid => grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label))
            .GetVisualDescendants().OfType<Button>()
            .First(button => button.Content is DockPanel panel
                && panel.Children.OfType<TextBlock>().Any(text => text.Text == "▼"));

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
    }

    private static PickerPage? Page(PanelView panel) =>
        panel.GetVisualDescendants().OfType<PickerPage>().SingleOrDefault();

    /// <summary>List rows drawn wholly inside the list's own viewport.</summary>
    private static int RowsInView(PickerPage page)
    {
        var list = page.GetControl<ListBox>("Choices");

        return list.GetVisualDescendants().OfType<ListBoxItem>()
            .Count(item => item.IsVisible
                && item.TranslatePoint(default, list) is { } top
                && top.Y >= -0.5
                && top.Y + item.Bounds.Height <= list.Bounds.Height + 0.5);
    }

    /// <summary>Every facet option drawn at its full width and inside the page.</summary>
    private static void AssertFacetIsWhole(PickerPage page)
    {
        var options = page.GetControl<Segment>("FacetBox").GetVisualDescendants().OfType<RadioButton>().ToList();

        Assert.Equal(["All", "Female", "Male", "Unlabelled"], options.Select(option => option.Content as string));

        Assert.All(options, option =>
        {
            Assert.True(
                option.DesiredSize.Width <= option.Bounds.Width + 0.5,
                $"{option.Content} is squeezed to {option.Bounds.Width} of {option.DesiredSize.Width}.");

            var right = option.TranslatePoint(new Point(option.Bounds.Width, 0), page)!.Value.X;

            Assert.True(right <= page.Bounds.Width + 0.5, $"{option.Content} ends at {right} on a {page.Bounds.Width} page.");
        });
    }

    [AvaloniaFact]
    public void TheVoicePickerOpensAsAPageInTheWindowWithACrumbAboveIt()
    {
        var host = Desktop();
        var windows = host.Window.OwnedWindows.Count;

        Press(PickerButton(host.View, "Voice"));

        var page = Page(host.Panel);

        Assert.NotNull(page);
        Assert.True(host.Panel.Nav.Modal);
        Assert.Equal("Voice", host.Panel.Nav.Trail[^1].Word);
        Assert.Equal(windows, host.Window.OwnedWindows.Count);

        // The cost sentence and not the row's help.
        var note = page!.GetControl<TextBlock>("AuditionNote");

        Assert.True(note.IsVisible);
        Assert.Equal("Play a voice to hear it. This provider costs nothing.", note.Text);
        Assert.False(page.GetControl<TextBlock>("HelpText").IsVisible);

        host.Close();
    }

    [AvaloniaFact]
    public void AtTheSmallestPanelEightVoicesShowAndTheFacetIsWhole()
    {
        var host = Desktop();

        Press(PickerButton(host.View, "Voice"));

        var page = Page(host.Panel)!;

        Capture(host.Window, "picker-page-voice-800x500");

        Assert.True(RowsInView(page) >= 8, $"only {RowsInView(page)} voices are visible at {Width}x{Height}.");
        AssertFacetIsWhole(page);

        host.Close();
    }

    [AvaloniaFact]
    public void AnyOtherChoiceRowKeepsItsHelpOnThePage()
    {
        var host = Desktop();

        host.View.Reveal(ConversationCapability.Id);
        Jobs();

        Press(PickerButton(host.View, "Model"));

        var page = Page(host.Panel)!;

        Capture(host.Window, "picker-page-model-800x500");

        var help = page.GetControl<TextBlock>("HelpText");

        Assert.True(help.IsVisible);
        Assert.Equal("Which model at that endpoint. Leave it unset to use the provider's default.", help.Text);
        Assert.False(page.GetControl<TextBlock>("AuditionNote").IsVisible);

        host.Close();
    }

    [AvaloniaFact]
    public void ChoosingCommitsReturnsToSettingsAndTheRowSaysSo()
    {
        var host = Desktop(out var settings);

        Press(PickerButton(host.View, "Voice"));

        var page = Page(host.Panel)!;

        page.GetControl<TextBox>("FilterBox").Text = "number 7";
        Jobs();

        Press(page.GetControl<Button>("AcceptButton"));

        Assert.False(host.Panel.Nav.Modal);
        Assert.Null(Page(host.Panel));
        Assert.Equal("voice-07", settings.Read(SpeechCapability.VoiceKey));

        var shown = PickerButton(host.View, "Voice").GetVisualDescendants().OfType<TextBlock>()
            .First(text => text.Text != "▼");

        Assert.Contains("Voice number 7", shown.Text, StringComparison.Ordinal);

        host.Close();
    }

    public static TheoryData<string> WaysOut => ["Esc", "Back button", "Breadcrumb back", "Mouse back"];

    [AvaloniaTheory]
    [MemberData(nameof(WaysOut))]
    public void EveryWayOutLeavesTheSettingAlone(string way)
    {
        var host = Desktop(out var settings);
        var before = settings.Read(SpeechCapability.VoiceKey);

        Press(PickerButton(host.View, "Voice"));

        var page = Page(host.Panel)!;

        // A highlighted row that would be taken if leaving took anything.
        page.GetControl<ListBox>("Choices").SelectedIndex = 3;
        Jobs();

        switch (way)
        {
            case "Esc":
                page.GetControl<TextBox>("FilterBox").Focus();
                host.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                break;

            case "Back button":
                Press(page.GetControl<Button>("BackButton"));
                break;

            case "Breadcrumb back":
                Assert.True(host.Panel.GoBack());
                break;

            case "Mouse back":
                host.Window.MouseDown(new Point(Width / 2.0, Height / 2.0), MouseButton.XButton1);
                break;
        }

        Jobs();

        Assert.False(host.Panel.Nav.Modal);
        Assert.Null(Page(host.Panel));
        Assert.Equal(before, settings.Read(SpeechCapability.VoiceKey));

        host.Close();
    }

    [AvaloniaFact]
    public void ThePickerButtonStopsSayingItIsBusyOnceTheListIsUp()
    {
        var host = Desktop();
        var button = PickerButton(host.View, "Voice");

        Press(button);
        Jobs();

        Assert.NotNull(Page(host.Panel));

        var glyph = button.GetVisualAncestors().OfType<DockPanel>().First()
            .Children.OfType<BusyGlyph>().Single();

        Assert.False(glyph.IsVisible);
        Assert.True(button.IsEnabled);

        host.Close();
    }

    /// <summary>The headset's panel: offscreen, in a window never shown, pressed by a ray.</summary>
    [AvaloniaFact]
    public void InTheHeadsetThePickerOpensOnTheHeadsetPanelAndOpensNoWindow()
    {
        var (settings, viewState, paths) = TestSurface.Create(
            voices: Voices(),
            audition: (_, _, _) => Task.CompletedTask);

        settings.Apply(InterfaceCapability.ShowEverySettingKey, "true", SettingsCaller.Panel);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var view = new SettingsView();
        view.Attach(settings, viewState, paths);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.Classes.Add("headset");
        panel.EnableSettings(() => view);
        panel.Tab = PanelTab.Settings;

        using var surface = new OffscreenSurface(panel, new PixelSize(Width, Height));

        surface.Render();
        Jobs();

        view.Reveal(SpeechCapability.Id);
        Jobs();
        surface.Render();

        var button = PickerButton(view, "Voice");
        // Scrolled to near the top of the page, so the ray lands on it wherever the groups above push it.
        var scroller = view.GetControl<ScrollViewer>("Scroller");
        var top = button.TranslatePoint(default, (Visual)scroller.Content!)!.Value.Y;
        scroller.Offset = new Vector(0, Math.Max(0, top - 40));
        Jobs();
        surface.Render();

        var centre = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), panel)!.Value;

        var windows = (Application.Current!.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Windows.Count;

        Assert.True(surface.Click(centre), "the ray's press on the Voice row was refused.");
        Jobs();
        surface.Render();
        Jobs();

        var page = Page(panel);

        Assert.NotNull(page);
        Assert.True(panel.Nav.Modal);
        Assert.Equal(windows, (Application.Current!.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Windows.Count);

        Assert.True(RowsInView(page!) >= 8, $"only {RowsInView(page!)} voices are visible in the headset.");
        AssertFacetIsWhole(page!);

        // A ray on a row highlights it; Use this takes it.
        var row = page!.GetControl<ListBox>("Choices").GetVisualDescendants().OfType<ListBoxItem>().ElementAt(2);
        var at = row.TranslatePoint(new Point(20, row.Bounds.Height / 2), panel)!.Value;

        surface.Click(at);
        Jobs();

        var accept = page.GetControl<Button>("AcceptButton");
        surface.Click(accept.TranslatePoint(new Point(accept.Bounds.Width / 2, accept.Bounds.Height / 2), panel)!.Value);
        Jobs();

        Assert.False(panel.Nav.Modal);
        Assert.Equal("voice-03", settings.Read(SpeechCapability.VoiceKey));
    }

    private static void Capture(Window window, string name)
    {
        Jobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, $"{name}.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }
}
