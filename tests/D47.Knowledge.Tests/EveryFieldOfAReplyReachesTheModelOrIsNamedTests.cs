using System.Reflection;
using D47.Knowledge.Tests.ReplyFields;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>
/// Each service reply is recorded untrimmed. A manifest next to it (a static <c>Manifest</c> property of
/// type <see cref="ReplyManifest"/>) puts every key path of the recording in the model, an exclusion or pending.
/// </summary>
public sealed class EveryFieldOfAReplyReachesTheModelOrIsNamedTests
{
    public static TheoryData<string> Replies => [.. Manifests().Select(manifest => manifest.Fixture)];

    [Theory]
    [MemberData(nameof(Replies))]
    public void EveryKeyPathOfTheRecordingIsMapped(string fixture)
    {
        var manifest = Manifest(fixture);
        using var document = Fixture.Json($"{fixture}.json");

        var unmapped = ReplyKeyPaths.Of(document.RootElement, manifest.Dynamic).Keys
            .Where(path => !manifest.Paths.ContainsKey(path))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(unmapped.Count == 0, $"{fixture} has key paths no manifest entry covers:\n{string.Join('\n', unmapped)}");
    }

    [Theory]
    [MemberData(nameof(Replies))]
    public void EveryMappedPropertyIsFilledOnSomeRowTheRecordingFillsIt(string fixture)
    {
        var manifest = Manifest(fixture);
        using var document = Fixture.Json($"{fixture}.json");
        var model = manifest.Read(document);

        var failures = new List<string>();

        foreach (var (keyPath, filled) in ReplyKeyPaths.Of(document.RootElement, manifest.Dynamic).OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!manifest.Paths.TryGetValue(keyPath, out var mapping) || mapping is not ReplyMapping.Property { Path: var property })
            {
                continue;
            }

            if (ReplyModelPaths.Unresolved(model, property) is { } reason)
            {
                failures.Add($"{property} (from {keyPath}): {reason}");
            }
            else if (filled && !ReplyModelPaths.AnyPresent(model, property))
            {
                failures.Add($"{property} (from {keyPath}) is empty on every row, though the recording fills the field");
            }
        }

        Assert.True(failures.Count == 0, $"{fixture}:\n{string.Join('\n', failures)}");
    }

    private static ReplyManifest Manifest(string fixture) => Manifests().Single(manifest => manifest.Fixture == fixture);

    private static IEnumerable<ReplyManifest> Manifests() =>
        typeof(EveryFieldOfAReplyReachesTheModelOrIsNamedTests).Assembly.GetTypes()
            .Select(type => type.GetProperty("Manifest", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Where(property => property?.PropertyType == typeof(ReplyManifest))
            .Select(property => (ReplyManifest)property!.GetValue(null)!);
}
