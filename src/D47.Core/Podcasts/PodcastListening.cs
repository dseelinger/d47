using System.Text.Json;
using D47.Core.Storage;

namespace D47.Core.Podcasts;

/// <summary>Where the Commander stopped in an episode, and whether it is finished.</summary>
public sealed record PodcastProgress(string FeedUrl, string EpisodeId, TimeSpan Position, bool Finished);

/// <summary>What <see cref="PodcastListening.FromJson"/> read. <see cref="Problem"/> is set when the text did not parse.</summary>
public sealed record PodcastListeningRead(PodcastListening Listening, string? Problem);

/// <summary>The Commander's listening record, keyed by feed URL and episode id.</summary>
public sealed class PodcastListening
{
    private readonly Dictionary<(string Feed, string Episode), PodcastProgress> _progress;

    public PodcastListening()
        : this([])
    {
    }

    private PodcastListening(Dictionary<(string Feed, string Episode), PodcastProgress> progress) => _progress = progress;

    public IReadOnlyCollection<PodcastProgress> All => _progress.Values;

    public PodcastProgress? Find(Uri feedUrl, string episodeId) =>
        _progress.GetValueOrDefault((feedUrl.AbsoluteUri, episodeId));

    /// <summary>A copy with this episode's position replaced.</summary>
    public PodcastListening With(Uri feedUrl, string episodeId, TimeSpan position, bool finished)
    {
        var copy = new Dictionary<(string Feed, string Episode), PodcastProgress>(_progress)
        {
            [(feedUrl.AbsoluteUri, episodeId)] = new(feedUrl.AbsoluteUri, episodeId, position, finished),
        };

        return new PodcastListening(copy);
    }

    /// <summary>A copy without any episode of the feed.</summary>
    public PodcastListening Without(Uri feedUrl) =>
        new(_progress.Where(pair => pair.Key.Feed != feedUrl.AbsoluteUri).ToDictionary());

    public string ToJson() =>
        JsonSerializer.Serialize(
            new Document { Episodes = [.. _progress.Values.Select(Row.From)] },
            StoreJson.OmittingNulls);

    /// <summary>Never throws; text that does not parse gives an empty record and a reason.</summary>
    public static PodcastListeningRead FromJson(string json)
    {
        try
        {
            var document = JsonSerializer.Deserialize<Document>(json ?? "", StoreJson.OmittingNulls)
                ?? throw new JsonException("The file is empty.");
            var progress = new Dictionary<(string Feed, string Episode), PodcastProgress>();

            foreach (var row in document.Episodes ?? [])
            {
                if (string.IsNullOrEmpty(row.Feed) || string.IsNullOrEmpty(row.Episode) || row.Seconds < 0)
                {
                    continue;
                }

                progress[(row.Feed, row.Episode)] = new(row.Feed, row.Episode, TimeSpan.FromSeconds(row.Seconds), row.Finished);
            }

            return new PodcastListeningRead(new PodcastListening(progress), null);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or OverflowException)
        {
            return new PodcastListeningRead(new PodcastListening(), "The listening record could not be read, so it starts empty.");
        }
    }

    private sealed class Document
    {
        public List<Row>? Episodes { get; set; }
    }

    private sealed class Row
    {
        public string Feed { get; set; } = "";

        public string Episode { get; set; } = "";

        public double Seconds { get; set; }

        public bool Finished { get; set; }

        public static Row From(PodcastProgress progress) => new()
        {
            Feed = progress.FeedUrl,
            Episode = progress.EpisodeId,
            Seconds = progress.Position.TotalSeconds,
            Finished = progress.Finished,
        };
    }
}
