using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Podcasts;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Media › Podcasts lists the shows and their episodes, plays one, removes a show and adds one by address (#2659).</summary>
public class ThePodcastsPageListsShowsAndEpisodesTests
{
    private static readonly Uri FeedOne = new("https://example.test/one.xml");

    private static readonly Uri FeedTwo = new("https://example.test/two.xml");

    private static PodcastEpisode Episode(string id, int day) =>
        new(id, $"Episode {id}", new DateTimeOffset(2026, 11, day, 0, 0, 0, TimeSpan.Zero), new Uri($"https://cdn.example.test/{id}.mp3"), "audio/mpeg", null, TimeSpan.FromMinutes(40));

    private sealed class Fixture
    {
        public SettingsService Settings { get; } = TestSurface.Settings();

        public List<Uri> Feeds { get; } = [FeedOne, FeedTwo];

        public List<Uri> Removed { get; } = [];

        public int Refreshes { get; set; }

        public List<Uri> Added { get; } = [];

        public Dictionary<string, PodcastShowPlay> Plays { get; } = [];

        public List<(bool? Continuous, PodcastOrder? Order)> Sets { get; } = [];

        public List<Uri> PlayedShows { get; } = [];

        public PodcastsHost Host { get; }

        public Fixture(bool fail = false)
        {
            var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
            var music = new AmbientMusic(arbiter, () => CueLibrary.Load());
            var player = new PodcastPlayer(arbiter, music, episode => new MusicTrack(episode.Id, () => throw new IOException("no audio")), new MemoryFileSystem(), @"C:\d47\listening.json", NullLogger<PodcastPlayer>.Instance, work => work());

            CachedShow[] shows =
            [
                new(FeedOne, new PodcastShow("Deep Space Radio", [Episode("a", 21), Episode("b", 14), Episode("c", 7)])),
                new(FeedTwo, new PodcastShow("Coriolis Commons", [Episode("x", 3)])),
            ];

            Host = new PodcastsHost(
                Settings,
                player,
                () => [.. shows.Where(show => Feeds.Contains(show.Feed))],
                () => Feeds,
                () =>
                {
                    Refreshes++;
                    IReadOnlyList<PodcastRefresh> results = fail ? [new PodcastRefresh(FeedTwo, null, "Coriolis Commons did not answer.")] : [];
                    return Task.FromResult(results);
                },
                _ => Task.FromResult(new PodcastSearchOutcome([], null)),
                feed =>
                {
                    Added.Add(feed);

                    return Task.FromResult(feed.Host == "suggested.test"
                        ? new PodcastRefresh(feed, new PodcastShow("Added Show", [Episode("s", 1)]), null)
                        : new PodcastRefresh(feed, null, "This is not an RSS feed."));
                },
                feed =>
                {
                    Removed.Add(feed);
                    Feeds.Remove(feed);
                },
                feed => Plays.TryGetValue(feed.AbsoluteUri, out var play) ? play : new PodcastShowPlay { Feed = feed.AbsoluteUri },
                (feed, continuous, order) =>
                {
                    Sets.Add((continuous, order));
                    var current = Plays.TryGetValue(feed.AbsoluteUri, out var play) ? play : new PodcastShowPlay { Feed = feed.AbsoluteUri };
                    Plays[feed.AbsoluteUri] = current with { Continuous = continuous ?? current.Continuous, Order = order ?? current.Order };
                    Host!.RaiseChanged();
                },
                feed =>
                {
                    PlayedShows.Add(feed);
                    return Task.FromResult("Every episode of Deep Space Radio is finished.");
                });
        }
    }

    private static (Window Window, PanelView Panel) Open(Fixture fixture)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableMedia(fixture.Host);

        var window = new Window { Content = panel, Width = 1280, Height = 900 };

        window.Show();
        panel.Tab = PanelTab.Media;
        Assert.True(panel.Nav.SelectRoot(MediaFurnishing.PodcastsRoot));
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static IReadOnlyList<string> Drawn(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static Button Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().First(button => button.Name == name);

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheRootListsEachShowWithItsNewCountAndTheSelectedShowsEpisodes()
    {
        var (window, panel) = Open(new Fixture());
        var drawn = Drawn(panel);

        Assert.Contains("SHOWS · 2", drawn);
        Assert.Contains("DEEP SPACE RADIO", drawn);
        Assert.Contains("1 NEW · 1 EPISODE", drawn);
        Assert.Contains("1 NEW · 3 EPISODES", drawn);
        Assert.Contains("EPISODE A", drawn);
        Assert.Contains("NEW", drawn);
        Assert.Contains(NavLabel(panel), word => word == "Podcasts");
        window.SaveFrame("podcasts-desktop.png");

        window.Close();
    }

    private static IEnumerable<string> NavLabel(PanelView panel) => panel.Nav.Roots(PanelTab.Media).Select(root => root.Word);

    [AvaloniaFact]
    public void OpeningThePageRefreshesTheFeedsAndNamesOneThatFailed()
    {
        var fixture = new Fixture(fail: true);
        var (window, panel) = Open(fixture);

        Dispatcher.UIThread.RunJobs();

        Assert.True(fixture.Refreshes >= 1);
        Assert.Contains(panel.GetVisualDescendants().OfType<D47.App.Controls.Notice>(), notice => notice.IsVisible && notice.Text!.Contains("did not answer", StringComparison.Ordinal));

        window.Close();
    }

    [AvaloniaFact]
    public void NothingPlayingDisablesTheTransportAndSaysSo()
    {
        var (window, panel) = Open(new Fixture());

        Assert.False(Named(panel, PodcastsPage.PlayName).IsEnabled);
        Assert.False(Named(panel, PodcastsPage.NextName).IsEnabled);
        Assert.False(Named(panel, PodcastsPage.BackName).IsEnabled);
        Assert.Contains(PodcastsPage.NothingPlaying, Drawn(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void RemovingAShowAsksFirstThenForgetsIt()
    {
        var fixture = new Fixture();
        var (window, panel) = Open(fixture);

        Click(Named(panel, PodcastsPage.RemoveName));
        Assert.Empty(fixture.Removed);
        Assert.Contains(Drawn(panel), text => text.StartsWith("Remove Deep Space Radio and forget", StringComparison.Ordinal));

        Click(Named(panel, PodcastsPage.RemoveKeepName));
        Assert.Empty(fixture.Removed);

        Click(Named(panel, PodcastsPage.RemoveName));
        Click(Named(panel, PodcastsPage.RemoveConfirmName));

        Assert.Equal([FeedOne], fixture.Removed);
        Assert.DoesNotContain("DEEP SPACE RADIO", Drawn(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void AddAShowOpensItsPageAndAnAddressThatIsNotAFeedIsRefused()
    {
        var (window, panel) = Open(new Fixture());

        Click(Named(panel, PodcastsPage.AddName));
        Dispatcher.UIThread.RunJobs();

        var url = panel.GetVisualDescendants().OfType<TextBox>().First(box => box.Name == PodcastAddPage.UrlBoxName);
        var add = Named(panel, PodcastAddPage.UrlAddName);

        Assert.False(add.IsEnabled);

        url.Text = "https://example.org/podcast";
        Dispatcher.UIThread.RunJobs();
        Assert.True(add.IsEnabled);

        Click(add);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PodcastAddPage.NotAFeed, D47.App.Theming.FieldMessage.GetText(url));

        window.Close();
    }

    private static double Top(Visual control, Visual relativeTo) => control.TranslatePoint(default, relativeTo)!.Value.Y;

    private static D47.App.Controls.Level LevelOf(Control page) =>
        page.GetVisualDescendants().OfType<D47.App.Controls.Level>().First(level => level.Name == PodcastsPage.LevelName);

    private static void StepDown(D47.App.Controls.Level level)
    {
        level.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Left });
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(D47.App.Controls.Segment segment, int index)
    {
        segment.GetVisualDescendants().OfType<RadioButton>().ElementAt(index).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    private static void OpenAddPage(Control panel)
    {
        Click(Named(panel, PodcastsPage.AddName));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void AddAShowOffersTheSuggestedGroupsAndAddsAShowWithOnePress()
    {
        var fixture = new Fixture();
        var (window, panel) = Open(fixture);

        OpenAddPage(panel);

        var segment = panel.GetVisualDescendants().OfType<D47.App.Controls.Segment>().First(s => s.Name == PodcastAddPage.SuggestedName);
        var rows = () => panel.GetVisualDescendants().OfType<Border>().Count(row => row.Name == PodcastAddPage.SuggestionName);

        Assert.Equal(["Elite Dangerous", "Science fiction", "Science fact"], segment.ItemsSource);
        Assert.Equal(0, segment.SelectedIndex);
        Assert.Equal(3, rows());
        Assert.Contains(PodcastAddPage.SuggestedLine, Drawn(panel));
        Assert.Contains("3 SHOWS", Drawn(panel));
        Assert.Contains("15 SHOWS", Drawn(panel));
        Assert.Empty(fixture.Added);

        Press(segment, 2);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(15, rows());

        window.Close();
    }

    [AvaloniaFact]
    public void PressingAddOnASuggestedShowAddsItsFeedAndSaysSo()
    {
        var fixture = new Fixture();
        var (window, panel) = Open(fixture);

        OpenAddPage(panel);
        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == PodcastAddPage.SuggestionAddName));

        Assert.Equal([SuggestedPodcasts.Groups[0].Shows[0].Feed], fixture.Added);
        Assert.Contains(Drawn(panel), text => text.StartsWith("Lave Radio: an Elite Dangerous podcast could not be added.", StringComparison.Ordinal));

        window.Close();
    }

    [AvaloniaFact]
    public void ASuggestedShowThatIsAlreadyFollowedReadsFollowingInsteadOfAdd()
    {
        var fixture = new Fixture();

        fixture.Feeds.Add(SuggestedPodcasts.Groups[0].Shows[1].Feed);

        var (window, panel) = Open(fixture);

        OpenAddPage(panel);

        Assert.Equal(2, panel.GetVisualDescendants().OfType<Button>().Count(button => button.Name == PodcastAddPage.SuggestionAddName));
        Assert.Contains("FOLLOWING", Drawn(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void ThePanelPagesSuggestedShowsFourAtATimeAndSwitchingGroupReturnsToTheFirstPage()
    {
        var fixture = new Fixture();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableMedia(fixture.Host);

        var window = new Window { Content = panel, Width = 1280, Height = 900 };
        var page = new PodcastAddPage(fixture.Host, panel.Nav, big: true);

        window.Content = page;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var rows = () => page.GetVisualDescendants().OfType<Border>().Count(row => row.Name == PodcastAddPage.SuggestionName);

        Assert.Equal(3, rows());
        Assert.Contains("1–3 of 3", Drawn(page));

        var segment = page.GetVisualDescendants().OfType<D47.App.Controls.Segment>().First(s => s.Name == PodcastAddPage.SuggestedName);

        Press(segment, 2);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(4, rows());
        Assert.Contains("1–4 of 15", Drawn(page));
        Assert.False(Named(page, PodcastAddPage.SuggestedPreviousName).IsEnabled);

        Click(Named(page, PodcastAddPage.SuggestedNextName));
        Assert.Contains("5–8 of 15", Drawn(page));

        Press(segment, 1);
        Dispatcher.UIThread.RunJobs();
        Press(segment, 2);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("1–4 of 15", Drawn(page));

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThePodcastLevelBarSetsAndFollowsThePodcastChannelLevel(bool big)
    {
        var fixture = new Fixture();
        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = panel, Width = 1280, Height = 900 };
        var page = new PodcastsPage(fixture.Host, panel.Nav, big: big);

        window.Content = page;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var level = LevelOf(page);
        var before = fixture.Settings.Current.Audio.Podcast.Level;

        Assert.Contains("Podcast level", Drawn(page));
        Assert.Equal(before, level.Value, 3);
        Assert.False(level.Muted);

        StepDown(level);

        Assert.True(fixture.Settings.Current.Audio.Podcast.Level < before);
        Assert.Equal(fixture.Settings.Current.Audio.Podcast.Level, level.Value, 3);

        fixture.Settings.Apply(AudioCapability.LevelKey(AudioChannel.Podcast), "0.40", SettingsCaller.Hotkey);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0.4, level.Value, 3);

        fixture.Settings.Apply(AudioCapability.MuteKey(AudioChannel.Podcast), "true", SettingsCaller.Hotkey);
        Dispatcher.UIThread.RunJobs();
        Assert.True(level.Muted);
        Assert.True(level.IsEnabled);
        Assert.Equal(0.4, fixture.Settings.Current.Audio.Podcast.Level, 3);

        window.Close();
    }

    [AvaloniaFact]
    public void TheBigPanelTransportIsThreeLinesWithTheLevelOnTheTilesLine()
    {
        var fixture = new Fixture();
        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = panel, Width = 1280, Height = 900 };
        var page = new PodcastsPage(fixture.Host, panel.Nav, big: true);

        window.Content = page;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var seek = page.GetVisualDescendants().OfType<D47.App.Controls.SeekBar>().First();
        var level = LevelOf(page);
        var tiles = new[] { PodcastsPage.BackName, PodcastsPage.PlayName, PodcastsPage.ForwardName, PodcastsPage.NextName }.Select(name => Named(page, name)).ToList();

        Assert.All(tiles, tile => Assert.True(tile.Bounds.Width >= 120));
        Assert.All(tiles, tile => Assert.Equal(Top(tiles[0], page), Top(tile, page)));
        Assert.True(Top(level, page) >= Top(tiles[0], page) - 1);
        Assert.True(Top(tiles[0], page) > Top(seek, page));
        Assert.Equal(4, PodcastsPage.BigEpisodesPerPage);

        window.Close();
    }

    [AvaloniaFact]
    public void ABigPanelEpisodeRowShowsItsDateAndStaysFortyFourHigh()
    {
        var fixture = new Fixture();
        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = panel, Width = 1280, Height = 900 };
        var page = new PodcastsPage(fixture.Host, panel.Nav, big: true);

        window.Content = page;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var rows = page.GetVisualDescendants().OfType<Button>().Where(button => button.Name == PodcastsPage.EpisodeName).ToList();

        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.Equal(44, row.Bounds.Height));

        var drawn = page.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Inlines is { Count: > 0 } ? string.Concat(block.Inlines.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text)) : block.Text ?? string.Empty).ToList();

        Assert.Contains("21 NOV 2026", drawn);
        Assert.Contains("14 NOV 2026", drawn);
        Assert.Contains("07 NOV 2026", drawn);
        Assert.DoesNotContain("21 Nov 2026 · 0:40:00", drawn);

        window.Close();
    }

    private static CheckBox Continuous(Control root) =>
        root.GetVisualDescendants().OfType<CheckBox>().First(box => box.Name == PodcastsPage.ContinuousName);

    private static D47.App.Controls.Segment Order(Control root) =>
        root.GetVisualDescendants().OfType<D47.App.Controls.Segment>().First(segment => segment.Name == PodcastsPage.OrderName);

    [AvaloniaFact]
    public void AShowWithNoSettingReadsOffOldestFirstAndNamesTheKeptOrder()
    {
        var (window, panel) = Open(new Fixture());

        Assert.False(Continuous(panel).IsChecked);
        Assert.False(Order(panel).IsEnabled);
        Assert.Equal(0, Order(panel).SelectedIndex);
        Assert.Contains("Order kept for when it is on: oldest first", Drawn(panel));

        window.Close();
    }

    [AvaloniaFact]
    public void AShowOnNewestFirstEnablesTheOrderAndDropsTheKeptLine()
    {
        var fixture = new Fixture();

        fixture.Plays[FeedOne.AbsoluteUri] = new PodcastShowPlay { Feed = FeedOne.AbsoluteUri, Continuous = true, Order = PodcastOrder.NewestFirst };

        var (window, panel) = Open(fixture);

        Assert.True(Continuous(panel).IsChecked);
        Assert.True(Order(panel).IsEnabled);
        Assert.Equal(1, Order(panel).SelectedIndex);
        Assert.DoesNotContain(Drawn(panel), text => text.StartsWith("Order kept", StringComparison.Ordinal));

        window.Close();
    }

    [AvaloniaFact]
    public void ATickedBoxWritesTheSettingAndKeepsTheOrder()
    {
        var fixture = new Fixture();

        fixture.Plays[FeedOne.AbsoluteUri] = new PodcastShowPlay { Feed = FeedOne.AbsoluteUri, Order = PodcastOrder.NewestFirst };

        var (window, panel) = Open(fixture);

        Continuous(panel).IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal((true, null), fixture.Sets.Last());
        Assert.True(fixture.Plays[FeedOne.AbsoluteUri].Continuous);
        Assert.True(Order(panel).IsEnabled);
        Assert.Equal(1, Order(panel).SelectedIndex);

        window.Close();
    }

    [AvaloniaFact]
    public void APressedOrderWritesTheOrder()
    {
        var fixture = new Fixture();

        fixture.Plays[FeedOne.AbsoluteUri] = new PodcastShowPlay { Feed = FeedOne.AbsoluteUri, Continuous = true };

        var (window, panel) = Open(fixture);
        var newest = Order(panel).GetVisualDescendants().OfType<RadioButton>().Last();

        newest.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal((null, PodcastOrder.NewestFirst), fixture.Sets.Last());
        Assert.Equal(1, Order(panel).SelectedIndex);

        window.Close();
    }

    [AvaloniaFact]
    public void ASettingChangedFromOutsideRedrawsTheRow()
    {
        var fixture = new Fixture();
        var (window, panel) = Open(fixture);

        fixture.Plays[FeedOne.AbsoluteUri] = new PodcastShowPlay { Feed = FeedOne.AbsoluteUri, Continuous = true, Order = PodcastOrder.NewestFirst };
        fixture.Host.RaiseChanged();
        Dispatcher.UIThread.RunJobs();

        Assert.True(Continuous(panel).IsChecked);
        Assert.True(Order(panel).IsEnabled);
        Assert.Equal(1, Order(panel).SelectedIndex);

        window.Close();
    }
}
