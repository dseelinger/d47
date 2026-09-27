using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.Core.Interface;

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
/// A page, card or prompt title: Saira, at the caller's size and rank's ink. A name is upper case and
/// tracked; a sentence — a line such as a route summary or a prompt question — keeps its own case and
/// takes no tracking (#289).
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
    /// Applies the face, size, weight, tracking and ink to an existing block — for a title built as a
    /// subtype of <see cref="TextBlock"/>, or one whose text is set or changed separately. A
    /// <see cref="TitleRank.Group"/> heading never wraps; pair it with <see cref="GroupRow"/> for the rule.
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
            TitleRank.Window => FontWeight.Bold,
            TitleRank.Screen => FontWeight.Medium,
            TitleRank.Group => FontWeight.SemiBold,
            _ => FontWeight.Normal,
        };
        block.LetterSpacing = Tracking(size, rank, sentence);
        block.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ColourKey(rank)));

        if (rank == TitleRank.Group)
        {
            block.TextWrapping = TextWrapping.NoWrap;
        }

        return block;
    }

    /// <summary>The letter-spacing a title of this size, rank and case takes.</summary>
    public static double Tracking(double size, TitleRank rank, bool sentence = false) =>
        rank == TitleRank.Row || sentence ? 0
        : rank == TitleRank.Screen ? size * Fonts.TitleTracking
        : size * Fonts.ChromeTracking;

    /// <summary>A <see cref="TitleRank.Screen"/> title at <see cref="TypeScale.Title"/> as a title block.</summary>
    public static Control Screen(string text) => Block(Build(text, TypeScale.Title, TitleRank.Screen));

    /// <summary>
    /// A screen title block: an optional A context line above the title, an optional figure at the right, and
    /// a 1px A rule 10px under them.
    /// </summary>
    public static Control Block(TextBlock title, TextBlock? context = null, Control? figure = null)
    {
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Bottom };

        if (context is not null)
        {
            text.Children.Add(context);
        }

        text.Children.Add(title);

        var row = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 16,
            Children = { text },
        };

        if (figure is not null)
        {
            figure.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetColumn(figure, 1);
            row.Children.Add(figure);
        }

        return new StackPanel { Children = { row, Rule(new Thickness(0, 10, 0, 0)) } };
    }

    /// <summary>A title block's context line: 13px upper-case chrome in A, above the title.</summary>
    public static TextBlock Context(string text = "")
    {
        var block = new TextBlock
        {
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Small,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.Small * Fonts.ChromeTracking,
            TextWrapping = TextWrapping.Wrap,
            Text = text.ToUpperInvariant(),
        };
        block.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.AKey));
        return block;
    }

    /// <summary>A title block's figure: a Grey 12px label over a White 18px value, right-aligned.</summary>
    public static Control Figure(string label, string value)
    {
        var name = new TextBlock
        {
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Meta,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
            HorizontalAlignment = HorizontalAlignment.Right,
            Text = label.ToUpperInvariant(),
        };
        name.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.GreyKey));

        var figure = new TextBlock
        {
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Figure,
            FontWeight = FontWeight.Medium,
            HorizontalAlignment = HorizontalAlignment.Right,
            Text = value,
        };
        figure.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.WhiteKey));

        return new StackPanel { Spacing = 2, Children = { name, figure } };
    }

    /// <summary>Sets a title's text, upper-cased unless it is a sentence.</summary>
    public static void Show(TextBlock block, string text, bool sentence = false) =>
        block.Text = sentence ? text : text.ToUpperInvariant();

    /// <summary>The resource key a rank draws in.</summary>
    public static string ColourKey(TitleRank rank) => rank switch
    {
        TitleRank.Window => ThemeManager.WhiteKey,
        TitleRank.Screen => ThemeManager.WhiteKey,
        TitleRank.Group => ThemeManager.WhiteKey,
        TitleRank.Subgroup => ThemeManager.Grey2Key,
        TitleRank.Row => ThemeManager.WhiteKey,
        _ => throw new ArgumentOutOfRangeException(nameof(rank)),
    };

    /// <summary>Stacks a <see cref="TitleRank.Group"/> heading over a 1px accent rule (#357).</summary>
    public static Control GroupRow(Control heading) => new StackPanel
    {
        Children = { heading, Rule(new Thickness(0, 6, 0, 0)) },
    };

    private static Border Rule(Thickness margin)
    {
        var rule = new Border { Height = 1, Margin = margin };
        rule.Bind(Border.BackgroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.AKey));
        return rule;
    }
}
