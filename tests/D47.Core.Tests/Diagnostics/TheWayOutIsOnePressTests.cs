using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Diagnostics.Donation;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Diagnostics;

/// <summary>
/// The row a Commander withdraws from
/// (<a href="https://github.com/dseelinger/d47/issues/167">#167</a>).
/// <para>
/// <b>#167's criterion, stated as a test.</b> Consent is one button in a review pane; withdrawal
/// used to be deleting a file by hand and then asking somebody, in public, to delete the rest —
/// and *"withdrawal that is meaningfully harder than consent is a defect in the consent"*. So what
/// is asserted here is the shape of the way out: one press, on the page a Commander already opens
/// to ask what leaves, and unreachable from the tool surface.
/// </para>
/// <para>
/// <b>The row exists either way</b>, which is the "absent rows hide from every test" rule: a build
/// with no network composed still shows the identifier and still offers the local half, rather
/// than losing the row and taking the fault in it out of the suite's reach.
/// </para>
/// </summary>
public class TheWayOutIsOnePressTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d47-way-out").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string TokenFile => Path.Combine(_root, "donor-token.txt");

    private static SettingsService Settings(TempInstall install)
    {
        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        return new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance);
    }

    private SettingRow Row(TempInstall install, LongPress? forget) =>
        PrivacyCapability
            .Create(Settings(install), donorTokenFile: TokenFile, forgetDonations: forget)
            .Settings
            .Single(row => row.Key == PrivacyCapability.DonorKey);

    /// <summary>
    /// One press, and it is the press that reaches the store — not a second control beside the one
    /// that only forgets a file here.
    /// </summary>
    [Fact]
    public async Task WithSomewhereToAskThePressIsTheOneThatAsks()
    {
        using var install = new TempInstall();
        var asked = 0;

        var row = Row(install, (_, _) => { asked++; return Task.FromResult<string?>("done"); });

        // A row has one button. The synchronous press is the local-only one, and offering both
        // would be offering two different withdrawals from one row.
        Assert.Null(row.Press);
        Assert.NotNull(row.PressAsync);
        Assert.NotNull(row.PressLabel);

        await row.PressAsync!(new Progress<double>(), TestContext.Current.CancellationToken);

        Assert.Equal(1, asked);
    }

    /// <summary>
    /// The label says what the press actually does. "Forget it" was true when forgetting was all
    /// it did, and it is not the whole truth now that the press deletes at the store as well.
    /// </summary>
    [Fact]
    public void TheLabelSaysThatSomethingIsDeletedAndNotOnlyForgotten()
    {
        using var install = new TempInstall();

        Assert.Contains(
            "delete",
            Row(install, (_, _) => Task.FromResult<string?>(null)).PressLabel!,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// And the help says what a withdrawal does <b>not</b> reach, which is #167's central split:
    /// the data goes, the fix that came of it stays.
    /// </summary>
    [Fact]
    public void TheHelpSaysWhatSurvivesADeletion()
    {
        using var install = new TempInstall();
        var help = Row(install, (_, _) => Task.FromResult<string?>(null)).Help;

        Assert.Contains("stays fixed", help, StringComparison.Ordinal);
        Assert.Contains("post anywhere", help, StringComparison.Ordinal);
    }

    /// <summary>
    /// With nothing composed to ask with, the row keeps its identifier and its local press rather
    /// than disappearing — the designer's case, and every test that is not about donation.
    /// </summary>
    [Fact]
    public void WithNothingToAskWithTheRowIsStillThereAndStillForgets()
    {
        using var install = new TempInstall();

        Directory.CreateDirectory(_root);
        var token = DonorToken.Ensure(TokenFile);

        var row = Row(install, forget: null);

        Assert.Null(row.PressAsync);
        Assert.NotNull(row.Press);
        Assert.Contains(token, row.Binding!.Read(new D47Settings()), StringComparison.Ordinal);

        row.Press!();

        Assert.False(File.Exists(TokenFile));
    }

    /// <summary>
    /// <b>The receipt promises only what something enforces</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/167">#167</a>, raised by the retention
    /// lane). It said an excerpt is kept "30 days, or until the defect it was cut for is closed,
    /// whichever comes first" — and only the thirty days is a mechanism, being a lifecycle rule on
    /// the store. The other half was an intention written in the register of a guarantee, in the
    /// one document a donor keeps as evidence of what they were promised.
    /// </summary>
    [Fact]
    public void AnExcerptsReceiptPromisesTheRuleAndNotTheHabit()
    {
        var envelope = new DonationEnvelope(
            DonationEnvelope.CurrentFormat,
            DonationEnvelope.Excerpt,
            new string('a', DonorToken.Length),
            "0.90.0+abcdef",
            new DateTimeOffset(2026, 8, 29, 14, 25, 30, TimeSpan.Zero),
            Bytes: 12,
            Sha256: new string('b', 64));

        var receipt = DonationReceipt.Render(
            envelope,
            DonationOutcome.Stored("excerpts/a/one.md.gz"),
            "https://donate.invalid/donate",
            "excerpt.md",
            documentIsPayload: true);

        Assert.Contains("30 days", receipt, StringComparison.Ordinal);
        Assert.Contains("by a rule on the store", receipt, StringComparison.Ordinal);

        // The claim that nothing enforced. Its absence is the whole assertion.
        Assert.DoesNotContain("whichever comes first", receipt, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>And nothing on the tool surface can press it.</b> Info rows are refused by
    /// <see cref="SettingsService.Apply"/> outright, so the erasure needs no protected flag of its
    /// own — which matters more now that the press is destructive at a store rather than only on
    /// this disk.
    /// </summary>
    [Fact]
    public void TheModelCannotReachIt()
    {
        using var install = new TempInstall();
        var row = Row(install, (_, _) => Task.FromResult<string?>(null));

        Assert.Equal(SettingKind.Info, row.Kind);
        Assert.Null(row.Binding!.Write);
    }
}
