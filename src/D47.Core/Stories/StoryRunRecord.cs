using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Messages;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Stories;

/// <summary>One line a story run posted, as its record keeps it.</summary>
public sealed record StoryRunLine
{
    public DateTimeOffset Sent { get; init; }

    public string From { get; init; } = string.Empty;

    public string? Cast { get; init; }

    public string? Picture { get; init; }

    public string Subject { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    /// <summary>The message key it was posted under.</summary>
    public string Key { get; init; } = string.Empty;

    public string? AdventureKey { get; init; }

    /// <summary>The clip's file name in the run's folder.</summary>
    public string? Clip { get; init; }

    /// <summary>The provider and voice that spoke the clip.</summary>
    public MessageVoice? Voice { get; init; }
}

/// <summary>
/// Each stock story run's own record in <c>data\story-runs\&lt;run&gt;\</c>: the lines it posted, appended to
/// <see cref="LinesFile"/>, and their clips compressed, a clip in the Commander's own voice protected. Lines of no stock story
/// are not kept.
/// </summary>
public sealed class StoryRunRecord(
    IFileSystem files,
    string folder,
    StoryStore stories,
    Func<string?> commander,
    IClipCompressor? compressor,
    ISecretProtector protector,
    ILogger logger) : IMessageRecord
{
    public const string LinesFile = "lines.jsonl";

    private const string NoCommanderSegment = "none";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lock _gate = new();

    /// <summary>The run folder of each line posted this session, by message key, for a clip attached after the post.</summary>
    private readonly ConcurrentDictionary<string, string> _runOf = new(StringComparer.Ordinal);

    /// <summary>The run's folder name: the Commander, the story id and <see cref="Story.PickedAt"/> to the tick, in UTC.</summary>
    public static string FolderName(string? frontierId, string storyId, DateTimeOffset pickedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storyId);

        var who = string.IsNullOrEmpty(frontierId) ? NoCommanderSegment : frontierId;
        var at = pickedAt.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfffffff'Z'", CultureInfo.InvariantCulture);

        return $"{Segment(who)}_{Segment(storyId)}_{at}";
    }

    /// <summary>The run's lines in the order they were posted, each with the newest clip attached to it.</summary>
    public IReadOnlyList<StoryRunLine> Read(string? frontierId, Story run)
    {
        ArgumentNullException.ThrowIfNull(run);

        lock (_gate)
        {
            return Fold(Path.Combine(RunFolder(frontierId, run), LinesFile));
        }
    }

    /// <summary>Whether a line posted under <paramref name="adventureKey"/> goes to a stock story run's record.</summary>
    public bool Keeps(string adventureKey)
    {
        ArgumentNullException.ThrowIfNull(adventureKey);

        return RunOf(adventureKey) is not null;
    }

    /// <summary>The clip a recorded line kept, decrypted when it is in the Commander's own voice; null when it has none or it cannot be read.</summary>
    public AudioClip? Clip(string? frontierId, Story run, StoryRunLine line)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(line);

        if (line.Clip is not { } file)
        {
            return null;
        }

        var name = Path.GetFileName(file);

        try
        {
            byte[]? bytes;

            lock (_gate)
            {
                bytes = files.ReadBytes(Path.Combine(RunFolder(frontierId, run), name));
            }

            if (bytes is null)
            {
                return null;
            }

            if (MessageClips.IsProtected(name))
            {
                if (!protector.TryUnprotect(bytes, out var plain))
                {
                    return null;
                }

                bytes = plain;
                name = name[..^MessageClips.ProtectedExtension.Length];
            }

            if (name.EndsWith(MessageClips.PlainExtension, StringComparison.OrdinalIgnoreCase))
            {
                using var stream = new MemoryStream(bytes);
                return WavReader.Read(stream, line.Body);
            }

            return compressor?.Decompress(bytes, line.Body);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or WavFormatException or EndOfStreamException)
        {
            logger.LogWarning(ex, "Could not read the clip of message {Key}", line.Key);
            return null;
        }
    }

    public void Posted(D47Message message, SpokenClip? spoken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.AdventureKey is not { } key || RunOf(key) is not var (frontierId, run))
        {
            return;
        }

        var runFolder = RunFolder(frontierId, run);
        var clip = spoken is null ? null : Encode(message.Key, spoken);

        var line = new StoryRunLine
        {
            Sent = message.Sent,
            From = message.From,
            Cast = message.Cast,
            Picture = message.Picture,
            Subject = message.Subject,
            Body = message.Body,
            Key = message.Key,
            AdventureKey = key,
        };

        _runOf[message.Key] = runFolder;

        lock (_gate)
        {
            try
            {
                if (clip is var (file, bytes) && Keep(runFolder, message.Key, file, bytes))
                {
                    line = line with { Clip = file, Voice = new MessageVoice(spoken!.Provider, spoken.VoiceId) };
                }

                files.AppendText(Path.Combine(runFolder, LinesFile), JsonSerializer.Serialize(line, Json) + "\n");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not record message {Key} in {Folder}", message.Key, runFolder);
            }
        }
    }

    public void Attached(string messageKey, SpokenClip spoken)
    {
        ArgumentNullException.ThrowIfNull(spoken);

        if (!_runOf.TryGetValue(messageKey, out var runFolder) || Encode(messageKey, spoken) is not var (file, bytes))
        {
            return;
        }

        var entry = new StoryRunLine { Key = messageKey, Clip = file, Voice = new MessageVoice(spoken.Provider, spoken.VoiceId) };

        lock (_gate)
        {
            try
            {
                if (Keep(runFolder, messageKey, file, bytes))
                {
                    files.AppendText(Path.Combine(runFolder, LinesFile), JsonSerializer.Serialize(entry, Json) + "\n");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not record the clip of message {Key} in {Folder}", messageKey, runFolder);
            }
        }
    }

    /// <summary>Keeps <paramref name="spoken"/> as the clip of a recorded line; the line with it, or null when it could not be kept.</summary>
    public StoryRunLine? KeepClip(string? frontierId, Story run, StoryRunLine line, SpokenClip spoken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(spoken);

        if (Encode(line.Key, spoken) is not var (file, bytes))
        {
            return null;
        }

        var runFolder = RunFolder(frontierId, run);
        var voice = new MessageVoice(spoken.Provider, spoken.VoiceId);
        var entry = new StoryRunLine { Key = line.Key, Clip = file, Voice = voice };

        lock (_gate)
        {
            try
            {
                if (!Keep(runFolder, line.Key, file, bytes))
                {
                    return null;
                }

                files.AppendText(Path.Combine(runFolder, LinesFile), JsonSerializer.Serialize(entry, Json) + "\n");
                return line with { Clip = file, Voice = voice };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not record the clip of message {Key} in {Folder}", line.Key, runFolder);
                return null;
            }
        }
    }

    /// <summary>
    /// Fills the record of each run that has none from <paramref name="messages"/>, by the keys <see cref="Posted"/> uses, and
    /// returns the number of runs filled. A run with a record is left alone. Lines keep their time and order; a line whose
    /// clip <paramref name="clipOf"/> returns null is recorded without one. The lines file is written last, so a run
    /// interrupted before that is filled again.
    /// </summary>
    public int Backfill(IReadOnlyList<D47Message> messages, Func<D47Message, SpokenClip?> clipOf)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(clipOf);

        var byRun = new Dictionary<string, List<D47Message>>(StringComparer.OrdinalIgnoreCase);

        foreach (var message in messages.OrderBy(message => message.Sent))
        {
            if (message.AdventureKey is { } key && RunOf(key) is var (frontierId, run))
            {
                var runFolder = RunFolder(frontierId, run);

                if (!byRun.TryGetValue(runFolder, out var lines))
                {
                    byRun[runFolder] = lines = [];
                }

                lines.Add(message);
            }
        }

        var filled = 0;

        foreach (var (runFolder, lines) in byRun)
        {
            lock (_gate)
            {
                try
                {
                    if (files.Stat(Path.Combine(runFolder, LinesFile)) is not null)
                    {
                        continue;
                    }

                    var recorded = new List<StoryRunLine>(lines.Count);

                    foreach (var message in lines)
                    {
                        var line = new StoryRunLine
                        {
                            Sent = message.Sent,
                            From = message.From,
                            Cast = message.Cast,
                            Picture = message.Picture,
                            Subject = message.Subject,
                            Body = message.Body,
                            Key = message.Key,
                            AdventureKey = message.AdventureKey,
                        };

                        if (clipOf(message) is { } spoken
                            && Encode(message.Key, spoken) is var (file, bytes)
                            && Keep(runFolder, message.Key, file, bytes))
                        {
                            line = line with { Clip = file, Voice = new MessageVoice(spoken.Provider, spoken.VoiceId) };
                        }

                        recorded.Add(line);
                    }

                    files.WriteText(
                        Path.Combine(runFolder, LinesFile),
                        string.Concat(recorded.Select(line => JsonSerializer.Serialize(line, Json) + "\n")));
                    filled++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(ex, "Could not fill the record in {Folder}", runFolder);
                }
            }
        }

        return filled;
    }

    /// <summary>The clip of a message in <c>data\messages\</c> as a <see cref="SpokenClip"/>; null when it has none or cannot be read.</summary>
    public static Func<D47Message, SpokenClip?> StoredClips(MessageClips clips) => message =>
    {
        if (message.Clip is not { } file || clips.Load(file, message.Key) is not { } clip)
        {
            return null;
        }

        var voiceId = message.Voice?.Id;
        var own = MessageClips.IsProtected(file);

        return new SpokenClip([clip], message.Voice?.Provider ?? string.Empty, own && !MessageStore.SpokenInOwnVoice(message.Voice) ? OwnVoice.VoiceId : voiceId);
    };

    public void ForgetOwnVoice() => Forget(line => MessageStore.SpokenInOwnVoice(line.Voice));

    public void ForgetCustomVoice(string voiceId) =>
        Forget(line => string.Equals(line.Voice?.Id, voiceId, StringComparison.Ordinal));

    /// <summary>Deletes the run's folder with its lines and clips.</summary>
    public void Delete(string? frontierId, Story run)
    {
        ArgumentNullException.ThrowIfNull(run);

        var runFolder = RunFolder(frontierId, run);

        lock (_gate)
        {
            try
            {
                files.DeleteFolder(runFolder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not delete the record in {Folder}", runFolder);
            }
        }

        foreach (var pair in _runOf.Where(pair => string.Equals(pair.Value, runFolder, StringComparison.OrdinalIgnoreCase)))
        {
            _runOf.TryRemove(pair);
        }
    }

    /// <summary>
    /// The run a line belongs to: for a story-level key, the Commander's current run of that story or else its newest;
    /// for a chapter key, the run that lists the chapter.
    /// </summary>
    private (string? FrontierId, Story Run)? RunOf(string key)
    {
        var frontierId = commander();

        if (StoryLog.StoryIdOf(key) is { } storyId)
        {
            var current = stories.Current(frontierId);
            var run = current is not null && string.Equals(current.Id, storyId, StringComparison.OrdinalIgnoreCase)
                ? current
                : stories.Find(frontierId, storyId);

            return run is null ? null : (frontierId, run);
        }

        if (stories.For(frontierId).FirstOrDefault(run => run.Chapters.Contains(key, StringComparer.Ordinal)) is { } mine)
        {
            return (frontierId, mine);
        }

        foreach (var (other, run) in stories.All())
        {
            if (run.Chapters.Contains(key, StringComparer.Ordinal))
            {
                return (other, run);
            }
        }

        return null;
    }

    private string RunFolder(string? frontierId, Story run) => Path.Combine(folder, FolderName(frontierId, run.Id, run.PickedAt));

    /// <summary>The clip compressed, or as a WAV when compression fails; protected when it is in the Commander's own voice.</summary>
    private (string File, byte[] Bytes)? Encode(string messageKey, SpokenClip spoken)
    {
        if (spoken.Parts.Count == 0)
        {
            return null;
        }

        try
        {
            var clip = spoken.Joined(messageKey);
            var compressed = compressor?.Compress(clip);
            var bytes = compressed?.Bytes ?? WavWriter.ToBytes(clip.Pcm.Span, clip.Format);
            var file = messageKey + (compressed?.Extension ?? MessageClips.PlainExtension);

            if (spoken.Own)
            {
                var plain = bytes;
                bytes = protector.Protect(plain);
                Array.Clear(plain);
                file += MessageClips.ProtectedExtension;
            }

            return (file, bytes);
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(ex, "Could not protect the clip of message {Key}", messageKey);
            return null;
        }
    }

    /// <summary>Writes the clip, replacing any other clip of the message. Called holding <see cref="_gate"/>.</summary>
    private bool Keep(string runFolder, string messageKey, string file, byte[] bytes)
    {
        try
        {
            foreach (var earlier in files.Enumerate(runFolder, messageKey + ".*"))
            {
                if (!string.Equals(Path.GetFileName(earlier), file, StringComparison.OrdinalIgnoreCase))
                {
                    files.Delete(earlier);
                }
            }

            files.WriteBytes(Path.Combine(runFolder, file), bytes);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not keep the clip of message {Key} in {Folder}", messageKey, runFolder);
            return false;
        }
    }

    /// <summary>Deletes every protected clip <paramref name="spokenInIt"/> picks, in every run, and clears its line's clip and voice.</summary>
    private void Forget(Func<StoryRunLine, bool> spokenInIt)
    {
        lock (_gate)
        {
            IReadOnlyList<string> records;

            try
            {
                records = files.Enumerate(folder, LinesFile, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not list the story run records in {Folder}", folder);
                return;
            }

            foreach (var path in records)
            {
                var runFolder = Path.GetDirectoryName(path)!;
                var lines = Fold(path).ToList();
                var changed = false;

                for (var i = 0; i < lines.Count; i++)
                {
                    if (lines[i].Clip is { } clip && MessageClips.IsProtected(clip) && spokenInIt(lines[i]))
                    {
                        lines[i] = lines[i] with { Clip = null, Voice = null };
                        changed = true;

                        try
                        {
                            files.Delete(Path.Combine(runFolder, Path.GetFileName(clip)));
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            logger.LogWarning(ex, "Could not delete clip {Clip} in {Folder}", clip, runFolder);
                        }
                    }
                }

                if (!changed)
                {
                    continue;
                }

                try
                {
                    files.WriteText(path, string.Concat(lines.Select(line => JsonSerializer.Serialize(line, Json) + "\n")));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(ex, "Could not write {Path}", path);
                }
            }
        }
    }

    /// <summary>The lines file read as lines in posting order; an entry for a key already read sets that line's clip and voice.</summary>
    private List<StoryRunLine> Fold(string path)
    {
        string? text;

        try
        {
            text = files.ReadText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read {Path}", path);
            return [];
        }

        var lines = new List<StoryRunLine>();
        var at = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var raw in (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            StoryRunLine? entry;

            try
            {
                entry = JsonSerializer.Deserialize<StoryRunLine>(raw, Json);
            }
            catch (JsonException)
            {
                continue;
            }

            if (entry is null)
            {
                continue;
            }

            if (at.TryGetValue(entry.Key, out var index))
            {
                lines[index] = lines[index] with { Clip = entry.Clip, Voice = entry.Voice };
            }
            else
            {
                at[entry.Key] = lines.Count;
                lines.Add(entry);
            }
        }

        return lines;
    }

    private static string Segment(string part)
    {
        var invalid = Path.GetInvalidFileNameChars();

        return string.Concat(part.Select(c => invalid.Contains(c) || c == '_' ? '-' : c));
    }
}
