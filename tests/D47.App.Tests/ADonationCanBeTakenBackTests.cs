using System.Net;
using D47.App.Donation;
using D47.Core;
using D47.Core.Diagnostics.Donation;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Erasure on request, driven from the press a Commander actually makes
/// (<a href="https://github.com/dseelinger/d47/issues/167">#167</a>).
/// <para>
/// <b>The claim under test is the one that made a private destination necessary at all.</b> GitHub
/// was ruled out because a public transport cannot honour "ask and it is deleted" — so the store
/// that replaced it has to honour it, and the route to asking has to be no harder than the route
/// to consenting was.
/// </para>
/// <para>
/// <b>And the order matters more than the outcome.</b> The installation token is the only handle
/// anybody has on what was sent; forgetting it before the store confirms would strand a donor's
/// data permanently under a name nobody holds, which is a worse failure than not deleting it.
/// </para>
/// </summary>
public class ADonationCanBeTakenBackTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d47-forget").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>Answers every request the same way and keeps the last one.</summary>
    private sealed class Endpoint : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public Endpoint(HttpStatusCode status = HttpStatusCode.OK, string? body = null)
        {
            _status = status;
            _body = body ?? """{"ok":true,"deleted":2,"keys":["corpus/a/one.jsonl.gz","excerpts/a/two.md.gz"],"more":false}""";
        }

        public HttpRequestMessage? Last { get; private set; }

        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancel)
        {
            Last = request;
            Requests++;

            return Task.FromResult(
                new HttpResponseMessage(_status) { Content = new StringContent(_body) });
        }
    }

    private DonationDispatch Dispatch(Endpoint endpoint, string? address = "https://donate.invalid") =>
        new(new AppPaths(_root), () => address, new DonationUpload(new HttpClient(endpoint)));

    private string TokenFile => new AppPaths(_root).DonorTokenFile;

    private string Donated()
    {
        Directory.CreateDirectory(new AppPaths(_root).Data);
        return DonorToken.Ensure(TokenFile);
    }

    private static string Header(HttpRequestMessage request, string name) =>
        request.Headers.GetValues(name).Single();

    /// <summary>
    /// <b>The whole claim.</b> One press asks the store to delete everything sent under this
    /// installation's identifier, and the identifier goes with it.
    /// </summary>
    [Fact]
    public async Task OnePressAsksTheStoreAndForgetsTheIdentifier()
    {
        var token = Donated();
        var endpoint = new Endpoint();

        var forgotten = await Dispatch(endpoint)
            .ForgetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(endpoint.Last);
        Assert.Equal("https://donate.invalid/forget", endpoint.Last.RequestUri?.ToString());
        Assert.Equal(token, Header(endpoint.Last, DonationEnvelope.DonorHeader));

        Assert.True(forgotten.Outcome.Answered);
        Assert.Equal(2, forgotten.Outcome.Deleted);
        Assert.False(File.Exists(TokenFile));
    }

    /// <summary>
    /// <b>A refused erasure keeps the identifier</b>, because it is the only handle anybody has on
    /// what was sent — the store cannot find it without one and neither can the Commander. This is
    /// the assertion that stops a well-meaning tidy-up turning a failed deletion into a permanent
    /// one.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "the store did not answer")]
    [InlineData(HttpStatusCode.BadRequest, "no")]
    public async Task ARefusalKeepsTheIdentifierSoItCanBeAskedAgain(HttpStatusCode status, string body)
    {
        var token = Donated();

        var forgotten = await Dispatch(new Endpoint(status, body))
            .ForgetAsync(TestContext.Current.CancellationToken);

        Assert.False(forgotten.Outcome.Answered);
        Assert.True(File.Exists(TokenFile));
        Assert.Equal(token, DonorToken.Read(TokenFile));
        Assert.Contains("kept", forgotten.Outcome.Said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// An answer d47 cannot read is not a deletion. The opposite call is made for a send — an
    /// unreadable success there still stored something — but here it would tell a Commander their
    /// data is gone on no evidence at all.
    /// </summary>
    [Fact]
    public async Task AnUnreadableAnswerIsNotTreatedAsADeletion()
    {
        Donated();

        var forgotten = await Dispatch(new Endpoint(HttpStatusCode.OK, "deleted, thanks"))
            .ForgetAsync(TestContext.Current.CancellationToken);

        Assert.False(forgotten.Outcome.Answered);
        Assert.True(File.Exists(TokenFile));
    }

    /// <summary>
    /// <b>Never mints one to forget it.</b> An installation that has never donated has no
    /// identifier, and creating one in order to erase it would break the rule that a token exists
    /// only because somebody donated.
    /// </summary>
    [Fact]
    public async Task AnInstallationThatNeverDonatedIsNotGivenAnIdentifier()
    {
        var endpoint = new Endpoint();

        var forgotten = await Dispatch(endpoint).ForgetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, endpoint.Requests);
        Assert.False(forgotten.Outcome.Asked);
        Assert.False(File.Exists(TokenFile));
        Assert.Null(forgotten.Receipt);
    }

    /// <summary>
    /// With no address there is nobody to ask, and the local half is still done — which is exactly
    /// what the row did before #167 and is the whole of what is possible with nowhere to ask.
    /// </summary>
    [Fact]
    public async Task WithNoAddressTheIdentifierIsStillForgottenHere()
    {
        Donated();

        var forgotten = await Dispatch(new Endpoint(), address: null)
            .ForgetAsync(TestContext.Current.CancellationToken);

        Assert.False(forgotten.Outcome.Asked);
        Assert.False(File.Exists(TokenFile));
    }

    /// <summary>
    /// <b>The receipt keeps the identifier that was just forgotten.</b> That looks backwards and is
    /// the point: the token is gone from <c>data\</c>, and without it nobody — the custodian
    /// included — can find what an incomplete deletion left behind.
    /// </summary>
    [Fact]
    public async Task TheReceiptKeepsTheOneThingNothingElseHoldsAnyMore()
    {
        var token = Donated();

        var forgotten = await Dispatch(new Endpoint())
            .ForgetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(forgotten.Receipt);

        var written = File.ReadAllText(forgotten.Receipt);

        Assert.Contains(token, written, StringComparison.Ordinal);
        Assert.Contains("corpus/a/one.jsonl.gz", written, StringComparison.Ordinal);
        Assert.Contains("https://donate.invalid/forget", written, StringComparison.Ordinal);
    }

    /// <summary>
    /// And it says what a deletion does <b>not</b> reach — the fix, the release, the changelog
    /// line. That is #167's central split, and a receipt that only listed what went would let a
    /// donor believe the rest went too.
    /// </summary>
    [Fact]
    public async Task TheReceiptSaysWhatSurvivesAndWhy()
    {
        Donated();

        var forgotten = await Dispatch(new Endpoint())
            .ForgetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(forgotten.Receipt);

        var written = File.ReadAllText(forgotten.Receipt);

        Assert.Contains("stays fixed", written, StringComparison.Ordinal);
        Assert.Contains("never moves", written, StringComparison.Ordinal);
        Assert.Contains("archived beyond anyone's reach", written, StringComparison.Ordinal);
    }

    /// <summary>
    /// A refused erasure is written down too, rather than leaving a Commander with nothing saying
    /// they asked — the same rule <see cref="DonationReceipt"/> follows for a refused send.
    /// </summary>
    [Fact]
    public async Task ARefusalIsWrittenDownToo()
    {
        Donated();

        var forgotten = await Dispatch(new Endpoint(HttpStatusCode.ServiceUnavailable, "no"))
            .ForgetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(forgotten.Receipt);
        Assert.Contains(
            "Nothing is confirmed deleted",
            File.ReadAllText(forgotten.Receipt),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Where the store says there is more behind what it took, the Commander is told to press
    /// again rather than left believing a partial deletion was the whole of it.
    /// </summary>
    [Fact]
    public async Task MoreLeftBehindSaysSoRatherThanReadingAsDone()
    {
        Donated();

        var forgotten = await Dispatch(new Endpoint(
                HttpStatusCode.OK,
                """{"ok":true,"deleted":10000,"keys":[],"more":true}"""))
            .ForgetAsync(TestContext.Current.CancellationToken);

        Assert.True(forgotten.Outcome.More);
        Assert.Contains("Press again", forgotten.Outcome.Said, StringComparison.Ordinal);
    }
}
