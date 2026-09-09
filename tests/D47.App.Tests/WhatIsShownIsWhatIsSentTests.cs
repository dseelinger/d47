using System.IO.Compression;
using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Donation;
using D47.Core;
using D47.Core.Diagnostics.Donation;
using Xunit;

namespace D47.App.Tests;

/// <summary>The donation endpoint, driven from the button a Commander actually presses.</summary>
public class WhatIsShownIsWhatIsSentTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d47-send").FullName;

    private static readonly ExcerptPaperwork Paperwork = new(
        "0.89.0+abcdef", new DateTimeOffset(2026, 8, 29, 14, 25, 30, TimeSpan.Zero));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>
    /// Answers every request the same way and keeps the last one, so a test can read what actually went
    /// on the wire rather than what a caller meant to send.
    /// </summary>
    private sealed class Endpoint : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public Endpoint(HttpStatusCode status = HttpStatusCode.Created, string? body = null)
        {
            _status = status;
            _body = body ?? """{"ok":true,"key":"excerpts/token/object.md.gz"}""";
        }

        public HttpRequestMessage? Last { get; private set; }

        public byte[] Sent { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancel)
        {
            Last = request;
            Sent = request.Content is { } content
                ? await content.ReadAsByteArrayAsync(cancel)
                : [];

            return new HttpResponseMessage(_status) { Content = new StringContent(_body) };
        }
    }

    private DonationDispatch Dispatch(Endpoint endpoint, string? address = "https://donate.invalid") =>
        new(new AppPaths(_root), () => address, new DonationUpload(new HttpClient(endpoint)));

    private static string Ungzip(byte[] compressed)
    {
        using var gzip = new GZipStream(new MemoryStream(compressed), CompressionMode.Decompress);
        using var read = new StreamReader(gzip, new UTF8Encoding(false));
        return read.ReadToEnd();
    }

    private static string Header(HttpRequestMessage request, string name) =>
        request.Headers.GetValues(name).Single();

    private static T Control<T>(Window window, string name)
        where T : Avalonia.Controls.Control =>
        window.GetVisualDescendants().OfType<T>().Single(found => found.Name == name);

    private static HelpImproveWindow Shown(
        Func<ExcerptRequest, string> build,
        Func<string, CancellationToken, Task<DonationSent>>? send = null,
        string? destination = null)
    {
        var window = new HelpImproveWindow(
            new DateTimeOffset(2026, 8, 29, 14, 0, 0, TimeSpan.Zero), build, send, destination);

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    /// <summary>The whole claim, end to end.</summary>
    [AvaloniaFact]
    public void TheBytesOnTheWireAreTheTextThatWasOnScreen()
    {
        var endpoint = new Endpoint();
        var dispatch = Dispatch(endpoint);

        var window = Shown(
            _ => "### Incident excerpt\nexactly this\n",
            (text, cancel) => dispatch.SendExcerptAsync(text, Paperwork, cancel),
            dispatch.Destination);

        Control<Button>(window, "SendExcerpt").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(window.Text, Ungzip(endpoint.Sent));
    }

    /// <summary>
    /// And the hash on the envelope covers those bytes, which is what a donor's receipt lets them check
    /// without taking anybody's word for it.
    /// </summary>
    [AvaloniaFact]
    public void TheHashOnTheEnvelopeCoversWhatArrived()
    {
        var endpoint = new Endpoint();
        var dispatch = Dispatch(endpoint);

        var window = Shown(
            _ => "### Incident excerpt\nsomething happened\n",
            (text, cancel) => dispatch.SendExcerptAsync(text, Paperwork, cancel),
            dispatch.Destination);

        Control<Button>(window, "SendExcerpt").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(endpoint.Last);
        Assert.Equal(
            DonationEnvelope.HashOf(Ungzip(endpoint.Sent)),
            Header(endpoint.Last, DonationEnvelope.Sha256Header));
    }

    /// <summary>The donation identifier is on the envelope and never in the body.</summary>
    [AvaloniaFact]
    public void TheTokenTravelsOutsideThePayload()
    {
        var endpoint = new Endpoint();
        var dispatch = Dispatch(endpoint);

        var window = Shown(
            _ => "### Incident excerpt\nno identity in here\n",
            (text, cancel) => dispatch.SendExcerptAsync(text, Paperwork, cancel),
            dispatch.Destination);

        Control<Button>(window, "SendExcerpt").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(endpoint.Last);

        var token = Header(endpoint.Last, DonationEnvelope.DonorHeader);

        Assert.True(DonorToken.IsWellFormed(token));
        Assert.DoesNotContain(token, Ungzip(endpoint.Sent), StringComparison.Ordinal);
        Assert.DoesNotContain(token, window.Text, StringComparison.Ordinal);
    }

    /// <summary>The token is minted by the send and not before.</summary>
    [AvaloniaFact]
    public void NoIdentifierExistsUntilSomethingIsSent()
    {
        var endpoint = new Endpoint();
        var dispatch = Dispatch(endpoint);
        var token = new AppPaths(_root).DonorTokenFile;

        var window = Shown(
            _ => "### Incident excerpt\n",
            (text, cancel) => dispatch.SendExcerptAsync(text, Paperwork, cancel),
            dispatch.Destination);

        Assert.False(File.Exists(token));

        Control<Button>(window, "SendExcerpt").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(File.Exists(token));
    }

    /// <summary>
    /// The receipt lands beside the executable, and the copy it keeps is the payload byte for byte —
    /// read back off the disk rather than asserted about the string that was passed in.
    /// </summary>
    [AvaloniaFact]
    public void ARecieptOfExactlyWhatLeftIsKept()
    {
        var endpoint = new Endpoint();
        var dispatch = Dispatch(endpoint);

        var window = Shown(
            _ => "### Incident excerpt\nkept beside the executable\n",
            (text, cancel) => dispatch.SendExcerptAsync(text, Paperwork, cancel),
            dispatch.Destination);

        Control<Button>(window, "SendExcerpt").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var kept = Directory.GetFiles(new AppPaths(_root).Donations, "*.md")
            .Single(file => !file.EndsWith(".receipt.md", StringComparison.Ordinal));

        Assert.Equal(window.Text, File.ReadAllText(kept));
    }

    /// <summary>With nowhere to send, the window is the window that shipped before #175.</summary>
    [AvaloniaFact]
    public void WithNoAddressThereIsNoSendButton()
    {
        var window = Shown(_ => "### Incident excerpt\n");

        Assert.DoesNotContain(
            window.GetVisualDescendants().OfType<Button>(),
            button => button.Name == "SendExcerpt");

        // And the two routes that never needed one are still there.
        Assert.Contains(
            window.GetVisualDescendants().OfType<Button>(),
            button => button.Name == "CopyExcerpt");
    }

    /// <summary>Nor is one offered for an address that could only send in the clear.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://donate.invalid")]
    [InlineData("donate.invalid")]
    [InlineData("ftp://donate.invalid")]
    public void OnlyAnHttpsAddressCanBeSentTo(string? address) =>
        Assert.False(Dispatch(new Endpoint(), address).CanSend);

    /// <summary>Changing the span throws the send away.</summary>
    [AvaloniaFact]
    public void ChangingWhatWouldLeaveMakesTheSendAFreshDecision()
    {
        var endpoint = new Endpoint();
        var dispatch = Dispatch(endpoint);
        var nth = 0;

        var window = Shown(
            _ => $"### Incident excerpt\nrender {++nth}\n",
            (text, cancel) => dispatch.SendExcerptAsync(text, Paperwork, cancel),
            dispatch.Destination);

        var send = Control<Button>(window, "SendExcerpt");

        send.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Sent", send.Content);
        Assert.False(send.IsEnabled);

        Control<CheckBox>(window, "IncludeMySpeech").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Send it", send.Content);
        Assert.True(send.IsEnabled);
    }

    /// <summary>
    /// The window no longer tells a donor to paste into an issue — not in the intro, and not on the
    /// button after a copy.
    /// </summary>
    [AvaloniaFact]
    public void NothingTellsADonorToPasteIntoAnIssue()
    {
        var window = Shown(_ => "### Incident excerpt\n");

        var words = Words(window);

        Assert.DoesNotContain("into the issue", words, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("paste it", words, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public void TheRetiredDestinationIsNotNamedAnywhereInTheWindow()
    {
        // Over the size warning as well as the resting state, since that is where it lived.
        var window = Shown(_ => "### Incident excerpt\n" + new string('x', 70_000));

        var words = Words(window);

        Assert.Contains("more than a person reads", words, StringComparison.Ordinal);
        Assert.DoesNotContain("GitHub", words, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("comment", words, StringComparison.OrdinalIgnoreCase);
    }

    private static string Words(HelpImproveWindow window) =>
        string.Join(
            "\n",
            window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text)
                .Concat(window.GetVisualDescendants().OfType<Button>().Select(b => b.Content as string)));

    /// <summary>The linkage claim is not on the window: a random number, a file name and a way to delete it are how the grouping works rather than what leaves, and a Commander standing at a consent notice cannot act on any of it.</summary>
    [AvaloniaFact]
    public void TheLinkageClaimMovedToTheNoticeAndIsNotOnTheWindow()
    {
        var window = Shown(_ => "### Incident excerpt\n");

        var intro = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Single(text => text.Contains("Only the text below", StringComparison.Ordinal));

        Assert.DoesNotContain("random number identifying this installation", intro, StringComparison.Ordinal);
        Assert.DoesNotContain("donor-token", intro, StringComparison.Ordinal);

        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var notice = File.ReadAllText(
            Path.Combine(directory.FullName, "docs", "donation-privacy.md"));

        Assert.Contains("derived from nothing about you", notice, StringComparison.Ordinal);
        Assert.Contains("data\\donor-token.txt", notice, StringComparison.Ordinal);
    }

    /// <summary>A refusal says so on screen.</summary>
    [AvaloniaFact]
    public void ARefusedDonationSaysSoRatherThanLookingLikeItWorked()
    {
        var endpoint = new Endpoint(HttpStatusCode.RequestEntityTooLarge, "too big");
        var dispatch = Dispatch(endpoint);

        var window = Shown(
            _ => "### Incident excerpt\n",
            (text, cancel) => dispatch.SendExcerptAsync(text, Paperwork, cancel),
            dispatch.Destination);

        var send = Control<Button>(window, "SendExcerpt");

        send.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Send it", send.Content);

        var said = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Single(text => text.Contains("too large", StringComparison.Ordinal));

        Assert.Contains("shorter span", said, StringComparison.Ordinal);
    }

    /// <summary>And its receipt says the same, rather than the send going unrecorded.</summary>
    [AvaloniaFact]
    public void ARefusalIsWrittenDownToo()
    {
        var endpoint = new Endpoint(HttpStatusCode.BadRequest, "no");
        var dispatch = Dispatch(endpoint);

        var window = Shown(
            _ => "### Incident excerpt\n",
            (text, cancel) => dispatch.SendExcerptAsync(text, Paperwork, cancel),
            dispatch.Destination);

        Control<Button>(window, "SendExcerpt").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var receipt = Directory.GetFiles(new AppPaths(_root).Donations, "*.receipt.md").Single();

        Assert.Contains("did not arrive", File.ReadAllText(receipt), StringComparison.Ordinal);
    }

    /// <summary>The request goes to <c>/donate</c> on the configured address and nowhere else.</summary>
    [Fact]
    public async Task ThePathIsFixedWhateverTheAddressSays()
    {
        var endpoint = new Endpoint();

        await Dispatch(endpoint, "https://donate.invalid/somewhere/")
            .SendExcerptAsync("### Incident excerpt\n", Paperwork, TestContext.Current.CancellationToken);

        Assert.NotNull(endpoint.Last);
        Assert.Equal("https://donate.invalid/somewhere/donate", endpoint.Last.RequestUri?.ToString());
    }

    /// <summary>The endpoint's own key is what the receipt names, not d47's guess at it.</summary>
    [Fact]
    public async Task TheStoresOwnAnswerNamesTheObject()
    {
        var sent = await Dispatch(new Endpoint(
                HttpStatusCode.Created,
                """{"ok":true,"key":"excerpts/deadbeef/named-by-the-store.md.gz"}"""))
            .SendExcerptAsync("### Incident excerpt\n", Paperwork, TestContext.Current.CancellationToken);

        Assert.True(sent.Outcome.Sent);
        Assert.Equal("excerpts/deadbeef/named-by-the-store.md.gz", sent.Outcome.Key);
    }

    /// <summary>
    /// And a reply d47 cannot parse does not turn a stored donation into a failed one — it falls back
    /// to the key the envelope predicts.
    /// </summary>
    [Fact]
    public async Task AnUnreadableReplyStillCountsAsStored()
    {
        var sent = await Dispatch(new Endpoint(HttpStatusCode.Created, "stored, thanks"))
            .SendExcerptAsync("### Incident excerpt\n", Paperwork, TestContext.Current.CancellationToken);

        Assert.True(sent.Outcome.Sent);
        Assert.StartsWith("excerpts/", sent.Outcome.Key, StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing is posted anywhere with no address set, and the sentence says what to do instead.
    /// </summary>
    [Fact]
    public async Task WithNoAddressNothingIsPostedAnywhere()
    {
        var endpoint = new Endpoint();

        var sent = await Dispatch(endpoint, address: null)
            .SendExcerptAsync("### Incident excerpt\n", Paperwork, TestContext.Current.CancellationToken);

        Assert.Null(endpoint.Last);
        Assert.False(sent.Outcome.Sent);
        Assert.False(File.Exists(new AppPaths(_root).DonorTokenFile));
    }
}
