using System.Xml.Linq;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// The third gate: permissive licences only, no copyleft, verified across the transitive package graph
/// rather than the direct references (CLAUDE.md, "No telemetry" / licences).
/// </summary>
public class PackageLicenceGateTests
{
    /// <summary>Every project the solution builds, each with its resolved graph.</summary>
    private static readonly Lazy<IReadOnlyList<ProjectPackages>> Graph = new(Load);

    public static TheoryData<string, string> EveryPackage
    {
        get
        {
            var data = new TheoryData<string, string>();

            foreach (var package in Distinct())
            {
                data.Add(package.Id, package.Version);
            }

            return data;
        }
    }

    /// <summary>The gate proper.</summary>
    [Theory]
    [MemberData(nameof(EveryPackage))]
    public void EveryPackageInTheTransitiveGraphIsPermissivelyLicensed(string id, string version)
    {
        var package = new PackageId(id, version);
        var licence = Resolve(package);

        if (licence.Verdict == LicenceVerdict.Permissive)
        {
            return;
        }

        var problem = licence.Verdict == LicenceVerdict.Copyleft
            ? $"{package} is copyleft: {licence.Summary}."
            : $"{package} has no licence this gate can confirm as permissive: {licence.Summary}.";

        Assert.Fail(
            $"""
             {problem}

             d47 ships permissive licences only, with no copyleft, and the rule covers the whole
             transitive graph rather than the direct references (CLAUDE.md).

             It reached the build this way:
             {string.Join(Environment.NewLine, PathsTo(package).Select(path => "  " + path))}

             Read from: {licence.Source}

             Either drop the package that pulls it in, replace it with one that does not, or — if
             this licence really is acceptable — add its identifier to LicenceGate.Permissive with
             a comment saying who decided and when. Do not widen it silently.
             """);
    }

    /// <summary>
    /// The gate can only report an absence it was in a position to find, so this asserts it was.
    /// </summary>
    [Fact]
    public void TheGateActuallyWalkedSomething()
    {
        var projects = Graph.Value;

        Assert.NotEmpty(projects);

        var empty = projects.Where(project => !project.Packages.Any()).Select(p => p.Name).ToArray();

        Assert.True(
            empty.Length == 0,
            $"These projects resolved to no packages at all, which means their graph was not read: "
            + $"{string.Join(", ", empty)}.");

        // Transitivity, witnessed rather than trusted.
        Assert.Contains(
            Distinct(),
            package => string.Equals(
                package.Id,
                "Microsoft.Extensions.DependencyInjection.Abstractions",
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Every project in the solution was restored and read.</summary>
    [Fact]
    public void EveryProjectInTheSolutionIsInTheGraph()
    {
        var walked = Graph.Value.Select(project => project.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = SolutionProjects()
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Where(name => !walked.Contains(name))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"""
             These projects are in d47.slnx and were not walked, so their packages were not
             checked: {string.Join(", ", missing)}.

             Their project.assets.json is missing, which means they have not been restored. Run
             `dotnet restore` — or `dotnet test` from the repository root, which restores the whole
             solution — rather than testing one project in isolation.
             """);
    }

    private static Licence Resolve(PackageId package)
    {
        // The first project that has the package on disk answers for it.
        foreach (var project in Graph.Value)
        {
            if (project.FolderFor(package) is { } folder)
            {
                return LicenceGate.Resolve(package, folder);
            }
        }

        return LicenceGate.Resolve(package, folder: null);
    }

    private static IEnumerable<string> PathsTo(PackageId package) =>
        Graph.Value
            .Where(project => project.Packages.Any(p =>
                string.Equals(p.Id, package.Id, StringComparison.OrdinalIgnoreCase)))
            .Select(project => project.PathTo(package))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

    /// <summary>
    /// Every distinct package across every project, ordered so the theory's cases are stable between
    /// runs.
    /// </summary>
    private static IReadOnlyList<PackageId> Distinct() =>
        [.. Graph.Value
            .SelectMany(project => project.Packages)
            .DistinctBy(package => $"{package.Id}/{package.Version}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(package => package.Version, StringComparer.OrdinalIgnoreCase)];

    private static IReadOnlyList<ProjectPackages> Load()
    {
        var root = RepositoryRoot();
        var projects = new List<ProjectPackages>();

        foreach (var relative in SolutionProjects())
        {
            var directory = Path.GetDirectoryName(Path.Combine(root, relative));

            if (directory is null)
            {
                continue;
            }

            var assets = Path.Combine(directory, "obj", "project.assets.json");

            if (File.Exists(assets))
            {
                projects.Add(ProjectPackages.Read(Path.GetFileNameWithoutExtension(relative), assets));
            }
        }

        return projects;
    }

    /// <summary>
    /// The projects the solution builds, read from <c>d47.slnx</c> so this list cannot fall behind the
    /// one the build uses.
    /// </summary>
    private static IReadOnlyList<string> SolutionProjects() =>
        [.. XDocument
            .Load(Path.Combine(RepositoryRoot(), "d47.slnx"))
            .Descendants("Project")
            .Select(project => project.Attribute("Path")?.Value)
            .OfType<string>()];

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException(
                   $"Could not find the repository root: no d47.slnx above {AppContext.BaseDirectory}.");
    }
}
