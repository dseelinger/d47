using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using D47.App.Donation;
using D47.Core;
using D47.Core.Diagnostics.Donation;
using D47.Core.Storage;
using Xunit;

namespace D47.App.Tests;

/// <summary>A history too large for one request goes as several donations (#1784).</summary>
[Trait("Category", "Integration")]
public class ALargeCorpusIsSentInPartsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d47-corpus-parts").FullName;

    private static readonly ExcerptPaperwork Paperwork = new(
        "0.90.0+abcdef", new DateTimeOffset(2026, 8, 29, 14, 25, 30, TimeSpan.Zero));

    private static readonly IReadOnlyList<IReadOnlyList<string>> Three =
        [["a.log"], ["b.log", "c.log"], ["d.log"]];

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class Endpoint : HttpMessageHandler
    {
        private readonly int _refuseAt;

        public Endpoint(int refuseAt = 0) => _refuseAt = refuseAt;

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancel)
        {
            Requests.Add(request);

            return Task.FromResult(
                Requests.Count == _refuseAt
                    ? new HttpResponseMessage(HttpStatusCode.RequestEntityTooLarge)
                    : new HttpResponseMessage(HttpStatusCode.Created)
                    {
                        Content = new StringContent($$"""{"ok":true,"key":"corpus/t/{{Requests.Count}}.jsonl.gz"}"""),
                    });
        }
    }

    private DonationDispatch Dispatch(Endpoint endpoint) =>
        new(
            new AppPaths(_root),
            new DiskFileSystem(),
            () => "https://donate.invalid",
            new DonationUpload(new HttpClient(endpoint)));

    private static Func<IReadOnlyList<string>, Stream, IProgress<int>, CancellationToken, Task> Writer() =>
        async (files, stream, progress, cancel) =>
        {
            progress.Report(files.Count);

            await using var writer = new StreamWriter(
                stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);

            await writer.WriteAsync(string.Join('\n', files) + "\n");
        };

    private sealed class Steps : IProgress<DonationStep>
    {
        private readonly List<DonationStep> _seen = [];

        public List<DonationStep> Seen
        {
            get
            {
                lock (_seen)
                {
                    return [.. _seen];
                }
            }
        }

        public void Report(DonationStep step)
        {
            lock (_seen)
            {
                _seen.Add(step);
            }
        }
    }

    [Fact]
    public async Task EachPartIsPostedAsADonationOfItsOwn()
    {
        var endpoint = new Endpoint();

        var sent = await Dispatch(endpoint).SendCorpusAsync(
            "### Journal history\n", Three, Writer(), Paperwork, cancel: TestContext.Current.CancellationToken);

        Assert.True(sent.Outcome.Sent);
        Assert.Equal(3, endpoint.Requests.Count);

        var hashes = endpoint.Requests
            .Select(request => request.Headers.GetValues(DonationEnvelope.Sha256Header).Single())
            .ToList();

        Assert.Equal(3, hashes.Distinct().Count());
        Assert.All(
            endpoint.Requests,
            request =>
            {
                Assert.Equal(DonationEnvelope.Corpus, request.Headers.GetValues(DonationEnvelope.KindHeader).Single());
                Assert.Equal(
                    DonationEnvelope.Stamp(Paperwork.TakenAt),
                    request.Headers.GetValues(DonationEnvelope.TakenAtHeader).Single());
            });

        Assert.Equal(
            DonationEnvelope.HashOf("b.log\nc.log\n"),
            endpoint.Requests[1].Headers.GetValues(DonationEnvelope.Sha256Header).Single());

        var receipts = Directory.GetFiles(new AppPaths(_root).Donations, "*.receipt.md");
        Assert.Equal(3, receipts.Length);
        Assert.Contains(receipts, receipt => File.ReadAllText(receipt).Contains("part 2 of 3"));
    }

    [Fact]
    public async Task TheProgressSaysWhichPartIsSending()
    {
        var steps = new Steps();

        await Dispatch(new Endpoint()).SendCorpusAsync(
            "### Journal history\n", Three, Writer(), Paperwork, steps, TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3], steps.Seen.Where(step => step.Sending).Select(step => step.Part).Distinct());
        Assert.All(steps.Seen, step => Assert.Equal(3, step.Parts));
    }

    [Fact]
    public async Task ARefusedPartStopsTheRestAndSaysHowManyWereStored()
    {
        var endpoint = new Endpoint(refuseAt: 2);

        var sent = await Dispatch(endpoint).SendCorpusAsync(
            "### Journal history\n", Three, Writer(), Paperwork, cancel: TestContext.Current.CancellationToken);

        Assert.False(sent.Outcome.Sent);
        Assert.Equal(2, endpoint.Requests.Count);
        Assert.Contains("1 of 3 parts were stored", sent.Outcome.Said);
        Assert.Contains("Part 2 of 3", sent.Outcome.Said);

        var receipts = Directory.GetFiles(new AppPaths(_root).Donations, "*part1of3.receipt.md");
        Assert.Single(receipts);
    }

    [Fact]
    public async Task ASpanInOnePartCarriesNoPartWording()
    {
        var endpoint = new Endpoint();
        var steps = new Steps();

        var sent = await Dispatch(endpoint).SendCorpusAsync(
            "### Journal history\n",
            [[]],
            Writer(),
            Paperwork,
            steps,
            TestContext.Current.CancellationToken);

        Assert.True(sent.Outcome.Sent);
        Assert.DoesNotContain("part", sent.Outcome.Said);
        Assert.All(steps.Seen, step => Assert.Equal(1, step.Parts));
        Assert.DoesNotContain("part", Path.GetFileName(sent.Receipt));
        Assert.DoesNotContain("part", File.ReadAllText(sent.Receipt!));
    }

    [Fact]
    public async Task APartOverTheStoresLimitIsRefusedBeforeAnythingIsPosted()
    {
        var endpoint = new Endpoint();
        var random = new Random(47);

        // Incompressible, so the gzipped spool outgrows the limit.
        var sent = await Dispatch(endpoint).SendCorpusAsync(
            "### Journal history\n",
            [["a.log"], ["b.log"]],
            async (files, stream, progress, cancel) =>
            {
                if (files[0] == "a.log")
                {
                    await Writer()(files, stream, progress, cancel);
                    return;
                }

                var block = new byte[1024 * 1024];

                for (var megabyte = 0; megabyte < 97; megabyte++)
                {
                    random.NextBytes(block);
                    await stream.WriteAsync(block, cancel);
                }
            },
            Paperwork,
            cancel: TestContext.Current.CancellationToken);

        Assert.False(sent.Outcome.Sent);
        Assert.Single(endpoint.Requests);
        Assert.Contains("Part 2 of 2", sent.Outcome.Said);
        Assert.Contains("nothing was sent", sent.Outcome.Said);
    }

    /// <summary>The client's compressed-size check uses the Worker's number.</summary>
    [Fact]
    [Trait("Category", "Gate")]
    public void TheClientsLimitIsTheWorkersLimit()
    {
        var worker = File.ReadAllText(Path.Combine(AppSource.RepositoryRoot(), "worker", "src", "index.js"));

        var corpus = Regex.Match(worker, @"corpus:\s*\{[^}]*?mostBytes:\s*(\d+)\s*\*\s*1024\s*\*\s*1024", RegexOptions.Singleline);

        Assert.True(corpus.Success, "the corpus mostBytes in worker/src/index.js is no longer N * 1024 * 1024");
        Assert.Equal(DonationUpload.MostCorpusBytes, long.Parse(corpus.Groups[1].Value) * 1024 * 1024);
    }
}
