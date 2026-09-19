using Avalonia;
using Avalonia.Controls;

namespace D47.App.Theming;

/// <summary>
/// A page, card or prompt title: Saira Semi Condensed, Text ink, at the caller's size. A name is
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
    /// subtype of <see cref="TextBlock"/>, or one whose text is set or changed separately.
    /// </summary>
    public static TBlock Style<TBlock>(TBlock block, double size, bool sentence = false)
        where TBlock : TextBlock
    {
        block.FontFamily = Fonts.LabelFamily;
        block.FontSize = size;
        block.LetterSpacing = sentence ? 0 : 1;
        block.Bind(TextBlock.ForegroundProperty, Application.Current!.Resources.GetResourceObservable(ThemeManager.TextKey));
        return block;
    }

    /// <summary>Sets a title's text, upper-cased unless it is a sentence.</summary>
    public static void Show(TextBlock block, string text, bool sentence = false) =>
        block.Text = sentence ? text : text.ToUpperInvariant();
}
