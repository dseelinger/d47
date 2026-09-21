using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace D47.App.Theming;

/// <summary>A title's place in the type hierarchy, each with its own ink (#357).</summary>
public enum TitleRank
{
    /// <summary>The app window's own title, in the caption strip.</summary>
    Window,

    /// <summary>A screen's own title.</summary>
    Screen,

    /// <summary>A heading that groups the cards or rows beneath it; drawn nowrap with a trailing rule.</summary>
    Group,

    /// <summary>A card or dialog's second-rank heading.</summary>
    Subgroup,

    /// <summary>An ordinary row label.</summary>
    Row,
}

/// <summary>
/// A page, card or prompt title: Saira Condensed, at the caller's size and rank's ink. A name is
/// upper case and tracked; a sentence — a line such as a route summary or a prompt question —
/// keeps its own case and spacing (#289).
/// </summary>
public static class TitleText
{
    /// <summary>Builds a title <see cref="TextBlock"/> with its text already set.</summary>
    public static TextBlock Build(string text, double size, TitleRank rank, bool sentence = false)
    {
        var block = new TextBlock();
        Style(block, size, rank, sentence);
        Show(block, text, sentence);
        return block;
    }

    /// <summary>
    /// Applies the face, size, tracking and ink to an existing block — for a title built as a
    /// subtype of <see cref="TextBlock"/>, or one whose text is set or changed separately. Tracking
    /// follows the type scale's screen-title and group-heading rates; any other size gets a flat
    /// rate of 1. A <see cref="TitleRank.Group"/> heading never wraps; pair it with <see cref="GroupRow"/>
    /// for the trailing rule.
    /// </summary>
    public static TBlock Style<TBlock>(TBlock block, double size, TitleRank rank, bool sentence = false)
        where TBlock : TextBlock
    {
        block.FontFamily = rank switch
        {
            TitleRank.Subgroup => Fonts.MonoFamily,
            TitleRank.Row => Fonts.ProseFamily,
            _ => Fonts.ChromeFamily,
        };
        block.FontSize = size;
        block.FontWeight = rank switch
        {
            TitleRank.Subgroup => FontWeight.Normal,
            TitleRank.Row => FontWeight.Normal,
            _ => size == TypeScale.Title ? FontWeight.Bold : FontWeight.SemiBold,
        };
        block.LetterSpacing = rank switch
        {
            TitleRank.Subgroup => size * (1.2 / TypeScale.Caption),
            TitleRank.Row => 0,
            _ => sentence
                ? 0
                : size switch
                {
                    TypeScale.Title => size * 0.07,
                    TypeScale.Heading => size * 0.15,
                    _ => 1,
                },
        };
        block.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ColourKey(rank)));

        if (rank == TitleRank.Group)
        {
            block.TextWrapping = TextWrapping.NoWrap;
        }

        if (rank == TitleRank.Screen)
        {
            block.Bind(Visual.EffectProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.TitleBloomKey));
        }

        return block;
    }

    /// <summary>Sets a title's text, upper-cased unless it is a sentence.</summary>
    public static void Show(TextBlock block, string text, bool sentence = false) =>
        block.Text = sentence ? text : text.ToUpperInvariant();

    /// <summary>The resource key a rank draws in.</summary>
    public static string ColourKey(TitleRank rank) => rank switch
    {
        TitleRank.Window => ThemeManager.AccentInkKey,
        TitleRank.Screen => ThemeManager.AccentInkKey,
        TitleRank.Group => ThemeManager.TextKey,
        TitleRank.Subgroup => ThemeManager.TextFaintKey,
        TitleRank.Row => ThemeManager.TextKey,
        _ => throw new ArgumentOutOfRangeException(nameof(rank)),
    };

    /// <summary>
    /// Wraps a <see cref="TitleRank.Group"/> heading with a 1px rule filling the rest of the row (#357).
    /// </summary>
    public static Control GroupRow(TextBlock heading)
    {
        var rule = new Border { Height = 1, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        rule.Bind(Border.BackgroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.BorderKey));

        var row = new DockPanel();
        DockPanel.SetDock(heading, Dock.Left);
        row.Children.Add(heading);
        row.Children.Add(rule);
        return row;
    }
}
