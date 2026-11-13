using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Web;

public class TheWebAppSignInIsTheFlyingCommandersTests
{
    private static readonly CommanderIdentity Vale = new("F1234", "Ardent Vale");
    private static readonly CommanderIdentity Morrow = new("F5678", "Quill Morrow");

    private sealed class FakeWebApp : IWebAppClient
    {
        public WebAppSignInResult Answer { get; set; } = new("token-vale", "1234", null);

        public bool Deletes { get; set; } = true;

        public List<string> Deleted { get; } = [];

        public int SignIns { get; private set; }

        public Task<WebAppSignInResult> SignInAsync(CancellationToken cancellationToken)
        {
            SignIns++;
            return Task.FromResult(Answer);
        }

        public Task<bool> DeleteSnapshotAsync(string token, CancellationToken cancellationToken)
        {
            Deleted.Add(token);
            return Task.FromResult(Deletes);
        }

        public Task<WebAppPutResult> PutSnapshotAsync(string token, byte[] gzippedJson, CancellationToken cancellationToken) =>
            Task.FromResult(WebAppPutResult.Done);
    }

    private sealed class Rig
    {
        public Rig(bool on = true, bool canSend = true)
        {
            var install = new MemoryInstall();
            Secrets = new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance);
            Current = new D47Settings { WebApp = new WebAppSettings { Enabled = on } };
            Link = new WebAppLink(() => Current, Secrets, () => Flying, Client, canSend, NullLogger<WebAppLink>.Instance)
            {
                Dispatch = work => work().GetAwaiter().GetResult(),
            };
        }

        public D47Settings Current { get; private set; }

        public CommanderIdentity? Flying { get; set; } = Vale;

        public SecretStore Secrets { get; }

        public FakeWebApp Client { get; } = new();

        public WebAppLink Link { get; }

        public void Turn(bool on)
        {
            Current = Current with { WebApp = new WebAppSettings { Enabled = on } };
            Link.SettingsChanged();
        }

        public Task<string?> SignInAsync() => Link.SignInAsync(new Progress<double>(), CancellationToken.None);

        public string? Token(CommanderIdentity commander) =>
            Secrets.TryGet(CommanderSecret.Name(WebAppLink.TokenSecretName, commander.FrontierId), out var token) ? token : null;
    }

    [Fact]
    public async Task ASignInStoresTheTokenForTheCommanderWhoIsFlying()
    {
        var rig = new Rig();

        Assert.Equal("Signed in as CMDR Ardent Vale.", await rig.SignInAsync());
        Assert.Equal("token-vale", rig.Token(Vale));
        Assert.Equal("On, and signed in as CMDR Ardent Vale.", rig.Link.Note(rig.Current));
    }

    [Fact]
    public async Task ASignInWithAnotherCommandersAccountStoresNothingAndTheRowSaysSo()
    {
        var rig = new Rig();
        rig.Client.Answer = new WebAppSignInResult("token-morrow", "5678", null);

        Assert.Equal("The Frontier account that signed in is not CMDR Ardent Vale's, so nothing was stored.", await rig.SignInAsync());
        Assert.Null(rig.Token(Vale));
        Assert.Null(rig.Token(Morrow));
        Assert.Equal(
            "On, and not signed in. The Frontier account that signed in is not CMDR Ardent Vale's, so nothing was stored.",
            rig.Link.Note(rig.Current));
    }

    [Fact]
    public void TheCustomerIdMatchesTheFidWithoutItsLeadingF()
    {
        Assert.True(WebAppLink.BelongsTo("1234", "F1234"));
        Assert.False(WebAppLink.BelongsTo("1234", "1234"));
        Assert.False(WebAppLink.BelongsTo("12345", "F1234"));
        Assert.False(WebAppLink.BelongsTo(string.Empty, "F"));
    }

    [Fact]
    public async Task TheRowShowsOffOnAndNotSignedInAndOnAndSignedIn()
    {
        var rig = new Rig(on: false);

        Assert.Equal("Off. Nothing is sent to the web app.", rig.Link.Note(rig.Current));

        rig.Turn(on: true);
        Assert.Equal("On, and not signed in. Press Sign in to link CMDR Ardent Vale.", rig.Link.Note(rig.Current));

        await rig.SignInAsync();
        Assert.Equal("On, and signed in as CMDR Ardent Vale.", rig.Link.Note(rig.Current));

        rig.Flying = Morrow;
        Assert.Equal("On, and not signed in. Press Sign in to link CMDR Quill Morrow.", rig.Link.Note(rig.Current));
    }

    [Fact]
    public async Task TurningItOffDeletesEveryCommandersSnapshotAndRemovesEachToken()
    {
        var rig = new Rig();
        await rig.SignInAsync();
        rig.Flying = Morrow;
        rig.Client.Answer = new WebAppSignInResult("token-morrow", "5678", null);
        await rig.SignInAsync();

        rig.Turn(on: false);

        Assert.Equal(["token-morrow", "token-vale"], rig.Client.Deleted.Order(StringComparer.Ordinal));
        Assert.Null(rig.Token(Vale));
        Assert.Null(rig.Token(Morrow));
        Assert.Equal("Off. Nothing is sent to the web app.", rig.Link.Note(rig.Current));
    }

    [Fact]
    public async Task AFailedDeleteKeepsTheTokenSaysSoAndTheNextStartTriesAgain()
    {
        var rig = new Rig();
        await rig.SignInAsync();
        rig.Client.Deletes = false;

        rig.Turn(on: false);

        Assert.Equal("token-vale", rig.Token(Vale));
        Assert.Equal(
            "Off. The snapshot is not deleted from the web app yet. D47 tries again at the next start.",
            rig.Link.Note(rig.Current));

        rig.Client.Deletes = true;
        rig.Link.Start();

        Assert.Equal(["token-vale", "token-vale"], rig.Client.Deleted);
        Assert.Null(rig.Token(Vale));
    }

    [Fact]
    public void AnotherSettingChangingDoesNotTryTheDeleteAgain()
    {
        var rig = new Rig(on: false);
        rig.Secrets.Set(CommanderSecret.Name(WebAppLink.TokenSecretName, Vale.FrontierId), "token-vale");

        rig.Link.SettingsChanged();

        Assert.Empty(rig.Client.Deleted);
    }

    [Fact]
    public async Task OnATestDriveNothingIsSentAndEveryTokenIsKept()
    {
        var rig = new Rig(canSend: false);
        rig.Secrets.Set(CommanderSecret.Name(WebAppLink.TokenSecretName, Vale.FrontierId), "token-vale");

        Assert.Equal("This is a test drive, so nothing is sent to the web app.", await rig.SignInAsync());
        Assert.False(rig.Link.CanSignIn(rig.Current));

        rig.Turn(on: false);
        rig.Link.Start();

        Assert.Equal(0, rig.Client.SignIns);
        Assert.Empty(rig.Client.Deleted);
        Assert.Equal("token-vale", rig.Token(Vale));
    }

    [Fact]
    public async Task SignInIsRefusedWhileOffOrBeforeACommanderIsKnown()
    {
        var off = new Rig(on: false);
        Assert.Equal("Turn on Web app first.", await off.SignInAsync());
        Assert.False(off.Link.CanSignIn(off.Current));

        var nobody = new Rig { Flying = null };
        Assert.Equal("D47 has not read a Commander from the journal yet. Load a game in Elite, then sign in.", await nobody.SignInAsync());

        Assert.Equal(0, off.Client.SignIns + nobody.Client.SignIns);
    }

    [Fact]
    public async Task AFailedSignInSaysWhyAndStoresNothing()
    {
        var rig = new Rig();
        rig.Client.Answer = WebAppSignInResult.Failed("D47 could not reach the web app.");

        Assert.Equal("D47 could not reach the web app.", await rig.SignInAsync());
        Assert.Null(rig.Token(Vale));
    }

    [Fact]
    public void TheChallengeIsTheS256OfTheVerifier()
    {
        // Worked out with Python's hashlib and base64.urlsafe_b64encode, padding stripped.
        Assert.Equal("TP3DZQl9rJnri9eRuZaFS6Q7czCrth7sEZC00LreApo", Pkce.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWEOEjXk"));

        var verifier = Pkce.NewVerifier();
        Assert.Equal(43, verifier.Length);
        Assert.Equal(43, Pkce.Challenge(verifier).Length);
        Assert.DoesNotContain('=', verifier);
    }
}
