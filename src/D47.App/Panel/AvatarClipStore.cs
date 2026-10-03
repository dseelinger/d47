using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using D47.Core.Audio;
using D47.Core.Interface;
using Microsoft.Extensions.Logging;

namespace D47.App.Panel;

/// <summary>Fetches the avatar clips of the core aboard that are not on disk, into <c>data\avatar-clips</c>.</summary>
internal static class AvatarClipStore
{
    internal const string Source = "https://github.com/dseelinger/d47/releases/download/avatars-1/";

    /// <summary>A file is asked for once per session, whether it arrived or not.</summary>
    private static readonly HashSet<string> Asked = new(StringComparer.Ordinal);

    private static readonly HttpClient Http = CreateClient();

    private static string? _folder;
    private static Func<bool>? _allowed;
    private static ILogger? _logger;

    /// <summary>Raised on a background thread when a core's clip has landed, with the core id.</summary>
    internal static event Action<string>? Arrived;

    /// <summary>Turns fetching on. <paramref name="allowed"/> is read at each fetch.</summary>
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

    /// <summary>Asks for the core's clips that are not on disk. Never blocks.</summary>
    internal static void Want(string? coreId)
    {
        if (string.IsNullOrEmpty(coreId))
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

            foreach (var state in Enum.GetValues<LoopState>())
            {
                var file = CoreClips.FileName(coreId, state);

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

        _ = Task.Run(() => FetchAsync(coreId, missing));
    }

    private static async Task FetchAsync(string coreId, List<string> files)
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

        try
        {
            Arrived?.Invoke(coreId);
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "Redrawing after the clips for {Core} landed failed", coreId);
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
                    "No avatar clip for {File}: the release returned {Status}", file, response.StatusCode);

                return false;
            }

            await using (var target = File.Create(partial))
            {
                await response.Content.CopyToAsync(target, CancellationToken.None).ConfigureAwait(false);
            }

            File.Move(partial, destination, overwrite: true);

            _logger?.LogInformation("Avatar clip fetched: {File}", file);

            return true;
        }
        catch (Exception exception)
        {
            _logger?.LogInformation(exception, "Avatar clip {File} could not be fetched", file);

            try
            {
                File.Delete(partial);
            }
            catch (Exception)
            {
                // A leftover .part is not read by anything.
            }

            return false;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("d47-avatar-clips");

        return client;
    }
}
