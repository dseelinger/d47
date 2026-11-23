using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using D47.App.Controls;
using D47.App.Theming;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Books;
using D47.Core.Configuration;

namespace D47.App.Panel;

/// <summary>The parts Media › Books draws on its root page and on a book's page.</summary>
internal static class BooksKit
{
    public const string PauseName = "BookPause";
    public const string BackName = "BookBack";
    public const string NextName = "BookNext";
    public const string ChaptersName = "BookChapters";
    public const string NowName = "BookNow";
    public const string RetryName = "BookRetry";
    public const string VoiceSettingsName = "BookVoiceSettings";
    public const string PreviousPageName = "BookPreviousPage";
    public const string NextPageName = "BookNextPage";

    public const string NoVoiceLabel = "No voice on this computer";

    public const string NoVoiceText = "D47 reads books only with Kokoro or Chatterbox, which run on this computer. Install either one in Voice settings and your shelf reads.";

    public const string StoppedText = "The book is paused on this line. D47 reads books only with a voice on this computer, so no other voice took over.";

    public const string CommanderPaused = "Paused. Press Resume.";

    public const string PushToTalkPaused = "Paused while you speak. The book goes on from this line when D47 has answered.";

    public const string CalloutPaused = "Paused for a callout. The book goes on from this line when it ends.";

    /// <summary>The voice settings place.</summary>
    public const string VoicePlace = "voice";

    public static Button Tile(string name, string text, string accessibleName)
    {
        var tile = new Button
        {
            Name = name,
            Content = text,
            MinHeight = TypeScale.MinimumTarget,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(tile, accessibleName);

        return tile;
    }

    /// <summary>Where a book came from, as a shelf row says it.</summary>
    public static string SourceLabel(BookSource source) =>
        source.Gutenberg is { } number ? $"Project Gutenberg #{number}" : $"from a file · {source.File}";

    /// <summary>How far through the book <paramref name="place"/> is, by paragraphs, as a whole percent.</summary>
    public static int Percent(Book book, BookPlace place)
    {
        var total = 0;
        var before = 0;

        for (var chapter = 0; chapter < book.Chapters.Count; chapter++)
        {
            var count = book.Chapters[chapter].Paragraphs.Count;

            if (chapter < place.Chapter)
            {
                before += count;
            }
            else if (chapter == place.Chapter)
            {
                before += Math.Clamp(place.Paragraph, 0, count);
            }

            total += count;
        }

        return total == 0 ? 0 : Math.Clamp(before * 100 / total, 0, 100);
    }

    /// <summary>How far through the book an entry is by chapters, without opening the book.</summary>
    public static int ChapterPercent(ShelfBook entry) =>
        entry.Chapters == 0 ? 0 : Math.Clamp(entry.Place.Chapter * 100 / entry.Chapters, 0, 100);

    /// <summary>The shelf row's mono place: <c>CH 9 OF 28 · 31%</c>, <c>NOT STARTED</c> or <c>✓ FINISHED</c>.</summary>
    public static string PlaceOf(ShelfBook entry, BookPlayback? now)
    {
        if (now is not null && now.Book.Source == entry.Source)
        {
            return WithPlace(ChapterNumbering.Place(now.Place.Chapter, now.Book.Chapters.Count, now.Book.FrontMatter), $"{Percent(now.Book, now.Place)}%");
        }

        if (entry.Finished)
        {
            return "✓ FINISHED";
        }

        return entry.Place == new BookPlace(0, 0)
            ? "NOT STARTED"
            : WithPlace(ChapterNumbering.Place(entry.Place.Chapter, entry.Chapters, entry.FrontMatter), $"{ChapterPercent(entry)}%");
    }

    /// <summary>The chapter place and the progress joined; the progress alone when the chapter has no number.</summary>
    public static string WithPlace(string? chapter, string progress) => chapter is null ? progress : $"{chapter} · {progress}";

    /// <summary>The mono line under a sentence: place, progress and the voice, or <c>PAUSED</c>.</summary>
    public static string MonoLine(BooksHost host, BookPlayback now)
    {
        var settings = host.Settings;
        var place = WithPlace(ChapterNumbering.Place(now.Place.Chapter, now.Book.Chapters.Count, now.Book.FrontMatter), $"{Percent(now.Book, now.Place)}%");
        var provider = TtsProviderCatalog.Selected(settings.Provider).Name;

        return now.Pause == StoryRunPause.Commander
            ? $"{place} · PAUSED"
            : $"{place} · {provider} · {host.VoiceName(settings)}".ToUpperInvariant();
    }

    /// <summary>The sentence under the status line for a pause or a wait, or null while reading.</summary>
    public static string? PauseSentence(BookPlayback now) => now.NotArrived ? BookPlayer.RestNotArrived : now.Pause switch
    {
        StoryRunPause.Commander => CommanderPaused,
        StoryRunPause.PushToTalk => PushToTalkPaused,
        StoryRunPause.Interrupted => CalloutPaused,
        _ => null,
    };

    /// <summary>Puts what <paramref name="now"/> says about itself, if anything, into <paramref name="line"/>.</summary>
    public static void Say(StatusLine line, BooksHost host, BookPlayback? now)
    {
        if (now is null || now.Problem is not null)
        {
            line.Clear();
        }
        else if (PauseSentence(now) is { } paused)
        {
            line.Say(paused);
        }
        else if (now.Waiting)
        {
            line.Wait($"{TtsProviderCatalog.Selected(host.Settings.Provider).Name} is reading ahead…");
        }
        else
        {
            line.Clear();
        }
    }

    /// <summary>The slab: the sentence being read, a gauge and the mono line, headed by the title when <paramref name="titled"/>.</summary>
    public static Border NowBlock(BooksHost host, BookPlayback now, bool titled, bool big)
    {
        var stack = new StackPanel { Spacing = big ? 8 : 10 };

        if (titled)
        {
            stack.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children =
                {
                    Typeset.Chrome(now.Book.Title, 14, ThemeManager.WhiteKey, FontWeightMedium, vertical: VerticalAlignment.Center),
                    Typeset.Prose(
                        $"{now.Book.Author} · {now.Book.Chapters[now.Place.Chapter].Title}",
                        13,
                        ThemeManager.GreyKey,
                        vertical: VerticalAlignment.Center),
                },
            });
        }

        stack.Children.Add(Typeset.Prose(now.Text, 16, now.Waiting ? ThemeManager.GreyKey : ThemeManager.WhiteKey, wrap: true));

        var percent = Percent(now.Book, now.Place);
        var gauge = Gauge.Track(percent / 100.0, ThemeManager.AKey);
        AutomationProperties.SetName(gauge, $"{percent}% of the book read");
        stack.Children.Add(gauge);
        stack.Children.Add(Typeset.Mono(MonoLine(host, now), 12, ThemeManager.GreyKey, wrap: true));

        var slab = new Border { Name = NowName, Padding = big ? new Thickness(16, 12) : new Thickness(16), Child = stack };
        ThemeInk.Bind(slab, Border.BackgroundProperty, ThemeManager.SlabKey);

        return slab;
    }

    /// <summary>The red notice for a voice that stopped, with Retry and Voice settings.</summary>
    public static Notice Stopped(BooksHost host, BookPlayback now, Action openVoiceSettings)
    {
        var provider = TtsProviderCatalog.Selected(host.Settings.Provider).Name;
        var notice = new Notice(NoticeLevel.Error)
        {
            Label = $"{provider} stopped",
            Text = StoppedText,
            Detail = $"{provider} · {now.Problem}",
        };

        var retry = Tile(RetryName, "RETRY", "Retry");
        retry.MinHeight = TypeScale.MinimumTarget;
        retry.Click += (_, _) => host.Player.Resume();

        var settings = Tile(VoiceSettingsName, "VOICE SETTINGS", "Voice settings");
        settings.Click += (_, _) => openVoiceSettings();

        notice.Actions.Add(retry);
        notice.Actions.Add(settings);

        return notice;
    }

    /// <summary>The amber notice for a computer with neither local voice.</summary>
    public static Notice NoVoice(Action openVoiceSettings)
    {
        var notice = new Notice(NoticeLevel.Warning) { Label = NoVoiceLabel, Text = NoVoiceText };
        var settings = Tile(VoiceSettingsName, "VOICE SETTINGS", "Voice settings");

        settings.Click += (_, _) => openVoiceSettings();
        notice.Actions.Add(settings);

        return notice;
    }

    /// <summary>PAUSE, or RESUME while the Commander paused.</summary>
    public static string PauseText(BookPlayback now) => now.Pause == StoryRunPause.Commander ? "RESUME" : "PAUSE";

    public static void Pause(BooksHost host)
    {
        if (host.Player.Now is not { } now)
        {
            return;
        }

        if (now.Pause == StoryRunPause.Commander)
        {
            host.Player.Resume();
        }
        else
        {
            host.Player.Pause(StoryRunPause.Commander);
        }
    }

    /// <summary>Previous and Next tiles over a mono range, for a list drawn a page at a time.</summary>
    public static Grid Pager(int first, int pageSize, int count, Action<int> moveTo)
    {
        var previous = Tile(PreviousPageName, "◄ PREVIOUS", "Previous books");
        previous.IsEnabled = first > 0;
        previous.Click += (_, _) => moveTo(Math.Max(0, first - pageSize));

        var next = Tile(NextPageName, "NEXT ►", "More books");
        next.IsEnabled = first + pageSize < count;
        next.Click += (_, _) => moveTo(first + pageSize);

        var range = Typeset.Mono(
            count == 0 ? "0" : $"{first + 1}–{Math.Min(count, first + pageSize)} of {count}",
            12,
            ThemeManager.GreyKey,
            vertical: VerticalAlignment.Center,
            margin: new Thickness(12, 0, 0, 0));

        Grid.SetColumn(previous, 1);
        Grid.SetColumn(next, 2);

        return new Grid
        {
            ColumnSpacing = Gaps.Tile,
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)],
            Margin = new Thickness(0, 2, 0, 0),
            Children = { range, previous, next },
        };
    }

    public static Avalonia.Media.FontWeight FontWeightMedium => Avalonia.Media.FontWeight.Medium;
}
