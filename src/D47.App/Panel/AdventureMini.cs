using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
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

        if (_surface.Switched is { } switched)
        {
            switched.Changed += OnChanged;
        }

        if (_surface.Stories is { } stories)
        {
            stories.Stories.Changed += OnChanged;
        }

        Fill();
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

    private void OnChanged() => Dispatcher.UIThread.Post(Fill);

    private void Fill()
    {
        _body.Children.Clear();

        var commander = _surface.Commander();
        var active = _surface.Book.Active(commander);

        if (_surface.Stories is { } director && director.Stories.Current(commander) is { } story)
        {
            _body.Children.Add(StoriesView.StoryOnBox(_surface, director, story, commander));
        }

        if (active.Count == 0)
        {
            _body.Children.Add(AdventuresPage.Muted("No story under way."));
            return;
        }

        // The one being flown.
        var standing = active[0];
        var adventure = standing.Adventure;

        _body.Children.Add(TitleText.Build(adventure.Name, TypeScale.Heading, TitleRank.Screen));

        if (standing.Step() is { } step)
        {
            _body.Children.Add(AdventuresPage.Text(step, TypeScale.Small, ThemeManager.AKey));
        }

        // The short description: the premise, which is the one sentence the whole story was built out of.
        if (AdventuresPage.ShowsSpine(adventure) && adventure.Spine?.Premise is { Length: > 0 } premise)
        {
            _body.Children.Add(AdventuresPage.Text(premise, TypeScale.Secondary));
        }

        var here = _surface.State()?.Location?.StarSystem;

        if (standing.LastTrigger() is { } done)
        {
            _body.Children.Add(Row("Done", done, here));
        }

        if (standing.NextTrigger() is { } next)
        {
            _body.Children.Add(Row("Next", next, here));
        }

        AddChoice(standing);

        if (standing.LastSaid() is { } said)
        {
            _body.Children.Add(AdventuresPage.Text(
                $"“{Shorten(said.Text)}”", TypeScale.Secondary, ThemeManager.GreyKey));
        }

        if (_surface.Book.IsStirring(commander, adventure.Key))
        {
            _body.Children.Add(new AdventureThinking());
        }
    }

    /// <summary>The open choice as 44 px tiles with the phrase to say, or once made the option chosen.</summary>
    private void AddChoice(AdventureStanding standing)
    {
        if (standing.OpenChoice is { } choice)
        {
            for (var index = 0; index < choice.Options.Count; index++)
            {
                _body.Children.Add(OptionTile(index, choice.Options[index].Label));
            }

            _body.Children.Add(AdventuresPage.Text(BeatOption.Saying(choice.Options), TypeScale.Caption, ThemeManager.GreyKey));
        }
        else if (standing.InChoice
            && standing.Chosen.TryGetValue(standing.Current, out var chosen)
            && standing.CurrentBeat?.Steps[0].Options.ElementAtOrDefault(chosen) is { } option)
        {
            _body.Children.Add(Row("Chosen", $"option {(chosen + 1).ToString(CultureInfo.InvariantCulture)}, {option.Label}", null));
        }
    }

    /// <summary>
    /// One option: its number and label. A pressed border rather than a button, because an output-only mini surface hides every
    /// button; the ray presses it in the headset and the desktop mini panel is click-through.
    /// </summary>
    private Border OptionTile(int index, string label)
    {
        var number = new TextBlock
        {
            Text = (index + 1).ToString(CultureInfo.InvariantCulture),
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Small,
            VerticalAlignment = VerticalAlignment.Center,
        };

        ThemeInk.Bind(number, TextBlock.ForegroundProperty, ThemeManager.AKey);

        var text = new TextBlock
        {
            Text = label,
            FontSize = TypeScale.Small,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        ThemeInk.Bind(text, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("24,*"), ColumnSpacing = 8 };

        Grid.SetColumn(text, 1);
        grid.Children.Add(number);
        grid.Children.Add(text);

        var tile = ListRow.Dress(new Border { Child = grid, MinHeight = TypeScale.MinimumTarget, Background = Brushes.Transparent });

        AutomationProperties.SetName(tile, BeatOption.Phrase(index));
        tile.PointerPressed += (_, _) => _ = AdventuresPage.ChooseOption(_surface, index);

        return tile;
    }

    /// <summary>A trigger with its word in front, both in A so the pair reads as one.</summary>
    private static Control Row(string label, string trigger, string? here) =>
        AdventuresPage.Trigger($"{label}: {AdventuresPage.Sentence(trigger)}", here);

    /// <summary>The last line, trimmed to what mini can hold.</summary>
    private static string Shorten(string text)
    {
        const int Most = 180;

        var line = text.Trim();

        return line.Length <= Most ? line : line[..Most].TrimEnd() + "…";
    }
}
