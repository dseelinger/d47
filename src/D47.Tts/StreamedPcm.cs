using D47.Core.Audio;
using Microsoft.Extensions.Logging;

namespace D47.Tts;

/// <summary>Reads a 24 kHz PCM response body into an <see cref="ArrivingClip"/> as it arrives.</summary>
internal static class StreamedPcm
{
    /// <summary>
    /// Appends the body to <paramref name="arriving"/>, upsampled, and ends it. Disposes
    /// <paramref name="response"/> and calls <paramref name="release"/> when the body ends.
    /// </summary>
    public static async Task AppendAsync(
        HttpResponseMessage response,
        ArrivingClip arriving,
        string provider,
        string text,
        ILogger logger,
        Action release,
        TtsFault fault,
        CancellationToken cancellationToken)
    {
        var excerpt = text.Length <= 40 ? text : text[..40] + "…";

        try
        {
            using (response)
            {
                var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

                await using (body.ConfigureAwait(false))
                {
                    var upsample = new PcmUpsampler();
                    var buffer = new byte[16 * 1024];
                    var received = 0L;
                    int read;

                    while ((read = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        received += read;
                        arriving.Append(upsample.Push(buffer.AsSpan(0, read)));
                    }

                    if (received == 0)
                    {
                        throw new TtsException($"{provider} returned no audio for \"{excerpt}\".");
                    }

                    arriving.Append(upsample.Finish());
                    arriving.Complete();
                }
            }
        }
        catch (OperationCanceledException cancelled)
        {
            arriving.Fail(cancelled);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Provider} stopped sending \"{Text}\" part-way", provider, excerpt);

            arriving.Fail(ex as TtsException
                          ?? new TtsException($"{provider} could not finish \"{excerpt}\": {ex.Message}", ex, fault));
        }
        finally
        {
            release();
        }
    }
}
