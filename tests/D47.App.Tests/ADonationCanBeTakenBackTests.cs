using System.Net;
using D47.App.Donation;
using D47.Core;
using D47.Core.Diagnostics.Donation;
using Xunit;

namespace D47.App.Tests;

/// <summary>Erasure on request, driven from the press a Commander actually makes.</summary>
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

    /// <summary>The whole claim.</summary>
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
    /// A refused erasure keeps the identifier, because it is the only handle anybody has on what was
    /// sent — the store cannot find it without one and neither can the Commander.
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

    /// <summary>An answer d47 cannot read is not a deletion.</summary>
    [Fact]
    public async Task AnUnreadableAnswerIsNotTreatedAsADeletion()
    {
        Donated();

        var forgotten = await Dispatch(new Endpoint(HttpStatusCode.OK, "deleted, thanks"))
            .ForgetAsync(TestContext.Current.CancellationToken);

        Assert.False(forgotten.Outcome.Answered);
        Assert.True(File.Exists(TokenFile));
    }

    /// <summary>Never mints one to forget it.</summary>
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
    /// With no address there is nobody to ask, and the local half is still done — which is exactly what
    /// the row did before #167 and is the whole of what is possible with nowhere to ask.
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

    /// <summary>The receipt keeps the identifier that was just forgotten.</summary>
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

    /// <summary>And it says what a deletion does not reach — the fix, the release, the changelog line.</summary>
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
    /// A refused erasure is written down too, rather than leaving a Commander with nothing saying they
    /// asked — the same rule <see cref="DonationReceipt"/> follows for a refused send.
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
    /// Where the store says there is more behind what it took, the Commander is told to press again
    /// rather than left believing a partial deletion was the whole of it.
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
