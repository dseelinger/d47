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
        block.LetterSpacing = rank == TitleRank.Row || sentence ? 0 : size * Fonts.ChromeTracking;
        block.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ColourKey(rank)));

        if (rank == TitleRank.Group)
        {
            block.TextWrapping = TextWrapping.NoWrap;
        }

        return block;
    }

    /// <summary>A <see cref="TitleRank.Screen"/> title at <see cref="TypeScale.Title"/> over a 1px accent rule.</summary>
    public static Control Screen(string text) => new StackPanel
    {
        Children = { Build(text, TypeScale.Title, TitleRank.Screen), Rule(new Thickness(0, 6, 0, 0)) },
    };

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
        Children = { heading, Rule(new Thickness(0, 4, 0, 0)) },
    };

    private static Border Rule(Thickness margin)
    {
        var rule = new Border { Height = 1, Margin = margin };
        rule.Bind(Border.BackgroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.AKey));
        return rule;
    }
}
