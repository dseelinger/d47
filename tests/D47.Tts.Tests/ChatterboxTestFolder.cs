using D47.Core.Audio;

namespace D47.Tts.Tests;

/// <summary>A temporary model folder with a tiny tokenizer.json, and a voices folder with one valid clip.</summary>
internal sealed class ChatterboxTestFolder : IDisposable
{
    public ChatterboxTestFolder()
    {
        Root = Path.Combine(Path.GetTempPath(), "d47-chatterbox-" + Guid.NewGuid().ToString("N"));
        Models = Path.Combine(Root, "models");
        Voices = Path.Combine(Root, "voices");
        Fetched = Path.Combine(Root, "data", "voices", "chatterbox");

        Directory.CreateDirectory(Models);
        Directory.CreateDirectory(Voices);

        File.WriteAllText(Path.Combine(Models, "tokenizer.json"), Tokenizer);
        File.WriteAllText(
            Path.Combine(Voices, ChatterboxVoices.TableName),
            "id\tname\tgender\tlocale\trole\tsource\nmarlow\tMarlow\tfemale\ten\t\ta test clip\n");
        File.WriteAllBytes(
            Path.Combine(Voices, "marlow.wav"),
            WavWriter.ToBytes(new float[ChatterboxVoices.SampleRate * 6], ChatterboxVoices.SampleRate));
    }

    public string Root { get; }

    public string Models { get; }

    public string Voices { get; }

    /// <summary>Where fetched clips are kept; not created.</summary>
    public string Fetched { get; }

    /// <summary>A download that fails, for a test that must never reach the network.</summary>
    public static Task<byte[]> NoDownload(Uri url, long bytes, CancellationToken cancellationToken) =>
        throw new InvalidOperationException($"No download was expected, and {url} was asked for.");

    /// <summary>
    /// A byte-level BPE over "h", "i", "!" and the space byte "Ġ", merging "h"+"i", with two tags and the
    /// end-of-text pair the real template appends.
    /// </summary>
    private const string Tokenizer = """
        {
          "added_tokens": [
            { "id": 50256, "content": "<|endoftext|>" },
            { "id": 50268, "content": "[sigh]" },
            { "id": 50275, "content": "[laugh]" }
          ],
          "post_processor": {
            "type": "TemplateProcessing",
            "single": [
              { "Sequence": { "id": "A", "type_id": 0 } },
              { "SpecialToken": { "id": "<|endoftext|>", "type_id": 0 } },
              { "SpecialToken": { "id": "<|endoftext|>", "type_id": 0 } }
            ]
          },
          "model": {
            "vocab": { "h": 1, "i": 2, "!": 3, "Ġ": 4, "hi": 5, "Ġhi": 6 },
            "merges": [ ["h", "i"], ["Ġ", "hi"] ]
          }
        }
        """;

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
