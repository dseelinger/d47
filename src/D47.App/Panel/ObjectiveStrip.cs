using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Adventures;
using D47.Core.Campaigns;
using D47.Core.Configuration;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>
/// What the strip says: the label, the sentence split around its system, and the tag at the right (the story's name, or
/// the front's kind and faction). <paramref name="Toggle"/> is whether the Story and Campaign toggle shows.
/// </summary>
internal sealed record ObjectiveStripText(
    string Label, string Before, string? System, string? CopyValue, string? After, bool InSystem, bool Grey, string Tag,
    NextBarSource Source = NextBarSource.Story, bool Toggle = false);

/// <summary>The next story objective or campaign action on one line, under the title rule of every tab of the full panel.</summary>
internal sealed class ObjectiveStrip : UserControl
{
    private readonly AdventureSurface _surface;
    private readonly Func<Func<string, Task<bool>>?> _copy;
    private readonly NextBarMemory? _memory;
    private readonly Func<CampaignNextAction?>? _campaign;
    private ObjectiveStripText? _shown;

    public ObjectiveStrip(
        AdventureSurface surface, Func<Func<string, Task<bool>>?> copy, NextBarMemory? memory = null, Func<CampaignNextAction?>? campaign = null)
    {
        _surface = surface;
        _copy = copy;
        _memory = memory;
        _campaign = campaign;

        Height = TypeScale.MinimumTarget;
        Margin = new Thickness(0, 10, 0, 0);
        IsVisible = false;
        AutomationProperties.SetName(this, "Next story objective");
        ThemeInk.Bind(this, BackgroundProperty, ThemeManager.SlabKey);

        Refresh();
    }

    /// <summary>The strip's words for <paramref name="shown"/>, or null when it has nothing to say.</summary>
    internal static ObjectiveStripText? Describe(ShownObjective? shown, string? here, bool rewriting)
    {
        if (shown is null || shown.Paused)
        {
            return null;
        }

        const string label = "NEXT";
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
            return new ObjectiveStripText(label, sentence, null, null, null, false, false, name);
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
            false,
            name);
    }

    /// <summary>The strip's words for the chosen campaign front's next action, or null when there is none.</summary>
    internal static ObjectiveStripText? DescribeCampaign(CampaignNextAction? action, string? here)
    {
        if (action is null)
        {
            return null;
        }

        var sentence = action.Sentence;
        var stop = sentence.IndexOf(". ", StringComparison.Ordinal);
        sentence = stop >= 0 ? sentence[..stop] : sentence;
        sentence = sentence.EndsWith('.') ? sentence[..^1] : sentence;

        var front = action.Front;
        var kind = front.Kind == FrontKind.Conflict && front.Conflict is { Type.Length: > 0 } conflict
            ? conflict.Type
            : CampaignFronts.Describe(front.Kind);
        var faction = front.Kind == FrontKind.Conflict && front.Opponent is { Length: > 0 } opponent ? opponent : action.Faction;
        var tag = (faction.Length > 0 ? $"{kind} · {faction}" : kind).ToUpperInvariant();

        const string label = "NEXT";
        var system = front.System;

        if (AdventuresPage.Naming(sentence, system) is not { } at)
        {
            return new ObjectiveStripText(label, sentence, null, null, null, false, false, tag, NextBarSource.Campaign);
        }

        var end = at + system.Length;

        return new ObjectiveStripText(
            label,
            sentence[..at].TrimEnd() + " ",
            sentence[at..end],
            system,
            end < sentence.Length ? sentence[end..] : null,
            string.Equals(system, here, StringComparison.OrdinalIgnoreCase),
            false,
            tag,
            NextBarSource.Campaign);
    }

    /// <summary>
    /// What the strip draws: the one that exists, or the <paramref name="remembered"/> one when both do, with the toggle
    /// only in that case; null when neither does.
    /// </summary>
    internal static ObjectiveStripText? Choose(ObjectiveStripText? story, ObjectiveStripText? campaign, NextBarSource remembered)
    {
        if (story is not null && campaign is not null)
        {
            return (remembered == NextBarSource.Campaign ? campaign : story) with { Toggle = true };
        }

        return (story ?? campaign) is { } only ? only with { Toggle = false } : null;
    }

    /// <summary>Redraws when the words changed; true when it did.</summary>
    public bool Refresh()
    {
        var commander = _surface.Commander();
        var rewriting = _surface.Stories?.IsRewriting(commander) == true;
        var here = _surface.State()?.Location?.StarSystem;
        var story = Describe(_surface.Stories?.CurrentObjective(commander), here, rewriting);
        var campaign = DescribeCampaign(_campaign?.Invoke(), here);
        var text = Choose(story, campaign, _memory?.Source ?? NextBarSource.Story);

        if (Equals(text, _shown))
        {
            return false;
        }

        _shown = text;
        IsVisible = text is not null;
        Content = text is null ? null : Build(text);
        AutomationProperties.SetName(this, text?.Source == NextBarSource.Campaign ? "Next campaign action" : "Next story objective");

        return true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _surface.Book.Store.Changed += OnChanged;
        _surface.Book.StirringChanged += OnChanged;

        if (_memory is not null)
        {
            _memory.Changed += OnChanged;
        }

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

        if (_memory is not null)
        {
            _memory.Changed -= OnChanged;
        }

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

        return new StripPanel(label, sentence, system, glyph, after, Chrome(text.Tag, FontWeight.Medium, ThemeManager.GreyKey, trim: true), Toggle(text));
    }

    private Segment? Toggle(ObjectiveStripText text)
    {
        if (!text.Toggle || _memory is null)
        {
            return null;
        }

        var toggle = new Segment
        {
            ItemsSource = ["Story", "Campaign"],
            SelectedIndex = (int)text.Source,
            Width = StripPanel.ToggleWidth,
            Height = TypeScale.MinimumTarget,
        };
        AutomationProperties.SetName(toggle, "What NEXT shows");
        toggle.SelectionChanged += (_, _) => _memory.Remember(toggle.SelectedIndex == 1 ? NextBarSource.Campaign : NextBarSource.Story);

        return toggle;
    }

    private static TextBlock Prose(string text, string key) =>
        Typeset.Text(text, TypeScale.Description, key, vertical: VerticalAlignment.Center);

    private static TextBlock Chrome(string text, FontWeight weight, string key, bool trim = false) =>
        Typeset.Chrome(
            text, TypeScale.Meta, key,
            weight: weight, tracking: TypeScale.Meta * 0.08, trimming: trim ? TextTrimming.CharacterEllipsis : TextTrimming.None, vertical: VerticalAlignment.Center);

    /// <summary>
    /// One line: label, sentence, system, glyph, then the tag and the toggle at the right. When the line is too long the sentence
    /// shortens first, to no less than <see cref="LeastSentence"/>, then the name; the label, system and glyph stay whole.
    /// </summary>
    private sealed class StripPanel : Avalonia.Controls.Panel
    {
        private const double Gutter = 12;
        private const double LabelWidth = 52;
        private const double NameGap = 12;
        private const double NameInset = 14;
        private const double LeastSentence = 160;

        public const double ToggleWidth = 232;

        private readonly TextBlock _label;
        private readonly TextBlock _sentence;
        private readonly Control? _system;
        private readonly Control? _glyph;
        private readonly Control? _after;
        private readonly TextBlock _name;
        private readonly Control? _toggle;

        public StripPanel(TextBlock label, TextBlock sentence, Control? system, Control? glyph, Control? after, TextBlock name, Control? toggle)
        {
            _toggle = toggle;
            _label = label;
            _sentence = sentence;
            _system = system;
            _glyph = glyph;
            _after = after;
            _name = name;

            foreach (var child in new Control?[] { label, sentence, system, glyph, after, name, toggle })
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
                double.IsInfinity(availableSize.Width) ? Gutter + LabelOf() + sentence + Fixed(_system, _glyph, _after) + name + NameInset + ToggleOf() : availableSize.Width,
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

            var toggle = ToggleOf();

            if (_toggle is not null)
            {
                Place(_toggle, finalSize.Width - toggle, toggle, finalSize.Height);
            }

            Place(_name, Math.Max(x, finalSize.Width - toggle - NameInset - name), name, finalSize.Height);

            return finalSize;
        }

        private double ToggleOf() => _toggle is null ? 0 : ToggleWidth;

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

            var room = width - Gutter - LabelOf() - Fixed(_system, _glyph, _after) - NameInset - ToggleOf();

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
