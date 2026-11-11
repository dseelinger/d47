using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.App.Voice;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Messages;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>One story run's page on the Story bench: every line it has spoken, in chapter sections, with Jump to and Play.</summary>
public sealed class StoryRunPage : UserControl, IPageSummary
{
    /// <summary>The crumb key prefix; the rest is the story id and the pick's UTC ticks.</summary>
    public const string Prefix = "stories.run.";

    public const string JumpLabel = "Jump to";
    public const string ChapterLabel = "Chapter";
    public const string BeatLabel = "Objective";
    public const string NoLinesLabel = "No lines kept";
    public const string NoLinesText = "Lines from before this version were not kept, so there is nothing to play. Each chapter's text was kept, and is below.";
    public const string GoneText = "That story run is no longer on file.";
    public const string BeforeChapters = "Before the first chapter";
    public const string PlayAllLabel = "Play all";
    public const string PlayFromHereLabel = "Play from here";
    public const string PlayingMark = "▶ Playing";
    public const string SilencedText = "Other speech is silenced while this plays.";
    public const string PushToTalkText = "Paused for push-to-talk. Press PLAY to carry on.";
    public const string PausedText = "Paused. Press PLAY to carry on.";
    public const string NoClipNote = "No clip kept. Played, it is spoken again in the speaker's current voice.";
    public const string SpeakingAgainLabel = "Speaking again";
    public const string ShownForReadingLabel = "Shown for reading";
    public const string HeldText = "No voice to speak this. Play all moves on when the time is up.";
    public const string NoVoiceLabel = "No voice for this line";
    public const string GeneratingText = "GENERATING · voice provider";

    /// <summary>What the playback box says under a line being spoken again.</summary>
    public static string SpeakingAgainText(string speaker) => $"Speaking again in {speaker}'s current voice. The new clip is kept.";

    /// <summary>Marks the list of sections and lines, for a test to find it.</summary>
    public const string ListName = "StoryRunLines";

    /// <summary>Marks the Jump to chapter picker.</summary>
    public const string ChapterPickerName = "JumpChapter";

    /// <summary>Marks the Jump to beat picker.</summary>
    public const string BeatPickerName = "JumpBeat";

    private const double PickerWidth = 360;

    private readonly AdventureSurface _surface;
    private readonly StoryDirector _director;
    private readonly ToldEntries _entries;
    private readonly string _storyId;
    private readonly DateTimeOffset _pickedAt;
    private readonly StackPanel _top = new() { Spacing = 14 };
    private readonly StatusLine _status = new();
    private readonly ItemsControl _list;
    private readonly ScrollViewer _scroll;
    private readonly InlinePicker _chapterPicker = new() { Name = ChapterPickerName, Width = PickerWidth, Label = ChapterLabel };
    private readonly InlinePicker _beatPicker = new() { Name = BeatPickerName, Width = PickerWidth, Label = BeatLabel };

    private Story? _run;
    private List<object> _rows = [];
    private IReadOnlyList<StoryRunSection> _sections = [];
    private int _chapter = -1;
    private int _beat = -1;
    private object? _drawn;
    private List<StoryRunEntry> _entriesInOrder = [];
    private readonly Dictionary<StoryRunEntry, Border> _lineRows = [];
    private StoryRunEntry? _marked;
    private Button? _playAll;
    private readonly PanelPrompts? _prompts;
    private readonly PanelNavigator? _nav;
    private bool _deleting;

    internal StoryRunPage(
        AdventureSurface surface, StoryDirector director, ToldEntries entries, Story run, bool headset,
        PanelPrompts? prompts = null, PanelNavigator? nav = null)
    {
        _prompts = prompts;
        _nav = nav;
        _surface = surface;
        _director = director;
        _entries = entries;
        _storyId = run.Id;
        _pickedAt = run.PickedAt;

        _list = new ItemsControl
        {
            Name = ListName,
            // The headset lays out without a layout manager, so a virtualising panel there never learns its viewport.
            ItemsPanel = new FuncTemplate<Avalonia.Controls.Panel?>(() => headset ? new StackPanel() : new VirtualizingStackPanel()),
            ItemTemplate = new FuncDataTemplate<object>((item, _) => item switch
            {
                StoryRunSection section => SectionHead(section),
                StoryRunEntry entry => LineRow(entry),
                KeptChapter kept => KeptChapterView(kept),
                _ => null,
            }),
        };


        _chapterPicker.Picked += (_, id) => PickChapter(int.Parse(id, CultureInfo.InvariantCulture));
        _beatPicker.Picked += (_, id) => PickBeat(int.Parse(id, CultureInfo.InvariantCulture));

        Margin = new Thickness(14);
        Content = LoadoutPages.HeaderAndList(_top, _list, out _scroll);
        Rebuild();
    }

    public static string KeyOf(Story run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return Prefix + run.PickedAt.UtcTicks.ToString(CultureInfo.InvariantCulture) + "." + run.Id;
    }

    /// <summary>The Commander's run a crumb key names, or null.</summary>
    internal static Story? Find(StoryDirector director, string? commander, string key)
    {
        if (!key.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = key[Prefix.Length..];
        var dot = rest.IndexOf('.', StringComparison.Ordinal);

        if (dot < 0 || !long.TryParse(rest[..dot], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks))
        {
            return null;
        }

        var id = rest[(dot + 1)..];

        return director.Stories.Runs(commander).FirstOrDefault(run =>
            string.Equals(run.Id, id, StringComparison.OrdinalIgnoreCase) && run.PickedAt.UtcTicks == ticks);
    }

    public static string SummaryOf(string title) => $"Every line {title} has spoken, in order.";

    public string Summary => SummaryOf(_run?.Title ?? _storyId);

    public event EventHandler? SummaryChanged;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _director.Stories.Changed += OnChanged;

        if (_surface.Messages is { } messages)
        {
            messages.Changed += OnChanged;
        }

        if (_surface.RunPlayer is { } player)
        {
            player.Changed += OnPlayerChanged;
        }

        if (_surface.RunVoicing is { } voicing)
        {
            voicing.Kept += OnKept;
        }

        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _director.Stories.Changed -= OnChanged;

        if (_surface.Messages is { } messages)
        {
            messages.Changed -= OnChanged;
        }

        if (_surface.RunPlayer is { } player)
        {
            player.Changed -= OnPlayerChanged;
        }

        if (_surface.RunVoicing is { } voicing)
        {
            voicing.Kept -= OnKept;
        }

    }

    private void OnChanged() => Dispatcher.UIThread.Post(Rebuild);

    private void OnKept(Story run)
    {
        if (string.Equals(run.Id, _storyId, StringComparison.OrdinalIgnoreCase) && run.PickedAt == _pickedAt)
        {
            Dispatcher.UIThread.Post(Rebuild);
        }
    }

    private void OnPlayerChanged() => Dispatcher.UIThread.Post(ShowPlayback);

    private Adventure? ChapterOf(string? commander, Story run, string key) =>
        _director.Archive.Find(commander, run, key)?.Adventure ?? _surface.Book.Store.Find(commander, key);

    private void Rebuild()
    {
        var commander = _surface.Commander();
        _run = _director.Stories.Runs(commander).FirstOrDefault(run =>
            string.Equals(run.Id, _storyId, StringComparison.OrdinalIgnoreCase) && run.PickedAt == _pickedAt);
        SummaryChanged?.Invoke(this, EventArgs.Empty);

        if (_run is not { } run)
        {
            _drawn = null;
            _top.Children.Clear();
            _top.Children.Add(_status);
            _rows = [];
            _list.ItemsSource = _rows;
            _top.Children.Add(AdventuresPage.Muted(GoneText));
            return;
        }

        var lines = _director.Records?.Read(commander, run) ?? [];
        var drawn = (run.State, run.Chapters.Count, run.CluesGiven, lines.Count, lines.Count(line => line.Clip is not null));

        if (Equals(drawn, _drawn) && _top.Children.Count > 1)
        {
            return;
        }

        _drawn = drawn;
        var offset = _scroll.Offset;
        Dispatcher.UIThread.Post(() => _scroll.Offset = offset, DispatcherPriority.Background);

        _top.Children.Clear();
        _top.Children.Add(_status);
        _top.Children.Add(Strip(run, lines));

        if (_prompts is not null && StoryRunDeletion.Offers(run))
        {
            _top.Children.Add(DeleteRow(run));
        }

        if (lines.Count == 0)
        {
            _sections = [];
            _entriesInOrder = [];
            _rows = [.. KeptChapters(commander, run)];
            _list.ItemsSource = _rows;
            _top.Children.Add(new Notice(NoticeLevel.Warning) { Label = NoLinesLabel, Text = NoLinesText });
            return;
        }

        _sections = StoryRunLog.Of(run, lines, key => ChapterOf(commander, run, key));
        _entriesInOrder = [.. _sections.SelectMany(section => section.Entries)];
        _lineRows.Clear();
        _marked = null;
        _rows = [.. _sections.SelectMany(section => new object[] { section }.Concat(section.Entries))];
        _list.ItemsSource = _rows;

        if (!_sections.Any(section => section.Number == _chapter))
        {
            _chapter = _sections[^1].Number;
            _beat = -1;
        }

        _top.Children.Add(JumpRow());
        ShowPickers();
        ShowPlayback();
    }

    // ---- the strip -------------------------------------------------------------------------

    internal static string StateName(Story run) => run.IsCurrent
        ? "Active"
        : run.State switch
        {
            StoryState.Shelved => "Shelved",
            StoryState.Finished => "Finished",
            StoryState.Abandoned => "Abandoned",
            _ => "Ended",
        };

    private static Grid Strip(Story run, IReadOnlyList<StoryRunLine> lines)
    {
        var last = lines.Count > 0 ? StoryBenchPage.Day(lines.Max(line => line.Sent)) : StoryBenchPage.NoLine;
        var clues = $"{run.CluesGiven.ToString(CultureInfo.InvariantCulture)} / {run.Pacing.ClueDays.Count.ToString(CultureInfo.InvariantCulture)}";

        return StatTile.Grid(
            [
                StatTile.Build("State", StateName(run).ToUpperInvariant()),
                StatTile.Build("Length", run.Pacing.Name),
                StatTile.Build("Chapters so far", run.Chapters.Count.ToString(CultureInfo.InvariantCulture), StatInk.Number),
                StatTile.Build("Clues given", clues, StatInk.Number),
                StatTile.Build("Picked · last line", $"{StoryBenchPage.Day(run.PickedAt)} · {last}", StatInk.Number),
            ],
            maxColumns: 5);
    }

    private Button DeleteRow(Story run)
    {
        var delete = new Button
        {
            Name = "DeleteThisRun",
            Content = StoryRunDeletion.DeleteLabel,
            Width = 140,
            MinHeight = TypeScale.MinimumTarget,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsEnabled = !_deleting,
        };
        delete.Classes.Add(Settings.SettingsView.DestructiveClass);
        delete.Click += (_, _) => StoryRunDeletion.Ask(_prompts!, _director, _surface.Commander, _surface.Now, run, Act);
        return delete;
    }

    private void Act(string doing, Func<string?> work, string done)
    {
        if (_deleting)
        {
            return;
        }

        _deleting = true;
        _status.Say(doing);

        _ = Task.Run(() =>
        {
            var refusal = work();

            Dispatcher.UIThread.Post(() =>
            {
                _deleting = false;

                if (refusal is not null)
                {
                    _status.Fail(refusal);
                }
                else
                {
                    _status.Say(done);
                }

                _drawn = null;
                Rebuild();

                if (refusal is null && _run is null)
                {
                    _nav?.Back();
                }
            });
        });
    }

    // ---- Jump to ---------------------------------------------------------------------------

    internal static string ChapterName(StoryRunSection section) => section.Number == 0
        ? BeforeChapters
        : section.Name is { } name
            ? $"Chapter {section.Number.ToString(CultureInfo.InvariantCulture)} · {name}"
            : $"Chapter {section.Number.ToString(CultureInfo.InvariantCulture)}";

    internal static string BeatName(StoryRunSection section, int beat) =>
        section.Chapter?.Beats.ElementAtOrDefault(beat) is { } found
            ? $"Objective {(beat + 1).ToString(CultureInfo.InvariantCulture)} · {found.Title}"
            : $"Objective {(beat + 1).ToString(CultureInfo.InvariantCulture)}";

    private static List<int> BeatsOf(StoryRunSection section) =>
        [.. section.Entries.Select(entry => entry.Beat).Where(beat => beat >= 0).Distinct().Order()];

    private Grid JumpRow()
    {
        var label = Typeset.Chrome(
            JumpLabel.ToUpperInvariant(), TypeScale.Meta, ThemeManager.GreyKey,
            weight: FontWeight.SemiBold, tracking: TypeScale.Meta * Fonts.ChromeTracking, vertical: VerticalAlignment.Top);
        label.Width = 84;
        label.Margin = new Thickness(0, 14, 0, 0);

        _chapterPicker.VerticalAlignment = VerticalAlignment.Top;
        _beatPicker.VerticalAlignment = VerticalAlignment.Top;

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*,Auto"), ColumnSpacing = 2 };
        row.Children.Add(Detached(label));
        Grid.SetColumn(Detached(_chapterPicker), 1);
        row.Children.Add(_chapterPicker);
        Grid.SetColumn(Detached(_beatPicker), 2);
        row.Children.Add(_beatPicker);

        if (_surface.RunPlayer is not null)
        {
            var playAll = new Button
            {
                Name = "PlayAllFromJump",
                Content = PlayAllLabel,
                Width = 140,
                MinHeight = TypeScale.MinimumTarget,
                VerticalAlignment = VerticalAlignment.Top,
            };
            playAll.Click += (_, _) => PlayAllFrom(JumpStart());
            _playAll = playAll;
            Grid.SetColumn(playAll, 4);
            row.Children.Add(playAll);
        }

        return row;
    }

    /// <summary>The first line of the chosen beat, or else of the chosen chapter.</summary>
    private StoryRunEntry? JumpStart()
    {
        var section = _sections.FirstOrDefault(found => found.Number == _chapter);
        return _beat >= 0 && section?.Entries.FirstOrDefault(entry => entry.Beat == _beat) is { } beat ? beat : section?.Entries.FirstOrDefault();
    }

    internal void PlayFromStart() => PlayAllFrom(_entriesInOrder.FirstOrDefault());

    /// <summary>Plays the run's lines in page order from <paramref name="entry"/>.</summary>
    internal void PlayAllFrom(StoryRunEntry? entry)
    {
        if (entry is null || _run is not { } run || _surface.RunPlayer is not { } player)
        {
            return;
        }

        var from = _entriesInOrder.IndexOf(entry);

        if (from >= 0)
        {
            _status.Clear();
            player.Play(run, [.. _entriesInOrder.Select(each => each.Line)], from);
        }
    }

    // ---- playback --------------------------------------------------------------------------

    private static bool SameLine(StoryRunLine left, StoryRunLine right) =>
        left.Sent == right.Sent && string.Equals(left.Key, right.Key, StringComparison.Ordinal);

    /// <summary>What the player is doing with this run, or null when it plays nothing of it.</summary>
    private StoryRunPlayback? Playback =>
        _surface.RunPlayer?.Now is { } now && _run is { } run && now.Of(run) ? now : null;

    private StoryRunEntry? PlayingEntry(StoryRunPlayback? playback) =>
        playback is null ? null : _entriesInOrder.FirstOrDefault(entry => SameLine(entry.Line, playback.Line));

    /// <summary>Moves the playing mark.</summary>
    private void ShowPlayback()
    {
        var playback = Playback;
        var playing = PlayingEntry(playback);

        if (!ReferenceEquals(playing, _marked))
        {
            var was = _marked;
            _marked = playing;
            Refill(was);
        }

        Refill(playing);
        _playAll?.Classes.Set("selected", playback is { IsPaused: false });
    }

    private static Control Detached(Control control)
    {
        if (control.Parent is Avalonia.Controls.Panel panel)
        {
            panel.Children.Remove(control);
        }

        return control;
    }

    private void ShowPickers()
    {
        var section = _sections.First(found => found.Number == _chapter);

        _chapterPicker.Show(
            [.. _sections.Select(each => new InlinePickerOption(
                each.Number.ToString(CultureInfo.InvariantCulture), ChapterName(each), ThemeManager.AKey))],
            _chapter.ToString(CultureInfo.InvariantCulture),
            ChapterName(section),
            ThemeManager.AKey,
            null);

        var beats = BeatsOf(section);
        _beatPicker.IsEnabled = beats.Count > 0;
        _beatPicker.Show(
            [.. beats.Select(beat => new InlinePickerOption(beat.ToString(CultureInfo.InvariantCulture), BeatName(section, beat), ThemeManager.AKey))],
            _beat.ToString(CultureInfo.InvariantCulture),
            beats.Contains(_beat) ? BeatName(section, _beat) : StoryBenchPage.NoLine,
            ThemeManager.AKey,
            null);
    }

    /// <summary>Shows the chapter numbered <paramref name="number"/> at the top of the list.</summary>
    internal void PickChapter(int number)
    {
        if (!_sections.Any(section => section.Number == number))
        {
            return;
        }

        _chapter = number;
        _beat = -1;
        ShowPickers();
        ScrollTo(_rows.FindIndex(row => row is StoryRunSection section && section.Number == number));
    }

    /// <summary>Shows the first line of beat <paramref name="beat"/> of the chosen chapter at the top of the list.</summary>
    internal void PickBeat(int beat)
    {
        var section = _sections.FirstOrDefault(found => found.Number == _chapter);

        if (section?.Entries.FirstOrDefault(entry => entry.Beat == beat) is not { } first)
        {
            return;
        }

        _beat = beat;
        ShowPickers();
        ScrollTo(_rows.IndexOf(first));
    }

    private void ScrollTo(int index)
    {
        if (index < 0)
        {
            return;
        }

        if (_list.ContainerFromIndex(index) is null)
        {
            _list.ScrollIntoView(index);
            _list.UpdateLayout();
        }

        if (_list.ContainerFromIndex(index) is { } container && container.TranslatePoint(default, _list) is { } at)
        {
            _scroll.Offset = new Vector(_scroll.Offset.X, Math.Max(0, at.Y));
        }
    }

    // ---- the lines -------------------------------------------------------------------------

    private static StackPanel SectionHead(StoryRunSection section)
    {
        var head = StarSystemPage.GroupHead(
            ChapterName(section), string.Empty, section.Date is { } date ? StoryBenchPage.Day(date) : null);
        head.Margin = new Thickness(0, section.Number <= 1 ? 0 : 22, 0, 0);
        return head;
    }

    private Border LineRow(StoryRunEntry entry)
    {
        var row = new Border { BorderThickness = new Thickness(0, 0, 0, 1) };
        ThemeInk.Bind(row, Border.BorderBrushProperty, ThemeManager.Line2Key);
        _lineRows[entry] = row;
        Fill(row, entry);
        return row;
    }

    /// <summary>Draws a line's row again if the list holds one, so the playing mark follows the player.</summary>
    private void Refill(StoryRunEntry? entry)
    {
        if (entry is not null && _lineRows.TryGetValue(entry, out var row))
        {
            Fill(row, entry);
        }
    }

    private void Fill(Border row, StoryRunEntry entry)
    {
        var line = entry.Line;
        var playback = Playback;
        var playing = playback is not null && SameLine(playback.Line, line);

        var who = Typeset.Chrome(MessagesView.Sender(line.From), TypeScale.ControlLarge, ThemeManager.WhiteKey, weight: FontWeight.Medium);
        var kind = Typeset.Mono(entry.Kind.ToUpperInvariant(), TypeScale.Caption, ThemeManager.GreyKey);
        var time = Typeset.Mono(StoryBenchPage.Minute(line.Sent), TypeScale.Meta, ThemeManager.GreyKey);
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { who, kind, time } };

        if (playing)
        {
            head.Children.Add(Typeset.Chrome(
                PlayingMark.ToUpperInvariant(), TypeScale.MetaSmall, ThemeManager.CyanKey,
                weight: FontWeight.SemiBold, tracking: TypeScale.MetaSmall * Fonts.ChromeTracking, vertical: VerticalAlignment.Center));
        }

        var text = AdventuresPage.Text(line.Body, TypeScale.Body);
        text.Margin = new Thickness(0, 4, 0, 0);

        var words = new StackPanel { Children = { head, text } };

        if (line.Clip is null && _surface.RunPlayer is not null)
        {
            var note = Typeset.Prose(NoClipNote, TypeScale.Meta, ThemeManager.GreyKey);
            note.Name = "NoClipNote";
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 6, 0, 0);
            words.Children.Add(note);
        }
        var pictured = _entries.Pictured(words, StoryRunPlayer.PictureOf(line));

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 14 };
        grid.Children.Add(pictured);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Top };

        if (!playing && _surface.RunPlayer is { } idle && _run is { } run)
        {
            var button = new Button { Name = "PlayLine", Content = "Play", MinHeight = TypeScale.MinimumTarget };
            button.Click += (_, _) =>
            {
                _status.Clear();
                idle.Play(run, [line], 0);
            };
            actions.Children.Add(button);

            var all = new Button { Name = "PlayAllLine", Content = PlayFromHereLabel, MinHeight = TypeScale.MinimumTarget };
            all.Click += (_, _) => PlayAllFrom(entry);
            actions.Children.Add(all);
        }

        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        // A 3px left edge, cyan on cyan ground while the line plays and clear otherwise.
        var mark = new Border { Padding = new Thickness(8, 10, 0, 10), BorderThickness = new Thickness(3, 0, 0, 0), Child = grid };

        if (playing)
        {
            mark.Name = "PlayingLine";
            ThemeInk.Bind(mark, Border.BackgroundProperty, ThemeManager.CyanGroundKey);
            ThemeInk.Bind(mark, Border.BorderBrushProperty, ThemeManager.CyanKey);
        }

        row.Child = mark;
    }

    // ---- no lines --------------------------------------------------------------------------

    /// <summary>A chapter of a run with no recorded lines, with the text it was told in.</summary>
    internal sealed record KeptChapter(StoryRunSection Section, DateTimeOffset? Date, string Text);

    private List<KeptChapter> KeptChapters(string? commander, Story run)
    {
        var kept = new List<KeptChapter>();

        for (var index = 0; index < run.Chapters.Count; index++)
        {
            var chapter = ChapterOf(commander, run, run.Chapters[index]);
            var told = chapter?.Told.Where(entry => entry.Kind == AdventureToldKind.Beat).ToList() ?? [];
            var date = chapter?.AcceptedAt ?? (told.Count > 0 ? told[0].At : null);

            kept.Add(new KeptChapter(
                new StoryRunSection(index + 1, run.Chapters[index], chapter?.Name, chapter, []),
                date,
                string.Join("\n\n", told.Select(entry => entry.Text))));
        }

        return kept;
    }

    private static StackPanel KeptChapterView(KeptChapter kept)
    {
        var holder = new StackPanel { Spacing = 8 };
        var head = StarSystemPage.GroupHead(ChapterName(kept.Section), string.Empty, kept.Date is { } day ? StoryBenchPage.Day(day) : null);
        holder.Margin = new Thickness(0, kept.Section.Number <= 1 ? 0 : 22, 0, 0);
        holder.Children.Add(head);

        if (kept.Text.Length > 0)
        {
            holder.Children.Add(AdventuresPage.Text(kept.Text, TypeScale.Body));
        }

        return holder;
    }
}
