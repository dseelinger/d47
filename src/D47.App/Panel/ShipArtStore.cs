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
/// Fetches the large hull art the download does not carry
/// (<a href="https://github.com/dseelinger/d47/issues/289">#289</a>): a hull's 4K picture and its
/// turntable, once each, into <c>data\ships\</c>.
/// <para>
/// <b>Why anything is fetched at all.</b> The card still for every hull is 11 MB and ships. The
/// other two files are 260 MB across the fleet, which is not something to hand a Commander who
/// flies a Sidewinder, and it is not something to add to every update either. So they arrive for
/// the hull actually being looked at and stay on disk afterwards. This is <see cref="ShipArt"/>'s
/// "a file that appears" finally being the second half of what that comment always promised.
/// </para>
/// <para>
/// <b>Asked for by a press, not on a schedule.</b> Nothing here runs at startup, nothing sweeps
/// the fleet, and no hull is fetched because it happens to be owned. Opening a ship is the whole
/// trigger, which is also what makes the egress row honest: what leaves is the symbol of a hull
/// the Commander just clicked.
/// </para>
/// <para>
/// <b>Every failure is silence and a log line.</b> Offline, rate-limited, a 404 for a hull nobody
/// has rendered yet — all of them leave the card exactly as it was, drawing the still that came
/// with the build. A picture is not worth a dialog.
/// </para>
/// </summary>
internal static class ShipArtStore
{
    /// <summary>
    /// Where the art is published, pinned exactly as every URL in <c>UpdateChecker</c> is.
    /// <para>
    /// <b>A release asset rather than repository content, and that is a size decision.</b> The two
    /// hundred and sixty megabytes are not in git: putting them there would be permanent, would
    /// land in every clone and would be paid again by CI on every push, for files that change when
    /// somebody re-renders a hull and never otherwise.
    /// </para>
    /// <para>
    /// <b>Its own tag, and it is flagged pre-release on purpose.</b> Every other tag in this
    /// repository is a receipt for one exact <c>d47.exe</c>; this one is a receipt for a set of
    /// pictures. Marking it pre-release keeps it out of <c>/releases/latest</c>, which is what
    /// <c>UpdateChecker</c> reads — an art tag that ever became "latest" would offer every
    /// Commander an update to something with no executable in it. <c>promote.ps1</c> is safe
    /// against it separately: it parses a tag as <c>v0.0.0</c> and skips what does not.
    /// </para>
    /// <para>
    /// <b>The tag moves only when the art is re-rendered</b>, and moving it means publishing a new
    /// one — <c>ship-art-2</c> — rather than replacing this one's assets, for the same reason a
    /// build tag never moves.
    /// </para>
    /// </summary>
    internal const string Source = "https://github.com/dseelinger/d47/releases/download/ship-art-1/";

    /// <summary>
    /// The two files fetched per hull, as suffixes on the symbol. In the order they are wanted:
    /// the picture is what the page the Commander just opened is about, and the turntable is
    /// playing on a card they have already left.
    /// </summary>
    private static readonly string[] Wanted = [".4k.png", ".spin.mp4"];

    /// <summary>
    /// A file is asked for once per session, whether it arrived or not.
    /// <para>
    /// The same rule <see cref="ShipArt"/> keeps for a miss, and for the same reason: a hull with
    /// no art must not become a request every time a Commander steps back into the fleet. A
    /// restart is how you retry, which is also what a Commander who has just plugged the network
    /// back in would do anyway.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> Asked = new(StringComparer.Ordinal);

    private static readonly HttpClient Http = CreateClient();

    private static string? _folder;
    private static Func<bool>? _allowed;
    private static ILogger? _logger;

    /// <summary>Raised on a background thread when a hull's art has landed, with its symbol.</summary>
    internal static event Action<string>? Arrived;

    /// <summary>
    /// Turns fetching on, at startup.
    /// </summary>
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

    /// <summary>
    /// Asks for a hull's art if it is not already here. Returns at once; the work is a background
    /// task and <see cref="Arrived"/> says when it is done.
    /// </summary>
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

                // Already on disk beats already asked: a file dropped in by hand is not a fetch
                // this session refused, and neither is one fetched by the session before.
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

        // The cached miss has to go, or a hull looked at before its picture arrived keeps the
        // absence for the rest of the session.
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

        // Written beside the destination and renamed onto it, so a fetch that is cut off leaves
        // no half-file for the decoder to find. ShipArt survives a corrupt PNG, but it caches the
        // failure, and a truncated download would then cost the hull its picture until a restart.
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

    /// <summary>The same refusal <see cref="ShipArt"/> makes, for the same reason: this is a path.</summary>
    private static string? Symbol(string? hull)
    {
        if (hull is not { Length: > 0 })
        {
            return null;
        }

        var symbol = hull.Trim().ToLowerInvariant();

        return symbol.Length == 0
               || !symbol.All(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-')
            ? null
            : symbol;
    }

    private static HttpClient CreateClient()
    {
        // Longer than the update check's five seconds: that one is a tag and this is three
        // megabytes of video, and a fetch that times out mid-file is a fetch that is not retried
        // until the next launch.
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("d47-hull-art");

        return client;
    }
}
