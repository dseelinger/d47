using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// A type in Core names <c>File</c>, <c>Directory</c>, <c>FileStream</c>, <c>FileInfo</c>, <c>DirectoryInfo</c> or
/// <c>AtomicFile</c> only when it is listed here; every other type reaches the disk through <c>IFileSystem</c>.
/// </summary>
[Trait("Category", "Gate")]
public sealed class CoreReachesTheDiskOnlyThroughTheFileSeamGateTests
{
    private const string ImplicitUsings =
        "global using System; global using System.Collections.Generic; global using System.IO; " +
        "global using System.Linq; global using System.Net.Http; global using System.Threading; " +
        "global using System.Threading.Tasks;";

    private static readonly HashSet<string> Banned =
    [
        "System.IO.File", "System.IO.Directory", "System.IO.FileStream", "System.IO.FileInfo", "System.IO.DirectoryInfo",
        "D47.Core.Storage.AtomicFile",
    ];

    /// <summary>The types that are the disk, and stay listed.</summary>
    private static readonly string[] Disk =
    [
        "D47.Core.AppPaths",
        "D47.Core.Storage.AtomicFile",
        "D47.Core.Storage.DiskFileSystem",
    ];

    /// <summary>The types not yet converted to <c>IFileSystem</c>. Each conversion removes its own.</summary>
    private static readonly string[] NotYetConverted =
    [
        "D47.Core.Actions.MacroStore",
        "D47.Core.Activities.ActivityLedger",
        "D47.Core.Adventures.AdventureBook",
        "D47.Core.Adventures.AdventureStore",
        "D47.Core.Audio.ChatterboxCatalog",
        "D47.Core.Audio.ChatterboxVoices",
        "D47.Core.Audio.CustomVoices",
        "D47.Core.Audio.FolderAudioSource",
        "D47.Core.Audio.NameAccents",
        "D47.Core.Audio.OwnVoice",
        "D47.Core.Audio.WavReader",
        "D47.Core.Capabilities.Builtin.DiagnosticsCapability",
        "D47.Core.Catalog.ModelCatalogCache",
        "D47.Core.Conversation.BookmarkStore",
        "D47.Core.Conversation.LearnedPhrasesStore",
        "D47.Core.Conversation.SpendLedger",
        "D47.Core.Debrief.StandingDirectionsStore",
        "D47.Core.Diagnostics.Donation.CorpusDonation",
        "D47.Core.Diagnostics.Donation.DonationErasure",
        "D47.Core.Diagnostics.Donation.DonationReceipt",
        "D47.Core.Diagnostics.Donation.DonorToken",
        "D47.Core.Diagnostics.Donation.IncidentSources",
        "D47.Core.Diagnostics.Recording.RecordingLog",
        "D47.Core.Goals.GoalMiner",
        "D47.Core.Goals.GoalStore",
        "D47.Core.Hotas.SwitchStore",
        "D47.Core.Input.BindingProfiles",
        "D47.Core.Input.BindsResolver",
        "D47.Core.Input.BindsWatch",
        "D47.Core.Input.EliteBinds",
        "D47.Core.Interface.AvatarLibrary",
        "D47.Core.Interface.EliteDisplay",
        "D47.Core.Interface.ElitePalette",
        "D47.Core.Interface.SpeakerPictures",
        "D47.Core.Journal.CargoManifestReader",
        "D47.Core.Journal.CarrierBackfill",
        "D47.Core.Journal.CartographyLedger",
        "D47.Core.Journal.ColonisationBackfill",
        "D47.Core.Journal.CommanderBackfill",
        "D47.Core.Journal.CommodityLedger",
        "D47.Core.Journal.ExobiologyLedger",
        "D47.Core.Journal.FleetBackfill",
        "D47.Core.Journal.GameStatusReader",
        "D47.Core.Journal.HistoryBackfill",
        "D47.Core.Journal.JournalFolder",
        "D47.Core.Journal.JournalReader",
        "D47.Core.Journal.JournalSpine",
        "D47.Core.Journal.KitBackfill",
        "D47.Core.Journal.KitStore",
        "D47.Core.Journal.LoadoutBackfill",
        "D47.Core.Journal.LoadoutStore",
        "D47.Core.Journal.MailLedger",
        "D47.Core.Journal.MiningBackfill",
        "D47.Core.Journal.MissionBackfill",
        "D47.Core.Journal.ModulePowerReader",
        "D47.Core.Journal.NavRouteReader",
        "D47.Core.Journal.OutstandingCrimes",
        "D47.Core.Journal.PowerplayCycleBackfill",
        "D47.Core.Journal.SamplingStore",
        "D47.Core.Journal.SuitInventoryReader",
        "D47.Core.Journal.UnlockEvidenceBackfill",
        "D47.Core.Journal.UnsoldDataFile",
        "D47.Core.Journal.VisitedStarsBook",
        "D47.Core.Knowledge.MarketBook",
        "D47.Core.Knowledge.MarketReader",
        "D47.Core.Listening.HeardNamesStore",
        "D47.Core.Listening.SpokenNameMiner",
        "D47.Core.Logbook.LogDigestBuilder",
        "D47.Core.Logbook.LogFolder",
        "D47.Core.Logbook.LogRanges",
        "D47.Core.Lore.LoreStore",
        "D47.Core.Lore.LoreVisits",
        "D47.Core.Memory.MemoryStore",
        "D47.Core.Messages.MessageClips",
        "D47.Core.Messages.MessageStore",
        "D47.Core.Mining.MiningTargetStore",
        "D47.Core.Persona.OwnPersonaStore",
        "D47.Core.Persona.ShipCoreStore",
        "D47.Core.Reminders.JournalReminderStore",
        "D47.Core.Speech.ChatterboxAssets",
        "D47.Core.Speech.KokoroAssets",
        "D47.Core.Speech.PronunciationOverrides",
        "D47.Core.Storage.FileStamp",
        "D47.Core.Stories.StoryCatalog",
        "D47.Core.Stories.StoryChapterArchive",
        "D47.Core.Stories.StoryStore",
        "D47.Core.Utilities.AlarmStore",
    ];

    private static readonly Lazy<IReadOnlyDictionary<string, List<string>>> Surveyed = new(Survey);

    [Fact]
    public void NoUnlistedTypeNamesTheDisk()
    {
        var listed = Disk.Concat(NotYetConverted).ToHashSet(StringComparer.Ordinal);
        var unlisted = Surveyed.Value.Where(pair => !listed.Contains(pair.Key)).OrderBy(pair => pair.Key, StringComparer.Ordinal).ToList();

        Assert.True(
            unlisted.Count == 0,
            $"""
             These Core types name the disk directly. Read and write through IFileSystem instead.

             {string.Join(Environment.NewLine, unlisted.Select(pair => $"\"{pair.Key}\", // {string.Join(", ", pair.Value.Order(StringComparer.Ordinal))}"))}
             """);
    }

    [Fact]
    public void EveryListedTypeStillNamesTheDisk()
    {
        var stale = Disk.Concat(NotYetConverted).Where(type => !Surveyed.Value.ContainsKey(type)).ToList();

        Assert.True(
            stale.Count == 0,
            $"""
             These listed types no longer name the disk. Remove them from the list.

             {string.Join(Environment.NewLine, stale)}
             """);
    }

    /// <summary>Each Core type that names a banned type, keyed by its outermost type's full name, with the names it uses.</summary>
    private static Dictionary<string, List<string>> Survey()
    {
        var source = Path.Combine(RepositoryRoot(), "src", "D47.Core");
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        var trees = new List<SyntaxTree>();

        foreach (var path in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            var parts = Path.GetRelativePath(source, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (parts.Any(part => part is "obj" or "bin"))
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
            "d47-core-source",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var names = Banned.Select(name => name[(name.LastIndexOf('.') + 1)..]).ToHashSet(StringComparer.Ordinal);
        var found = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);

            foreach (var name in tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (!names.Contains(name.Identifier.ValueText)
                    || name.Ancestors().OfType<BaseTypeDeclarationSyntax>().LastOrDefault() is not { } outermost)
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

                if (type?.ToDisplayString() is not { } banned || !Banned.Contains(banned))
                {
                    continue;
                }

                var owner = model.GetDeclaredSymbol(outermost)!.ToDisplayString();

                if (!found.TryGetValue(owner, out var uses))
                {
                    found[owner] = uses = [];
                }

                if (!uses.Contains(name.Identifier.ValueText))
                {
                    uses.Add(name.Identifier.ValueText);
                }
            }
        }

        return found;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No d47.slnx above the test binary.");
    }
}
