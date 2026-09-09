using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Adventures;

namespace D47.App.Panel;

/// <summary>The story, on the mini panel (asked for 2026-08-22).</summary>
public sealed class AdventureMini : UserControl
{
    private readonly AdventureSurface _surface;
    private readonly StackPanel _body = new() { Spacing = 3 };

    public AdventureMini(AdventureSurface surface)
    {
        _surface = surface;

        Content = new ScrollViewer
        {
            Content = _body,
            Margin = new Thickness(10, 8),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        Fill();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _surface.Book.Store.Changed += OnChanged;
        _surface.Book.StirringChanged += OnChanged;
        Fill();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _surface.Book.Store.Changed -= OnChanged;
        _surface.Book.StirringChanged -= OnChanged;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Fill);

    private void Fill()
    {
        _body.Children.Clear();

        var commander = _surface.Commander();
        var active = _surface.Book.Active(commander);

        if (active.Count == 0)
        {
            _body.Children.Add(AdventuresPage.Muted("No adventure under way."));
            return;
        }

        // The one being flown.
        var standing = active[0];
        var adventure = standing.Adventure;

        _body.Children.Add(AdventuresPage.Title(adventure.Name));

        if (standing.Step() is { } step)
        {
            _body.Children.Add(AdventuresPage.Text(step, TypeScale.Small, ThemeManager.TextMutedKey));
        }

        // The short description: the premise, which is the one sentence the whole story was built out of.
        if (adventure.Spine?.Premise is { Length: > 0 } premise)
        {
            _body.Children.Add(AdventuresPage.Text(premise, TypeScale.Secondary));
        }

        if (standing.LastTrigger() is { } done)
        {
            _body.Children.Add(Row("Done", done));
        }

        if (standing.NextTrigger() is { } next)
        {
            _body.Children.Add(Row("Next", next));
        }

        if (standing.LastSaid() is { } said)
        {
            _body.Children.Add(AdventuresPage.Text(
                $"“{Shorten(said.Text)}”", TypeScale.Secondary, ThemeManager.TextMutedKey));
        }

        if (_surface.Book.IsStirring(commander, adventure.Key))
        {
            _body.Children.Add(new AdventureThinking());
        }
    }

    /// <summary>A trigger with its word in front, both in the highlight colour so the pair reads as one.</summary>
    private static Control Row(string label, string trigger) =>
        AdventuresPage.Trigger($"{label}: {AdventuresPage.Sentence(trigger)}");

    /// <summary>The last line, trimmed to what mini can hold.</summary>
    private static string Shorten(string text)
    {
        const int Most = 180;

        var line = text.Trim();

        return line.Length <= Most ? line : line[..Most].TrimEnd() + "…";
    }
}
