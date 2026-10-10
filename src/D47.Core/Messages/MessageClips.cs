using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Storage;

namespace D47.Core.Messages;

/// <summary>
/// The clips spoken messages keep, one file per message in <c>data\messages\</c>: a WAV, or for a line in the
/// Commander's own voice the WAV protected with <see cref="ISecretProtector"/>, decrypted into memory to play.
/// </summary>
public sealed class MessageClips(IFileSystem files, string folder, ISecretProtector protector)
{
    public const string PlainExtension = ".wav";

    public const string ProtectedExtension = ".own";

    public string Folder => folder;

    public static bool IsProtected(string file) =>
        file.EndsWith(ProtectedExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>Writes the clip for one message and returns its file name.</summary>
    public string Save(string messageKey, SpokenClip spoken)
    {
        ArgumentNullException.ThrowIfNull(spoken);

        var clip = spoken.Joined(messageKey);
        var wav = WavWriter.ToBytes(clip.Pcm.Span, clip.Format);
        var file = messageKey + (spoken.Own ? ProtectedExtension : PlainExtension);

        if (spoken.Own)
        {
            var plain = wav;
            wav = protector.Protect(plain);
            Array.Clear(plain);
        }

        files.WriteBytes(Path.Combine(folder, file), wav);

        return file;
    }

    /// <summary>The clip, or null when the file is missing, unreadable, or does not decrypt for this Windows user.</summary>
    public AudioClip? Load(string file, string name)
    {
        var path = Path.Combine(folder, Path.GetFileName(file));

        try
        {
            if (files.ReadBytes(path) is not { } bytes)
            {
                return null;
            }

            if (IsProtected(file))
            {
                if (!protector.TryUnprotect(bytes, out var plain))
                {
                    return null;
                }

                bytes = plain;
            }

            using var stream = new MemoryStream(bytes);
            return WavReader.Read(stream, name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or WavFormatException or EndOfStreamException)
        {
            return null;
        }
    }

    public void Delete(string file)
    {
        try
        {
            files.Delete(Path.Combine(folder, Path.GetFileName(file)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left for the next sweep.
        }
    }

    /// <summary>Deletes every file in the folder whose name <paramref name="kept"/> does not hold.</summary>
    public void Sweep(IReadOnlySet<string> kept)
    {
        IReadOnlyList<string> present;

        try
        {
            present = files.Enumerate(folder, "*");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var path in present)
        {
            if (!kept.Contains(Path.GetFileName(path)))
            {
                Delete(path);
            }
        }
    }
}
