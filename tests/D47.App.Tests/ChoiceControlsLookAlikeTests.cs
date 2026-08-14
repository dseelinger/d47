using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Two implementations, one look (list.md Phase 12).
/// <para>
/// A row that opens a short list uses a combo box and a row that opens a long one uses a button
/// that opens the picker, and the Commander is not supposed to be able to tell. They could:
/// reported from a screenshot of the Provider row above the Model row, one bright and one dim.
/// The combo carried Fluent's own fill and a 60%-white border, the button carried d47's surface
/// and a border two thirds darker, and they stood a pixel apart in height — because each was
/// dressed in its own place and a resemblance kept by hand drifts.
/// </para>
/// </summary>
public class ChoiceControlsLookAlikeTests
{
    [AvaloniaFact]
    public void TheComboBoxAndThePickerButtonAreDressedTheSame()
    {
        var window = Open();

        var combo = Row(window, "Provider").GetVisualDescendants().OfType<ComboBox>().First();
        var button = PickerButtons(Row(window, "Model")).First();

        foreach (var difference in Differences(combo, button))
        {
            Assert.Fail($"the Provider combo box and the Model picker button differ: {difference}");
        }
    }

    /// <summary>
    /// Every one of them, so the next row added cannot reintroduce this by being dressed in a
    /// third place. Controls are only comparable once laid out; a collapsed section has none.
    /// </summary>
    [AvaloniaFact]
    public void EveryChoiceControlOnTheSurfaceIsDressedTheSame()
    {
        var window = Open();

        var combos = window.GetVisualDescendants().OfType<ComboBox>()
            .Where(control => control.Bounds.Height > 0).ToList();

        var buttons = PickerButtons(window).Where(control => control.Bounds.Height > 0).ToList();

        Assert.NotEmpty(combos);
        Assert.NotEmpty(buttons);

        var reference = combos[0];

        foreach (var control in combos.Skip(1).Cast<TemplatedControl>().Concat(buttons))
        {
            foreach (var difference in Differences(reference, control))
            {
                Assert.Fail($"a choice control differs from the rest: {difference}");
            }
        }
    }

    /// <summary>
    /// What the two have to agree about: everything a Commander sees at rest. Not the width,
    /// which follows the value and is capped at the column by
    /// <see cref="VoiceRowFitsItsColumnTests"/>, and not the chevron, which one draws from its
    /// template and the other writes as a character.
    /// </summary>
    private static IEnumerable<string> Differences(TemplatedControl a, TemplatedControl b)
    {
        if (a.Bounds.Height != b.Bounds.Height)
        {
            yield return $"height {a.Bounds.Height:0.#} against {b.Bounds.Height:0.#}";
        }

        if (a.FontSize != b.FontSize)
        {
            yield return $"font size {a.FontSize:0.#} against {b.FontSize:0.#}";
        }

        if (a.Padding != b.Padding)
        {
            yield return $"padding {a.Padding} against {b.Padding}";
        }

        if (a.BorderThickness != b.BorderThickness)
        {
            yield return $"border thickness {a.BorderThickness} against {b.BorderThickness}";
        }

        if (a.CornerRadius != b.CornerRadius)
        {
            yield return $"corner radius {a.CornerRadius} against {b.CornerRadius}";
        }

        if (Colour(a.Background) != Colour(b.Background))
        {
            yield return $"fill {Colour(a.Background)} against {Colour(b.Background)}";
        }

        if (Colour(a.BorderBrush) != Colour(b.BorderBrush))
        {
            yield return $"border {Colour(a.BorderBrush)} against {Colour(b.BorderBrush)}";
        }

        if (a.MinWidth != b.MinWidth)
        {
            yield return $"minimum width {a.MinWidth:0.#} against {b.MinWidth:0.#}";
        }
    }

    private static Color? Colour(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    /// <summary>
    /// The buttons that open the picker, told from the ordinary ones — Store, Clear, Unbind —
    /// by the chevron they carry, which is the same thing that tells the Commander.
    /// </summary>
    private static IEnumerable<Button> PickerButtons(Visual within) =>
        within.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Content is DockPanel panel
                && panel.Children.OfType<TextBlock>().Any(text => text.Text == "⌄"));

    private static SettingsWindow Open()
    {
        var (settings, viewState, paths) = TestSurface.Create(
            voices: [new VoiceInfo("bill", "Bill - Wise, Mature, Balanced", "american", "male")]);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var window = new SettingsWindow();
        window.Attach(settings, viewState, paths);
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static Grid Row(SettingsWindow window, string label) =>
        window.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.ColumnDefinitions.Count == 3 && grid.ColumnDefinitions[1].Width.IsAbsolute)
            .First(grid => grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));
}
