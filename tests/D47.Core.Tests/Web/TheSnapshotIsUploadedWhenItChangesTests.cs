using System.IO.Compression;
using System.Text.Json;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Web;

public class TheSnapshotIsUploadedWhenItChangesTests
{
    private static readonly CommanderIdentity Other = new(SnapshotBench.OtherFid, "Brin Tasker");

    private sealed record Put(string Token, string FrontierId, string Body);

    private sealed class FakeWebApp : IWebAppClient
    {
        private TaskCompletionSource<WebAppPutResult>? _held;

        public List<Put> Puts { get; } = [];

        public WebAppPutResult Answer { get; set; } = WebAppPutResult.Done;

        /// <summary>When set, each upload waits for <see cref="Release"/>.</summary>
        public bool Hold { get; set; }

        public int InFlight { get; private set; }

        public int MostInFlight { get; private set; }

        public Task<WebAppSignInResult> SignInAsync(CancellationToken cancellationToken) =>
            Task.FromResult(WebAppSignInResult.Failed("not used"));

        public Task<bool> DeleteSnapshotAsync(string token, CancellationToken cancellationToken) => Task.FromResult(true);

        public async Task<WebAppPutResult> PutSnapshotAsync(string token, byte[] gzippedJson, CancellationToken cancellationToken)
        {
            using var gzip = new GZipStream(new MemoryStream(gzippedJson), CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            var body = reader.ReadToEnd();
            using var document = JsonDocument.Parse(body);
            Puts.Add(new Put(token, document.RootElement.GetProperty("frontierId").GetString()!, body));

            InFlight++;
            MostInFlight = Math.Max(MostInFlight, InFlight);

            try
            {
                if (Hold)
                {
                    _held = new TaskCompletionSource<WebAppPutResult>();
                    return await _held.Task;
                }

                return Answer;
            }
            finally
            {
                InFlight--;
            }
        }

        public void Release() => _held!.SetResult(Answer);
    }

    private sealed class Rig
    {
        public Rig(bool canSend = true)
        {
            var install = new MemoryInstall();
            Secrets = new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance);
            Link = new WebAppLink(() => Current, Secrets, () => Active?.Identity, Client, canSend, NullLogger<WebAppLink>.Instance);
            Active = Bench.State;
            Upload = new WebSnapshotUpload(
                () => Current,
                () => Active,
                (state, now) => WebSnapshotBuilder.Build(state, Bench.Stores, "elite", null, TimeZoneInfo.Utc, now),
                Link,
                Client,
                canSend,
                Time,
                NullLogger<WebSnapshotUpload>.Instance)
            {
                Dispatch = work => _ = work(),
            };
            Time.Advance(SnapshotBench.Now - Time.GetUtcNow());
        }

        public SnapshotBench Bench { get; } = new();

        public ManualTime Time { get; } = new();

        public D47Settings Current { get; set; } = new() { WebApp = new WebAppSettings { Enabled = true } };

        public CommanderGameState? Active { get; set; }

        public SecretStore Secrets { get; }

        public FakeWebApp Client { get; } = new();

        public WebAppLink Link { get; }

        public WebSnapshotUpload Upload { get; }

        public void Store(string fid, string token) => Secrets.Set(CommanderSecret.Name(WebAppLink.TokenSecretName, fid), token);

        public bool HasToken(string fid) => Secrets.Names.Contains(CommanderSecret.Name(WebAppLink.TokenSecretName, fid));

        /// <summary>Moves the clock on and ticks once.</summary>
        public void TickAfter(TimeSpan by)
        {
            Time.Advance(by);
            Upload.Tick(Time.GetUtcNow());
        }

        public void Change(string note) => Bench.Note(SnapshotBench.Fid, "Ada Quillon", note, "Fit a fuel scoop");
    }

    private static Rig SignedIn(bool canSend = true)
    {
        var rig = new Rig(canSend);
        rig.Store(SnapshotBench.Fid, "token-ada");
        return rig;
    }

    [Fact]
    public void TheFirstTickSendsTheDocumentWithTheCommandersToken()
    {
        var rig = SignedIn();

        rig.TickAfter(TimeSpan.Zero);

        var put = Assert.Single(rig.Client.Puts);
        Assert.Equal("token-ada", put.Token);
        Assert.Equal(SnapshotBench.Fid, put.FrontierId);
    }

    [Fact]
    public void ADocumentIsBuiltNoMoreOftenThanOnceAMinute()
    {
        var rig = SignedIn();
        rig.TickAfter(TimeSpan.Zero);

        rig.Change("Buy limpets");
        rig.TickAfter(TimeSpan.FromSeconds(59));
        Assert.Single(rig.Client.Puts);

        rig.TickAfter(TimeSpan.FromSeconds(1));
        Assert.Equal(2, rig.Client.Puts.Count);
        Assert.Contains("Buy limpets", rig.Client.Puts[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public void ADocumentThatHasNotChangedIsNotSentAgain()
    {
        var rig = SignedIn();

        rig.TickAfter(TimeSpan.Zero);
        rig.TickAfter(TimeSpan.FromMinutes(1));
        rig.TickAfter(TimeSpan.FromMinutes(1));

        Assert.Single(rig.Client.Puts);
    }

    [Fact]
    public void ASignInAgainSendsAnUnchangedDocumentWithTheNewToken()
    {
        var rig = SignedIn();
        rig.TickAfter(TimeSpan.Zero);

        rig.Store(SnapshotBench.Fid, "token-ada-again");
        rig.TickAfter(TimeSpan.FromMinutes(1));

        Assert.Equal(["token-ada", "token-ada-again"], rig.Client.Puts.Select(put => put.Token));
    }

    [Fact]
    public void NothingIsBuiltWithoutAStoredToken()
    {
        var rig = new Rig();

        rig.TickAfter(TimeSpan.Zero);
        rig.TickAfter(TimeSpan.FromMinutes(1));

        Assert.Empty(rig.Client.Puts);
    }

    [Fact]
    public void NothingIsSentOnATestDriveEvenWithATokenStored()
    {
        var rig = SignedIn(canSend: false);

        rig.TickAfter(TimeSpan.Zero);
        rig.TickAfter(TimeSpan.FromMinutes(1));
        rig.Upload.SendWaiting(TimeSpan.Zero);

        Assert.Empty(rig.Client.Puts);
        Assert.True(rig.HasToken(SnapshotBench.Fid));
    }

    [Fact]
    public void NothingIsSentWhileTheSettingIsOff()
    {
        var rig = SignedIn();
        rig.Current = new D47Settings { WebApp = new WebAppSettings { Enabled = false } };

        rig.TickAfter(TimeSpan.Zero);

        Assert.Empty(rig.Client.Puts);
    }

    [Fact]
    public void A401RemovesTheTokenSaysSoOnTheRowAndIsNotRetried()
    {
        var rig = SignedIn();
        rig.Client.Answer = new WebAppPutResult(false, 401, null);

        rig.TickAfter(TimeSpan.Zero);

        Assert.False(rig.HasToken(SnapshotBench.Fid));
        Assert.Equal("On, and not signed in. Press Sign in to link CMDR Ada Quillon.", rig.Link.Note(rig.Current));

        rig.Change("Buy limpets");
        rig.TickAfter(TimeSpan.FromMinutes(1));
        rig.TickAfter(TimeSpan.FromMinutes(10));

        Assert.Single(rig.Client.Puts);
    }

    [Fact]
    public void AnyOtherFailureHoldsTheNextAttemptForFiveMinutes()
    {
        var rig = SignedIn();
        rig.Client.Answer = new WebAppPutResult(false, 500, null);

        rig.TickAfter(TimeSpan.Zero);
        rig.Client.Answer = WebAppPutResult.Done;

        rig.TickAfter(TimeSpan.FromMinutes(1));
        rig.TickAfter(TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(59));
        Assert.Single(rig.Client.Puts);

        rig.TickAfter(TimeSpan.FromSeconds(1));
        Assert.Equal(2, rig.Client.Puts.Count);
        Assert.True(rig.HasToken(SnapshotBench.Fid));
    }

    [Fact]
    public void AFailedDocumentIsSentAgainEvenThoughItHasNotChanged()
    {
        var rig = SignedIn();
        rig.Client.Answer = new WebAppPutResult(false, null, "HttpRequestException");

        rig.TickAfter(TimeSpan.Zero);
        rig.Client.Answer = WebAppPutResult.Done;
        rig.TickAfter(TimeSpan.FromMinutes(5));

        Assert.Equal(2, rig.Client.Puts.Count);
    }

    [Fact]
    public void OneUploadIsInFlightAndANewerDocumentReplacesOneStillWaiting()
    {
        var rig = SignedIn();
        rig.Client.Hold = true;

        rig.TickAfter(TimeSpan.Zero);
        rig.Change("First change");
        rig.TickAfter(TimeSpan.FromMinutes(1));
        rig.Change("Second change");
        rig.TickAfter(TimeSpan.FromMinutes(1));

        Assert.Single(rig.Client.Puts);

        rig.Client.Release();
        rig.Client.Release();

        Assert.Equal(2, rig.Client.Puts.Count);
        Assert.Contains("Second change", rig.Client.Puts[1].Body, StringComparison.Ordinal);
        Assert.Equal(1, rig.Client.MostInFlight);
    }

    [Fact]
    public void TurningTheSettingOffDropsTheDocumentStillWaiting()
    {
        var rig = SignedIn();
        rig.Client.Hold = true;

        rig.TickAfter(TimeSpan.Zero);
        rig.Change("Buy limpets");
        rig.TickAfter(TimeSpan.FromMinutes(1));

        rig.Current = new D47Settings { WebApp = new WebAppSettings { Enabled = false } };
        rig.TickAfter(TimeSpan.FromMilliseconds(100));
        rig.Client.Release();

        Assert.Single(rig.Client.Puts);
    }

    [Fact]
    public void AfterACommanderSwitchEachDocumentGoesWithItsOwnCommandersToken()
    {
        var rig = SignedIn();
        rig.Store(Other.FrontierId, "token-brin");

        rig.TickAfter(TimeSpan.Zero);
        rig.Active = new CommanderGameState(Other);
        rig.TickAfter(TimeSpan.FromMinutes(1));

        Assert.Equal(
            [("token-ada", SnapshotBench.Fid), ("token-brin", Other.FrontierId)],
            rig.Client.Puts.Select(put => (put.Token, put.FrontierId)));
    }

    [Fact]
    public void ACommanderWithNoTokenSendsNothingAfterASwitch()
    {
        var rig = SignedIn();

        rig.TickAfter(TimeSpan.Zero);
        rig.Active = new CommanderGameState(Other);
        rig.TickAfter(TimeSpan.FromMinutes(1));

        Assert.Single(rig.Client.Puts);
    }

    [Fact]
    public async Task ADocumentStillWaitingAtExitIsSent()
    {
        var rig = SignedIn();
        rig.Client.Hold = true;

        rig.TickAfter(TimeSpan.Zero);
        rig.Change("Buy limpets");
        rig.TickAfter(TimeSpan.FromMinutes(1));

        var exit = Task.Run(() => rig.Upload.SendWaiting(TimeSpan.FromSeconds(30)), TestContext.Current.CancellationToken);
        rig.Client.Hold = false;
        rig.Client.Release();
        await exit.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(2, rig.Client.Puts.Count);
        Assert.Contains("Buy limpets", rig.Client.Puts[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExitWaitEndsWhenTheUploadDoesNotAnswer()
    {
        var rig = SignedIn();
        rig.Client.Hold = true;

        rig.TickAfter(TimeSpan.Zero);
        rig.Upload.SendWaiting(TimeSpan.FromMilliseconds(50));

        Assert.Single(rig.Client.Puts);
    }
}
