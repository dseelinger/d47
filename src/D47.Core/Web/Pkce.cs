using System.Security.Cryptography;
using System.Text;

namespace D47.Core.Web;

/// <summary>The PKCE verifier and S256 challenge a web app sign-in proves itself with (RFC 7636).</summary>
public static class Pkce
{
    /// <summary>32 random bytes as base64url: 43 characters.</summary>
    public static string NewVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>The SHA-256 of the verifier's ASCII bytes, as base64url.</summary>
    public static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
