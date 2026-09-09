using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace D47.App.Panel;

/// <summary>
/// Fetches the large hull art the download does not carry (#289): a hull's 4K picture and its
/// turntable, once each, into <c>data\ships\</c>.
/// </summary>
internal static class ShipArtStore
{
    /// <summary>Where the art is published, pinned exactly as every URL in <c>UpdateChecker</c> is.</summary>
    internal const string Source = "https://github.com/dseelinger/d47/releases/download/ship-art-1/";

    /// <summary>The two files fetched per hull, as suffixes on the symbol.</summary>
    private static readonly string[] Wanted = [".4k.png", ".spin.mp4"];

    /// <summary>A file is asked for once per session, whether it arrived or not.</summary>
    private static readonly HashSet<string> Asked = new(StringComparer.Ordinal);

    private static readonly HttpClient Http = CreateClient();

    private static string? _folder;
    private static Func<bool>? _allowed;
    private static ILogger? _logger;

    /// <summary>Raised on a background thread when a hull's art has landed, with its symbol.</summary>
    internal static event Action<string>? Arrived;

    /// <summary>Turns fetching on, at startup.</summary>
    /// <param name="folder">Where files land — <c>AppPaths.Ships</c>, the Commander's own.</param>
    /// <param name="allowed">
    /// Read at each fetch rather than captured, so turning the setting off stops the next one.
    /// </param>
    internal static void Enable(string folder, Func<bool> allowed, ILogger logger)
    {
        lock (Asked)
        {
            _folder = folder;
            _allowed = allowed;
            _logger = logger;
            Asked.Clear();
        }
    }

    /// <summary>Asks for a hull's art if it is not already here.</summary>
    internal static void Want(string? hull)
    {
        var symbol = Symbol(hull);

        if (symbol is null)
        {
            return;
        }

        List<string> missing = [];

        lock (Asked)
        {
            if (_folder is not { Length: > 0 } || _allowed?.Invoke() != true)
            {
                return;
            }

            foreach (var suffix in Wanted)
            {
                var file = symbol + suffix;

                // Already on disk beats already asked: a file dropped in by hand is not a fetch this session
                // refused, and neither is one fetched by the session before.
                if (File.Exists(Path.Combine(_folder, file)) || !Asked.Add(file))
                {
                    continue;
                }

                missing.Add(file);
            }
        }

        if (missing.Count == 0)
        {
            return;
        }

        _ = Task.Run(() => FetchAsync(symbol, missing));
    }

    private static async Task FetchAsync(string symbol, List<string> files)
    {
        var landed = false;

        foreach (var file in files)
        {
            landed |= await FetchAsync(file).ConfigureAwait(false);
        }

        if (!landed)
        {
            return;
        }

        // The cached miss has to go, or a hull looked at before its picture arrived keeps the absence for the
        // rest of the session.
        ShipArt.Forget(symbol);

        try
        {
            Arrived?.Invoke(symbol);
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "Redrawing after {File} landed failed", symbol);
        }
    }

    private static async Task<bool> FetchAsync(string file)
    {
        string folder;

        lock (Asked)
        {
            if (_folder is not { Length: > 0 } here)
            {
                return false;
            }

            folder = here;
        }

        // Written beside the destination and renamed onto it, so a fetch that is cut off leaves no half-file
        // for the decoder to find.
        var destination = Path.Combine(folder, file);
        var partial = destination + ".part";

        try
        {
            Directory.CreateDirectory(folder);

            using var response = await Http.GetAsync(
                Source + file,
                HttpCompletionOption.ResponseHeadersRead,
                CancellationToken.None).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogInformation(
                    "No hull art for {File}: the art release returned {Status}", file, response.StatusCode);

                return false;
            }

            await using (var target = File.Create(partial))
            {
                await response.Content.CopyToAsync(target, CancellationToken.None).ConfigureAwait(false);
            }

            File.Move(partial, destination, overwrite: true);

            _logger?.LogInformation("Hull art fetched: {File}", file);

            return true;
        }
        catch (Exception exception)
        {
            _logger?.LogInformation(exception, "Hull art for {File} could not be fetched", file);

            try
            {
                File.Delete(partial);
            }
            catch (Exception)
            {
            // A leftover .part is untidy, not broken: nothing reads that name.
            }

            return false;
        }
    }

    /// <summary>The same answer <see cref="ShipArt"/> gives, by asking it.</summary>
    private static string? Symbol(string? hull) => ShipArt.Symbol(hull);

    private static HttpClient CreateClient()
    {
        // Longer than the update check's five seconds: that one is a tag and this is three megabytes of
        // video, and a fetch that times out mid-file is a fetch that is not retried until the next launch.
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("d47-hull-art");

        return client;
    }
}
