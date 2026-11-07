using System.Security.Cryptography;
using System.Text;

namespace D47.App.Tests;

internal static class DonationHash
{
    /// <summary>SHA-256 of the bytes, lowercase hex.</summary>
    public static string Of(byte[] payload) => Convert.ToHexStringLower(SHA256.HashData(payload));

    /// <summary>SHA-256 of the text as UTF-8, lowercase hex.</summary>
    public static string Of(string payload) => Of(Encoding.UTF8.GetBytes(payload));
}
