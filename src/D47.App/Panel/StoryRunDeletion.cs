using System.Globalization;
using D47.Core.Interface;
using D47.Core.Stories;

namespace D47.App.Panel;

/// <summary>The confirmations for deleting a story run, shared by the Story bench and a run's page.</summary>
internal static class StoryRunDeletion
{
    internal const string DeleteLabel = "Delete";
    internal const string DeleteRunLabel = "Delete run";
    internal const string AbandonLabel = "Abandon";
    internal const string CancelLabel = "Cancel";
    internal const string ShelvedFirstTitle = "Abandon it first";
    internal const string ShelvedText = "A shelved story can come back, so its run cannot be deleted. Abandon it first; then it can be deleted.";
    internal const string DeleteText = "Every line and clip this run kept is deleted. The story stays in the Catalogue, and picking it again starts a new run.";

    /// <summary>Whether a run can offer Delete: every run but the active one.</summary>
    internal static bool Offers(Story run) => !run.IsCurrent;

    internal static string Title(Story run) => $"Delete this run of {run.Title}?";

    internal static string ShelvedNotice(Story run) => $"{run.Title} is shelved. {ShelvedText}";

    internal static string Size(long bytes) => bytes < 1024 * 1024
        ? $"{(bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture)} KB"
        : $"{(bytes / (1024.0 * 1024.0)).ToString("0.#", CultureInfo.InvariantCulture)} MB";

    internal static string Context(int lines, long clipBytes) =>
        $"Lines: {lines.ToString(CultureInfo.InvariantCulture)}\nClips on disk: {Size(clipBytes)}\n{DeleteText}";

    /// <summary>
    /// Asks before deleting <paramref name="run"/>, or before abandoning it when it is shelved. <paramref name="act"/> runs the
    /// work off the tick, given what is being done, the work, which returns a refusal or null, and the line to show once done.
    /// </summary>
    internal static void Ask(
        PanelPrompts prompts, StoryDirector director, Func<string?> commander, Func<DateTimeOffset> now, Story run,
        Action<string, Func<string?>, string> act)
    {
        var drawnFor = commander();

        if (run.State == StoryState.Shelved)
        {
            prompts.Choose(
                new ChoiceRequest(
                    "stories.bench.abandon-first",
                    StoryBenchPage.PageName,
                    ShelvedFirstTitle,
                    ShelvedNotice(run),
                    [new ChoiceOption("abandon", AbandonLabel), new ChoiceOption("cancel", CancelLabel)],
                    null,
                    ChoiceSurface.Layer),
                option =>
                {
                    if (option.Key == "abandon" && string.Equals(drawnFor, commander(), StringComparison.Ordinal))
                    {
                        act($"Abandoning {run.Title}…", () => director.AbandonShelved(drawnFor, run, now()), $"{run.Title} is abandoned. It can be deleted now.");
                    }
                });
            return;
        }

        var lines = director.Records?.Read(drawnFor, run).Count ?? 0;
        var clips = director.Records?.ClipBytes(drawnFor, run) ?? 0;

        prompts.Choose(
            new ChoiceRequest(
                "stories.bench.delete",
                StoryBenchPage.PageName,
                Title(run),
                Context(lines, clips),
                [new ChoiceOption("delete", DeleteRunLabel), new ChoiceOption("cancel", CancelLabel)],
                null,
                ChoiceSurface.Layer),
            option =>
            {
                if (option.Key == "delete" && string.Equals(drawnFor, commander(), StringComparison.Ordinal))
                {
                    act($"Deleting the run of {run.Title}…", () => director.DeleteRun(drawnFor, run), $"The run of {run.Title} is deleted.");
                }
            });
    }
}
