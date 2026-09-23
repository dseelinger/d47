using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Interface;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Path = Avalonia.Controls.Shapes.Path;

namespace D47.App.Tests;

/// <summary>
/// Every element #377's tier table names glows through one ghost per stop of its tier, each ghost
/// carrying one stop's effect, and Dark and Light draw none of them.
/// </summary>
public class EveryGlowIsAStackOfGhostsTests
{
    private static ThemeManager Manager() =>
        new(Application.Current!, NullLogger<ThemeManager>.Instance);

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.ElitePaletteId)]
    public void EveryGlowingElementHasOneLitGhostPerStop(string themeId)
    {
        using var kit = ControlKitTheme();
        Manager().Apply(themeId);

        using var scene = Scene.Open();

        foreach (var (name, tier, stack) in scene.Glows())
        {
            Assert.True(stack.IsLit, $"{name} is not lit");
            Assert.Equal(BloomTiers.Table(tier).Count, stack.Ghosts.Count);
            Assert.Equal(tier, stack.Tier);

            foreach (var ghost in stack.Ghosts)
            {
                var effect = Assert.IsType<DropShadowEffect>(ghost.Effect);
                Assert.InRange(effect.Opacity, 0, 1);
                Assert.True(ghost.IsVisible, $"{name} has a hidden ghost");
                Assert.False(ghost.IsHitTestVisible, $"{name} has a ghost that takes a hit test");
            }
        }
    }

    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Dark)]
    [InlineData(ThemeCatalog.Light)]
    public void DarkAndLightDrawNoGhost(string themeId)
    {
        using var kit = ControlKitTheme();
        Manager().Apply(themeId);

        using var scene = Scene.Open();

        foreach (var (name, _, stack) in scene.Glows())
        {
            Assert.All(stack.Ghosts, ghost => Assert.False(ghost.IsVisible, $"{name} draws a ghost in {themeId}"));
        }
    }

    /// <summary>A cyan HUD matrix, applied the way the Control Kit's Accent entry applies one.</summary>
    [AvaloniaFact]
    public void ACyanHudMatrixMakesEveryAccentHaloCyan()
    {
        using var kit = ControlKitTheme();
        Manager().Apply(ThemeCatalog.ElitePaletteId, new GuiColourMatrix(0, 0, 0, 1, 0, 0, 1, 0, 0));

        var accent = ((ISolidColorBrush)Application.Current!.Resources[ThemeManager.AccentKey]!).Color;
        Assert.True(accent.R < accent.G && accent.R < accent.B, $"{accent} is not cyan");

        using var scene = Scene.Open();

        foreach (var (name, _, stack) in scene.Glows().Where(glow => glow.Name != "microphone dot"))
        {
            Assert.All(stack.Ghosts, ghost => Assert.Equal(accent, ((DropShadowEffect)ghost.Effect!).Color));
        }
    }

    /// <summary>The dot glows in its own fill colour, and the row around it does not glow as a box.</summary>
    [AvaloniaFact]
    public void TheMicrophoneDotGlowsInItsOwnColourAndTheRowDoesNot()
    {
        using var kit = ControlKitTheme();
        Manager().Apply(ThemeCatalog.Elite);

        using var scene = Scene.Open();
        scene.Model.Microphone = MicrophoneState.Armed;
        Dispatcher.UIThread.RunJobs();

        var warn = ((ISolidColorBrush)Application.Current!.Resources[ThemeManager.WarnKey]!).Color;
        var dot = scene.Panel.FindControl<BloomStack>("MicrophoneBloom")!;

        Assert.All(dot.Ghosts, ghost => Assert.Equal(warn, ((DropShadowEffect)ghost.Effect!).Color));
        Assert.Null(scene.Panel.FindControl<Border>("MicrophoneRow")!.Effect);
    }

    /// <summary>A hollow dot — the microphone off — does not glow.</summary>
    [AvaloniaFact]
    public void AHollowDotDoesNotGlow()
    {
        using var kit = ControlKitTheme();
        Manager().Apply(ThemeCatalog.Elite);

        using var scene = Scene.Open();
        scene.Model.Microphone = MicrophoneState.Off;
        Dispatcher.UIThread.RunJobs();

        var dot = scene.Panel.FindControl<BloomStack>("MicrophoneBloom")!;

        Assert.All(dot.Ghosts, ghost => Assert.False(ghost.IsVisible));
    }

    /// <summary>No element sets its own Effect to a glow; only a bloom stack's ghosts carry one.</summary>
    [AvaloniaFact]
    public void OnlyGhostsCarryAGlow()
    {
        using var kit = ControlKitTheme();
        Manager().Apply(ThemeCatalog.Elite);

        using var scene = Scene.Open();

        foreach (var window in scene.Windows)
        {
            var ghosts = window.GetVisualDescendants().OfType<BloomStack>().SelectMany(stack => stack.Ghosts).ToHashSet();

            var glowing = window.GetVisualDescendants().OfType<Visual>()
                .Where(visual => visual.Effect is DropShadowEffect && !ghosts.Contains(visual))
                .Select(visual => visual.GetType().Name + " " + (visual as Control)?.Name)
                .ToList();

            Assert.Empty(glowing);
        }
    }

    /// <summary>Merges ControlKitTheme.axaml onto Application.Current, removed again on Dispose.</summary>
    private static IDisposable ControlKitTheme()
    {
        var include = new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
        };

        Application.Current!.Styles.Add(include);

        return new Removal(include);
    }

    private sealed class Removal(IStyle style) : IDisposable
    {
        public void Dispose() => Application.Current!.Styles.Remove(style);
    }

    /// <summary>The panel, a captioned window, and one of each kit control in its glowing state.</summary>
    private sealed class Scene : IDisposable
    {
        private Scene(PanelView panel, PanelViewModel model, Window panelWindow, Window kitWindow, Controls kit)
        {
            Panel = panel;
            Model = model;
            Windows = [panelWindow, kitWindow];
            Kit = kit;
        }

        public PanelView Panel { get; }

        public PanelViewModel Model { get; }

        public IReadOnlyList<Window> Windows { get; }

        private Controls Kit { get; }

        public static Scene Open()
        {
            var model = new PanelViewModel { Microphone = MicrophoneState.Idle };
            var panel = new PanelView { DataContext = model };
            var panelWindow = new Window { Content = panel, Width = 1180, Height = 880 };
            panelWindow.Show();

            var glyph = new Button
            {
                Theme = (ControlTheme)Application.Current!.FindResource("D47.GlyphButton")!,
                Content = "↺",
                Width = 44,
                Height = 44,
            };

            var kit = new Controls(
                TitleText.Screen("Screen title"),
                new Button { Content = "Primary", Classes = { "primary" } },
                new ToggleSwitch { IsChecked = true },
                glyph,
                new Slider { Minimum = 0, Maximum = 100, Value = 50, Width = 300 });

            var kitWindow = new Window
            {
                Title = "Directive 47 — 0.1.0",
                Width = 800,
                Height = 600,
                Content = new StackPanel { Children = { kit.Title, kit.Primary, kit.Switch, kit.Glyph, kit.Level } },
            };
            CaptionStrip.Apply(kitWindow);
            kitWindow.Show();

            Dispatcher.UIThread.RunJobs();

            // Held down, not released.
            var centre = glyph.TranslatePoint(new Point(22, 22), kitWindow)!.Value;
            kitWindow.MouseDown(centre, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            return new Scene(panel, model, panelWindow, kitWindow, kit);
        }

        /// <summary>Every glow the tier table names, by the element it belongs to.</summary>
        public IEnumerable<(string Name, BloomTier Tier, BloomStack Stack)> Glows()
        {
            var caption = Windows[1].GetVisualDescendants().OfType<BloomStack>()
                .Where(stack => stack.Child is Path or TextBlock { Text: "DIRECTIVE 47" })
                .ToList();

            yield return ("caption diamond", BloomTier.High, caption.Single(stack => stack.Child is Path));
            yield return ("caption name", BloomTier.High, caption.Single(stack => stack.Child is TextBlock));
            yield return ("screen title", BloomTier.High, (BloomStack)Kit.Title);
            yield return ("level fill", BloomTier.High, Within(Kit.Level, stack => stack.Child is Border));
            yield return ("level handle", BloomTier.High, Within(Kit.Level, stack => stack.Child is Rectangle));
            yield return ("microphone dot", BloomTier.High, Panel.FindControl<BloomStack>("MicrophoneBloom")!);
            yield return ("active tab", BloomTier.Normal, Within(Panel.FindControl<RadioButton>("TranscriptTab")!, stack => stack.Name == "Glow"));
            yield return ("primary button", BloomTier.Normal, Within(Kit.Primary, stack => stack.Name == "Glow"));
            yield return ("lit switch half", BloomTier.Normal, Within(Kit.Switch));
            yield return ("pressed glyph button", BloomTier.Normal, Within(Kit.Glyph, stack => stack.Name == "Glow"));
        }

        public void Dispose()
        {
            foreach (var window in Windows)
            {
                window.Close();
            }
        }

        private static BloomStack Within(Control control, Func<BloomStack, bool>? which = null) =>
            control.GetVisualDescendants().OfType<BloomStack>().Single(stack => which?.Invoke(stack) ?? true);
    }

    private sealed record Controls(
        Control Title, Button Primary, ToggleSwitch Switch, Button Glyph, Slider Level);
}
