using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Commander › Ledgers › Codex › a region › an entry › Choose a picture: the kept captures to cut a codex picture from.</summary>
public sealed class CodexChoosePage : UserControl, IPageSummary
{
    private const int ThumbnailWidth = 80;

    private const int ThumbnailHeight = 45;

    private readonly CodexPictureKey _key;
    private readonly CodexPictureSet _pictures;
    private readonly PanelNavigator _nav;
    private readonly CodexPictureWork _work;
    private readonly StackPanel _body = new() { Spacing = 14 };

    public CodexChoosePage(CodexPictureKey key, CodexPictureSet pictures, PanelNavigator nav, CodexPictureWork work)
    {
        _key = key;
        _pictures = pictures;
        _nav = nav;
        _work = work;

        Margin = new Thickness(14);
        Content = LoadoutPages.Scrolling(_body);
    }

    public string Summary => CodexPage.SummaryText;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    /// <summary>The panel keeps a level once built, so the captures are listed again each time the page is shown.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Draw();
    }

    private void Draw()
    {
        var captures = _pictures.Captures(_key);
        var thumbnails = new List<(Image Slot, string Path)>();

        _body.Children.Clear();

        if (captures.Count == 0)
        {
            _body.Children.Add(CreditsKit.Message(CodexModel.NoCapturesText));
        }
        else
        {
            Group(CodexModel.TakenWithHead, captures.Where(capture => capture.Noted).ToList(), thumbnails);
            Group(CodexModel.OtherCapturesHead, captures.Where(capture => !capture.Noted).ToList(), thumbnails);
        }

        var back = new Button { Name = "CodexChooseBack", Content = "Back", HorizontalAlignment = HorizontalAlignment.Left };
        back.Click += (_, _) => _nav.Back();
        _body.Children.Add(back);

        if (thumbnails.Count > 0)
        {
            _ = Task.Run(() => LoadThumbnails(thumbnails));
        }
    }

    private void Group(string head, IReadOnlyList<CodexCapture> captures, List<(Image, string)> thumbnails)
    {
        if (captures.Count == 0)
        {
            return;
        }

        var group = new StackPanel { Spacing = Gaps.Tile };
        group.Children.Add(StarSystemPage.GroupHead(head, CodexKit.Line(string.Empty), CodexModel.Number(captures.Count)));

        foreach (var capture in captures)
        {
            group.Children.Add(Row(capture, thumbnails));
        }

        _body.Children.Add(group);
    }

    private Button Row(CodexCapture capture, List<(Image, string)> thumbnails)
    {
        var slot = new Image { Width = ThumbnailWidth, Height = ThumbnailHeight, Stretch = Avalonia.Media.Stretch.UniformToFill };
        var thumbnail = new Border { Width = ThumbnailWidth, Height = ThumbnailHeight, ClipToBounds = true, Child = slot };
        ThemeInk.Bind(thumbnail, Border.BackgroundProperty, ThemeManager.SlabKey);
        thumbnails.Add((slot, capture.Path));

        var text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                ListRow.Name(new TextBlock { Text = capture.System ?? CodexModel.UnknownSystemText }),
                ListRow.Sub(new TextBlock { Text = capture.Body ?? string.Empty, IsVisible = capture.Body is not null }),
            },
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 12 };
        CreditsKit.Place(grid, 0, thumbnail);
        CreditsKit.Place(grid, 1, text);
        CreditsKit.Place(grid, 2, CreditsKit.Mono(CodexModel.DateTime(capture.At), TypeScale.Small, ThemeManager.WhiteKey, right: true));
        CreditsKit.Place(grid, 3, ListRow.Aside(new TextBlock { Text = "›" }));

        var button = ListRow.Dress(new Button
        {
            Name = "CodexCapture." + Path.GetFileNameWithoutExtension(capture.Path),
            Content = grid,
            MinHeight = TypeScale.MinimumTarget,
            Padding = new Thickness(14, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        Avalonia.Automation.AutomationProperties.SetName(button, CodexModel.DateTime(capture.At));
        button.Click += (_, _) =>
        {
            _work.Capture = capture.Path;
            _nav.Drill(CodexPage.CropCrumb(_key, kept: false));
        };

        return button;
    }

    private void LoadThumbnails(List<(Image Slot, string Path)> thumbnails)
    {
        foreach (var (slot, path) in thumbnails)
        {
            if (_pictures.CaptureBytes(path) is not { } bytes)
            {
                continue;
            }

            Bitmap? small = null;

            try
            {
                using var stream = new MemoryStream(bytes);
                small = Bitmap.DecodeToWidth(stream, ThumbnailWidth * 2);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
            {
                continue;
            }

            Dispatcher.UIThread.Post(() => slot.Source = small);
        }
    }
}
