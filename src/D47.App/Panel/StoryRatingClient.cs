using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using D47.Core.Configuration;
using D47.Core.Stories;
using Microsoft.Extensions.Logging;

namespace D47.App.Panel;

/// <summary>
/// Reads stock stories' average ratings once per session and sends the Commander's own votes to the ratings Worker.
/// A vote is stored on the story as pending before it is sent, and a vote still pending is sent again when the
/// Stories page next opens. Every request runs on the pool.
/// </summary>
public sealed class StoryRatingClient
{
    internal const string SendFailed = "Your rating is saved and will be sent next time.";

    private readonly StoryStore _stories;
    private readonly Func<bool> _allowed;
    private readonly ILogger _logger;
    private readonly HttpClient _http;
    private readonly string _address;
    private readonly SemaphoreSlim _sending = new(1, 1);
    private StoryRatings _ratings = StoryRatings.Empty;
    private int _averagesAsked;

    /// <param name="allowed">Read at each request, so turning the setting off stops the next one.</param>
    public StoryRatingClient(StoryStore stories, Func<bool> allowed, ILogger logger, HttpMessageHandler? handler = null, string address = StoryRatingSettings.Address)
    {
        _stories = stories;
        _allowed = allowed;
        _logger = logger;
        _address = address.TrimEnd('/');
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(20);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47-ratings");
    }

    /// <summary>Whether the Story ratings setting is on.</summary>
    public bool Enabled => _allowed();

    /// <summary>The averages fetched this session, with each vote's reply applied.</summary>
    public StoryRatings Ratings => Volatile.Read(ref _ratings);

    /// <summary>Raised on a background thread when <see cref="Ratings"/> changes.</summary>
    public event Action? Changed;

    /// <summary>
    /// Fetches the averages the first time it is called in a session while ratings are on, and sends every vote still
    /// pending. Averages that could not be fetched leave every story unrated and are asked for again on the next call.
    /// Returns the failure to show, or null.
    /// </summary>
    public Task<string?> Open()
    {
        if (!_allowed())
        {
            return Task.FromResult<string?>(null);
        }

        var fetch = Interlocked.Exchange(ref _averagesAsked, 1) == 0;

        return Task.Run(async () =>
        {
            if (fetch && !await FetchAveragesAsync().ConfigureAwait(false))
            {
                Interlocked.Exchange(ref _averagesAsked, 0);
            }

            return await ResendAsync().ConfigureAwait(false) ? null : SendFailed;
        });
    }

    /// <summary>Records <paramref name="stars"/> as the Commander's vote for a story they have picked, and sends it. False when it was not sent.</summary>
    public Task<bool> Rate(string? commander, string id, int stars)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(stars, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(stars, 5);

        return Cast(commander, id, stars);
    }

    /// <summary>Withdraws the Commander's vote for a story, and sends the withdrawal. False when it was not sent.</summary>
    public Task<bool> Clear(string? commander, string id) => Cast(commander, id, null);

    private Task<bool> Cast(string? commander, string id, int? stars)
    {
        if (!_allowed() || _stories.Find(commander, id) is null)
        {
            return Task.FromResult(false);
        }

        return Task.Run(async () =>
        {
            _stories.Update(commander, id, story => story with { Rating = stars, RatingPending = true });

            return await SendAsync(commander, id).ConfigureAwait(false);
        });
    }

    private async Task<bool> ResendAsync()
    {
        var sent = true;

        foreach (var (commander, story) in _stories.All().Where(pair => pair.Story.RatingPending))
        {
            sent &= await SendAsync(commander, story.Id).ConfigureAwait(false);
        }

        return sent;
    }

    /// <summary>Sends the story's stored vote if it is still pending; one request at a time, so votes arrive in the order they were cast.</summary>
    private async Task<bool> SendAsync(string? commander, string id)
    {
        await _sending.WaitAsync().ConfigureAwait(false);

        try
        {
            if (!_allowed())
            {
                return false;
            }

            if (_stories.Find(commander, id) is not { RatingPending: true } story)
            {
                return true;
            }

            var stars = story.Rating;
            using var request = new HttpRequestMessage(stars is null ? HttpMethod.Delete : HttpMethod.Put, $"{_address}/ratings/{Uri.EscapeDataString(id)}");

            request.Headers.Add("d47-format", StoryRatings.Format.ToString(CultureInfo.InvariantCulture));
            request.Headers.Add("d47-voter", _stories.EnsureVoter(commander));

            if (stars is { } given)
            {
                request.Content = new StringContent(
                    string.Create(CultureInfo.InvariantCulture, $"{{\"stars\":{given}}}"), Encoding.UTF8, "application/json");
            }

            using var response = await _http.SendAsync(request, CancellationToken.None).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("A vote for story {StoryId} was refused: the ratings Worker returned {Status}.", id, response.StatusCode);
                return false;
            }

            var body = await response.Content.ReadAsStringAsync(CancellationToken.None).ConfigureAwait(false);

            _stories.Update(commander, id, current => current.Rating == stars ? current with { RatingPending = false } : current);

            if (TryAggregate(body, out var rating))
            {
                Publish(rating is null ? Ratings.Without(id) : Ratings.With(id, rating));
            }

            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogInformation("A vote for story {StoryId} could not be sent ({Error}).", id, ex.GetType().Name);
            return false;
        }
        finally
        {
            _sending.Release();
        }
    }

    private async Task<bool> FetchAveragesAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _address + "/ratings");
            request.Headers.Add("d47-format", StoryRatings.Format.ToString(CultureInfo.InvariantCulture));

            using var response = await _http.SendAsync(request, CancellationToken.None).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Story ratings were not fetched: the ratings Worker returned {Status}.", response.StatusCode);
                return false;
            }

            if (StoryRatings.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None).ConfigureAwait(false)) is not { } ratings)
            {
                _logger.LogInformation("Story ratings were not fetched: the reply was not in format {Format}.", StoryRatings.Format);
                return false;
            }

            Publish(ratings);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogInformation("Story ratings could not be fetched ({Error}).", ex.GetType().Name);
            return false;
        }
    }

    /// <summary>Reads a vote reply's aggregate; <paramref name="rating"/> is null when the story has no votes left.</summary>
    private static bool TryAggregate(string json, out StoryRating? rating)
    {
        rating = null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("count", out var count)
                || !count.TryGetInt32(out var votes)
                || !root.TryGetProperty("average", out var average)
                || !average.TryGetDouble(out var mean))
            {
                return false;
            }

            rating = votes >= 1 && mean is >= 1 and <= 5 ? new StoryRating(mean, votes) : null;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private void Publish(StoryRatings ratings)
    {
        Volatile.Write(ref _ratings, ratings);

        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redrawing after story ratings changed failed");
        }
    }
}
