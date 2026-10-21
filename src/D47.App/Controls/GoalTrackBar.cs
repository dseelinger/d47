using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Goals;

namespace D47.App.Controls;

/// <summary>
/// A goal's one segmented track: chapter labels above it, 2px between segments, and the current segment's name and
/// percent under it. Display-only; nothing on it depends on hover.
/// </summary>
public static class GoalTrackBar
{
    public const double RowHeight = 6;

    public const double OpenHeight = 10;

    private const double LabelStartLimit = 0.6;

    /// <summary>The percent of the whole that <paramref name="share"/> is, rounded; "&lt;1%" for a share that rounds to nothing.</summary>
    public static string Percent(double share)
    {
        var percent = (int)Math.Round(share * 100, MidpointRounding.AwayFromZero);

        return percent == 0 && share > 0
            ? "<1%"
            : percent.ToString(CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>The whole goal's percent, rounded down so only a finished goal reads 100%.</summary>
    public static string Whole(GoalTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);

        return ((int)Math.Floor(track.Whole * 100)).ToString(CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>The caption under the track, such as "ELITE IV · 56%", or null once the goal is done.</summary>
    public static string? Caption(GoalTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);

        if (track.IsDone)
        {
            return null;
        }

        var name = track.Segments[track.Current].Name.ToUpperInvariant();

        return track.Fill is { } fill
            ? name + " · " + ((int)Math.Floor(fill * 100)).ToString(CultureInfo.InvariantCulture) + "%"
            : name;
    }

    public static Control Build(GoalTrack track, double height)
    {
        ArgumentNullException.ThrowIfNull(track);

        var columns = Columns(track);
        var labels = new Grid { ColumnDefinitions = columns, ClipToBounds = true };
        var segments = new Grid { ColumnDefinitions = Columns(track), Height = height };
        var current = track.CurrentChapter;
        var drawn = new List<(TextBlock Label, int First, int Last, bool Always)>();

        foreach (var chapter in track.Chapters)
        {
            var label = new TextBlock
            {
                Text = $"{chapter.Label.ToUpperInvariant()} · {Percent(track.ShareOf(chapter))}",
                FontFamily = new FontFamily(Fonts.ChromeFamily),
                FontSize = TypeScale.MetaSmall,
                FontWeight = FontWeight.Medium,
                LetterSpacing = TypeScale.MetaSmall * Fonts.ChromeTracking,
                TextWrapping = TextWrapping.NoWrap,
                Margin = new Thickness(0, 0, 0, 3),
            };

            Themed(label, TextBlock.ForegroundProperty, chapter == current ? ThemeManager.CyanKey : ThemeManager.GreyKey);

            Grid.SetColumn(label, chapter.First * 2);
            Grid.SetColumnSpan(label, chapter.Count * 2 - 1);
            labels.Children.Add(label);
            drawn.Add((label, chapter.First * 2, (chapter.First + chapter.Count) * 2 - 2, chapter == current));
        }

        for (var index = 0; index < track.Segments.Count; index++)
        {
            var segment = Segment(track, index);
            Grid.SetColumn(segment, index * 2);
            segments.Children.Add(segment);
        }

        labels.SizeChanged += (_, _) => DropLabelsThatDoNotFit(labels, drawn);

        var root = new StackPanel { Spacing = 0 };
        root.Children.Add(labels);
        root.Children.Add(segments);

        if (Caption(track) is { } caption)
        {
            var line = new TextBlock
            {
                Text = caption,
                FontFamily = new FontFamily(Fonts.ChromeFamily),
                FontSize = TypeScale.Meta,
                FontWeight = FontWeight.Medium,
                LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
                Margin = new Thickness(0, 4, 0, 0),
                HorizontalAlignment = track.StartOf(track.Current) > LabelStartLimit ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            };

            Themed(line, TextBlock.ForegroundProperty, ThemeManager.CyanKey);
            root.Children.Add(line);
        }

        AutomationProperties.SetName(root, "Progress " + Whole(track) + (Caption(track) is { } said ? ", " + said : string.Empty));

        return root;
    }

    private static ColumnDefinitions Columns(GoalTrack track)
    {
        var columns = new ColumnDefinitions();

        for (var index = 0; index < track.Segments.Count; index++)
        {
            if (index > 0)
            {
                columns.Add(new ColumnDefinition(new GridLength(Gaps.Tile)));
            }

            columns.Add(new ColumnDefinition(new GridLength(track.Segments[index].Weight, GridUnitType.Star)));
        }

        return columns;
    }

    private static Control Segment(GoalTrack track, int index)
    {
        if (index < track.Current)
        {
            var passed = new Border();
            Themed(passed, Border.BackgroundProperty, ThemeManager.AKey);

            return passed;
        }

        var ground = new Border();
        Themed(ground, Border.BackgroundProperty, ThemeManager.BgKey);

        if (index > track.Current)
        {
            return ground;
        }

        var fill = Math.Clamp(track.Fill ?? 0, 0, 1);

        var inside = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(fill, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(2)),
                new ColumnDefinition(new GridLength(1 - fill, GridUnitType.Star)),
            },
        };

        var filled = new Border();
        Themed(filled, Border.BackgroundProperty, ThemeManager.AKey);

        var marker = new Border();
        Themed(marker, Border.BackgroundProperty, ThemeManager.CyanKey);
        Grid.SetColumn(marker, 1);

        inside.Children.Add(filled);
        inside.Children.Add(marker);
        ground.Child = inside;

        return ground;
    }

    private static void DropLabelsThatDoNotFit(Grid labels, List<(TextBlock Label, int First, int Last, bool Always)> drawn)
    {
        foreach (var (label, first, last, always) in drawn)
        {
            var width = 0.0;

            for (var column = first; column <= last; column++)
            {
                width += labels.ColumnDefinitions[column].ActualWidth;
            }

            label.Measure(Size.Infinity);
            label.IsVisible = always || label.DesiredSize.Width <= width;
        }
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
