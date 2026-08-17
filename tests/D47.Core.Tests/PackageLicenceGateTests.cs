using System.Xml.Linq;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// The third gate: permissive licences only, no copyleft, verified across the <b>transitive</b>
/// package graph rather than the direct references (CLAUDE.md, "No telemetry" / licences).
/// <para>
/// It lives as a test for the same reason <see cref="CoreDependencyTests"/> and
/// <see cref="DocumentationGateTests"/> do — a CI step is a second place the rule is written down,
/// and two places drift. Until now this invariant was enforced by care alone, which is to say it
/// was enforced whenever somebody remembered.
/// </para>
///
/// <para>
/// <b>This gate is about code, and only about code.</b> It walks NuGet packages: the
/// <c>project.assets.json</c> that restore writes, and the <c>.nuspec</c> inside each resolved
/// package in the NuGet cache. It never enumerates a file in this repository and has no way to be
/// given one.
/// </para>
/// <para>
/// That is not fastidiousness, it is the correction of a specific mistake CLAUDE.md records: the
/// licence rule was once applied to <b>game data</b>, and it cost accuracy in both directions — a
/// source was rejected on licence grounds when its real problem lay elsewhere, and two were
/// accepted without anybody noticing whose data was inside them. Ship figures, blueprints,
/// material names and engineer locations are facts about Elite Dangerous. They are Frontier's,
/// governed by Frontier's published rules, and a community repository's MIT grant covers that
/// repository's code and says nothing about the data underneath it — coriolis-data says so in as
/// many words. <c>src/D47.Core/Knowledge/*.tsv</c> is therefore <b>not</b> in scope here and must
/// never be brought into scope: the provenance of those tables is a generator's job and
/// <c>NOTICE</c>'s, and pointing a package-licence gate at them is the exact error that was paid
/// for once already.
/// </para>
///
/// <para>
/// <b>What it cannot see.</b> It reads what a package <em>claims</em>. A package whose nuspec says
/// Apache-2.0 while it packs a differently-licensed native binary is invisible to any metadata
/// check, and d47 has met exactly that — <c>OpenCvSharp4.runtime.win</c> declares Apache-2.0 and
/// packs an LGPL-2.1 FFmpeg DLL, found by a person reading the package and not by a tool. So this
/// raises the floor and does not replace the reading. Its honest claim is that nothing in the
/// graph <em>declares</em> a licence d47 may not ship.
/// </para>
///
/// <para>
/// <b>A second blind spot, found by walking into it.</b> The gate reads <c>targets</c> and
/// <c>libraries</c>, which is where ordinary <c>PackageReference</c> resolution lands. A
/// <c>FrameworkReference</c> does not land there: it arrives in <c>downloadDependencies</c> and
/// <c>frameworkReferences</c>, and is invisible here. That covered nothing but the .NET runtime
/// packs until Phase 21, which moved <c>D47.App</c> to <c>net10.0-windows10.0.26100.0</c> for
/// <c>Windows.Gaming.Input</c> and so pulled in <c>Microsoft.Windows.SDK.NET.Ref</c> — a package
/// that ships <c>Microsoft.Windows.SDK.NET.dll</c> and <c>WinRT.Runtime.dll</c> into the
/// executable, declares <b>no SPDX expression at all</b>, points its <c>licenseUrl</c> at the
/// Windows SDK terms, and sets <c>requireLicenseAcceptance</c>. Not copyleft, and it is Microsoft's
/// own grant for code that runs on Windows — but it is a redistributable binary in the shipped exe
/// that this gate did not examine and would not have named. Recorded here rather than fixed,
/// because widening the walk to download dependencies would put the .NET runtime packs themselves
/// in scope and turn the gate into an argument about the platform d47 is written for. The honest
/// claim above stands and is narrower than it reads: nothing <em>in the package graph</em>
/// declares a licence d47 may not ship.
/// </para>
///
/// <para>
/// <b>The gate's own dependencies obey the gate.</b> It adds no package: the assets file is JSON
/// and the nuspec is XML, both read with what the framework already provides, so the graph it
/// walks is not enlarged by the walking.
/// </para>
/// </summary>
public class PackageLicenceGateTests
{
    /// <summary>
    /// Every project the solution builds, each with its resolved graph. Static because it is read
    /// once and asked many questions.
    /// </summary>
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

    /// <summary>
    /// The gate proper. One case per package so a failure names the offender in the test name
    /// as well as in the message.
    /// </summary>
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
    /// <para>
    /// A licence gate that walks nothing passes every time and looks identical to a clean graph
    /// from the outside. Restore not having run, an assets file moving, or a parser change that
    /// quietly yields nothing would all present as "no violations" — so the count, the projects
    /// and transitivity itself are each asserted rather than assumed.
    /// </para>
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

        // Transitivity, witnessed rather than trusted. No csproj in this repository references
        // DependencyInjection.Abstractions; it is in the graph only because Logging.Abstractions
        // brings it. If the walk ever stopped at direct references, this is the assertion that
        // would notice.
        Assert.Contains(
            Distinct(),
            package => string.Equals(
                package.Id,
                "Microsoft.Extensions.DependencyInjection.Abstractions",
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Every project in the solution was restored and read. A project whose assets file is absent
    /// is skipped by everything above, so it is failed here instead of being silently trusted.
    /// </summary>
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
        // The first project that has the package on disk answers for it. They all resolve to the
        // same folder in the same cache; taking the first that exists simply skips a project whose
        // cache entry has been cleaned.
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
    /// Every distinct package across every project, ordered so the theory's cases are stable
    /// between runs.
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
    /// The projects the solution builds, read from <c>d47.slnx</c> so this list cannot fall behind
    /// the one the build uses. Anything outside the solution — <c>spike/</c> — is outside the
    /// shipped product and outside this gate, exactly as <c>spike/README.md</c> says.
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
