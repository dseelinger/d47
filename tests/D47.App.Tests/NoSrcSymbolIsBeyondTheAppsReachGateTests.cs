using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Every symbol under <c>src\</c> is reachable from <c>Program.Main</c> in the Debug build's IL, or is listed in
/// <c>UnreachableSymbols.txt</c>. The list may only shrink. Vendored OpenVR bindings are not reported.
/// </summary>
[Trait("Category", "Gate")]
public sealed class NoSrcSymbolIsBeyondTheAppsReachGateTests
{
    private static readonly Lazy<IReadOnlyList<string>> Unreached = new(Sweep);

    [Fact]
    public void NoUnlistedSymbolIsUnreachable()
    {
        SkipUnlessDebug();

        var listed = Listed().ToHashSet(StringComparer.Ordinal);
        var unlisted = Unreached.Value.Where(name => !listed.Contains(name)).ToList();

        Assert.True(
            unlisted.Count == 0,
            $"""
             Nothing the app runs reaches these symbols. Reach each one from the app, move it to test code, or delete it.

             {string.Join(Environment.NewLine, unlisted)}
             """);
    }

    [Fact]
    public void EveryListedSymbolIsStillUnreachable()
    {
        SkipUnlessDebug();

        var unreached = Unreached.Value.ToHashSet(StringComparer.Ordinal);
        var stale = Listed().Where(name => !unreached.Contains(name)).ToList();

        Assert.True(
            stale.Count == 0,
            $"""
             The app now reaches these listed symbols, or they no longer exist. Remove these lines from UnreachableSymbols.txt.

             {string.Join(Environment.NewLine, stale)}
             """);
    }

    private static void SkipUnlessDebug()
    {
        var configuration = typeof(AppHost).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;

        if (configuration != "Debug")
        {
            Assert.Skip($"This gate reads the Debug build, and this is a {configuration} build.");
        }
    }

    private static List<string> Listed()
    {
        using var stream = typeof(NoSrcSymbolIsBeyondTheAppsReachGateTests).Assembly.GetManifestResourceStream("UnreachableSymbols.txt")
                           ?? throw new InvalidOperationException("UnreachableSymbols.txt is not embedded.");
        using var reader = new StreamReader(stream);

        return [.. reader.ReadToEnd().Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)];
    }

    /// <summary>Sweeps <c>d47.dll</c> and every <c>D47.*</c> assembly it references, from beside this test.</summary>
    private static IReadOnlyList<string> Sweep()
    {
        var images = new List<ImmutableArray<byte>>();
        var pending = new Queue<string>(["d47"]);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (pending.TryDequeue(out var name))
        {
            if (!seen.Add(name))
            {
                continue;
            }

            var bytes = ImmutableArray.Create(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, name + ".dll")));
            images.Add(bytes);

            using var pe = new PEReader(bytes);
            var md = pe.GetMetadataReader();

            foreach (var handle in md.AssemblyReferences)
            {
                var referenced = md.GetString(md.GetAssemblyReference(handle).Name);

                if (referenced.StartsWith("D47.", StringComparison.Ordinal))
                {
                    pending.Enqueue(referenced);
                }
            }
        }

        return ReachabilitySweep.Unreached(images, [("D47.App.Program", "Main"), ("D47.App.Program", "BuildAvaloniaApp")], ["Valve.VR"]);
    }
}
