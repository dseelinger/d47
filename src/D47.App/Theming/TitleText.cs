using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace D47.App.Theming;

/// <summary>
/// A page, card or prompt title: Saira Condensed, Text ink, at the caller's size. A name is
/// upper case and tracked; a sentence — a line such as a route summary or a prompt question —
/// keeps its own case and spacing (#289).
/// </summary>
public static class TitleText
{
    /// <summary>Builds a title <see cref="TextBlock"/> with its text already set.</summary>
    public static TextBlock Build(string text, double size, bool sentence = false)
    {
        var block = new TextBlock();
        Style(block, size, sentence);
        Show(block, text, sentence);
        return block;
    }

    /// <summary>
    /// Applies the face, size, tracking and ink to an existing block — for a title built as a
    /// subtype of <see cref="TextBlock"/>, or one whose text is set or changed separately. Tracking
    /// follows the type scale's screen-title and group-heading rates; any other size gets a flat
    /// rate of 1.
    /// </summary>
    public static TBlock Style<TBlock>(TBlock block, double size, bool sentence = false)
        where TBlock : TextBlock
    {
        block.FontFamily = Fonts.ChromeFamily;
        block.FontSize = size;
        block.FontWeight = size == TypeScale.Title ? FontWeight.Bold : FontWeight.SemiBold;
        block.LetterSpacing = sentence
            ? 0
            : size switch
            {
                TypeScale.Title => size * 0.07,
                TypeScale.Heading => size * 0.15,
                _ => 1,
            };
        block.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.TextKey));
        return block;
    }

    /// <summary>Sets a title's text, upper-cased unless it is a sentence.</summary>
    public static void Show(TextBlock block, string text, bool sentence = false) =>
        block.Text = sentence ? text : text.ToUpperInvariant();
}
