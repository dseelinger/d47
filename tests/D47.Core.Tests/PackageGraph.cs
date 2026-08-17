using System.Xml.Linq;
using System.Text.Json;

namespace D47.Core.Tests;

/// <summary>One package in the resolved graph, at the exact version NuGet settled on.</summary>
internal sealed record PackageId(string Id, string Version)
{
    public override string ToString() => $"{Id} {Version}";
}

/// <summary>
/// What a package's own metadata claims about its licence, and where that claim came from.
/// </summary>
/// <param name="Verdict">Permissive, Copyleft or Unknown. Unknown fails: see <see cref="LicenceGate"/>.</param>
/// <param name="Summary">The licence as stated, for the failure message.</param>
/// <param name="Source">Which piece of metadata was read, so a disagreement can be chased.</param>
internal sealed record Licence(LicenceVerdict Verdict, string Summary, string Source);

internal enum LicenceVerdict
{
    Permissive,
    Copyleft,
    Unknown,
}

/// <summary>
/// The transitive <b>package</b> graph of one project, read from the <c>project.assets.json</c>
/// NuGet writes at restore.
/// <para>
/// That file is the only honest source for this. A csproj lists what somebody typed; the assets
/// file lists what was actually resolved, at the versions actually used, including every package
/// nobody typed — which is the half the invariant is about.
/// </para>
/// </summary>
internal sealed class ProjectPackages
{
    private ProjectPackages(
        string name,
        IReadOnlyList<string> roots,
        IReadOnlyDictionary<string, PackageId> resolved,
        IReadOnlyDictionary<string, IReadOnlyList<string>> edges,
        IReadOnlyDictionary<string, string> packagePaths,
        IReadOnlyList<string> packageFolders)
    {
        Name = name;
        Roots = roots;
        Resolved = resolved;
        Edges = edges;
        PackagePaths = packagePaths;
        PackageFolders = packageFolders;
    }

    public string Name { get; }

    /// <summary>Lower-cased ids of everything this project asks for by name — packages and project references.</summary>
    private IReadOnlyList<string> Roots { get; }

    /// <summary>Lower-cased id to the resolved package. Project references are absent: they are not packages.</summary>
    private IReadOnlyDictionary<string, PackageId> Resolved { get; }

    private IReadOnlyDictionary<string, IReadOnlyList<string>> Edges { get; }

    private IReadOnlyDictionary<string, string> PackagePaths { get; }

    private IReadOnlyList<string> PackageFolders { get; }

    public IEnumerable<PackageId> Packages => Resolved.Values;

    /// <summary>
    /// Where a package's contents are on disk, or null if the folder is not there. Used only to
    /// read the licence file a nuspec points at.
    /// </summary>
    public string? FolderFor(PackageId package)
    {
        if (!PackagePaths.TryGetValue(package.Id.ToLowerInvariant(), out var relative))
        {
            return null;
        }

        return PackageFolders
            .Select(root => Path.Combine(root, relative))
            .FirstOrDefault(Directory.Exists);
    }

    /// <summary>
    /// How this project came to depend on a package: the shortest chain from something it names
    /// outright down to the package itself.
    /// <para>
    /// Shortest rather than any, because "you asked for this" and "this arrived four levels down
    /// behind something else" are different problems with different fixes, and the chain is what
    /// tells them apart.
    /// </para>
    /// </summary>
    public string PathTo(PackageId package)
    {
        var wanted = package.Id.ToLowerInvariant();
        var cameFrom = new Dictionary<string, string?>(StringComparer.Ordinal);
        var queue = new Queue<string>();

        foreach (var root in Roots)
        {
            if (cameFrom.TryAdd(root, null))
            {
                queue.Enqueue(root);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (string.Equals(current, wanted, StringComparison.Ordinal))
            {
                return string.Join(" -> ", Chain(cameFrom, current).Prepend(Name));
            }

            foreach (var next in Edges.GetValueOrDefault(current, []))
            {
                if (cameFrom.TryAdd(next, current))
                {
                    queue.Enqueue(next);
                }
            }
        }

        // Reachable in the resolved graph but not from anything this project names. That is what a
        // package pulled in purely by a project reference looks like from here, and the project
        // that does name it reports its own path.
        return $"{Name} -> (via a project reference) -> {package.Id}";
    }

    private IEnumerable<string> Chain(IReadOnlyDictionary<string, string?> cameFrom, string leaf)
    {
        var chain = new List<string>();

        for (string? at = leaf; at is not null; at = cameFrom[at])
        {
            chain.Add(Resolved.TryGetValue(at, out var package) ? package.ToString() : at);
        }

        chain.Reverse();
        return chain;
    }

    /// <summary>
    /// Reads one project's assets file. Every target is walked, not just the first: a
    /// runtime-identifier target is exactly where a native package appears that the portable
    /// target has never heard of, which is the kind of thing that only ships in a release.
    /// </summary>
    public static ProjectPackages Read(string name, string assetsPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
        var root = document.RootElement;

        var resolved = new Dictionary<string, PackageId>(StringComparer.Ordinal);
        var edges = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var roots = new List<string>();

        foreach (var target in Property(root, "targets").EnumerateObject())
        {
            foreach (var entry in target.Value.EnumerateObject())
            {
                var separator = entry.Name.LastIndexOf('/');
                var id = separator < 0 ? entry.Name : entry.Name[..separator];
                var version = separator < 0 ? string.Empty : entry.Name[(separator + 1)..];
                var key = id.ToLowerInvariant();

                var kind = entry.Value.TryGetProperty("type", out var type) ? type.GetString() : null;

                if (string.Equals(kind, "package", StringComparison.Ordinal))
                {
                    resolved[key] = new PackageId(id, version);
                }
                else if (string.Equals(kind, "project", StringComparison.Ordinal) && !roots.Contains(key))
                {
                    // A referenced project is a root as well as a node, so a package that arrives
                    // behind one still has a readable path rather than an unexplained appearance.
                    roots.Add(key);
                }

                var dependencies = entry.Value.TryGetProperty("dependencies", out var listed)
                    ? listed.EnumerateObject().Select(d => d.Name.ToLowerInvariant()).ToList()
                    : [];

                edges[key] = [.. edges.GetValueOrDefault(key, []).Union(dependencies, StringComparer.Ordinal)];
            }
        }

        foreach (var framework in Property(Property(root, "project"), "frameworks").EnumerateObject())
        {
            if (!framework.Value.TryGetProperty("dependencies", out var dependencies))
            {
                continue;
            }

            foreach (var dependency in dependencies.EnumerateObject())
            {
                var declared = dependency.Value.TryGetProperty("target", out var target)
                    ? target.GetString()
                    : null;

                var key = dependency.Name.ToLowerInvariant();

                if (string.Equals(declared, "Package", StringComparison.Ordinal) && !roots.Contains(key))
                {
                    roots.Add(key);
                }
            }
        }

        var paths = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var library in Property(root, "libraries").EnumerateObject())
        {
            var kind = library.Value.TryGetProperty("type", out var type) ? type.GetString() : null;

            if (!string.Equals(kind, "package", StringComparison.Ordinal))
            {
                continue;
            }

            var separator = library.Name.LastIndexOf('/');
            var id = (separator < 0 ? library.Name : library.Name[..separator]).ToLowerInvariant();

            if (library.Value.TryGetProperty("path", out var relative) && relative.GetString() is { } value)
            {
                paths[id] = value;
            }
        }

        var folders = root.TryGetProperty("packageFolders", out var packageFolders)
            ? packageFolders.EnumerateObject().Select(folder => folder.Name).ToList()
            : [];

        return new ProjectPackages(name, roots, resolved, edges, paths, folders);
    }

    private static JsonElement Property(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value : default;
}
