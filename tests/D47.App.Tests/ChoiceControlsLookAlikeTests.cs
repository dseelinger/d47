using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Three implementations — segment, stepper, picker button (#274) — one look.</summary>
public class ChoiceControlsLookAlikeTests
{
    [AvaloniaFact]
    public void TheSegmentAndThePickerButtonAreDressedTheSame()
    {
        var host = Open();

        var segment = Row(host, "Provider").GetVisualDescendants().OfType<Segment>().First();
        var button = PickerButtons(Row(host, "Model")).First();

        foreach (var difference in Differences(segment, button))
        {
            Assert.Fail($"the Provider segment and the Model picker button differ: {difference}");
        }
    }

    /// <summary>
    /// Every one of them, so the next row added cannot reintroduce this by being dressed in a fourth
    /// place.
    /// </summary>
    [AvaloniaFact]
    public void EveryChoiceControlOnTheSurfaceIsDressedTheSame()
    {
        var host = Open();

        var segments = host.View.GetVisualDescendants().OfType<Segment>()
            .Where(control => control.Bounds.Height > 0).Cast<TemplatedControl>();

        var steppers = host.View.GetVisualDescendants().OfType<Stepper>()
            .Where(control => control.Bounds.Height > 0).Cast<TemplatedControl>();

        var buttons = PickerButtons(host.View).Where(control => control.Bounds.Height > 0);

        var all = segments.Concat(steppers).Concat(buttons).ToList();

        Assert.NotEmpty(all);

        var reference = all[0];

        foreach (var control in all.Skip(1))
        {
            foreach (var difference in Differences(reference, control))
            {
                Assert.Fail($"a choice control differs from the rest: {difference}");
            }
        }
    }

    /// <summary>
    /// What the two have to agree about: shape and size at rest. Not fill, border colour or
    /// corner radius — each keeps its own theme's for those, rather than DressAsAChoice forcing
    /// one across all three (#289).
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

        if (a.MinWidth != b.MinWidth)
        {
            yield return $"minimum width {a.MinWidth:0.#} against {b.MinWidth:0.#}";
        }
    }

    /// <summary>
    /// The buttons that open the picker, told from the ordinary ones — Store, Clear, CLEAR — by the
    /// chevron they carry, which is the same thing that tells the Commander.
    /// </summary>
    private static IEnumerable<Button> PickerButtons(Visual within) =>
        within.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Content is DockPanel panel
                && panel.Children.OfType<TextBlock>().Any(text => text.Text == "⌄"));

    private static SettingsHost Open()
    {
        var (settings, viewState, paths) = TestSurface.Create(
            voices: [new VoiceInfo("bill", "Bill - Wise, Mature, Balanced", "american", "male")]);

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        // Provider and Model live in Language model, The ship's AI's own area (#220).
        host.View.Reveal(D47.Core.Capabilities.Builtin.ConversationCapability.Id);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return host;
    }

    private static Grid Row(SettingsHost host, string label) =>
        host.View.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass))
            .First(grid => grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));
}
