using D47.Core.Speech;
using D47.Core.Storage;
using Xunit;

namespace D47.App.Tests;

/// <summary>Puts every file the local voice needs into a file system, so it reports itself installed and lists its voices.</summary>
internal static class SeededKokoro
{
    public static void Install(IFileSystem files, string folder)
    {
        foreach (var name in new[] { "model.onnx", "tokenizer.json", "phoneme_dict.json" })
        {
            files.WriteBytes(Path.Combine(folder, name), [0]);
        }

        foreach (var voice in KokoroAssets.VoiceIds)
        {
            files.WriteBytes(Path.Combine(folder, "voices", voice + ".bin"), [0]);
        }
    }

    /// <summary>Copies the clips and catalogue shipped beside the test binary into the file system.</summary>
    [Trait("Category", "Integration")]
    public static void CopyShippedChatterboxClips(IFileSystem files)
    {
        var shipped = Path.Combine(AppContext.BaseDirectory, "voices", "chatterbox");

        foreach (var file in Directory.EnumerateFiles(shipped))
        {
            files.WriteBytes(file, File.ReadAllBytes(file));
        }
    }
}
