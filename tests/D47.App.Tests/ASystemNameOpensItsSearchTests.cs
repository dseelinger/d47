using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

[Trait("Category", "Integration")]
public class ASystemNameOpensItsSearchTests
{
    private static Task<bool> Copied(string value) => Task.FromResult(true);

    private static void Hold(Control control, string pseudo, bool on) => ((IPseudoClasses)control.Classes).Set(pseudo, on);

    private static Button Link(Visual root) =>
        root.GetVisualDescendants().OfType<Button>().First(button => Avalonia.Automation.AutomationProperties.GetName(button) == SystemLink.AutomationName);

    private static string? Colour(IBrush? brush) => (brush as ISolidColorBrush)?.Color.ToString();

    private static string? Key(string key) => Colour(Application.Current!.FindResource(key) as IBrush);

    private static Border Face(Button link) => (Border)link.Content!;

    private static (Window Window, StackPanel Host, Button Link, List<string> Opened) Shown(Func<Control> build, bool open = true)
    {
        new ThemeManager(Application.Current!, Microsoft.Extensions.Logging.Abstractions.NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Dark);

        var opened = new List<string>();
        var host = new StackPanel { Children = { build() } };

        if (open)
        {
            SystemLink.SetOpen(host, opened.Add);
        }

        var window = new Window { Content = host, Width = 600, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, host, Link(host), opened);
    }

    private static Control Beside(string system = "Deciat") =>
        CopyGlyph.LinkBeside(Typeset.Chrome(system, TypeScale.ControlLarge, ThemeManager.AKey), system, Copied);

    [AvaloniaFact]
    public void PressingALinkedNameOpensItsSystemAndIsHandled()
    {
        var (window, _, link, opened) = Shown(() => Beside());
        var press = new RoutedEventArgs(Button.ClickEvent);

        link.RaiseEvent(press);

        Assert.Equal(["Deciat"], opened);
        Assert.True(press.Handled);

        window.Close();
    }

    [AvaloniaFact]
    public void PressingTheCopyGlyphBesideALinkedNameStillCopies()
    {
        var copies = new List<string>();
        var (window, host, _, opened) = Shown(() =>
            CopyGlyph.LinkBeside(Typeset.Chrome("Deciat", TypeScale.ControlLarge, ThemeManager.AKey), "Deciat", value =>
            {
                copies.Add(value);
                return Task.FromResult(true);
            }));

        var glyph = host.GetVisualDescendants().OfType<Button>().Single(button => CopyGlyph.GetCopies(button) == "Deciat");

        glyph.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Deciat"], copies);
        Assert.Empty(opened);

        window.Close();
    }

    [AvaloniaFact]
    public void ALinkOnASurfaceWithNoSearchIsPlainText()
    {
        var (window, _, link, _) = Shown(() => Beside(), open: false);

        Assert.False(link.IsHitTestVisible);
        Assert.Equal(new Thickness(0), Face(link).Padding);
        Assert.Null(((TextBlock)Face(link).Child!).TextDecorations);

        window.Close();
    }

    [AvaloniaFact]
    public void ALinkIsA44PixelFaceWithEightPixelsEitherSideOfTheName()
    {
        var (window, _, link, _) = Shown(() => Beside());

        Assert.Equal(TypeScale.MinimumTarget, Face(link).Bounds.Height);
        Assert.Equal(new Thickness(8, 0), Face(link).Padding);

        window.Close();
    }

    [AvaloniaFact]
    public void ALinkIsUnderlinedAtHalfInkAtRestAndInFullInkOnHoverAndFocus()
    {
        var (window, _, link, _) = Shown(() => Beside());
        var name = (TextBlock)Face(link).Child!;
        var underline = Assert.Single(name.TextDecorations!);

        Assert.Equal(1, underline.StrokeThickness);
        Assert.Equal(4, underline.StrokeOffset);
        Assert.Equal(0.55, ((ISolidColorBrush)underline.Stroke!).Opacity, 2);

        Hold(link, ":pointerover", true);
        Assert.Equal(1, ((ISolidColorBrush)name.TextDecorations![0].Stroke!).Opacity);
        Assert.Equal(Key(ThemeManager.Tile2Key), Colour(Face(link).Background));

        Hold(link, ":pointerover", false);
        Hold(link, ":focus-visible", true);
        Assert.Equal(Key(ThemeManager.Tile2Key), Colour(Face(link).Background));

        window.Close();
    }

    [AvaloniaFact]
    public void APressedLinkIsAFaceWithKnockInkAndUnderline()
    {
        var (window, _, link, _) = Shown(() => Beside());
        var name = (TextBlock)Face(link).Child!;
        var ink = Colour(name.Foreground);

        Hold(link, ":pressed", true);

        Assert.Equal(Key(ThemeManager.AKey), Colour(Face(link).Background));
        Assert.Equal(Key(ThemeManager.KnockKey), Colour(name.Foreground));
        Assert.Equal(Key(ThemeManager.KnockKey), Colour(name.TextDecorations![0].Stroke));

        Hold(link, ":pressed", false);

        Assert.Equal(ink, Colour(name.Foreground));

        window.Close();
    }

    [AvaloniaFact]
    public void APressableRowStaysAtRestWhileThePointerIsOnTheLinkInsideIt()
    {
        var pressed = 0;
        var row = new Button { Content = Beside() };
        row.Click += (_, _) => pressed++;

        var (window, _, link, opened) = Shown(() => row);

        Hold(link, ":pointerover", true);
        Assert.Contains(SystemLink.RowHeld, row.Classes);

        Hold(link, ":pointerover", false);
        Assert.DoesNotContain(SystemLink.RowHeld, row.Classes);

        link.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(["Deciat"], opened);
        Assert.Equal(0, pressed);

        window.Close();
    }

    [AvaloniaFact]
    public void ASearchRowsNameLooksLikeALinkAndGoesKnockWhenTheRowIsPressed()
    {
        new ThemeManager(Application.Current!, Microsoft.Extensions.Logging.Abstractions.NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Dark);

        var name = SystemLink.Look(Typeset.Chrome("Lave", TypeScale.Secondary, ThemeManager.AKey));
        var row = new Button { Content = name };
        var window = new Window { Content = row, Width = 400, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0.55, ((ISolidColorBrush)name.TextDecorations![0].Stroke!).Opacity, 2);

        Hold(row, ":pressed", true);

        Assert.Equal(Key(ThemeManager.KnockKey), Colour(name.Foreground));
        Assert.Equal(Key(ThemeManager.KnockKey), Colour(name.TextDecorations[0].Stroke));

        window.Close();
    }

    [AvaloniaFact]
    public void ANameInASentenceBecomesALinkAndTheRestStaysText()
    {
        var line = new TextBlock { Text = "Meet them in Deciat before dawn." };
        var named = SystemLink.InSentence(line, "Deciat", part => new TextBlock { Text = part });

        Assert.True(named);
        Assert.Null(line.Text);
        Assert.Equal(3, line.Inlines!.Count);

        Assert.False(SystemLink.InSentence(new TextBlock { Text = "Nowhere to go." }, "Deciat", part => new TextBlock { Text = part }));
    }

    /// <summary>
    /// A link in a sentence shares the sentence's baseline and leaves the line its own height; its 44px face
    /// reaches above and below the line, and is drawn and pressed there.
    /// </summary>
    [AvaloniaFact]
    public void ALinkInASentenceSitsOnTheSentencesBaseline()
    {
        var prose = new FontFamily(Fonts.ProseFamily);
        var plain = new TextBlock { Text = "Meet them in Deciat before dawn.", FontFamily = prose, FontSize = TypeScale.Secondary };
        var line = new TextBlock { Text = "Meet them in Deciat before dawn.", FontFamily = prose, FontSize = TypeScale.Secondary };
        SystemLink.InSentence(line, "Deciat", part => new TextBlock { Text = part, FontFamily = prose, FontSize = TypeScale.Secondary });

        var (window, _, link, _) = Shown(() => new StackPanel { Spacing = 20, Margin = new Thickness(0, 40), Children = { plain, line } });

        var name = (TextBlock)Face(link).Child!;
        var nameBaseline = name.TranslatePoint(new Point(0, name.TextLayout.TextLines[0].Baseline), line)!.Value.Y;

        Assert.Equal(line.TextLayout.TextLines[0].Baseline, nameBaseline, 0.5);
        Assert.Equal(plain.Bounds.Height, line.Bounds.Height, 0.5);
        Assert.Equal(TypeScale.MinimumTarget, Face(link).Bounds.Height);

        var aboveTheLine = link.TranslatePoint(new Point(4, 2), window)!.Value;
        Assert.True(aboveTheLine.Y < line.TranslatePoint(default, window)!.Value.Y);

        var hit = Avalonia.Input.InputExtensions.InputHitTest(window, aboveTheLine) as Visual;
        Assert.True(hit == link || (hit?.GetVisualAncestors().Contains(link) ?? false));

        window.Close();
    }

    [AvaloniaFact]
    public void ThePanelOpensSystemsThroughSearchSystem()
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        Assert.NotNull(SystemLink.GetOpen(panel));
    }

    [AvaloniaFact]
    public void ALinkOnTheEngineersTabOpensTheEngineersSystem()
    {
        var surface = EngineersTabTests.Open(clipboard: new D47.Core.Capabilities.Builtin.RecordingClipboard());
        var opened = new List<string>();
        SystemLink.SetOpen(surface.Panel, opened.Add);

        surface.Panel.GetVisualDescendants().OfType<Button>()
            .First(button => button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "FELICITY FARSEER"))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var link = surface.Panel.GetVisualDescendants().OfType<Button>()
            .First(button => Avalonia.Automation.AutomationProperties.GetName(button) == SystemLink.AutomationName
                && button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Deciat"));

        link.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(["Deciat"], opened);

        surface.Window.Close();
    }
}
