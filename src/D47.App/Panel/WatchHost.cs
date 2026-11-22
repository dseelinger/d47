using System.Net.Http;
using D47.App.Media;
using D47.Core.Capabilities.Builtin;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Storage;
using D47.Core.Viewing;
using D47.Core.Vr;

namespace D47.App.Panel;

/// <summary>What Media › Movies reads and does, shared by the window and the headset.</summary>
public sealed class WatchHost : IDisposable
{
    /// <summary>The source id of a video file the Commander chose on this computer.</summary>
    public const string FileSource = "file";

    /// <summary>How often the place is saved while a video plays.</summary>
    public static readonly TimeSpan SaveEvery = TimeSpan.FromSeconds(10);

    private static readonly HttpClient Web = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly SettingsService _settings;
    private readonly IFileSystem _files;
    private readonly TimeProvider _time;
    private readonly Func<Uri, CancellationToken, Task<string?>> _readSubtitles;
    private readonly Action<Action> _background;
    private readonly IVideoCatalogue? _catalogue;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();

    private VideoTitle? _current;
    private IReadOnlyList<SubtitleCue> _cues = [];
    private bool _cuesLoading;
    private bool _captionsOn = true;
    private VideoState? _lastState;
    private CancellationTokenSource? _loading;

    /// <param name="catalogue">Whether the video catalogue page exists, so FIND A VIDEO and a search can open it.</param>
    /// <param name="readSubtitles">Reads a subtitle file's text, or null when it cannot be read; the web and the disk when null.</param>
    /// <param name="videos">What the catalogue page searches, or null where there is none.</param>
    /// <param name="background">Runs work off the calling thread; the thread pool when null.</param>
    public WatchHost(
        VideoPlayback video,
        WatchStore store,
        SettingsService settings,
        IFileSystem files,
        TimeProvider? time = null,
        bool catalogue = false,
        Func<Uri, CancellationToken, Task<string?>>? readSubtitles = null,
        Action<Action>? background = null,
        IVideoCatalogue? videos = null)
    {
        _catalogue = videos;
        Video = video;
        Store = store;
        _settings = settings;
        _files = files;
        _time = time ?? TimeProvider.System;
        HasCatalogue = catalogue;
        _readSubtitles = readSubtitles ?? ReadSubtitlesAsync;
        _background = background ?? (work => _ = Task.Run(work));

        video.Changed += OnVideoChanged;
        store.Changed += RaiseChanged;
        settings.Changed += _ => RaiseChanged();
        _timer = _time.CreateTimer(_ => SaveWhilePlaying(), null, SaveEvery, SaveEvery);
    }

    public VideoPlayback Video { get; }

    public WatchStore Store { get; }

    /// <summary>Whether the catalogue page exists.</summary>
    public bool HasCatalogue { get; }

    /// <summary>The words a search on the Watch page asked the catalogue for, or null.</summary>
    public string? CatalogueQuery { get; set; }

    /// <summary>The sources the catalogue searches: the ticked ones, or every source when none is ticked.</summary>
    public IReadOnlyList<string> SearchedSources => _settings.Current.Watch.SearchedSources();

    /// <summary>The sources ticked; none when the Commander has ticked none, which searches every source.</summary>
    public IReadOnlyList<string> TickedSources =>
        [.. VideoSources.All.Where(source => (_settings.Current.Watch.Sources ?? []).Contains(source, StringComparer.Ordinal))];

    /// <summary>Writes the sources the catalogue searches.</summary>
    public void SetSources(IReadOnlyList<string> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var chosen = VideoSources.All.Where(source => sources.Contains(source, StringComparer.Ordinal)).ToList();

        _settings.Replace(
            "the video catalogue's sources were chosen",
            current => current.Watch.Sources.SequenceEqual(chosen, StringComparer.Ordinal)
                ? current
                : current with { Watch = current.Watch with { Sources = chosen } });
    }

    /// <summary>Page <paramref name="page"/>, counted from 1, of the catalogue's results; an empty page with a problem where there is no catalogue.</summary>
    public Task<VideoPage> SearchAsync(string query, VideoFilters filters, int page, CancellationToken cancellationToken) =>
        _catalogue is null
            ? Task.FromResult(VideoPage.Empty with { Problem = "There is no video catalogue to search." })
            : _catalogue.SearchAsync(query, filters, page, cancellationToken);

    /// <summary>Raised, on any thread, when the title, its captions, the lists or a setting changed.</summary>
    public event Action? Changed;

    /// <summary>The title the player page is on, or null.</summary>
    public VideoTitle? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <summary>Whether captions are drawn over the picture.</summary>
    public bool CaptionsOn
    {
        get => _captionsOn;
        set
        {
            _captionsOn = value;
            RaiseChanged();
        }
    }

    /// <summary>The current title's subtitle cues; none until they are read, or when it has no subtitle file.</summary>
    public IReadOnlyList<SubtitleCue> Cues
    {
        get
        {
            lock (_gate)
            {
                return _cues;
            }
        }
    }

    /// <summary>Whether the current title's subtitle file is being read.</summary>
    public bool CuesLoading
    {
        get
        {
            lock (_gate)
            {
                return _cuesLoading;
            }
        }
    }

    public bool PauseForPushToTalk => _settings.Current.Watch.PauseForPushToTalk;

    /// <summary>The video's volume, 0 to 1.</summary>
    public double Volume => _settings.Current.Audio.Video.Level;

    /// <summary>The ids of the catalogue sources the titles kept, and the one now playing, stream from.</summary>
    public IReadOnlyList<string> HeldSources
    {
        get
        {
            var held = Store.Sources().ToList();

            if (Current is { } current)
            {
                held.Add(current.Source);
            }

            return [.. held.Where(source => VideoSources.All.Contains(source, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal)];
        }
    }

    /// <summary>What a title is played from: its file's path, or its address.</summary>
    public static string PlaySource(VideoTitle title) =>
        title.Media is { IsAbsoluteUri: true } media ? media.IsFile ? media.LocalPath : media.AbsoluteUri : string.Empty;

    /// <summary>A title for a file on this computer, with the subtitle file beside it when there is one.</summary>
    public VideoTitle FileTitle(string path)
    {
        var folder = Path.GetDirectoryName(path) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(path);
        var beside = new[] { ".srt", ".vtt" }
            .Select(extension => Path.Combine(folder, stem + extension))
            .FirstOrDefault(candidate => _files.Stat(candidate) is not null);

        return new VideoTitle(
            FileSource,
            path,
            Path.GetFileName(path),
            VideoKind.Other,
            string.Empty,
            null,
            new Uri(path, UriKind.Absolute),
            beside is null ? null : new Uri(beside, UriKind.Absolute));
    }

    /// <summary>Where a title's captions come from, as a row's sub says it.</summary>
    public static string CaptionsOf(VideoTitle title) => title.Subtitles is null ? "no captions yet" : "subtitle file";

    /// <summary>How a row names a title's source and licence.</summary>
    public static string SourceOf(VideoTitle title)
    {
        var source = title.Source == FileSource
            ? "Your file"
            : VideoSources.All.Contains(title.Source, StringComparer.Ordinal) ? VideoSources.NameOf(title.Source) : title.Source;

        return string.IsNullOrEmpty(title.Licence) ? source : $"{source} · {title.Licence}";
    }

    /// <summary>The titles with a place that are not finished, newest first, each with what was kept of it.</summary>
    public IReadOnlyList<(WatchEntry Entry, VideoTitle Title)> CarryOn() => Titled(Store.CarryOnWatching());

    /// <summary>The titles in Your list, oldest added first.</summary>
    public IReadOnlyList<(WatchEntry Entry, VideoTitle Title)> YourList() => Titled(Store.YourList());

    /// <summary>The title a crumb key names: the one playing or last opened, else one kept in the store.</summary>
    public VideoTitle? Find(string source, string id)
    {
        if (Current is { } current && current.Source == source && current.Id == id)
        {
            return current;
        }

        return Store.Find(source, id)?.Video;
    }

    /// <summary>Makes <paramref name="title"/> the one the player page is on, without starting it.</summary>
    public void Open(VideoTitle title)
    {
        ArgumentNullException.ThrowIfNull(title);

        Flush();
        Select(title);
    }

    /// <summary>
    /// Plays <paramref name="title"/> from <paramref name="from"/>, or from the place kept for it. False while a story
    /// plays or for a title that cannot be played.
    /// </summary>
    public bool Play(VideoTitle title, TimeSpan? from = null)
    {
        ArgumentNullException.ThrowIfNull(title);

        if (!title.Playable || Video.StoryPlaying)
        {
            return false;
        }

        Flush();

        var start = from ?? (Store.Find(title.Source, title.Id) is { Finished: false } kept ? kept.Place : TimeSpan.Zero);

        Select(title);

        if (!Video.Play(PlaySource(title), start))
        {
            return false;
        }

        _lastState = null;
        Store.SavePlace(title.Source, title.Id, start, false, _time.GetUtcNow(), title);

        return true;
    }

    /// <summary>Plays the current title on: resumes it when it is loaded, otherwise starts it from its place.</summary>
    public bool Resume()
    {
        if (Current is not { } title)
        {
            return false;
        }

        if (Video.Source == PlaySource(title) && Video.State is VideoState.Paused or VideoState.Playing or VideoState.Stalled or VideoState.Opening)
        {
            Video.Resume();
            return true;
        }

        return Play(title, Video.Source == PlaySource(title) && Video.State == VideoState.Failed ? Video.Position : null);
    }

    public void Pause() => Video.Pause(VideoPause.Commander);

    /// <summary>Starts the current title again from its first second.</summary>
    public bool FromStart() => Current is { } title && Play(title, TimeSpan.Zero);

    /// <summary>Saves the place of the video now playing, or paused, on the calling thread.</summary>
    public void Flush()
    {
        if (Current is not { } title || Video.Source != PlaySource(title))
        {
            return;
        }

        var state = Video.State;

        if (state is null or VideoState.Opening)
        {
            return;
        }

        var finished = state == VideoState.Ended;
        var length = Video.Length ?? title.Length;
        var place = finished && length is { } end ? end : Video.Position;

        Store.SavePlace(title.Source, title.Id, place, finished, _time.GetUtcNow(), title);
    }

    public void SetPauseForPushToTalk(bool on) =>
        _settings.Apply(AudioCapability.VideoPushToTalkKey, on ? "true" : "false", SettingsCaller.Panel);

    public void SetVolume(double level) =>
        _settings.Apply(
            AudioCapability.LevelKey(AudioChannel.Video),
            level.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            SettingsCaller.Panel);

    public void RaiseChanged() => Changed?.Invoke();

    public void Dispose()
    {
        _timer.Dispose();
        _loading?.Cancel();
    }

    private IReadOnlyList<(WatchEntry Entry, VideoTitle Title)> Titled(IReadOnlyList<WatchEntry> entries) =>
        [.. entries.Where(entry => entry.Video is not null).Select(entry => (entry, entry.Video!))];

    private void Select(VideoTitle title)
    {
        CancellationToken token;

        lock (_gate)
        {
            _loading?.Cancel();
            _loading = new CancellationTokenSource();
            token = _loading.Token;
            _current = title;
            _cues = [];
            _cuesLoading = title.Subtitles is not null;
        }

        if (title.Subtitles is { } subtitles)
        {
            _background(() => _ = LoadCuesAsync(title, subtitles, token));
        }

        RaiseChanged();
    }

    private async Task LoadCuesAsync(VideoTitle title, Uri subtitles, CancellationToken token)
    {
        IReadOnlyList<SubtitleCue> cues = [];

        try
        {
            if (await _readSubtitles(subtitles, token).ConfigureAwait(false) is { } text)
            {
                cues = SubtitleCues.Parse(text);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or TaskCanceledException or OperationCanceledException)
        {
        }

        lock (_gate)
        {
            if (token.IsCancellationRequested || !ReferenceEquals(_current, title))
            {
                return;
            }

            _cues = cues;
            _cuesLoading = false;
        }

        RaiseChanged();
    }

    private async Task<string?> ReadSubtitlesAsync(Uri address, CancellationToken token)
    {
        if (address.IsFile)
        {
            return _files.ReadText(address.LocalPath);
        }

        return address.Scheme is "http" or "https" ? await Web.GetStringAsync(address, token).ConfigureAwait(false) : null;
    }

    private void OnVideoChanged()
    {
        var state = Video.State;

        if (state != _lastState)
        {
            _lastState = state;

            if (state is VideoState.Paused or VideoState.Ended or VideoState.Failed)
            {
                _background(Flush);
            }
        }

        RaiseChanged();
    }

    private void SaveWhilePlaying()
    {
        if (Video.State is VideoState.Playing or VideoState.Stalled)
        {
            Flush();
        }
    }
}
