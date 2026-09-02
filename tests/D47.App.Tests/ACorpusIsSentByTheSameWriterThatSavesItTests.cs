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

/// <summary>
/// The journal-history donation reaching the endpoint
/// (<a href="https://github.com/dseelinger/d47/issues/181">#181</a>).
/// <para>
/// <b>The gap this closes is one call wide, and it was the payload the whole endpoint was built to
/// carry.</b> #174 shipped the consent flow before there was a transport, and #175 built the
/// transport without reaching into #174's window — so a Commander could donate an excerpt with one
/// press and could only save a journal history to a file.
/// </para>
/// <para>
/// <b>What is asserted here is the corpus form of "what is shown is what leaves".</b> An excerpt
/// can be checked directly, because the string in the pane is the payload. A corpus cannot: what
/// was read is a report and what leaves is hundreds of thousands of lines. So the property that
/// stands in for it is that the bytes on the wire come out of <b>the same writer the Save button
/// uses</b> — one payload writer, two ways of asking for it — and that the hash on the envelope
/// covers exactly those bytes.
/// </para>
/// </summary>
public class ACorpusIsSentByTheSameWriterThatSavesItTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d47-corpus-send").FullName;

    private static readonly ExcerptPaperwork Paperwork = new(
        "0.90.0+abcdef", new DateTimeOffset(2026, 8, 29, 14, 25, 30, TimeSpan.Zero));

    /// <summary>What a fake corpus writer puts on the stream. Several lines, so gzip has work to do.</summary>
    private const string Payload =
        """{"timestamp":"2026-08-29T14:00:00Z","event":"FSDJump","StarSystem":"Sol"}"""
        + "\n"
        + """{"timestamp":"2026-08-29T14:05:00Z","event":"Docked","StationName":"Abraham Lincoln"}"""
        + "\n";

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>Answers every request the same way and keeps what actually went on the wire.</summary>
    private sealed class Endpoint : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public Endpoint(HttpStatusCode status = HttpStatusCode.Created, string? body = null)
        {
            _status = status;
            _body = body ?? """{"ok":true,"key":"corpus/token/object.jsonl.gz"}""";
        }

        public HttpRequestMessage? Last { get; private set; }

        public long? DeclaredLength { get; private set; }

        public byte[] Sent { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancel)
        {
            Last = request;

            if (request.Content is { } content)
            {
                DeclaredLength = content.Headers.ContentLength;
                Sent = await content.ReadAsByteArrayAsync(cancel);
            }

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

    /// <summary>
    /// The one writer both roads go through, standing in for <c>CorpusDonation.Write</c>. It
    /// reports a file count like the real one, so the progress path is driven too.
    /// </summary>
    private static Func<Stream, IProgress<int>, CancellationToken, Task> Writer(string payload) =>
        async (stream, progress, cancel) =>
        {
            progress.Report(1);

            // No BOM, exactly as the real writer is constructed — a byte order mark in front of
            // the first event is a payload that fails at line one.
            await using var writer = new StreamWriter(
                stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);

            await writer.WriteAsync(payload);
        };

    private static T Control<T>(Window window, string name)
        where T : Avalonia.Controls.Control =>
        window.GetVisualDescendants().OfType<T>().Single(found => found.Name == name);

    private static HelpImproveWindow.CorpusReading Reading(string report) =>
        new(new CorpusSurvey(null, null, 0, 0, new CorpusTally(0, 0, 0, 0, 0, 0), []), report);

    private static HelpImproveWindow Shown(
        string report = "### Journal history\nwhat you are agreeing to\n",
        Func<string, IProgress<DonationStep>, CancellationToken, Task<DonationSent>>? send = null,
        string? destination = null)
    {
        // The history half of the merged window (#238): with both of its delegates present the
        // toggle opens checked, so this is the flow on screen.
        var window = new HelpImproveWindow(
            new DateTimeOffset(2026, 8, 31, 14, 0, 0, TimeSpan.Zero),
            _ => string.Empty,
            destination: destination,
            read: (_, _, _) => Task.FromResult(Reading(report)),
            write: (_, _, _) => Task.CompletedTask,
            sendCorpus: send);

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static async Task PressAsync(HelpImproveWindow window, string button)
    {
        Control<Button>(window, button).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        await Task.Delay(50, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// <b>The bytes on the wire, decompressed, are what the writer wrote</b> — the writer being the
    /// same delegate the Save button hands a file stream.
    /// </summary>
    [Fact]
    public async Task TheBytesOnTheWireAreWhatTheSaveButtonWouldHaveWritten()
    {
        var endpoint = new Endpoint();

        await Dispatch(endpoint).SendCorpusAsync(
            "### Journal history\n",
            Writer(Payload),
            Paperwork,
            cancel: TestContext.Current.CancellationToken);

        Assert.Equal(Payload, Ungzip(endpoint.Sent));
    }

    /// <summary>
    /// <b>And the hash on the envelope covers exactly those bytes</b>, which is #181's stated
    /// requirement: whichever way the hashing question was settled, the receipt's hash must be of
    /// the bytes that left. It is taken as they stream past on the one pass that produced them.
    /// </summary>
    [Fact]
    public async Task TheHashCoversExactlyTheBytesThatLeft()
    {
        var endpoint = new Endpoint();

        await Dispatch(endpoint).SendCorpusAsync(
            "### Journal history\n",
            Writer(Payload),
            Paperwork,
            cancel: TestContext.Current.CancellationToken);

        Assert.NotNull(endpoint.Last);

        Assert.Equal(
            DonationEnvelope.HashOf(Ungzip(endpoint.Sent)),
            Header(endpoint.Last, DonationEnvelope.Sha256Header));

        // Of the payload and not of the wire, which is the whole reason the hash is taken above the
        // compressor: gzip output is not reproducible from its input across levels.
        Assert.NotEqual(
            DonationEnvelope.HashOf(endpoint.Sent),
            Header(endpoint.Last, DonationEnvelope.Sha256Header));
    }

    /// <summary>
    /// <b>The length is declared, and this is what settled the hashing question.</b> The endpoint
    /// refuses a donation that does not say how large it is — that is the hard stop the other two
    /// rest on — so the compressed bytes have to exist somewhere seekable before the request. Once
    /// they do, a second pass purely to hash would be reading the journals twice.
    /// </summary>
    [Fact]
    public async Task ThePayloadDeclaresItsLengthRatherThanArrivingChunked()
    {
        var endpoint = new Endpoint();

        await Dispatch(endpoint).SendCorpusAsync(
            "### Journal history\n",
            Writer(Payload),
            Paperwork,
            cancel: TestContext.Current.CancellationToken);

        Assert.NotNull(endpoint.DeclaredLength);
        Assert.Equal(endpoint.Sent.Length, endpoint.DeclaredLength);

        // And the counted size on the envelope is the payload's, not the compressed body's.
        Assert.NotNull(endpoint.Last);
        Assert.Equal(
            Encoding.UTF8.GetByteCount(Payload).ToString(),
            Header(endpoint.Last, DonationEnvelope.BytesHeader));
    }

    /// <summary>
    /// <b>The same two claims against a payload that does not fit in one buffer</b>, which is the
    /// only shape the real thing ever has. A corpus is hundreds of megabytes written a journal
    /// file at a time; a hash taken over the last chunk, or over a buffer that was reused, agrees
    /// with itself perfectly and is wrong — and it would pass every assertion above, where the
    /// whole payload fits in one write.
    /// </summary>
    [Fact]
    public async Task ThePayloadIsHashedWholeEvenWhenItArrivesInPieces()
    {
        var many = new StringBuilder();

        for (var nth = 0; nth < 20_000; nth++)
        {
            many.Append(
                """{"timestamp":"2026-08-29T14:00:00Z","event":"FSDJump","StarSystem":"Sol """)
                .Append(nth)
                .Append("\"}\n");
        }

        var payload = many.ToString();
        var endpoint = new Endpoint();

        var sent = await Dispatch(endpoint).SendCorpusAsync(
            "### Journal history\n",
            Writer(payload),
            Paperwork,
            cancel: TestContext.Current.CancellationToken);

        Assert.True(sent.Outcome.Sent);
        Assert.NotNull(endpoint.Last);

        // Well past any single buffer, and past the point where gzip has emitted several blocks.
        Assert.True(payload.Length > 1_000_000, "the payload has to outgrow one buffer to be a test");

        Assert.Equal(payload, Ungzip(endpoint.Sent));
        Assert.Equal(
            DonationEnvelope.HashOf(payload),
            Header(endpoint.Last, DonationEnvelope.Sha256Header));

        Assert.Equal(
            Encoding.UTF8.GetByteCount(payload).ToString(),
            Header(endpoint.Last, DonationEnvelope.BytesHeader));
    }

    /// <summary>
    /// It goes under the corpus prefix, because the two retention rules are opposite and a prefix
    /// is what a lifecycle rule is written against.
    /// </summary>
    [Fact]
    public async Task ItTravelsAsAJournalHistoryAndNotAsAnExcerpt()
    {
        var endpoint = new Endpoint();

        await Dispatch(endpoint).SendCorpusAsync(
            "### Journal history\n",
            Writer(Payload),
            Paperwork,
            cancel: TestContext.Current.CancellationToken);

        Assert.NotNull(endpoint.Last);
        Assert.Equal(DonationEnvelope.Corpus, Header(endpoint.Last, DonationEnvelope.KindHeader));
    }

    /// <summary>
    /// <b>The spool does not survive the send.</b> Tens of megabytes of a Commander's scrubbed
    /// history left beside the executable would be a second copy of the thing the donation path
    /// exists to be careful with, and nobody would ever look for it.
    /// </summary>
    [Fact]
    public async Task NothingIsLeftBehindInTheDonationsFolderButTheReceipt()
    {
        var sent = await Dispatch(new Endpoint()).SendCorpusAsync(
            "### Journal history\n",
            Writer(Payload),
            Paperwork,
            cancel: TestContext.Current.CancellationToken);

        Assert.True(sent.Outcome.Sent);

        var left = Directory.GetFiles(new AppPaths(_root).Donations).Select(Path.GetFileName);

        Assert.All(left, name => Assert.DoesNotContain(".sending", name, StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>What is kept is the report, and the receipt says so rather than claiming the hash covers
    /// it.</b> The payload is hundreds of megabytes; a second copy of it on the donor's own disk
    /// would tell them nothing the hash does not.
    /// </summary>
    [Fact]
    public async Task TheReceiptKeepsWhatWasReadAndSaysTheHashIsOfWhatWasNot()
    {
        var report = "### Journal history\nthis is what was on screen\n";

        var sent = await Dispatch(new Endpoint()).SendCorpusAsync(
            report, Writer(Payload), Paperwork, cancel: TestContext.Current.CancellationToken);

        Assert.NotNull(sent.Receipt);

        var kept = Directory.GetFiles(new AppPaths(_root).Donations, "*.md")
            .Single(file => !file.EndsWith(".receipt.md", StringComparison.Ordinal));

        Assert.Equal(report, File.ReadAllText(kept));
        Assert.Contains(
            "The payload itself is not kept here",
            File.ReadAllText(sent.Receipt),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Nothing is assembled with nowhere to send it.</b> Reading and compressing a full history
    /// to discover there is no address would be the whole cost of the feature for none of it — and
    /// it must not mint an identifier on the way.
    /// </summary>
    [Fact]
    public async Task WithNoAddressNothingIsAssembledAndNoIdentifierIsMinted()
    {
        var written = false;

        var sent = await Dispatch(new Endpoint(), address: null).SendCorpusAsync(
            "### Journal history\n",
            (_, _, _) => { written = true; return Task.CompletedTask; },
            Paperwork,
            cancel: TestContext.Current.CancellationToken);

        Assert.False(written);
        Assert.False(sent.Outcome.Sent);
        Assert.False(File.Exists(new AppPaths(_root).DonorTokenFile));
    }

    /// <summary>
    /// The window offers the send only where one was composed — the same rule the excerpt window
    /// and the donate button itself already follow.
    /// </summary>
    [AvaloniaFact]
    public void WithNoAddressThereIsNoSendButton()
    {
        var window = Shown();

        Assert.DoesNotContain(
            window.GetVisualDescendants().OfType<Button>(),
            button => button.Name == "SendCorpus");

        // And the route that never needed one is still there.
        Assert.Contains(
            window.GetVisualDescendants().OfType<Button>(),
            button => button.Name == "SaveCorpus");
    }

    /// <summary>
    /// And with nowhere to send, the window still says nothing reaches a network — which is then
    /// true, and is the sentence #181 had to stop saying unconditionally rather than delete.
    /// </summary>
    [AvaloniaFact]
    public void WithNoAddressItStillSaysNothingGoesToANetwork()
    {
        var lede = Shown().GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Single(text => text.Contains("Nothing is read, written or sent", StringComparison.Ordinal));

        Assert.Contains("nothing here goes to a network", lede, StringComparison.Ordinal);
    }

    /// <summary>
    /// With an address, the lede names it before anything is pressed, and states the linkage claim
    /// — the same two things the excerpt window says, in the same words.
    /// </summary>
    [AvaloniaFact]
    public void WithAnAddressItNamesTheDestinationBeforeAnythingIsPressed()
    {
        var window = Shown(
            send: (_, _, _) => Task.FromResult(new DonationSent(DonationOutcome.Stored("k"), null)),
            destination: "https://donate.invalid/donate");

        var lede = window.GetVisualDescendants().OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Single(text => text.Contains("Nothing is read, written or sent", StringComparison.Ordinal));

        Assert.Contains("https://donate.invalid/donate", lede, StringComparison.Ordinal);
        Assert.DoesNotContain("nothing here goes to a network", lede, StringComparison.Ordinal);
        Assert.Contains("random number identifying this installation", lede, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Nothing is sent until there is a report to be sending.</b> The consent is the report, so
    /// a send available before one has been read would be a yes to a document nobody has.
    /// </summary>
    [AvaloniaFact]
    public async Task TheSendIsOfferedOnlyOnceThereIsSomethingToConsentTo()
    {
        var window = Shown(
            send: (_, _, _) => Task.FromResult(new DonationSent(DonationOutcome.Stored("k"), null)),
            destination: "https://donate.invalid/donate");

        Assert.False(Control<Button>(window, "SendCorpus").IsEnabled);

        await PressAsync(window, "ReadJournals");

        Assert.True(Control<Button>(window, "SendCorpus").IsEnabled);
    }

    /// <summary>
    /// <b>What is sent is the report that was on screen</b>, and pressing sends once — the same
    /// rules and the same words as the excerpt window's button.
    /// </summary>
    [AvaloniaFact]
    public async Task ThePressSendsTheReportThatWasRead()
    {
        var report = "### Journal history\nexactly this\n";
        string? consented = null;

        var window = Shown(
            report,
            send: (document, _, _) =>
            {
                consented = document;
                return Task.FromResult(new DonationSent(DonationOutcome.Stored("corpus/a/b.jsonl.gz"), null));
            },
            destination: "https://donate.invalid/donate");

        await PressAsync(window, "ReadJournals");
        await PressAsync(window, "SendCorpus");

        Assert.Equal(report, consented);

        var send = Control<Button>(window, "SendCorpus");

        Assert.Equal("Sent", send.Content);
        Assert.False(send.IsEnabled);
    }

    /// <summary>
    /// <b>Changing the scope throws the send away with the report.</b> A button reading "Sent" over
    /// a report that no longer describes what left is the one failure a consent step must not have
    /// — the rule this window already enforced for its Save button.
    /// </summary>
    [AvaloniaFact]
    public async Task ChangingTheScopeMakesTheSendAFreshDecision()
    {
        var window = Shown(
            send: (_, _, _) => Task.FromResult(new DonationSent(DonationOutcome.Stored("k"), null)),
            destination: "https://donate.invalid/donate");

        await PressAsync(window, "ReadJournals");
        await PressAsync(window, "SendCorpus");

        Assert.Equal("Sent", Control<Button>(window, "SendCorpus").Content);

        Control<ComboBox>(window, "Scope").SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        var send = Control<Button>(window, "SendCorpus");

        Assert.Equal("Send it", send.Content);
        Assert.False(send.IsEnabled);
    }

    /// <summary>
    /// A refusal says so and offers the press again, rather than looking like it worked — the one
    /// thing worse than a donation that did not arrive is one that silently did not.
    /// </summary>
    [AvaloniaFact]
    public async Task ARefusedDonationSaysSoAndCanBePressedAgain()
    {
        var window = Shown(
            send: (_, _, _) => Task.FromResult(
                new DonationSent(DonationOutcome.Refused("The endpoint refused it."), null)),
            destination: "https://donate.invalid/donate");

        await PressAsync(window, "ReadJournals");
        await PressAsync(window, "SendCorpus");

        var send = Control<Button>(window, "SendCorpus");

        Assert.Equal("Send it", send.Content);
        Assert.True(send.IsEnabled);

        Assert.Contains(
            window.GetVisualDescendants().OfType<TextBlock>()
                .Select(block => block.Text ?? string.Empty),
            text => text.Contains("The endpoint refused it.", StringComparison.Ordinal));
    }
}
