using D47.Core;
using D47.Core.Diagnostics.Donation;
using Xunit;

namespace D47.Core.Tests.Diagnostics;

/// <summary>
/// The per-installation donor token
/// (<a href="https://github.com/dseelinger/d47/issues/176">#176</a>).
/// <para>
/// <b>What is under test is a reversal, not a feature.</b> <see cref="Pseudonyms"/> used to
/// guarantee that two donations from one Commander could not be joined, and an accumulating corpus
/// cannot be had at the same time as that guarantee. So these assert the two properties the weaker
/// claim rests on: the token identifies an <i>install</i> and is capable of identifying nothing
/// else, and getting rid of it is one file delete.
/// </para>
/// </summary>
public class ADonationSaysWhoseItIsWithoutSayingWhoTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d47-donor").FullName;

    private string File_ => Path.Combine(_root, "donor-token.txt");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>
    /// <b>The strongest thing this suite can assert about "never derived".</b> Two fresh tokens
    /// from the same machine, the same process and the same second differ — so nothing about the
    /// machine, the Commander or the moment is in them. A derived token would agree with itself.
    /// </summary>
    [Fact]
    public void TwoTokensFromOneMachineAreDifferent()
    {
        var tokens = Enumerable.Range(0, 64).Select(_ => DonorToken.NewToken()).ToList();

        Assert.Equal(tokens.Count, tokens.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>The shape the endpoint checks, and the shape that cannot contain a path.</summary>
    [Fact]
    public void ATokenIsThirtyTwoLowercaseHexCharacters()
    {
        var token = DonorToken.NewToken();

        Assert.Equal(32, token.Length);
        Assert.True(DonorToken.IsWellFormed(token));
        Assert.All(token, character => Assert.Contains(character, "0123456789abcdef"));
    }

    /// <summary>
    /// Anything else is no token at all, which matters most on the way <em>out</em>: a hand-edited
    /// file must fail as "there is none" rather than travel as one.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("not a token")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("../../etc/passwd")]
    [InlineData("0123456789abcdef0123456789abcde")]
    [InlineData("0123456789abcdef0123456789abcdef0")]
    public void AnythingElseIsNotAToken(string candidate) =>
        Assert.False(DonorToken.IsWellFormed(candidate));

    /// <summary>
    /// <b>A read is a read.</b> An installation that has never donated has no identifier and no
    /// file, and asking whether it has one must not be what creates it.
    /// </summary>
    [Fact]
    public void ReadingDoesNotMintOne()
    {
        Assert.Null(DonorToken.Read(File_));
        Assert.False(System.IO.File.Exists(File_));
    }

    /// <summary>The same installation keeps the same identifier, which is the whole point of it.</summary>
    [Fact]
    public void TheSameInstallationKeepsTheSameToken()
    {
        var first = DonorToken.Ensure(File_);

        Assert.Equal(first, DonorToken.Ensure(File_));
        Assert.Equal(first, DonorToken.Read(File_));
    }

    /// <summary>
    /// Withdrawal, and it is #167's "no harder than consent" criterion made literal: one file, one
    /// delete, nothing posted anywhere.
    /// </summary>
    [Fact]
    public void ForgettingItEndsTheGroupingAndStartsANewOne()
    {
        var first = DonorToken.Ensure(File_);

        Assert.Equal(first, DonorToken.Forget(File_));
        Assert.Null(DonorToken.Read(File_));
        Assert.NotEqual(first, DonorToken.Ensure(File_));
    }

    /// <summary>A file that was hand-edited into nonsense is no identifier, and is replaced.</summary>
    [Fact]
    public void AMangledFileIsNotAnIdentity()
    {
        System.IO.File.WriteAllText(File_, "CMDR ALPHA");

        Assert.Null(DonorToken.Read(File_));
        Assert.True(DonorToken.IsWellFormed(DonorToken.Ensure(File_)));
    }

    /// <summary>
    /// It lives beside everything else d47 writes, which is what makes an ordinary upgrade keep it
    /// — the installer and <c>get-local</c> never touch <c>data\</c>.
    /// </summary>
    [Fact]
    public void ItLivesInTheDataFolder()
    {
        var paths = new AppPaths(_root);

        Assert.Equal(paths.Data, Path.GetDirectoryName(paths.DonorTokenFile));
        Assert.Equal(paths.Data, Path.GetDirectoryName(paths.Donations));
    }

    /// <summary>
    /// What a Commander is told. <b>Both halves matter</b>: that it is not derived from them, and
    /// that forgetting it is forward-only.
    /// </summary>
    [Fact]
    public void TheSummarySaysWhatItIsAndWhatForgettingItDoes()
    {
        var summary = DonorToken.Summarise(DonorToken.Ensure(File_));

        Assert.Contains("not derived", summary, StringComparison.Ordinal);
        Assert.Contains("donations and nothing else", summary, StringComparison.Ordinal);
        Assert.Contains("future", summary, StringComparison.Ordinal);
    }

    /// <summary>And nothing at all is said to have an identifier it does not have.</summary>
    [Fact]
    public void WithNoTokenTheSummarySaysThereIsNone() =>
        Assert.Contains(
            "No donation identifier",
            DonorToken.Summarise(null),
            StringComparison.Ordinal);
}
