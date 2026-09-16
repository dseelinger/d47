using D47.Donations.R2;
using Xunit;

namespace D47.Donations.Tests;

/// <summary>
/// The request line and the signature are built from the same canonical strings. Signing one spelling
/// and sending another is the failure this gate exists for, and R2 reports it only as a refusal.
/// </summary>
public class TheSignatureIsOverWhatIsActuallySentTests
{
    private static readonly DateTimeOffset When = new(2026, 9, 14, 10, 9, 26, TimeSpan.Zero);
    private const string Host = "account.r2.cloudflarestorage.com";

    private static readonly R2Credentials Credentials = new("account", "AKIDEXAMPLE", "a-secret");

    [Fact]
    public void TheQueryIsEncodedThenSortedOnTheEncodedName()
    {
        // A continuation token is base64 and carries the three characters that must not travel raw.
        Assert.Equal(
            "continuation-token=a%2Bb%2Fc%3D&list-type=2",
            SignatureV4.CanonicalQuery([("list-type", "2"), ("continuation-token", "a+b/c=")]));
    }

    [Fact]
    public void UnreservedCharactersSurviveAndEverythingElseIsPercentEncoded()
    {
        Assert.Equal("~-._Aa0", SignatureV4.Encode("~-._Aa0"));
        Assert.Equal("%20", SignatureV4.Encode(" "));
        Assert.Equal("%2F", SignatureV4.Encode("/"));
    }

    [Fact]
    public void EverySegmentOfThePathIsEncodedAndTheSeparatorsAreNot()
    {
        Assert.Equal(
            "/d47-donations/excerpts/0123/20260914T100926Z-aa.md.gz",
            SignatureV4.CanonicalPath("/d47-donations/excerpts/0123/20260914T100926Z-aa.md.gz"));

        Assert.Equal("/a%20b/c", SignatureV4.CanonicalPath("/a b/c"));
    }

    [Fact]
    public void TheCanonicalRequestIsTheNineLinesAwsSpecifies()
    {
        var lines = SignatureV4
            .CanonicalRequest("GET", "/d47-donations", [("list-type", "2")], Host, When)
            .Split('\n');

        Assert.Equal("GET", lines[0]);
        Assert.Equal("/d47-donations", lines[1]);
        Assert.Equal("list-type=2", lines[2]);
        Assert.Equal($"host:{Host}", lines[3]);
        Assert.Equal($"x-amz-content-sha256:{SignatureV4.EmptyPayloadSha256}", lines[4]);
        Assert.Equal("x-amz-date:20260914T100926Z", lines[5]);
        Assert.Equal(string.Empty, lines[6]);
        Assert.Equal("host;x-amz-content-sha256;x-amz-date", lines[7]);
        Assert.Equal(SignatureV4.EmptyPayloadSha256, lines[8]);
    }

    [Fact]
    public void TheScopeNamesTheDayTheRegionAndTheService() =>
        Assert.Equal("20260914/auto/s3/aws4_request", SignatureV4.Scope(When));

    [Fact]
    public void TheAuthorizationCarriesTheCredentialScopeAndASignature()
    {
        var authorization = SignatureV4.Authorization(
            Credentials, "GET", "/d47-donations", [("list-type", "2")], Host, When);

        Assert.StartsWith(
            "AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/20260914/auto/s3/aws4_request, "
            + "SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature=",
            authorization,
            StringComparison.Ordinal);

        var signature = authorization[(authorization.IndexOf("Signature=", StringComparison.Ordinal) + 10)..];

        Assert.Equal(64, signature.Length);
        Assert.All(signature, character => Assert.True(char.IsAsciiHexDigitLower(character)));
    }

    [Fact]
    public void ADifferentSecretProducesADifferentSignature()
    {
        var one = SignatureV4.Authorization(Credentials, "GET", "/d47-donations", [], Host, When);
        var other = SignatureV4.Authorization(
            Credentials with { SecretAccessKey = "another-secret" }, "GET", "/d47-donations", [], Host, When);

        Assert.NotEqual(one, other);
    }

    [Fact]
    public void TheSameRequestSignsTheSameWayTwice() =>
        Assert.Equal(
            SignatureV4.Authorization(Credentials, "GET", "/d47-donations", [("list-type", "2")], Host, When),
            SignatureV4.Authorization(Credentials, "GET", "/d47-donations", [("list-type", "2")], Host, When));
}
