using System.Net;
using System.Net.Http.Headers;
using D47.App.Media;
using D47.Audio;
using NAudio.MediaFoundation;
using NAudio.Utils;
using NAudio.Wave;
using Xunit;

namespace D47.App.Tests.Media;

/// <summary>Media Foundation decodes an episode read through <see cref="HttpRangeStream"/>, seeking within it by range.</summary>
[Trait("Category", "Integration")]
public class AStreamedEpisodeDecodesFromRangesTests
{
    private static readonly Uri Enclosure = new("https://feeds.example/episode.mp3");

    [Fact]
    public void AnEpisodeServedInRangesDecodesAndSeeks()
    {
        var mp3 = Mp3(seconds: 2);
        using var http = new HttpClient(new RangeServer(mp3));
        using var stream = new MediaFoundationDecoder().Open(HttpRangeStream.Open(http, Enclosure));

        Assert.True(stream.CanSeek);
        Assert.InRange(stream.Length!.Value.TotalSeconds, 1.99, 2.1);

        stream.Seek(TimeSpan.FromSeconds(1));

        var buffer = new byte[4_096];
        var total = 0;
        int read;

        while ((read = stream.Read(buffer)) > 0)
        {
            total += read;
        }

        Assert.InRange(total, 48_000 * 4 * 90 / 100, 48_000 * 4 * 115 / 100);
    }

    private static byte[] Mp3(double seconds)
    {
        var format = new WaveFormat(44_100, 16, 2);
        var frames = (int)(format.SampleRate * seconds);
        var wav = new MemoryStream();

        using (var writer = new WaveFileWriter(new IgnoreDisposeStream(wav), format))
        {
            for (var frame = 0; frame < frames; frame++)
            {
                var sample = (float)(Math.Sin(2 * Math.PI * 440 * frame / format.SampleRate) * 0.5);
                writer.WriteSample(sample);
                writer.WriteSample(sample);
            }
        }

        wav.Position = 0;

        var mp3 = new MemoryStream();

        using (var reader = new WaveFileReader(wav))
        {
            MediaFoundationApi.Startup();
            MediaFoundationEncoder.EncodeToMp3(reader, mp3);
        }

        return mp3.ToArray();
    }

    /// <summary>Answers every request with the asked range of the file.</summary>
    private sealed class RangeServer(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var range = request.Headers.Range!.Ranges.First();
            var from = range.From!.Value;
            var to = Math.Min(range.To!.Value, content.Length - 1);
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent(content[(int)from..(int)(to + 1)]),
                RequestMessage = request,
            };

            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, to, content.Length);
            return Task.FromResult(response);
        }
    }
}
