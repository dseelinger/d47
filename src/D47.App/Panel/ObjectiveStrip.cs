using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Adventures;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>What the strip says: the label, the objective sentence split around its system, and the story's name.</summary>
internal sealed record ObjectiveStripText(
    string Label, string Before, string? System, string? CopyValue, string? After, bool InSystem, bool Grey, string StoryName);

/// <summary>The next story objective on one line, under the title rule of every tab of the full panel.</summary>
internal sealed class ObjectiveStrip : UserControl
{
    private readonly AdventureSurface _surface;
    private readonly Func<Func<string, Task<bool>>?> _copy;
    private ObjectiveStripText? _shown;

    public ObjectiveStrip(AdventureSurface surface, Func<Func<string, Task<bool>>?> copy)
    {
        _surface = surface;
        _copy = copy;

        Height = TypeScale.MinimumTarget;
        Margin = new Thickness(0, 10, 0, 0);
        IsVisible = false;
        AutomationProperties.SetName(this, "Next story objective");
        AdventuresPage.Themed(this, BackgroundProperty, ThemeManager.SlabKey);

        Refresh();
    }

    /// <summary>The strip's words for <paramref name="shown"/>, or null when it has nothing to say.</summary>
    internal static ObjectiveStripText? Describe(ShownObjective? shown, string? here, bool rewriting)
    {
        if (shown is null)
        {
            return null;
        }

        var label = shown.Paused ? "PAUSED" : "NEXT";
        var name = shown.StoryName.ToUpperInvariant();

        if (rewriting)
        {
            return new ObjectiveStripText(label, "Writing a different objective…", null, null, null, false, true, name);
        }

        if (shown.Standing.NextTrigger() is not { } next)
        {
            return null;
        }

        var sentence = AdventuresPage.Sentence(next);
        sentence = sentence.EndsWith('.') ? sentence[..^1] : sentence;

        var system = AdventureNudge.Destination(shown.Standing);

        if (AdventuresPage.Naming(sentence, system) is not { } at)
        {
            return new ObjectiveStripText(label, sentence, null, null, null, false, shown.Paused, name);
        }

        var end = at + system!.Length;
        var after = end < sentence.Length ? sentence[end..] : null;

        return new ObjectiveStripText(
            label,
            sentence[..at].TrimEnd() + " ",
            sentence[at..end],
            system,
            after,
            string.Equals(system, here, StringComparison.OrdinalIgnoreCase),
            shown.Paused,
            name);
    }

    /// <summary>Redraws when the words changed; true when it did.</summary>
    public bool Refresh()
    {
        var commander = _surface.Commander();
        var rewriting = _surface.Stories?.IsRewriting(commander) == true;
        var text = Describe(_surface.Stories?.CurrentObjective(commander), _surface.State()?.Location?.StarSystem, rewriting);

        if (Equals(text, _shown))
        {
            return false;
        }

        _shown = text;
        IsVisible = text is not null;
        Content = text is null ? null : Build(text);

        return true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _surface.Book.Store.Changed += OnChanged;
        _surface.Book.StirringChanged += OnChanged;

        if (_surface.Switched is { } switched)
        {
            switched.Changed += OnChanged;
        }

        if (_surface.Stories is { } stories)
        {
            stories.Stories.Changed += OnChanged;
        }

        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _surface.Book.Store.Changed -= OnChanged;
        _surface.Book.StirringChanged -= OnChanged;

        if (_surface.Switched is { } switched)
        {
            switched.Changed -= OnChanged;
        }

        if (_surface.Stories is { } stories)
        {
            stories.Stories.Changed -= OnChanged;
        }
    }

    private void OnChanged() => Dispatcher.UIThread.Post(() => Refresh());

    internal Control Build(ObjectiveStripText text)
    {
        var label = Chrome(text.Label, FontWeight.SemiBold, ThemeManager.GreyKey);
        var ink = text.Grey ? ThemeManager.GreyKey : ThemeManager.AKey;

        var sentence = Prose(text.Before, ink);
        sentence.TextTrimming = TextTrimming.CharacterEllipsis;

        Control? system = null;
        Control? glyph = null;
        Control? after = null;

        if (text.System is not null)
        {
            system = Prose(text.System, text.InSystem && !text.Grey ? ThemeManager.CyanKey : ink);

            if (text.CopyValue is not null && _copy() is { } copy)
            {
                glyph = CopyGlyph.For(text.CopyValue, copy);
            }
        }

        if (text.After is not null)
        {
            after = Prose(text.After, ink);
        }

        return new StripPanel(label, sentence, system, glyph, after, Chrome(text.StoryName, FontWeight.Medium, ThemeManager.GreyKey, trim: true));
    }

    private static TextBlock Prose(string text, string key)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = TypeScale.Description,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        AdventuresPage.Themed(block, TextBlock.ForegroundProperty, key);

        return block;
    }

    private static TextBlock Chrome(string text, FontWeight weight, string key, bool trim = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Meta,
            FontWeight = weight,
            LetterSpacing = TypeScale.Meta * 0.08,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = trim ? TextTrimming.CharacterEllipsis : TextTrimming.None,
            VerticalAlignment = VerticalAlignment.Center,
        };

        AdventuresPage.Themed(block, TextBlock.ForegroundProperty, key);

        return block;
    }

    /// <summary>
    /// One line: label, sentence, system, glyph, then the story's name at the right. When the line is too long the sentence
    /// shortens first, to no less than <see cref="LeastSentence"/>, then the name; the label, system and glyph stay whole.
    /// </summary>
    private sealed class StripPanel : Avalonia.Controls.Panel
    {
        private const double Gutter = 12;
        private const double LabelWidth = 52;
        private const double NameGap = 12;
        private const double NameInset = 14;
        private const double LeastSentence = 160;

        private readonly TextBlock _label;
        private readonly TextBlock _sentence;
        private readonly Control? _system;
        private readonly Control? _glyph;
        private readonly Control? _after;
        private readonly TextBlock _name;

        public StripPanel(TextBlock label, TextBlock sentence, Control? system, Control? glyph, Control? after, TextBlock name)
        {
            _label = label;
            _sentence = sentence;
            _system = system;
            _glyph = glyph;
            _after = after;
            _name = name;

            foreach (var child in new Control?[] { label, sentence, system, glyph, after, name })
            {
                if (child is not null)
                {
                    Children.Add(child);
                }
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var (sentence, name) = Widths(availableSize.Width);

            _sentence.Measure(new Size(sentence, double.PositiveInfinity));
            _name.Measure(new Size(name, double.PositiveInfinity));

            return new Size(
                double.IsInfinity(availableSize.Width) ? Gutter + LabelOf() + sentence + Fixed(_system, _glyph, _after) + name + NameInset : availableSize.Width,
                TypeScale.MinimumTarget);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var (sentence, name) = Widths(finalSize.Width);
            var x = Gutter;

            x = Place(_label, x, LabelOf(), finalSize.Height);
            x = Place(_sentence, x, sentence, finalSize.Height);

            foreach (var fixedChild in new[] { _system, _glyph, _after })
            {
                if (fixedChild is not null)
                {
                    x = Place(fixedChild, x, fixedChild.DesiredSize.Width, finalSize.Height);
                }
            }

            Place(_name, Math.Max(x, finalSize.Width - NameInset - name), name, finalSize.Height);

            return finalSize;
        }

        private double LabelOf()
        {
            _label.Measure(Size.Infinity);

            return Math.Max(LabelWidth, _label.DesiredSize.Width + 6);
        }

        private static double Fixed(params Control?[] children)
        {
            var width = 0.0;

            foreach (var child in children)
            {
                if (child is not null)
                {
                    child.Measure(Size.Infinity);
                    width += child.DesiredSize.Width;
                }
            }

            return width;
        }

        /// <summary>The widths the sentence and the story's name get out of <paramref name="width"/>.</summary>
        private (double Sentence, double Name) Widths(double width)
        {
            _sentence.Measure(Size.Infinity);
            _name.Measure(Size.Infinity);

            var sentence = _sentence.DesiredSize.Width;
            var name = _name.DesiredSize.Width + NameGap;

            if (double.IsInfinity(width))
            {
                return (sentence, name);
            }

            var room = width - Gutter - LabelOf() - Fixed(_system, _glyph, _after) - NameInset;

            if (sentence + name <= room)
            {
                return (sentence, name - NameGap);
            }

            var sentenceWidth = Math.Max(0, Math.Min(sentence, Math.Max(LeastSentence, room - name)));
            sentenceWidth = Math.Min(sentenceWidth, Math.Max(0, room));
            var nameWidth = Math.Max(0, Math.Min(name - NameGap, room - sentenceWidth - NameGap));

            return (sentenceWidth, nameWidth);
        }

        private static double Place(Control child, double x, double width, double height)
        {
            child.Measure(new Size(width, height));
            child.Arrange(new Rect(x, (height - child.DesiredSize.Height) / 2, width, child.DesiredSize.Height));

            return x + width;
        }
    }
}
