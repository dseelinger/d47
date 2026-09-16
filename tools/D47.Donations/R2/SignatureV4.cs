using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace D47.Donations.R2;

/// <summary>
/// AWS Signature Version 4, which is how R2's S3-compatible endpoint authenticates a request.
/// </summary>
/// <remarks>
/// Only what a reader needs: GET and HEAD, and therefore an empty payload every time.
/// </remarks>
public static class SignatureV4
{
    public const string Algorithm = "AWS4-HMAC-SHA256";

    /// <summary>R2 has one region and it is spelled this way.</summary>
    public const string Region = "auto";

    public const string Service = "s3";

    /// <summary>SHA-256 of no bytes. Every request signed here carries no body.</summary>
    public const string EmptyPayloadSha256 =
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    /// <summary>The header set this signs, which is also the set a caller must actually send.</summary>
    public static IReadOnlyList<string> SignedHeaderNames => ["host", "x-amz-content-sha256", "x-amz-date"];

    public static string AmzDate(DateTimeOffset when) =>
        when.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    public static string DateStamp(DateTimeOffset when) =>
        when.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    public static string Scope(DateTimeOffset when) =>
        $"{DateStamp(when)}/{Region}/{Service}/aws4_request";

    /// <summary>
    /// The canonical request: method, path, query, the signed headers, and the payload hash.
    /// </summary>
    public static string CanonicalRequest(
        string method,
        string path,
        IReadOnlyList<(string Name, string Value)> query,
        string host,
        DateTimeOffset when)
    {
        var headers = string.Concat(
            $"host:{host}\n",
            $"x-amz-content-sha256:{EmptyPayloadSha256}\n",
            $"x-amz-date:{AmzDate(when)}\n");

        return string.Join(
            '\n',
            method,
            CanonicalPath(path),
            CanonicalQuery(query),
            headers,
            string.Join(';', SignedHeaderNames),
            EmptyPayloadSha256);
    }

    /// <summary>
    /// The query as both the signature and the request line must spell it: encoded, then sorted on the
    /// encoded name.
    /// </summary>
    public static string CanonicalQuery(IReadOnlyList<(string Name, string Value)> query) =>
        string.Join(
            '&',
            query
                .Select(parameter => (Name: Encode(parameter.Name), Value: Encode(parameter.Value)))
                .OrderBy(parameter => parameter.Name, StringComparer.Ordinal)
                .Select(parameter => $"{parameter.Name}={parameter.Value}"));

    public static string StringToSign(DateTimeOffset when, string canonicalRequest) =>
        string.Join(
            '\n',
            Algorithm,
            AmzDate(when),
            Scope(when),
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))));

    public static byte[] SigningKey(string secretAccessKey, DateTimeOffset when)
    {
        var date = Hmac(Encoding.UTF8.GetBytes($"AWS4{secretAccessKey}"), DateStamp(when));
        var region = Hmac(date, Region);
        var service = Hmac(region, Service);

        return Hmac(service, "aws4_request");
    }

    /// <summary>The value for the <c>Authorization</c> header.</summary>
    public static string Authorization(
        R2Credentials credentials,
        string method,
        string path,
        IReadOnlyList<(string Name, string Value)> query,
        string host,
        DateTimeOffset when)
    {
        var signature = Convert.ToHexStringLower(
            Hmac(
                SigningKey(credentials.SecretAccessKey, when),
                StringToSign(when, CanonicalRequest(method, path, query, host, when))));

        return $"{Algorithm} "
               + $"Credential={credentials.AccessKeyId}/{Scope(when)}, "
               + $"SignedHeaders={string.Join(';', SignedHeaderNames)}, "
               + $"Signature={signature}";
    }

    /// <summary>Each segment encoded, the separators left alone.</summary>
    public static string CanonicalPath(string path) =>
        string.Join('/', path.Split('/').Select(Encode));

    /// <summary>RFC 3986 percent-encoding, over the unreserved set SigV4 specifies.</summary>
    public static string Encode(string value)
    {
        var encoded = new StringBuilder(value.Length);

        foreach (var octet in Encoding.UTF8.GetBytes(value))
        {
            var character = (char)octet;

            if (char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or '~')
            {
                encoded.Append(character);
            }
            else
            {
                encoded.Append(CultureInfo.InvariantCulture, $"%{octet:X2}");
            }
        }

        return encoded.ToString();
    }

    private static byte[] Hmac(byte[] key, string message) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(message));
}
