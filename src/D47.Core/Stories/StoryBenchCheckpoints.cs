using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace D47.Core.Stories;

/// <summary>One checkpoint of the active slot's current story, as the bench page lists it.</summary>
public sealed record BenchCheckpoint(int Chapter, string Stage, DateTimeOffset At);

public sealed partial class StoryBench
{
    private const string MetaFile = "checkpoint.json";
    private const string CheckpointFolder = "checkpoints";
    private const string MyStoryFolder = "mine";

    /// <summary>Held only to queue a checkpoint and to read <see cref="_queued"/>.</summary>
    private readonly Lock _queueGate = new();
    private Task _tail = Task.CompletedTask;
    private string? _queued;

    /// <summary>Raised on the pool after a checkpoint has been written.</summary>
    public event Action? CheckpointsChanged;

    /// <summary>Completes once the checkpoints queued so far are written.</summary>
    public Task Settled()
    {
        lock (_queueGate)
        {
            return _tail;
        }
    }

    /// <summary>The active slot's checkpoints of the story <paramref name="frontierId"/> has current, oldest chapter first.</summary>
    public IReadOnlyList<BenchCheckpoint> Checkpoints(string? frontierId)
    {
        if (_stories.Current(frontierId) is not { } story)
        {
            return [];
        }

        return
        [
            .. ReadAll(CheckpointRoot(Active))
                .Where(item => SameStory(item.Meta, story))
                .OrderBy(item => item.Meta.Chapter)
                .Select(item => new BenchCheckpoint(item.Meta.Chapter, item.Meta.Stage, item.Meta.At)),
        ];
    }

    /// <summary>
    /// Restores the active slot's checkpoint for <paramref name="chapter"/>: copies its files over the slot's, rereads them,
    /// abandons the restored chapter's adventure and pauses the story at <paramref name="now"/>, with the time since the
    /// checkpoint counted as switched off. Writes files, so it never runs on the tick. Returns a refusal or null.
    /// </summary>
    public string? Rewind(int chapter, DateTimeOffset now)
    {
        lock (_gate)
        {
            var slot = Active;

            var found = ReadAll(CheckpointRoot(slot)).FirstOrDefault(item => item.Meta.Chapter == chapter);

            if (found.Folder is null)
            {
                return $"There is no checkpoint for chapter {chapter.ToString(CultureInfo.InvariantCulture)}.";
            }

            var meta = found.Meta;

            if (_stories.Find(meta.FrontierId, meta.StoryId) is not { IsCurrent: true } current || current.PickedAt != meta.PickedAt)
            {
                return "That checkpoint belongs to a story that is no longer running.";
            }

            if (Director is { } held && !held.HoldForSwitch())
            {
                return Writing;
            }

            try
            {
                var files = ActiveFiles(slot);

                CopyFiles(StorySlotFiles.In(found.Folder), files);

                lock (_queueGate)
                {
                    _queued = Token(slot, meta.FrontierId, meta.StoryId, meta.PickedAt, meta.Chapter, meta.Key);
                }

                _archive.Reopen(files.Chapters);
                _adventures.Repoint(files.Adventures);
                _stories.Reopen(files.Stories);
                PauseRestored(meta, now);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Could not rewind the story bench to chapter {Chapter}", chapter);
                return "The checkpoint could not be restored.";
            }
            finally
            {
                Director?.ReleaseSwitch();
            }
        }

        _logger.LogInformation("Rewound the story bench to chapter {Chapter}", chapter);
        return null;
    }

    /// <summary>Queues a checkpoint when the current story has a chapter none has been taken for. Does no file I/O.</summary>
    private void Evaluate()
    {
        if (_gate.IsHeldByCurrentThread || Current() is not { } entry || entry.Story.Chapters.Count == 0)
        {
            return;
        }

        if (Director is { } director && (director.IsWriting(entry.FrontierId) || director.IsRewriting(entry.FrontierId)))
        {
            return;
        }

        var slot = Active;
        var story = entry.Story;
        var token = Token(slot, entry.FrontierId, story.Id, story.PickedAt, story.Chapters.Count, story.CurrentChapter!);

        lock (_queueGate)
        {
            if (token == _queued)
            {
                return;
            }

            _queued = token;
            _tail = _tail.ContinueWith(_ => Take(slot, token), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }

    private void Take(string? slot, string token)
    {
        var taken = false;

        try
        {
            lock (_gate)
            {
                if (!string.Equals(Active, slot, StringComparison.Ordinal)
                    || Current() is not { } entry
                    || Director is { } director && (director.IsWriting(entry.FrontierId) || director.IsRewriting(entry.FrontierId)))
                {
                    Forget(token);
                    return;
                }

                var story = entry.Story;
                var chapter = story.Chapters.Count;

                if (Token(slot, entry.FrontierId, story.Id, story.PickedAt, chapter, story.CurrentChapter ?? string.Empty) != token)
                {
                    Forget(token);
                    return;
                }

                var meta = new Meta(
                    chapter,
                    entry.FrontierId,
                    story.Id,
                    story.PickedAt,
                    story.CurrentChapter!,
                    StoryClues.StageName(StoryClues.Stage(story)),
                    Director?.Clock() ?? DateTimeOffset.UtcNow);
                var root = CheckpointRoot(slot);
                var existing = ReadAll(root);

                if (existing.Any(item => item.Meta.Chapter == chapter && item.Meta.Key == meta.Key && SameStory(item.Meta, story)))
                {
                    return;
                }

                foreach (var stale in existing.Where(item => item.Meta.Chapter >= chapter || !SameStory(item.Meta, story)))
                {
                    DeleteCheckpoint(stale.Folder);
                }

                var folder = Path.Combine(root, chapter.ToString(CultureInfo.InvariantCulture));

                CopyFiles(ActiveFiles(slot), StorySlotFiles.In(folder));
                _files.WriteText(Path.Combine(folder, MetaFile), JsonSerializer.Serialize(meta, Json));
                taken = true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Could not take a story bench checkpoint");
            Forget(token);
        }

        if (taken)
        {
            CheckpointsChanged?.Invoke();
        }
    }

    private void Forget(string token)
    {
        lock (_queueGate)
        {
            if (token == _queued)
            {
                _queued = null;
            }
        }
    }

    /// <summary>Abandons the restored chapter's adventure and pauses the story. Called after the stores have reread the restored files.</summary>
    private void PauseRestored(Meta meta, DateTimeOffset now)
    {
        if (_stories.Find(meta.FrontierId, meta.StoryId) is not { } restored)
        {
            return;
        }

        if (restored.CurrentChapter is { } key
            && _adventures.Find(meta.FrontierId, key) is { IsActive: true } adventure
            && _adventures.Save(meta.FrontierId, adventure with { AbandonedAt = now }) is { } refusal)
        {
            _logger.LogWarning("Could not abandon the restored chapter {Chapter}: {Refusal}", key, refusal);
        }

        var basis = restored.IsOff ? restored.SwitchedOn(meta.At) : restored;

        if (basis.State == StoryState.Paused)
        {
            basis = basis.Resumed(meta.At);
        }

        var spans = now > meta.At ? [.. basis.OffSpans, new StoryOffSpan(meta.At, now)] : basis.OffSpans;
        var rewound = (basis with { OffSpans = spans }).Paused(now);

        _stories.Update(meta.FrontierId, meta.StoryId, _ => rewound);
    }

    private (string FrontierId, Story Story)? Current()
    {
        foreach (var entry in _stories.All())
        {
            if (entry.Story.IsCurrent && !entry.Story.IsOff)
            {
                return entry;
            }
        }

        return null;
    }

    private IReadOnlyList<(Meta Meta, string Folder)> ReadAll(string root)
    {
        var found = new List<(Meta, string)>();

        foreach (var path in _files.Enumerate(root, MetaFile, recursive: true))
        {
            try
            {
                if (_files.ReadText(path) is { } text && JsonSerializer.Deserialize<Meta>(text, Json) is { } meta)
                {
                    found.Add((meta, Path.GetDirectoryName(path)!));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogWarning(ex, "Skipped the unreadable checkpoint {Path}", path);
            }
        }

        return found;
    }

    private void DeleteCheckpoint(string folder)
    {
        _files.Delete(Path.Combine(folder, MetaFile));

        var files = StorySlotFiles.In(folder);

        _files.Delete(files.Stories);
        _files.Delete(files.Adventures);
        _files.Delete(files.Chapters);
    }

    /// <summary>Copies each of the three files, and removes the destination's when the source has none.</summary>
    private void CopyFiles(StorySlotFiles from, StorySlotFiles to)
    {
        CopyOrDelete(from.Stories, to.Stories);
        CopyOrDelete(from.Adventures, to.Adventures);
        CopyOrDelete(from.Chapters, to.Chapters);
    }

    private void CopyOrDelete(string from, string to)
    {
        if (_files.Stat(from) is null)
        {
            _files.Delete(to);
        }
        else
        {
            _files.Copy(from, to);
        }
    }

    private StorySlotFiles ActiveFiles(string? slot) => StorySlotFiles.In(slot is null ? _data : SlotFolder(_data, slot));

    private string CheckpointRoot(string? slot) => Path.Combine(_data, Folder, slot ?? MyStoryFolder, CheckpointFolder);

    private static bool SameStory(Meta meta, Story story) =>
        string.Equals(meta.StoryId, story.Id, StringComparison.OrdinalIgnoreCase) && meta.PickedAt == story.PickedAt;

    private static string Token(string? slot, string frontierId, string storyId, DateTimeOffset pickedAt, int chapter, string key) =>
        string.Join('|', slot ?? string.Empty, frontierId, storyId, pickedAt.UtcTicks.ToString(CultureInfo.InvariantCulture), chapter.ToString(CultureInfo.InvariantCulture), key);

    private sealed record Meta(int Chapter, string FrontierId, string StoryId, DateTimeOffset PickedAt, string Key, string Stage, DateTimeOffset At);
}
