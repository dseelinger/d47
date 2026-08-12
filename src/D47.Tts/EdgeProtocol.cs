using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace D47.Tts;

/// <summary>
/// The wire format of Edge's Read Aloud endpoint, separated from the socket that carries it.
/// <para>
/// Split out because this is the part that can be wrong without the network being involved:
/// an unescaped ship name, a rate expressed in the wrong units, an upsample that halves the
/// pitch. All of those are testable offline, and none of them would be if they only existed
/// inside a method that opens a websocket.
/// </para>
/// </summary>
internal static class EdgeProtocol
{
    /// <summary>The token Edge itself ships with. Public knowledge, not a secret of ours.</summary>
    public const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";

    /// <summary>
    /// Tracks what current Edge actually ships. The service checks this against expectations:
    /// the working reference (edge-tts 7.2.8, verified against this endpoint from this machine)
    /// carries 143, and 130 gets a flat 403.
    /// </summary>
    public const string ChromiumVersion = "143.0.3650.75";

    /// <summary>"143" from "143.0.3650.75". The User-Agent wants major-only, below.</summary>
    public static readonly string ChromiumMajor = ChromiumVersion.Split('.')[0];

    /// <summary>
    /// What the MP3 decodes to. Raw PCM output was removed from this endpoint in mid-2026 —
    /// the service now closes the socket with "Unsupported Edge output format" for the raw
    /// and riff formats, so 24 kHz mono MP3 (the format Edge itself uses) is what there is.
    /// The arbiter runs at 48 kHz, so decoded audio is upsampled 2× — an exact integer ratio.
    /// </summary>
    public const int SourceSampleRate = 24_000;

    /// <summary>
    /// Major-version-only ("Chrome/143.0.0.0"), never the full build. Real Chromium reduces the
    /// UA this way, and the full version appearing in a UA is exactly the kind of tell the
    /// service's checks look for. The full version goes in Sec-MS-GEC-Version instead.
    /// </summary>
    public static readonly string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
        $"Chrome/{ChromiumMajor}.0.0.0 Safari/537.36 Edg/{ChromiumMajor}.0.0.0";

    /// <summary>
    /// A fresh randomly-generated MUID cookie, sent on every request. Required as of mid-2026:
    /// the endpoint 403s any request without one, which is precisely how d47 went mute while
    /// edge-tts 7.x (which sends it) kept working. Random per request rather than stored —
    /// this is an impersonation of a browser cookie, not a durable identity, and d47 does not
    /// keep identifiers it does not need.
    /// </summary>
    public static string MuidCookie() =>
        "muid=" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)) + ";";

    public const string Origin = "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold";

    /// <summary>
    /// The endpoint requires a rolling token derived from the clock and its own client token.
    /// It rounds to a five-minute window, so a machine within a few minutes of correct time
    /// works and one badly out of sync gets a 403 — worth knowing, because that failure looks
    /// like nothing else this app can produce.
    /// </summary>
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

    /// <summary>
    /// Rate arrives normalised at 1.0 and leaves as the percentage offset the service wants.
    /// Normalising at the seam is what keeps per-provider rate units out of settings
    /// (list.md Phase 11).
    /// </summary>
    public static string RateAttribute(double rate)
    {
        var percent = (int)Math.Round((rate - 1.0) * 100);
        return percent >= 0
            ? "+" + percent.ToString(CultureInfo.InvariantCulture) + "%"
            : percent.ToString(CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>
    /// Untrusted text reaches here. A ship name or an in-game message containing a bare angle
    /// bracket would otherwise close the prosody element and let the rest be read as markup —
    /// which at minimum garbles the reply, and at worst lets a hostile name pick the voice the
    /// remainder is spoken in (architecture.md §7).
    /// </summary>
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

    /// <summary>
    /// Splits a binary frame into its header and its audio. Two bytes of big-endian header
    /// length, the header, then the payload.
    /// </summary>
    public static ReadOnlySpan<byte> AudioOf(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 2)
        {
            return [];
        }

        var start = 2 + ((frame[0] << 8) | frame[1]);
        return start < frame.Length ? frame[start..] : [];
    }

    /// <summary>
    /// 24 kHz to the arbiter's 48 kHz. An exact 2× ratio, so each output pair is the original
    /// sample and the midpoint to the next — linear interpolation, which for speech is
    /// inaudible against the alternative and costs one pass.
    /// </summary>
    public static byte[] Upsample(ReadOnlySpan<byte> pcm)
    {
        var samples = pcm.Length / 2;
        var output = new byte[samples * 4];

        for (var i = 0; i < samples; i++)
        {
            var current = (short)(pcm[i * 2] | (pcm[(i * 2) + 1] << 8));
            var next = i + 1 < samples
                ? (short)(pcm[(i + 1) * 2] | (pcm[((i + 1) * 2) + 1] << 8))
                : current;

            var midpoint = (short)((current + next) / 2);

            output[i * 4] = (byte)current;
            output[(i * 4) + 1] = (byte)(current >> 8);
            output[(i * 4) + 2] = (byte)midpoint;
            output[(i * 4) + 3] = (byte)(midpoint >> 8);
        }

        return output;
    }
}
