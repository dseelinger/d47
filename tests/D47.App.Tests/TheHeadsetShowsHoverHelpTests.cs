using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using Xunit;

namespace D47.App.Tests;

[Trait("Category", "Integration")]
public class TheHeadsetShowsHoverHelpTests
{
    private const string Docked =
        """{"timestamp":"2026-09-29T21:01:23Z","event":"Docked","StationName":"BNH-T2F","StationType":"FleetCarrier","Taxi":false,"Multicrew":false,"StarSystem":"LTT 7786","SystemAddress":633608311522,"MarketID":3715429376,"StationFaction":{"Name":"FleetCarrier"},"DistFromStarLS":213.28735,"LandingPads":{"Small":4,"Medium":4,"Large":8}}""";

    private static void Press(Control target)
    {
        var root = (Visual)target.FindAncestorOfType<Window>()!;
        var at = target.TranslatePoint(new Point(4, 4), root)!.Value;
        var pointer = new Pointer(47, PointerType.Mouse, isPrimary: true);

        target.RaiseEvent(new PointerPressedEventArgs(
            target,
            pointer,
            root,
            at,
            0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));

        pointer.Capture(null);
        Dispatcher.UIThread.RunJobs();
    }

    private static bool Shows(Control scope, string text) =>
        scope.GetVisualDescendants().OfType<TextBlock>()
            .Any(block => block.IsEffectivelyVisible && (block.Inlines?.Text ?? block.Text) == text);

    private static TextBlock[] LabelsWithHelp(Control scope) =>
        scope.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.Focusable && block.IsEffectivelyVisible && ToolTip.GetTip(block) is TextBlock { Text.Length: > 0 })
            .ToArray();

    private static string HelpOf(TextBlock label) => ((TextBlock)ToolTip.GetTip(label)!).Text!;

    private static SettingsHost Settings(bool headset)
    {
        var (settings, viewState, paths) = TestSurface.Create();

        return SettingsHost.Open(settings, viewState, paths, headset: headset);
    }

    [AvaloniaFact]
    public void PressingASettingsLabelOnTheHeadsetShowsItsHelpBelowAndAgainHidesIt()
    {
        var host = Settings(headset: true);
        var label = LabelsWithHelp(host.View).First();
        var help = HelpOf(label);

        Assert.False(Shows(host.View, help));

        Press(label);
        Assert.True(Shows(host.View, help));

        Press(label);
        Assert.False(Shows(host.View, help));

        host.Close();
    }

    [AvaloniaFact]
    public void OpeningASecondSettingsHelpLineClosesTheFirst()
    {
        var host = Settings(headset: true);
        var labels = LabelsWithHelp(host.View).DistinctBy(HelpOf).Take(2).ToArray();
        var (first, second) = (HelpOf(labels[0]), HelpOf(labels[1]));

        Press(labels[0]);
        Press(labels[1]);

        Assert.False(Shows(host.View, first));
        Assert.True(Shows(host.View, second));

        host.Close();
    }

    [AvaloniaFact]
    public void PressingASettingsLabelInTheWindowShowsNoHelpLine()
    {
        var host = Settings(headset: false);
        var label = LabelsWithHelp(host.View).First();

        Press(label);

        Assert.False(Shows(host.View, HelpOf(label)));

        host.Close();
    }

    [AvaloniaFact]
    public void ASettingsLabelOnTheHeadsetIsAsTallAsAFingerTarget()
    {
        var host = Settings(headset: true);

        Assert.True(LabelsWithHelp(host.View).First().Bounds.Height >= 44);

        host.Close();
    }

    private static JournalEntry Entry()
    {
        using var document = System.Text.Json.JsonDocument.Parse(Docked);
        var root = document.RootElement;
        var log = new JournalLog();

        log.Add(
            [new JournalEvent(
                DateTimeOffset.Parse(root.GetProperty("timestamp").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                "Docked",
                root.Clone())],
            []);

        return Assert.Single(log.Read(noise: true));
    }

    /// <summary>The pane inside a panel, as the Raw Journal page has it.</summary>
    private static (JournalReadingPane Pane, Window Window) Reading(bool headset)
    {
        var pane = new JournalReadingPane(_ => null, () => null);
        var view = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            view.Classes.Add("headset");
        }

        view.Content = new ScrollViewer { Content = pane };

        var window = new Window { Content = view, Width = 700, Height = 1000 };
        window.Show();

        pane.Show(Entry());
        Dispatcher.UIThread.RunJobs();

        return (pane, window);
    }

    private static TextBlock[] FieldNames(JournalReadingPane pane) =>
        pane.GetVisualDescendants().OfType<TextBlock>().Where(block => block.Cursor is not null).ToArray();

    private static Border[] OpenMeanings(JournalReadingPane pane) =>
        pane.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Width == 300 && border.IsEffectivelyVisible)
            .ToArray();

    [AvaloniaFact]
    public void PressingAJournalFieldNameOnTheHeadsetShowsItsMeaningBelowTheRowAndAgainHidesIt()
    {
        var (pane, window) = Reading(headset: true);
        var label = FieldNames(pane)[0];

        Assert.Empty(OpenMeanings(pane));

        Press(label);

        var open = Assert.Single(OpenMeanings(pane));
        var said = open.GetVisualDescendants().OfType<TextBlock>().Single().Inlines!.Text;

        Assert.Contains(" — ", said, StringComparison.Ordinal);
        Assert.True(open.TranslatePoint(default, pane)!.Value.Y > label.TranslatePoint(default, pane)!.Value.Y);

        Press(label);
        Assert.Empty(OpenMeanings(pane));

        window.Close();
    }

    [AvaloniaFact]
    public void OnlyOneJournalMeaningIsOpenAtATime()
    {
        var (pane, window) = Reading(headset: true);
        var labels = FieldNames(pane);

        Press(labels[0]);
        Press(labels[1]);

        Assert.Single(OpenMeanings(pane));

        window.Close();
    }

    [AvaloniaFact]
    public void PressingAJournalFieldNameInTheWindowOpensNothing()
    {
        var (pane, window) = Reading(headset: false);

        Press(FieldNames(pane)[0]);

        Assert.Empty(OpenMeanings(pane));

        window.Close();
    }

    [AvaloniaFact]
    public void AJournalFieldNameOnTheHeadsetIsAsTallAsAFingerTarget()
    {
        var (pane, window) = Reading(headset: true);

        Assert.True(FieldNames(pane)[0].Bounds.Height >= 44);

        window.Close();
    }

    [AvaloniaFact]
    public void TheHeadsetTranscriptHasAScrollAboveTheFoldTileThatDoesWhatTheMenuItemDoes()
    {
        var (settings, _, _) = TestSurface.Create();
        settings.Apply(VrCapability.ModeKey, "full", SettingsCaller.Panel);

        var model = new PanelViewModel();
        model.Append("Fuel at 12 percent.\n");

        using var panel = new VrPanelSurface(model, settings, _ => null, dumpTo: TestSurface.VrDump);

        var view = (PanelView)typeof(VrPanelSurface)
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(panel)!;

        Dispatcher.UIThread.RunJobs();

        var (width, height) = panel.Size;
        var buffer = new byte[width * height * 4];

        unsafe
        {
            fixed (byte* pixels = buffer)
            {
                panel.Draw((IntPtr)pixels, width * 4);
                panel.Draw((IntPtr)pixels, width * 4);
            }
        }

        Dispatcher.UIThread.RunJobs();

        var tile = view.FindControl<Button>("ScrollPastReadingButton")!;

        Assert.True(tile.IsVisible);
        Assert.True(tile.IsEnabled);
        Assert.True(tile.MinHeight >= 44);
        Assert.Equal("Scroll what is shown above the fold", tile.Content);
        Assert.False(view.ReadingIsAboveTheFold);

        tile.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.ReadingIsAboveTheFold);
    }

    [AvaloniaFact]
    public void TheWindowTranscriptHasNoScrollAboveTheFoldTile()
    {
        var view = new PanelView { DataContext = new PanelViewModel() };
        view.EnableSearch();

        var window = new Window { Content = view, Width = 900, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.False(view.FindControl<Button>("ScrollPastReadingButton")!.IsVisible);

        window.Close();
    }
}
