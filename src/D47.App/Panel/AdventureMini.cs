using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Adventures;

namespace D47.App.Panel;

/// <summary>The story on the mini panel: the title line, what to do next, then what was told, newest first.</summary>
public sealed class AdventureMini : UserControl
{
    private readonly AdventureSurface _surface;
    private readonly Func<SpeakerPortraits?> _portraits;
    private readonly StackPanel _body = new() { Spacing = 3 };

    public AdventureMini(AdventureSurface surface, Func<SpeakerPortraits?>? portraits = null)
    {
        _surface = surface;
        _portraits = portraits ?? (() => null);

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
            stories.WritingChanged += OnChanged;
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
            stories.WritingChanged -= OnChanged;
        }
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Fill);

    private void Fill()
    {
        _body.Children.Clear();

        var commander = _surface.Commander();
        var director = _surface.Stories;
        var story = director?.Stories.Current(commander);

        // A stock story's current chapter is shown whether or not it is still under way: while the next one is
        // written, after it was abandoned and once it is done.
        var standing = story?.CurrentChapter is { } key ? _surface.Book.Standing(commander, key) : null;
        standing ??= _surface.Book.Active(commander).FirstOrDefault();

        if (story is null && standing is null)
        {
            _body.Children.Add(AdventuresPage.Muted("No story under way."));
            return;
        }

        _body.Children.Add(TitleLine(standing?.Adventure.Name ?? story!.Title, standing?.Step()));

        AddObjective(director, story, standing, commander);

        if (standing is null)
        {
            return;
        }

        var adventure = standing.Adventure;

        if (_surface.Book.IsStirring(commander, adventure.Key))
        {
            _body.Children.Add(new AdventureThinking());
        }

        var entries = new ToldEntries(_surface, _portraits);
        var here = _surface.State()?.Location?.StarSystem;

        for (var index = adventure.Told.Count - 1; index >= 0; index--)
        {
            _body.Children.Add(entries.Entry(adventure, adventure.Told[index], here, copy: null));
        }
    }

    /// <summary>The story's name and its step as one wrapping line.</summary>
    private static TextBlock TitleLine(string name, string? step)
    {
        var line = TitleText.Build(name, TypeScale.Heading, TitleRank.Screen);
        line.TextWrapping = TextWrapping.Wrap;

        if (step is null)
        {
            return line;
        }

        // Non-breaking, so the dot and the step wrap to the next line whole.
        var at = new Run($" ·\u00A0{step.Replace(' ', '\u00A0')}") { FontFamily = new FontFamily(Fonts.ProseFamily), FontSize = TypeScale.Small, FontWeight = FontWeight.Normal };
        ThemeInk.Bind(at, TextElement.ForegroundProperty, ThemeManager.AKey);

        line.Inlines = [new Run(line.Text), at];
        return line;
    }

    /// <summary>What to do next, or why there is nothing to do: a write in flight or failed, the story off or paused, a choice, or the end.</summary>
    private void AddObjective(D47.Core.Stories.StoryDirector? director, D47.Core.Stories.Story? story, AdventureStanding? standing, string? commander)
    {
        if (director is not null && story is not null)
        {
            var shown = StoriesView.StandingOf(director, story, commander);

            switch (shown.Kind)
            {
                case StoryStandingKind.Writing when director.Progress(commander) is { } writing:
                    var progress = new WriteProgressBar("Writing the chapter", () => director.Progress(commander));
                    progress.Show(writing);
                    _body.Children.Add(progress);
                    return;

                case StoryStandingKind.UnderWay:
                    break;

                default:
                    _body.Children.Add(AdventuresPage.Text(shown.Sentence, TypeScale.Secondary, ThemeManager.GreyKey));
                    return;
            }
        }
        else if (director?.IsRewriting(commander) == true)
        {
            _body.Children.Add(AdventuresPage.Text("Writing a different objective…", TypeScale.Secondary, ThemeManager.GreyKey));
            return;
        }

        if (standing is null)
        {
            return;
        }

        if (standing.NextTrigger() is { } next)
        {
            _body.Children.Add(Row("Next", next, _surface.State()?.Location?.StarSystem));
        }

        AddChoice(standing);

        if (standing.IsDone)
        {
            _body.Children.Add(AdventuresPage.Muted("The end."));
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
}
