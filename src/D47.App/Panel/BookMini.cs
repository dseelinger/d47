using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.App.Voice;

namespace D47.App.Panel;

/// <summary>The book on the mini panel: its title and place, the sentence being read, the progress bar and a hint. No buttons.</summary>
public sealed class BookMini : UserControl
{
    public const string ReadingHint = "Say “pause the book” or “next chapter”.";

    public const string PausedHint = "Say “resume the book”.";

    public const string NoBook = "No book is being read.";

    private readonly BooksHost _host;
    private readonly StackPanel _body = new() { Spacing = 8 };

    public BookMini(BooksHost host)
    {
        _host = host;

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
        _host.Player.Changed += OnChanged;
        Fill();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _host.Player.Changed -= OnChanged;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Fill);

    private void Fill()
    {
        _body.Children.Clear();

        if (_host.Player.Now is not { } now)
        {
            _body.Children.Add(AdventuresPage.Muted(NoBook));
            return;
        }

        var place = $"CH {now.Place.Chapter + 1} OF {now.Book.Chapters.Count}";

        _body.Children.Add(Typeset.Chrome($"{now.Book.Title} · {place}", 13, ThemeManager.GreyKey, wrap: true));
        _body.Children.Add(Typeset.Prose(now.Text, 15, now.Waiting ? ThemeManager.GreyKey : ThemeManager.WhiteKey, wrap: true));

        var percent = BooksKit.Percent(now.Book, now.Place);
        var gauge = Gauge.Track(percent / 100.0, ThemeManager.AKey);
        AutomationProperties.SetName(gauge, $"{percent}% of the book read");
        _body.Children.Add(gauge);

        _body.Children.Add(Typeset.Prose(now.Pause == StoryRunPause.Commander ? PausedHint : ReadingHint, 13, ThemeManager.GreyKey, wrap: true));
    }
}
