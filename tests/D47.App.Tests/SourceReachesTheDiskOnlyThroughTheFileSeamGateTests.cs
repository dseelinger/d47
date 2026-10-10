using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A type under <c>src\</c> names <c>File</c>, <c>Directory</c>, <c>FileStream</c>, <c>FileInfo</c>, <c>DirectoryInfo</c>
/// or <c>FileSystemWatcher</c>, or passes a string to a library parameter named for a path, file or directory, only when
/// it is listed here; every other type reaches the disk through <c>IFileSystem</c>. Vendored code is not surveyed.
/// </summary>
[Trait("Category", "Gate")]
public sealed class SourceReachesTheDiskOnlyThroughTheFileSeamGateTests
{
    private const string ImplicitUsings =
        "global using System; global using System.Collections.Generic; global using System.IO; " +
        "global using System.Linq; global using System.Net.Http; global using System.Threading; " +
        "global using System.Threading.Tasks;";

    private static readonly HashSet<string> Banned =
    [
        "System.IO.File", "System.IO.Directory", "System.IO.FileStream", "System.IO.FileInfo", "System.IO.DirectoryInfo",
        "System.IO.FileSystemWatcher",
    ];

    /// <summary>The types that are the disk, and stay listed.</summary>
    private static readonly string[] Disk =
    [
        "D47.App.AppHost",
        "D47.App.Controls.ControlKitWindow",
        "D47.App.Diagnostics.EliteWindowCapture",
        "D47.App.Donation.DonationDispatch",
        "D47.App.Headset.VrFrameDump",
        "D47.App.Logging.LoggingSetup",
        "D47.App.Logging.LogTail",
        "D47.App.Media.AudioFolderWatch",
        "D47.App.Panel.AvatarClipStore",
        "D47.App.Panel.HullGpuCapture",
        "D47.App.Panel.ShipArtStore",
        "D47.App.Panel.StoryDownloader",
        "D47.App.SelfTest",
        "D47.App.StaleBuildCheck",
        "D47.App.StartMenuShortcut",
        "D47.App.Updates.UpdateInstaller",
        "D47.Audio.MediaFoundationDecoder",
        "D47.Audio.MediaFoundationFileDecoder",
        "D47.Core.AppPaths",
        "D47.Core.Diagnostics.Recording.RecordingLog",
        "D47.Core.Input.BindingProfiles",
        "D47.Core.Input.BindsWatch",
        "D47.Core.Storage.DiskFileSystem",
        "D47.Stt.HttpModelStore",
        "D47.Stt.WhisperTranscriber",
        "D47.Tts.ChatterboxInstaller",
        "D47.Tts.KokoroInstaller",
        "D47.Tts.ModelDownload",
        "D47.Vr.OpenVrLoader",
    ];

    /// <summary>
    /// Types whose path parameters are not the app's own file I/O: path arithmetic, shell launches, a form field's
    /// file name, and a model path handed to the ONNX runtime.
    /// </summary>
    private static readonly HashSet<string> NotTheDisk =
    [
        "System.IO.Path", "System.Diagnostics.Process", "System.Diagnostics.ProcessStartInfo",
        "System.Net.Http.MultipartFormDataContent", "Microsoft.ML.OnnxRuntime.InferenceSession",
    ];

    private static readonly Lazy<IReadOnlyDictionary<string, List<string>>> Surveyed = new(Survey);

    [Fact]
    public void NoUnlistedTypeNamesTheDisk()
    {
        var listed = Disk.ToHashSet(StringComparer.Ordinal);
        var unlisted = Surveyed.Value.Where(pair => !listed.Contains(pair.Key)).OrderBy(pair => pair.Key, StringComparer.Ordinal).ToList();

        Assert.True(
            unlisted.Count == 0,
            $"""
             These types name the disk directly. Read and write through IFileSystem instead.

             {string.Join(Environment.NewLine, unlisted.Select(pair => $"\"{pair.Key}\", // {string.Join(", ", pair.Value.Order(StringComparer.Ordinal))}"))}
             """);
    }

    [Fact]
    public void EveryListedTypeStillNamesTheDisk()
    {
        var stale = Disk.Where(type => !Surveyed.Value.ContainsKey(type)).ToList();

        Assert.True(
            stale.Count == 0,
            $"""
             These listed types no longer name the disk. Remove them from the list.

             {string.Join(Environment.NewLine, stale)}
             """);
    }

    /// <summary>Each type under <c>src\</c> that names a banned type or passes a path to a library call, keyed by its outermost type's full name, with what it uses.</summary>
    private static Dictionary<string, List<string>> Survey()
    {
        var source = Path.Combine(AppSource.RepositoryRoot(), "src");
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["DEBUG"]);
        var trees = new List<SyntaxTree>();

        foreach (var path in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            var parts = Path.GetRelativePath(source, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (parts.Any(part => part is "obj" or "bin" or "vendor"))
            {
                continue;
            }

            trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(path), parse, path));
        }

        trees.Add(CSharpSyntaxTree.ParseText(ImplicitUsings, parse, "ImplicitUsings.cs"));

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(path => !Path.GetFileName(path).StartsWith("D47.", StringComparison.Ordinal))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "d47-source",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var names = Banned.Select(name => name[(name.LastIndexOf('.') + 1)..]).ToHashSet(StringComparer.Ordinal);
        var found = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);

            void Note(SyntaxNode node, string use)
            {
                if (node.Ancestors().OfType<BaseTypeDeclarationSyntax>().LastOrDefault() is not { } outermost)
                {
                    return;
                }

                var owner = model.GetDeclaredSymbol(outermost)!.ToDisplayString();

                if (!found.TryGetValue(owner, out var uses))
                {
                    found[owner] = uses = [];
                }

                if (!uses.Contains(use))
                {
                    uses.Add(use);
                }
            }

            foreach (var name in tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (!names.Contains(name.Identifier.ValueText))
                {
                    continue;
                }

                var info = model.GetSymbolInfo(name);
                var type = (info.Symbol ?? info.CandidateSymbols.FirstOrDefault()) switch
                {
                    INamedTypeSymbol named => named,
                    IMethodSymbol { MethodKind: MethodKind.Constructor } constructor => constructor.ContainingType,
                    _ => null,
                };

                if (type?.ToDisplayString() is { } banned && Banned.Contains(banned))
                {
                    Note(name, name.Identifier.ValueText);
                }
            }

            foreach (var call in tree.GetRoot().DescendantNodes().Where(node => node is BaseObjectCreationExpressionSyntax or InvocationExpressionSyntax))
            {
                var (method, arguments) = model.GetOperation(call) switch
                {
                    IObjectCreationOperation creation => (creation.Constructor, creation.Arguments),
                    IInvocationOperation invocation => (invocation.TargetMethod, invocation.Arguments),
                    _ => (null, ImmutableArray<IArgumentOperation>.Empty),
                };

                if (method is null
                    || !method.Locations.Any(location => location.IsInMetadata)
                    || NotTheDisk.Contains(method.ContainingType.ToDisplayString())
                    || method.ContainingType.Name.EndsWith("Exception", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var argument in arguments)
                {
                    if (argument is { ArgumentKind: not ArgumentKind.DefaultValue, Parameter: { Type.SpecialType: SpecialType.System_String } parameter }
                        && PathLike(parameter.Name))
                    {
                        Note(call, $"{method.ContainingType.Name}.{(method.MethodKind == MethodKind.Constructor ? "new" : method.Name)}({parameter.Name})");
                    }
                }
            }
        }

        return found;
    }

    private static bool PathLike(string parameter) =>
        parameter.Contains("path", StringComparison.OrdinalIgnoreCase)
        || parameter.Contains("file", StringComparison.OrdinalIgnoreCase)
        || parameter.Contains("director", StringComparison.OrdinalIgnoreCase)
        || parameter.Equals("uri", StringComparison.Ordinal);
}
