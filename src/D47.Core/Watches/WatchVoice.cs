using D47.Core.Capabilities;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Core.Watches;

/// <summary>Adds and deletes next-system watches for the Commander's own words and says what came of it.</summary>
public sealed class WatchVoice(
    Func<WatchStore?> store,
    Func<IGalaxyService?> galaxy,
    Func<string?> currentSystem,
    ILogger<WatchVoice> logger)
{
    private const string NoStore = "Nothing here keeps watches.";

    /// <summary>Said when the service asked for is not one a station has.</summary>
    public static string Declined(string said)
    {
        var words = string.IsNullOrWhiteSpace(said) ? "That" : char.ToUpperInvariant(said.Trim()[0]) + said.Trim()[1..];

        return "I can watch for a station service, Commander, such as a material trader, a technology broker or "
               + $"Interstellar Factors. {words} is not one of them.";
    }

    /// <summary>Stores a next-system watch and says so; when the Commander is in a system that already has a match, names the station.</summary>
    public async Task<ToolResult> AddAsync(string? service, string? kind, string? pad, CancellationToken cancellationToken)
    {
        if (store() is not { } watches)
        {
            return ToolResult.Error(NoStore);
        }

        if (NextSystemWatch.ServiceFor(service) is not { } known)
        {
            return ToolResult.Ok(Declined(service ?? string.Empty));
        }

        PadSize? minPad = Pad(pad);
        var watch = new Watch(
            Guid.NewGuid(),
            NextSystemWatch.ServiceName(known.Name, NextSystemWatch.KindFor(known.Name, kind)),
            WatchKind.NextSystem,
            Service: known.Name,
            ServiceKind: NextSystemWatch.KindFor(known.Name, kind),
            MinPad: minPad);

        if (WatchStore.Refusal(watch) is { } refusal)
        {
            return ToolResult.Error($"I could not keep that watch: {refusal}");
        }

        var said = NextSystemWatch.Said(known.Name, watch.ServiceKind);

        if (watches.Watches.Any(existing => Same(existing, watch) && existing.Enabled))
        {
            return ToolResult.Ok($"I am already watching for {said}, Commander.");
        }

        if (watches.Watches.Count >= WatchStore.MaxWatches)
        {
            return ToolResult.Ok(
                $"You have {WatchStore.MaxWatches} watches, the most I keep. Delete one under Commander, Watches, then ask again.");
        }

        if (watches.Add(watch) is { } failed)
        {
            return ToolResult.Error($"I could not keep that watch: {failed}");
        }

        if (galaxy() is not { } search)
        {
            return ToolResult.Ok(
                $"Watching for {said}, Commander. The watch is saved. D47 cannot tell what a system's stations have until Galaxy search is on.");
        }

        var added = $"Watching for {said}, Commander. I'll tell you when we arrive in a system with one.";

        if (currentSystem()?.Trim() is not { Length: > 0 } here)
        {
            return ToolResult.Ok(added);
        }

        try
        {
            var result = await search.FindStationsAsync(StationQuery.InSystem(here), cancellationToken).ConfigureAwait(false);

            if (WatchRule.Arrival(watch, here, result.Stations) is { } station)
            {
                return ToolResult.Ok(
                    $"{here} has one now, Commander: {NextSystemWatch.Place(station)}. I'll watch from the next system on.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogInformation("The station check for {System} after adding a watch failed: {Reason}", here, ex.Message);
        }

        return ToolResult.Ok(added);
    }

    /// <summary>Deletes the next-system watches for a service and kind and says so.</summary>
    public ToolResult Delete(string? service, string? kind)
    {
        if (store() is not { } watches)
        {
            return ToolResult.Error(NoStore);
        }

        if (NextSystemWatch.ServiceFor(service) is not { } known)
        {
            return ToolResult.Ok(Declined(service ?? string.Empty));
        }

        var wanted = NextSystemWatch.KindFor(known.Name, kind);
        var ofService = watches.Watches
            .Where(watch => watch.Kind == WatchKind.NextSystem && string.Equals(watch.Service, known.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var matches = wanted is not null
            ? [.. ofService.Where(watch => string.Equals(watch.ServiceKind, wanted, StringComparison.OrdinalIgnoreCase))]
            : ofService.Count == 1
                ? ofService
                : [.. ofService.Where(watch => watch.ServiceKind is null)];

        if (matches.Count == 0 && wanted is null && ofService.Count > 1)
        {
            return ToolResult.Ok($"You have {ofService.Count} watches for that service, Commander. Say which kind to delete.");
        }

        if (matches.Count == 0)
        {
            return ToolResult.Ok($"You have no watch for {NextSystemWatch.Said(known.Name, wanted)}, Commander.");
        }

        var deleted = matches.Count(watch => watches.Delete(watch.Id));

        return deleted == 0
            ? ToolResult.Error("I could not delete that watch. The watch file may need fixing by hand.")
            : ToolResult.Ok(deleted == 1 ? "Done, Commander. That watch is deleted." : "Done, Commander. Those watches are deleted.");
    }

    private static PadSize? Pad(string? pad) =>
        string.IsNullOrWhiteSpace(pad) ? null : Enum.TryParse<PadSize>(pad.Trim(), ignoreCase: true, out var size) ? size : null;

    private static bool Same(Watch left, Watch right) =>
        left.Kind == WatchKind.NextSystem
        && string.Equals(left.Service, right.Service, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.ServiceKind, right.ServiceKind, StringComparison.OrdinalIgnoreCase)
        && left.MinPad == right.MinPad;
}
