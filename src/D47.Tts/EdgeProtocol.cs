using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace D47.Tts;

/// <summary>The wire format of Edge's Read Aloud endpoint, separated from the socket that carries it.</summary>
internal static class EdgeProtocol
{
    /// <summary>The token Edge itself ships with.</summary>
    public const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";

    /// <summary>Tracks what current Edge actually ships.</summary>
    public const string ChromiumVersion = "143.0.3650.75";

    /// <summary>"143" from "143.0.3650.75".</summary>
    public static readonly string ChromiumMajor = ChromiumVersion.Split('.')[0];

    /// <summary>What the MP3 decodes to.</summary>
    public const int SourceSampleRate = 24_000;

    /// <summary>Major-version-only ("Chrome/143.0.0.0"), never the full build.</summary>
    public static readonly string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
        $"Chrome/{ChromiumMajor}.0.0.0 Safari/537.36 Edg/{ChromiumMajor}.0.0.0";

    /// <summary>A fresh randomly-generated MUID cookie, sent on every request.</summary>
    public static string MuidCookie() =>
        "muid=" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)) + ";";

    public const string Origin = "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold";

    /// <summary>The endpoint requires a rolling token derived from the clock and its own client token.</summary>
    public static string SecurityQuery(DateTimeOffset now)
    {
        // Windows file time in 100 ns units, rounded down to the enclosing 5 minutes.
        var seconds = now.ToUnixTimeSeconds() + 11_644_473_600L;
        seconds -= seconds % 300;

        var digest = Convert.ToHexString(
            SHA256.HashData(Encoding.ASCII.GetBytes($"{seconds * 10_000_000L}{TrustedClientToken}")));

        return $"Sec-MS-GEC={digest}&Sec-MS-GEC-Version=1-{ChromiumVersion}";
    }

    public static string Configuration(DateTimeOffset now) =>
        "X-Timestamp:" + Timestamp(now) + "\r\n" +
        "Content-Type:application/json; charset=utf-8\r\n" +
        "Path:speech.config\r\n\r\n" +
        """
        {"context":{"synthesis":{"audio":{"metadataoptions":{"sentenceBoundaryEnabled":"false","wordBoundaryEnabled":"false"},"outputFormat":"audio-24khz-48kbitrate-mono-mp3"}}}}
        """;

    public static string SsmlRequest(string text, string voice, double rate, string requestId, DateTimeOffset now)
    {
        var ssml =
            "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>" +
            $"<voice name='{Escape(voice)}'>" +
            $"<prosody pitch='+0Hz' rate='{RateAttribute(rate)}' volume='+0%'>" +
            Escape(text) +
            "</prosody></voice></speak>";

        return "X-RequestId:" + requestId + "\r\n" +
               "Content-Type:application/ssml+xml\r\n" +
               "X-Timestamp:" + Timestamp(now) + "\r\n" +
               "Path:ssml\r\n\r\n" +
               ssml;
    }

    /// <summary>Rate arrives normalised at 1.0 and leaves as the percentage offset the service wants.</summary>
    public static string RateAttribute(double rate)
    {
        var percent = (int)Math.Round((rate - 1.0) * 100);
        return percent >= 0
            ? "+" + percent.ToString(CultureInfo.InvariantCulture) + "%"
            : percent.ToString(CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>Untrusted text reaches here.</summary>
    public static string Escape(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("'", "&apos;", StringComparison.Ordinal);

    public static string Timestamp(DateTimeOffset now) =>
        now.UtcDateTime.ToString(
            "ddd MMM dd yyyy HH:mm:ss 'GMT+0000 (Coordinated Universal Time)'",
            CultureInfo.InvariantCulture);

    /// <summary>Splits a binary frame into its header and its audio.</summary>
    public static ReadOnlySpan<byte> AudioOf(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 2)
        {
            return [];
        }

        var start = 2 + ((frame[0] << 8) | frame[1]);
        return start < frame.Length ? frame[start..] : [];
    }

    /// <summary>24 kHz to the arbiter's 48 kHz.</summary>
    public static byte[] Upsample(ReadOnlySpan<byte> pcm) => PcmUpsample.Double(pcm);
}
