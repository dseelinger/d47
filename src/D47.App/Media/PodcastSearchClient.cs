using System.Text;
using D47.Core.Configuration;
using D47.Core.Podcasts;
using Microsoft.Extensions.Logging;

namespace D47.App.Media;

/// <summary>What one search found. <see cref="Problem"/> is a sentence for the Commander, null when the directory answered.</summary>
internal sealed record PodcastSearchOutcome(IReadOnlyList<PodcastSearchResult> Results, string? Problem);

/// <summary>Searches Apple's podcast directory. Sends only the words and the fixed query; nothing about the Commander.</summary>
internal sealed class PodcastSearchClient(HttpClient http, ILogger logger)
{
    /// <summary>The largest answer read; a longer one is refused.</summary>
    internal const long MostBytes = 1024 * 1024;

    /// <summary>The longest a search's request and answer may take.</summary>
    private static readonly TimeSpan Answer = TimeSpan.FromSeconds(30);

    /// <summary>Searches on the pool.</summary>
    internal Task<PodcastSearchOutcome> SearchAsync(string words) => Task.Run(() => FetchAsync(words));

    /// <summary>The search address: the words, <c>media=podcast</c>, <c>entity=podcast</c> and <c>limit=10</c>.</summary>
    internal static Uri Address(string words) =>
        new($"https://{EgressDisclosure.PodcastSearchHost}/search?term={Uri.EscapeDataString(words.Trim())}&media=podcast&entity=podcast&limit=10");

    private async Task<PodcastSearchOutcome> FetchAsync(string words)
    {
        if (string.IsNullOrWhiteSpace(words))
        {
            return new PodcastSearchOutcome([], "Say or type what to search for.");
        }

        string text;

        try
        {
            using var answer = new CancellationTokenSource(Answer);
            using var response = await http.GetAsync(Address(words), HttpCompletionOption.ResponseHeadersRead, answer.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new PodcastSearchOutcome([], $"The podcast directory returned {(int)response.StatusCode}.");
            }

            if (response.Content.Headers.ContentLength > MostBytes
                || await PodcastStore.ReadAtMostAsync(response.Content, MostBytes, answer.Token).ConfigureAwait(false) is not { } bytes)
            {
                return new PodcastSearchOutcome([], "The podcast directory's answer is larger than 1 MB, so D47 does not read it.");
            }

            text = Encoding.UTF8.GetString(bytes);
        }
        catch (OperationCanceledException exception)
        {
            logger.LogInformation(exception, "The podcast directory did not answer in time");
            return new PodcastSearchOutcome([], "The podcast directory did not answer within 30 seconds.");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            logger.LogInformation(exception, "The podcast directory could not be reached");
            return new PodcastSearchOutcome([], "The podcast directory could not be reached.");
        }

        try
        {
            return new PodcastSearchOutcome(PodcastSearch.Parse(text), null);
        }
        catch (PodcastSearchException exception)
        {
            logger.LogInformation("The podcast directory's answer did not parse");
            return new PodcastSearchOutcome([], exception.Message);
        }
    }
}
