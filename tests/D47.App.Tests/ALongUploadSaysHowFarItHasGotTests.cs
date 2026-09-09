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

/// <summary>The upload reports itself while it happens.</summary>
public class ALongUploadSaysHowFarItHasGotTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d47-upload-progress").FullName;

    private static readonly ExcerptPaperwork Paperwork = new(
        "0.90.0+abcdef", new DateTimeOffset(2026, 9, 1, 9, 30, 0, TimeSpan.Zero));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>Reads the whole body, which is what makes the metered stream turn.</summary>
    private sealed class Endpoint : HttpMessageHandler
    {
        public byte[] Sent { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancel)
        {
            if (request.Content is { } content)
            {
                Sent = await content.ReadAsByteArrayAsync(cancel);
            }

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"ok":true,"key":"corpus/token/object.jsonl.gz"}"""),
            };
        }
    }

    /// <summary>Keeps every step in the order it arrived.</summary>
    private sealed class Steps : IProgress<DonationStep>
    {
        private readonly List<DonationStep> _seen = [];

        public IReadOnlyList<DonationStep> Seen => _seen;

        public void Report(DonationStep step) => _seen.Add(step);
    }

    private DonationDispatch Dispatch(Endpoint endpoint) =>
        new(
            new AppPaths(_root),
            () => "https://donate.invalid",
            new DonationUpload(new HttpClient(endpoint)));

    /// <summary>A payload that does not compress away.</summary>
    private static string Noise(int lines)
    {
        // Seeded, so a run that fails fails again with the same bytes.
        var random = new Random(47);
        var text = new StringBuilder();
        var blob = new char[96];

        for (var nth = 0; nth < lines; nth++)
        {
            for (var at = 0; at < blob.Length; at++)
            {
                blob[at] = (char)('a' + random.Next(26));
            }

            text.Append("""{"timestamp":"2026-09-01T09:00:00Z","event":"FSDJump","StarSystem":""")
                .Append('"')
                .Append(blob)
                .Append("\"}\n");
        }

        return text.ToString();
    }

    private static Func<Stream, IProgress<int>, CancellationToken, Task> Writer(string payload) =>
        async (stream, progress, cancel) =>
        {
            progress.Report(1);

            await using var writer = new StreamWriter(
                stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);

            await writer.WriteAsync(payload.AsMemory(), cancel);
        };

    private static T Control<T>(Window window, string name)
        where T : Avalonia.Controls.Control =>
        window.GetVisualDescendants().OfType<T>().Single(found => found.Name == name);

    /// <summary>The status line by name, not by what it says.</summary>
    private static string Status(Window window) =>
        Control<TextBlock>(window, "SendStatus").Text ?? string.Empty;

    private static HelpImproveWindow Shown(
        Func<string, IProgress<DonationStep>, CancellationToken, Task<DonationSent>> send)
    {
        var window = new HelpImproveWindow(
            new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero),
            _ => string.Empty,
            destination: "https://donate.invalid/donate",
            read: (_, _, _) => Task.FromResult(
                new HelpImproveWindow.CorpusReading(
                    new CorpusSurvey(null, null, 0, 0, new CorpusTally(0, 0, 0, 0, 0, 0), []),
                    "### Journal history\nwhat you are agreeing to\n")),
            write: (_, _, _) => Task.CompletedTask,
            sendCorpus: send);

        window.Show();
        Dispatcher.UIThread.RunJobs();

        // The bar these tests are about belongs to the history send, which is one press in: one donation page
        // for every reading opens on the excerpt, and the toggle asks for the other half.
        Control<CheckBox>(window, "IncludeHistory").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static void Press(HelpImproveWindow window, string button) =>
        Control<Button>(window, button).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static async Task SettleAsync()
    {
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The upload speaks more than once, and after it has started.</summary>
    [Fact]
    public async Task TheUploadReportsItselfWhileItIsHappening()
    {
        var endpoint = new Endpoint();
        var steps = new Steps();

        var sent = await Dispatch(endpoint).SendCorpusAsync(
            "### Journal history\n",
            Writer(Noise(12_000)),
            Paperwork,
            steps,
            TestContext.Current.CancellationToken);

        Assert.True(sent.Outcome.Sent);

        var sending = steps.Seen.Where(step => step.Sending).ToList();

        // Past two, which a single report plus a final one would also give.
        Assert.True(
            sending.Count(step => step.Sent > 0) >= 3,
            $"the upload reported {sending.Count} times, {sending.Count(s => s.Sent > 0)} of them "
            + "with bytes behind them");

        // Big enough that the meter had notches to cross.
        Assert.True(endpoint.Sent.Length > 256 * 1024, "the body has to outgrow a few notches");
    }

    /// <summary>Forwards, and all the way.</summary>
    [Fact]
    public async Task TheFractionOnlyRisesAndReachesOne()
    {
        var endpoint = new Endpoint();
        var steps = new Steps();

        await Dispatch(endpoint).SendCorpusAsync(
            "### Journal history\n",
            Writer(Noise(12_000)),
            Paperwork,
            steps,
            TestContext.Current.CancellationToken);

        var fractions = steps.Seen
            .Where(step => step.Sending)
            .Select(step => step.Fraction)
            .ToList();

        Assert.All(fractions, fraction => Assert.NotNull(fraction));

        var seen = fractions.Select(fraction => fraction!.Value).ToList();

        for (var nth = 1; nth < seen.Count; nth++)
        {
            Assert.True(seen[nth] >= seen[nth - 1], $"step {nth} went backwards to {seen[nth]}");
        }

        Assert.Equal(0, seen[0]);
        Assert.Equal(1, seen[^1]);

        // The total is what is on the wire, not the history's own size: the two differ by about twelve to
        // one, and a bar counting to the raw figure would finish at eight percent.
        Assert.All(
            steps.Seen.Where(step => step.Sending),
            step => Assert.Equal(endpoint.Sent.Length, step.Total));
    }

    /// <summary>The preparing step draws nothing: it is counted in journal files and has no denominator, so a bar sitting at the left would claim no progress had been made while a number beside it climbed.</summary>
    [Fact]
    public void PreparingHasNoFractionToDraw()
    {
        Assert.Null(new DonationStep(Sending: false, 936).Fraction);
        Assert.Null(new DonationStep(Sending: true, 936, Sent: 0, Total: 0).Fraction);
        Assert.Equal(0.5, new DonationStep(Sending: true, 936, Sent: 16, Total: 32).Fraction);
    }

    /// <summary>
    /// The window draws it, and keeps the sentence. "Nothing else is being sent, and nothing is being
    /// kept anywhere else" is doing work about scope that a percentage cannot do, so the bar sits
    /// beside it rather than in place of it.
    /// </summary>
    [AvaloniaFact]
    public async Task TheWindowDrawsABarWhileItSendsAndKeepsTheSentence()
    {
        var held = new TaskCompletionSource();

        var window = Shown(async (_, progress, _) =>
        {
            progress.Report(new DonationStep(Sending: false, 512));
            progress.Report(new DonationStep(Sending: true, 936, 12_000_000, 32_000_000));

            await held.Task;

            return new DonationSent(DonationOutcome.Stored("corpus/a/b.jsonl.gz"), null);
        });

        Press(window, "ReadJournals");
        await SettleAsync();

        var bar = Control<ProgressBar>(window, "SendProgress");

        Assert.False(bar.IsVisible);

        Press(window, "SendCorpus");
        await SettleAsync();

        Assert.True(bar.IsVisible);
        Assert.Equal(0.375, bar.Value, 3);

        var said = Status(window);

        Assert.Contains("11.4 MB of 30.5 MB compressed", said, StringComparison.Ordinal);
        Assert.Contains(
            "Nothing else is being sent, and nothing is being kept anywhere else",
            said,
            StringComparison.Ordinal);

        held.SetResult();
        await SettleAsync();

        // Gone the moment the bytes stop moving, so a full bar never stands over an outcome.
        Assert.False(bar.IsVisible);
        Assert.Equal("Sent", Control<Button>(window, "SendCorpus").Content);
    }

    /// <summary>And a refusal takes the bar with it.</summary>
    [AvaloniaFact]
    public async Task ARefusedSendLeavesNoBarBehind()
    {
        var window = Shown((_, progress, _) =>
        {
            progress.Report(new DonationStep(Sending: true, 936, 32_000_000, 32_000_000));

            return Task.FromResult(
                new DonationSent(DonationOutcome.Refused("The endpoint refused it."), null));
        });

        Press(window, "ReadJournals");
        await SettleAsync();

        Press(window, "SendCorpus");
        await SettleAsync();

        Assert.False(Control<ProgressBar>(window, "SendProgress").IsVisible);

        Assert.Contains(
            window.GetVisualDescendants().OfType<TextBlock>()
                .Select(block => block.Text ?? string.Empty),
            text => text.Contains("The endpoint refused it.", StringComparison.Ordinal));
    }
}
