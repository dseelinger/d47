using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Publish and Unpublish buttons on a codex entry's page (#1371).</summary>
[Trait("Category", "Integration")]
public class PublishAndUnpublishShowOnlyWithATokenTests
{
    private const string Folder = @"C:\d47\data\codex-pictures";

    private static readonly CodexPictureKey Key = new(101, 18);

    private static readonly byte[] Picture = [0xFF, 0xD8, 0xFF, 0xD9];

    /// <summary>Counts every request and answers 404.</summary>
    private sealed class Counting : HttpMessageHandler
    {
        public ConcurrentQueue<string> Asked { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.Enqueue(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class Surface
    {
        public MemoryFileSystem Files { get; } = new();

        public Counting GitHub { get; } = new();

        public PanelNavigator Nav { get; } = new();

        public CodexPictureStore Store { get; }

        public CodexPicturePublisher Publisher { get; }

        public CodexPictureSet Pictures { get; }

        public Surface(bool token = true, Func<string?>? read = null)
        {
            Store = new CodexPictureStore(Files, Folder, () => true, NullLogger.Instance, new Counting(), commander: () => "F1");
            Publisher = new CodexPicturePublisher(Store, read ?? (() => token ? "t" : null), () => token, NullLogger.Instance, GitHub);
            Pictures = new CodexPictureSet(Store, () => true, Files, @"C:\d47\placeholders", null, Publisher);
            Nav.Register(PanelTab.Commander, new NavCrumb("ledgers", "Ledgers"));
            Nav.Select(PanelTab.Commander);
            Nav.Drill(CodexPage.EntryCrumb(18, 101, "Entry 101"));
        }

        public void OwnPicture() => Files.WriteBytes(Path.Combine(Folder, CodexPictureStore.LocalFolder, Key.FileName), Picture);

        public void Stock()
        {
            var sha = Convert.ToHexStringLower(SHA256.HashData(Picture));
            var row = new CodexPictureRow(Key.EntryId, Key.RegionId, CodexPictureIndex.FirstTag, CodexPictureIndex.AssetName(Key.EntryId, Key.RegionId, sha), sha, Picture.Length);
            Files.WriteText(Path.Combine(Folder, CodexPictureStore.StockFolder, CodexPictureIndex.IndexName), CodexPictureIndex.Write([row]));
            Files.WriteBytes(Path.Combine(Folder, CodexPictureStore.StockFolder, row.Asset), Picture);
        }

        public CodexEntryPage Page(bool headset = false, Func<string, Task<bool>>? confirm = null) =>
            new(() => new CommanderGameState(new CommanderIdentity("F1", "Test")), 101, 18, "Entry 101", Pictures, headset, null, Nav, confirm);
    }

    private static (Window Window, T Page) Open<T>(Func<T> make)
        where T : Control
    {
        var look = AppLook.Put();
        var page = make();
        var window = new Window
        {
            Content = page,
            Width = 1280,
            Height = 900,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Closed += (_, _) => look.Dispose();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page);
    }

    private static Button? Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == name);

    private static bool Says(Control root, string text) =>
        root.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == text);

    private static void Press(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void WithATokenTheOwnPictureOffersPublishAndTheStockOneUnpublish()
    {
        var surface = new Surface();
        surface.OwnPicture();
        surface.Stock();

        var (window, page) = Open(() => surface.Page());

        Assert.Equal("Publish", Named(page, "CodexPublish")?.Content);
        Assert.Equal("Unpublish", Named(page, "CodexUnpublish")?.Content);

        window.Close();
    }

    [AvaloniaFact]
    public void WithNoTokenNeitherShows()
    {
        var surface = new Surface(token: false);
        surface.OwnPicture();
        surface.Stock();

        var (window, page) = Open(() => surface.Page());

        Assert.Null(Named(page, "CodexPublish"));
        Assert.Null(Named(page, "CodexUnpublish"));

        window.Close();
    }

    [AvaloniaFact]
    public void OnTheBigPanelNeitherShows()
    {
        var surface = new Surface();
        surface.OwnPicture();
        surface.Stock();

        var (window, page) = Open(() => surface.Page(headset: true));

        Assert.Null(Named(page, "CodexPublish"));
        Assert.Null(Named(page, "CodexUnpublish"));

        window.Close();
    }

    [AvaloniaFact]
    public void PublishNeedsThePicturesOwnAndUnpublishNeedsAStockOne()
    {
        var surface = new Surface();
        surface.Stock();

        var (window, page) = Open(() => surface.Page());

        Assert.Null(Named(page, "CodexPublish"));
        Assert.NotNull(Named(page, "CodexUnpublish"));

        window.Close();
    }

    [AvaloniaFact]
    public void CancellingTheUnpublishQuestionSendsNothing()
    {
        var surface = new Surface();
        surface.Stock();
        string? asked = null;

        var (window, page) = Open(() => surface.Page(confirm: question =>
        {
            asked = question;
            return Task.FromResult(false);
        }));

        Press(Named(page, "CodexUnpublish")!);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(CodexEntryPage.UnpublishQuestion("Entry 101", 18), asked);
        Assert.StartsWith("Remove the stock picture of Entry 101 in ", asked, StringComparison.Ordinal);
        Assert.EndsWith(" for every Commander? Pictures they saved themselves are kept.", asked, StringComparison.Ordinal);
        Assert.Empty(surface.GitHub.Asked);
        Assert.Null(surface.Publisher.Last);

        window.Close();
    }

    [AvaloniaFact]
    public async Task AnsweringYesUnpublishes()
    {
        var surface = new Surface();
        surface.Stock();

        var (window, page) = Open(() => surface.Page(confirm: _ => Task.FromResult(true)));

        Press(Named(page, "CodexUnpublish")!);

        for (var i = 0; i < 200 && surface.Publisher.Last is null; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.NotEmpty(surface.GitHub.Asked);
        Assert.Equal(Key, surface.Publisher.Last?.Key);

        window.Close();
    }

    [AvaloniaFact]
    public async Task WhileOneRunsBothButtonsAreDisabledAndThePageSaysSo()
    {
        var gate = new ManualResetEventSlim();
        var surface = new Surface(read: () =>
        {
            gate.Wait(TestContext.Current.CancellationToken);
            return "t";
        });
        surface.OwnPicture();
        surface.Stock();

        var (window, page) = Open(() => surface.Page());
        var running = surface.Publisher.Publish(Key)!;
        Dispatcher.UIThread.RunJobs();

        Assert.False(Named(page, "CodexPublish")!.IsEnabled);
        Assert.False(Named(page, "CodexUnpublish")!.IsEnabled);
        Assert.True(Says(page, CodexPicturePublisher.PublishingText));

        gate.Set();
        await running;
        Dispatcher.UIThread.RunJobs();

        Assert.True(Named(page, "CodexUnpublish")!.IsEnabled);
        Assert.False(Says(page, CodexPicturePublisher.PublishingText));

        window.Close();
    }
}
