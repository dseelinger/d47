using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Books;
using D47.Core.Configuration;

namespace D47.App.Panel;

/// <summary>What Media › Books reads and does, shared by the window and the headset.</summary>
public sealed class BooksHost(
    BookShelf shelf,
    BookPlayer player,
    Func<BookSettings> settings,
    Action<Func<BookSettings, BookSettings>> change,
    Func<string, bool> installed,
    Func<string, CancellationToken, Task<VoiceCatalogue>> voices,
    BookFolder? folder = null)
{
    /// <summary>The local providers a book is read with, in the order the page lists them.</summary>
    public static IReadOnlyList<string> Providers { get; } = [TtsProviderCatalog.KokoroId, TtsProviderCatalog.ChatterboxId];

    private readonly Dictionary<string, VoiceCatalogue> _listed = new(StringComparer.OrdinalIgnoreCase);

    public BookShelf Shelf { get; } = shelf;

    public BookPlayer Player { get; } = player;

    /// <summary>The books folder; null where the surface has none.</summary>
    public BookFolder? Folder { get; } = folder;

    public BookSettings Settings => settings() ?? new BookSettings();

    /// <summary>Raised, on the calling thread, after a book was taken off the shelf.</summary>
    public event Action? Changed;

    /// <summary>Raised, on any thread, when a file was added to or removed from the books folder.</summary>
    public event Action? FilesChanged;

    public void NotifyFilesChanged() => FilesChanged?.Invoke();

    /// <summary>
    /// Puts the file on the shelf and reads it from its start. False when it cannot be put on the shelf; <paramref name="reading"/>
    /// is false when it is on the shelf but a story is playing, so the book is not read.
    /// </summary>
    public bool AddAndRead(BookFile file, out bool reading)
    {
        reading = false;

        if (Folder?.AddToShelf(file) is null)
        {
            return false;
        }

        Changed?.Invoke();
        FilesChanged?.Invoke();
        reading = ReadFrom(file.Source, 0);

        return true;
    }

    /// <summary>Whether the provider's model is on this computer.</summary>
    public bool Installed(string providerId) => installed(providerId);

    public bool AnyInstalled => Providers.Any(Installed);

    public void Choose(string providerId) =>
        change(current => string.Equals(current.Provider, providerId, StringComparison.OrdinalIgnoreCase)
            ? current
            : current with { Provider = providerId, Voice = null });

    public void ChooseVoice(string? voiceId) => change(current => current with { Voice = voiceId });

    /// <summary>Lists the provider's voices off the calling thread and remembers them for <see cref="VoiceName"/>.</summary>
    public async Task<VoiceCatalogue> Voices(string providerId, CancellationToken cancellationToken = default)
    {
        VoiceCatalogue listed;

        try
        {
            listed = await Task.Run(() => voices(providerId, cancellationToken), cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is TtsException or HttpRequestException or IOException)
        {
            listed = VoiceCatalogue.Unreachable(ex.Message);
        }

        _listed[providerId] = listed;
        return listed;
    }

    /// <summary>The name of the voice books are read with, from the last listing; its id until one is made.</summary>
    public string VoiceName(BookSettings now)
    {
        if (string.IsNullOrWhiteSpace(now.Voice))
        {
            return "Default voice";
        }

        return _listed.TryGetValue(now.Provider, out var listed)
               && listed.Voices.FirstOrDefault(voice => string.Equals(voice.Id, now.Voice, StringComparison.OrdinalIgnoreCase)) is { } found
            ? found.Name
            : now.Voice;
    }

    /// <summary>Reads from the start of <paramref name="chapter"/>.</summary>
    public bool ReadFrom(BookSource source, int chapter, int paragraph = 0)
    {
        if (Player.StoryPlaying || Shelf.MoveTo(source, new BookPlace(chapter, paragraph), finished: false) is null)
        {
            return false;
        }

        return Player.Play(source);
    }

    /// <summary>Reads from where the book was left, or from its start once finished.</summary>
    public bool ReadOn(BookSource source) => Player.Play(source);

    /// <summary>Reads the book being read again from the start of the paragraph before the one it is in.</summary>
    public bool BackAParagraph()
    {
        if (Player.Now is not { } now)
        {
            return false;
        }

        var chapter = now.Place.Chapter;
        var paragraph = now.Place.Paragraph - 1;

        while (paragraph < 0 && chapter > 0)
        {
            chapter--;
            paragraph = now.Book.Chapters[chapter].Paragraphs.Count - 1;
        }

        return ReadFrom(now.Book.Source, chapter, Math.Max(0, paragraph));
    }

    /// <summary>Whether the book being read has a chapter after the one it is in.</summary>
    public bool HasNextChapter => Player.Now is { } now && now.Place.Chapter + 1 < now.Book.Chapters.Count;

    public bool NextChapter() =>
        Player.Now is { } now && now.Place.Chapter + 1 < now.Book.Chapters.Count && ReadFrom(now.Book.Source, now.Place.Chapter + 1);

    /// <summary>Stops the book if it is the one being read, then takes it and its text off the shelf.</summary>
    public bool TakeOff(BookSource source)
    {
        if (Player.Now is { } now && now.Book.Source == source)
        {
            Player.Stop();
        }

        var removed = Shelf.TakeOff(source);

        Changed?.Invoke();

        return removed;
    }
}
