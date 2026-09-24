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

/// <summary>
/// Every segment row shares one look. The stepper and the dropdown tile are left out: each draws its own
/// 44px filled frame (#349, #439). The segment's height is left out too: it grows to fit its buttons,
/// wrapped lines included, rather than clipping them (#408).
/// </summary>
public class ChoiceControlsLookAlikeTests
{
    /// <summary>
    /// Every one of them, so the next row added cannot reintroduce this by being dressed in another place.
    /// </summary>
    [AvaloniaFact]
    public void EveryChoiceControlOnTheSurfaceIsDressedTheSame()
    {
        var host = Open();

        var all = host.View.GetVisualDescendants().OfType<Segment>()
            .Where(control => control.Bounds.Height > 0).Cast<TemplatedControl>().ToList();

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

    /// <summary>The dropdown tile stands as tall as a stepper, the other filled choice control.</summary>
    [AvaloniaFact]
    public void TheDropdownTileStandsAsTallAsAStepper()
    {
        var host = Open();

        var tile = Row(host, "Model").GetVisualDescendants().OfType<Button>().First(button => button.Name == "DropdownTile");

        Assert.Equal(TypeScale.MinimumTarget, tile.Bounds.Height);
    }

    /// <summary>
    /// What segments have to agree about at rest. Not fill, border colour or corner radius, which come from
    /// the theme (#289), and not height, which grows to fit wrapped labels (#408).
    /// </summary>
    private static IEnumerable<string> Differences(TemplatedControl a, TemplatedControl b)
    {
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
