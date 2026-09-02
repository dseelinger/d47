using System.Text;
using System.Text.RegularExpressions;
using D47.Core.Journal;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// The gate that binds <see cref="HandledEvents"/> to the code
/// (<a href="https://github.com/dseelinger/d47/issues/270">#270</a>). It lives as a test so CI
/// needs no separate step that could drift from it, like the three gates before it.
/// <para>
/// <b>It reads the source with the compiler, not with grep</b>, because grep is what the issue
/// caught under-reporting: dispatch on an event's name is written three ways across dozens of
/// files, and a pattern that matches two of them reports a gap that is not one. So this compiles
/// every <c>.cs</c> under <c>src/</c>, asks the semantic model for every read of
/// <see cref="JournalEvent.Kind"/> — the one question grep cannot answer, since a dozen other
/// types have a <c>Kind</c> — and follows each read to the names it is compared against.
/// </para>
/// <para>
/// <b>The shapes it follows</b>: <c>==</c> and <c>!=</c>; <c>is</c> with any combination of
/// <c>or</c>, <c>and</c>, <c>not</c> and parentheses; a <c>switch</c> statement's labels and a
/// <c>switch</c> expression's arms; a property pattern <c>{ Kind: … }</c>; a tuple the kind sits
/// in, matched positionally; membership in a collection — <c>Contains</c>, <c>ContainsKey</c>,
/// <c>TryGetValue</c>, an indexer — whose initializer lists the names; a local the kind is copied
/// into; an argument to one of d47's own methods, followed into the parameter; and <b>a copy
/// stored in a record or a field</b>, after which every read of that property is a read of the
/// kind. That last one is what finds the Journal File reading's noise list: the kind goes into a
/// <c>JournalEntry</c> and is tested against the set from there.
/// </para>
/// <para>
/// <b>A comparison the gate cannot resolve fails it outright</b> rather than being left out —
/// the kind compared with a parameter, a method's result, or a prefix — because the list is only
/// worth having if the code cannot drift from it without a red build. A comparison against
/// runtime text — the search box, another event's kind — is not a dispatch and is passed through,
/// and so is a kind read as text: logged, interpolated, spaced into words.
/// </para>
/// <para>
/// <b>Two blocks, by where the read is.</b> A name compared only inside the Journal File reading's
/// own files is one that reading knows — it has a sentence for it, or hides it as noise — and
/// nothing else reacts to. That is the answer to "why did it say nothing when X happened" far
/// more often than a missing event is, so the list keeps it apart.
/// </para>
/// </summary>
public sealed class HandledEventsGateTests
{
    private const string ListPath = "src/D47.Core/Journal/HandledEvents.cs";

    /// <summary>
    /// The Journal File reading: the sentence per event and the log of entries it draws from. A
    /// kind compared only here is narrated, not acted on.
    /// </summary>
    private static readonly string[] Narrator =
    [
        "src/D47.Core/Journal/JournalSentence.cs",
        "src/D47.Core/Journal/JournalLog.cs",
    ];

    private static readonly Lazy<Survey> Surveyed = new(Survey.Run);

    [Fact]
    public void TheListNamesExactlyTheEventsTheCodeDispatchesOn()
    {
        var sites = Surveyed.Value.Constants
            .GroupBy(sighting => sighting.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(sighting => sighting.Where).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        var actedOn = sites
            .Where(pair => pair.Value.Any(where => !IsNarrator(where)))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);

        var narratedOnly = sites.Keys.Except(actedOn, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

        var report = new StringBuilder();

        Compare(
            "ActedOn", HandledEvents.ActedOn, actedOn, sites, report,
            "something outside the Journal File reading compares the kind with each");

        Compare(
            "NarratedOnly", HandledEvents.NarratedOnly, narratedOnly, sites, report,
            "only the Journal File reading compares the kind with each");

        Assert.True(report.Length == 0, report.ToString());
    }

    [Fact]
    public void EveryComparisonOfAnEventsKindIsOneTheGateCanResolve()
    {
        var opaque = Surveyed.Value.Opaque;

        Assert.True(
            opaque.Count == 0,
            $"""
             These reads of a journal event's kind compare it with something this gate cannot
             resolve to event names, so HandledEvents could drift from them without any test
             noticing. Compare the kind with string constants — ==, is, switch — or test membership
             in a collection whose initializer lists them; those are the shapes the gate follows.

             {string.Join(Environment.NewLine, opaque)}
             """);
    }

    [Fact]
    public void TheBlocksAreAlphabeticalDisjointAndComplete()
    {
        var blocks = Blocks(File.ReadAllLines(Path.Combine(RepositoryRoot(), ListPath)));

        foreach (var (block, names) in blocks)
        {
            Assert.True(
                names.SequenceEqual(names.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal),
                $"HandledEvents.{block} is not in alphabetical order, or names an event twice.");
        }

        Assert.Empty(blocks["ActedOn"].Intersect(blocks["NarratedOnly"], StringComparer.Ordinal));
        Assert.Equal(HandledEvents.ActedOn.Count, blocks["ActedOn"].Count);
        Assert.Equal(HandledEvents.NarratedOnly.Count, blocks["NarratedOnly"].Count);
    }

    private static bool IsNarrator(string where) =>
        Narrator.Any(file => where.StartsWith(file + ":", StringComparison.Ordinal));

    private static void Compare(
        string block,
        IReadOnlySet<string> listed,
        IReadOnlySet<string> found,
        Dictionary<string, List<string>> sites,
        StringBuilder report,
        string meaning)
    {
        var missing = found.Except(listed, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var stale = listed.Except(found, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        if (missing.Count > 0)
        {
            report.AppendLine(
                $"Add these to HandledEvents.{block} in {ListPath}, in alphabetical order — {meaning}:");

            foreach (var name in missing)
            {
                report.AppendLine($"        \"{name}\",    // {string.Join(", ", sites[name])}");
            }

            report.AppendLine();
        }

        if (stale.Count > 0)
        {
            report.AppendLine(
                $"Remove these from HandledEvents.{block}; nothing that would put them there compares the kind with them any more:");

            foreach (var name in stale)
            {
                report.AppendLine($"        \"{name}\"");
            }

            report.AppendLine();
        }
    }

    /// <summary>The names in each block of the list's source, in the order written.</summary>
    private static Dictionary<string, List<string>> Blocks(string[] lines)
    {
        var blocks = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        List<string>? current = null;

        foreach (var line in lines)
        {
            var opening = Regex.Match(line, @"^\s*public static readonly FrozenSet<string> (\w+) = FrozenSet\.ToFrozenSet");

            if (opening.Success)
            {
                current = [];
                blocks[opening.Groups[1].Value] = current;
                continue;
            }

            if (current is null)
            {
                continue;
            }

            var name = Regex.Match(line, "^\\s*\"([A-Za-z0-9_]+)\",?\\s*$");

            if (name.Success)
            {
                current.Add(name.Groups[1].Value);
            }
            else if (line.TrimStart().StartsWith(']'))
            {
                current = null;
            }
        }

        return blocks;
    }

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

    /// <summary>One event name the code compares a kind against, and where.</summary>
    private sealed record Sighting(string Name, string Where);

    /// <summary>What one expression the kind is compared with turned out to be.</summary>
    private enum Comparand
    {
        /// <summary>A string constant, or a readonly field or local initialised to one.</summary>
        Constant,

        /// <summary>A null: asks whether there is a kind, not which.</summary>
        Nothing,

        /// <summary>Text decided at runtime — a search box, another event's kind. Not a dispatch.</summary>
        Runtime,

        /// <summary>A parameter, a method's result, or anything else the gate cannot see through.</summary>
        Unknown,
    }

    /// <summary>
    /// The compile and the walk. One per test run, shared by the facts above through the
    /// <see cref="Lazy{T}"/>, because compiling a hundred thousand lines is the expensive half.
    /// </summary>
    private sealed class Survey
    {
        private const string ImplicitUsings =
            "global using System; global using System.Collections.Generic; global using System.IO; " +
            "global using System.Linq; global using System.Net.Http; global using System.Threading; " +
            "global using System.Threading.Tasks;";

        private const int MaxDepth = 4;

        private readonly string _root;

        /// <summary>
        /// Every property, field or method whose value is a journal event's kind: the original,
        /// and every copy the walk finds it stored into. Grows until a pass adds nothing.
        /// </summary>
        private readonly HashSet<ISymbol> _tracked = new(SymbolEqualityComparer.Default);

        private readonly List<ISymbol> _queue = [];

        private CSharpCompilation _compilation = null!;

        private Survey(string root)
        {
            _root = root;
        }

        public List<Sighting> Constants { get; } = [];

        public List<string> Opaque { get; } = [];

        public List<string> PassedThrough { get; } = [];

        public static Survey Run()
        {
            var survey = new Survey(RepositoryRoot());
            var parse = new CSharpParseOptions(LanguageVersion.Preview);
            var source = Path.Combine(survey._root, "src");
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

            // Everything the test process itself loaded, minus d47's own assemblies — those are
            // the source being compiled, and a type defined both in source and in a reference
            // binds to the source with a warning, which is noise rather than a problem. The App's
            // own references — Avalonia and the rest — are absent, so App code that touches them
            // binds to error types; a `.Kind` on one of those is caught below rather than skipped.
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Where(path => !Path.GetFileName(path).StartsWith("D47.", StringComparison.Ordinal))
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .ToList();

            survey._compilation = CSharpCompilation.Create(
                "d47-source",
                trees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var origin = survey._compilation.GetTypeByMetadataName("D47.Core.Journal.JournalEvent")
                ?.GetMembers("Kind").OfType<IPropertySymbol>().FirstOrDefault()
                ?? throw new InvalidOperationException("D47.Core.Journal.JournalEvent.Kind did not compile.");

            survey._tracked.Add(origin);
            survey._queue.Add(origin);

            // Each pass walks the whole source for the symbols found since the last one. The first
            // pass is JournalEvent.Kind itself; a second finds the reads of whatever it was stored
            // into; and so on until a pass stores it nowhere new.
            while (survey._queue.Count > 0)
            {
                var batch = new HashSet<ISymbol>(survey._queue, SymbolEqualityComparer.Default);
                var names = batch.Select(symbol => symbol.Name).ToHashSet(StringComparer.Ordinal);
                survey._queue.Clear();

                foreach (var tree in trees)
                {
                    survey.Walk(tree, batch, names);
                }
            }

            return survey;
        }

        private void Walk(SyntaxTree tree, HashSet<ISymbol> batch, HashSet<string> names)
        {
            var model = _compilation.GetSemanticModel(tree);

            foreach (var name in tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (!names.Contains(name.Identifier.ValueText))
                {
                    continue;
                }

                var info = model.GetSymbolInfo(name);
                var symbol = (info.Symbol ?? info.CandidateSymbols.FirstOrDefault())?.OriginalDefinition;

                if (symbol is null)
                {
                    // A `.Kind` the compiler could not place, compared against text: it might be
                    // an event's, and a gate that guessed "probably not" would be grep again.
                    if (name.Identifier.ValueText == "Kind"
                        && name.Parent is MemberAccessExpressionSyntax access
                        && access.Name == name
                        && ComparesWithText(access))
                    {
                        Note(Opaque, name, "the gate could not resolve what this `.Kind` belongs to");
                    }

                    continue;
                }

                if (batch.Contains(symbol))
                {
                    Start(name, symbol, model);
                }
            }
        }

        private static bool ComparesWithText(ExpressionSyntax expression) => expression.Parent switch
        {
            BinaryExpressionSyntax binary => binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression),
            IsPatternExpressionSyntax test => test.Expression == expression,
            SwitchStatementSyntax statement => statement.Expression == expression,
            SwitchExpressionSyntax switched => switched.GoverningExpression == expression,
            _ => false,
        };

        /// <summary>One read of a tracked symbol, from the identifier the compiler bound.</summary>
        private void Start(IdentifierNameSyntax name, ISymbol symbol, SemanticModel model)
        {
            // A write — `with { Kind = … }`, an object initializer's left side — is where a copy
            // is made, and the right side is what gets followed. nameof() reads the property's
            // name rather than the event's.
            if (name.Parent is AssignmentExpressionSyntax assignment && assignment.Left == name)
            {
                return;
            }

            if (name.Ancestors().OfType<InvocationExpressionSyntax>()
                .Any(call => call.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" }))
            {
                return;
            }

            if (symbol is IMethodSymbol)
            {
                var call = name.Ancestors().OfType<InvocationExpressionSyntax>()
                    .FirstOrDefault(candidate => candidate.Expression == name || candidate.Expression == name.Parent);

                if (call is not null)
                {
                    Classify(call, model, depth: 0);
                }

                return;
            }

            switch (name.Parent)
            {
                case MemberAccessExpressionSyntax access when access.Name == name:
                    Classify(access, model, depth: 0);
                    break;

                case MemberBindingExpressionSyntax binding:
                    SyntaxNode top = binding;

                    while (top.Parent is ConditionalAccessExpressionSyntax conditional && conditional.WhenNotNull == top)
                    {
                        top = conditional;
                    }

                    Classify((ExpressionSyntax)top, model, depth: 0);
                    break;

                case NameColonSyntax { Parent: SubpatternSyntax sub }:
                    Pattern(sub.Pattern, model, Where(name));
                    break;

                default:
                    Classify(name, model, depth: 0);
                    break;
            }
        }

        /// <summary>What the code does with one expression whose value is the kind.</summary>
        private void Classify(ExpressionSyntax kind, SemanticModel model, int depth)
        {
            if (depth > MaxDepth)
            {
                Note(Opaque, kind, "followed through too many hops");
                return;
            }

            switch (kind.Parent)
            {
                case ParenthesizedExpressionSyntax parenthesised:
                    Classify(parenthesised, model, depth);
                    return;

                case BinaryExpressionSyntax binary
                    when binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression):
                    Equality(binary.Left == kind ? binary.Right : binary.Left, model, kind);
                    return;

                case IsPatternExpressionSyntax test when test.Expression == kind:
                    Pattern(test.Pattern, model, Where(kind));
                    return;

                case SwitchStatementSyntax statement when statement.Expression == kind:
                    foreach (var label in statement.Sections.SelectMany(section => section.Labels))
                    {
                        switch (label)
                        {
                            case CaseSwitchLabelSyntax plain:
                                Equality(plain.Value, model, kind);
                                break;

                            case CasePatternSwitchLabelSyntax patterned:
                                Pattern(patterned.Pattern, model, Where(kind));
                                break;
                        }
                    }

                    return;

                case SwitchExpressionSyntax expression when expression.GoverningExpression == kind:
                    foreach (var arm in expression.Arms)
                    {
                        Pattern(arm.Pattern, model, Where(kind));
                    }

                    return;

                case ArgumentSyntax argument:
                    Argument(argument, kind, model, depth);
                    return;

                case EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator }:
                    if (declarator.Parent?.Parent is FieldDeclarationSyntax)
                    {
                        Track(model.GetDeclaredSymbol(declarator), kind);
                    }
                    else
                    {
                        Local(declarator, model, depth);
                    }

                    return;

                case EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property }:
                    Track(model.GetDeclaredSymbol(property), kind);
                    return;

                case AssignmentExpressionSyntax assignment when assignment.Right == kind && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression):
                    Track(model.GetSymbolInfo(assignment.Left).Symbol, kind);
                    return;

                case ReturnStatementSyntax or ArrowExpressionClauseSyntax:
                    Returned(kind, model);
                    return;

                case ExpressionColonSyntax { Parent: SubpatternSyntax sub }:
                    Pattern(sub.Pattern, model, Where(kind));
                    return;

                case MemberAccessExpressionSyntax access when access.Expression == kind:
                    OnTheKind(access, model, kind);
                    return;

                case ElementAccessExpressionSyntax element when element.Expression == kind:
                    Note(PassedThrough, kind, "indexed as text");
                    return;

                default:
                    Note(PassedThrough, kind, kind.Parent?.Kind().ToString() ?? "no parent");
                    return;
            }
        }

        /// <summary>The kind is on the left of a dot: a method or property of the string itself.</summary>
        private void OnTheKind(MemberAccessExpressionSyntax access, SemanticModel model, ExpressionSyntax kind)
        {
            var member = access.Name.Identifier.ValueText;

            if (access.Parent is not InvocationExpressionSyntax call)
            {
                Note(PassedThrough, kind, $"{member} read as text");
                return;
            }

            var argument = call.ArgumentList.Arguments.FirstOrDefault()?.Expression;

            if (member == "Equals")
            {
                if (argument is null)
                {
                    Note(Opaque, kind, "Equals with nothing to compare against");
                    return;
                }

                Equality(argument, model, kind);
                return;
            }

            // A prefix or substring test against a constant is a dispatch on the kind's shape,
            // which no list of names can be checked against. Against runtime text it is a search.
            if (member is "Contains" or "StartsWith" or "EndsWith" or "IndexOf" or "LastIndexOf")
            {
                switch (argument is null ? Comparand.Unknown : Compared(argument, model).Kind)
                {
                    case Comparand.Runtime:
                        Note(PassedThrough, kind, $"{member} against runtime text");
                        return;

                    default:
                        Note(Opaque, kind, $"`{call}` tests the kind's shape rather than its name; compare the names instead");
                        return;
                }
            }

            Note(PassedThrough, kind, $"{member}() read as text");
        }

        /// <summary>The kind is compared for equality with one expression.</summary>
        private void Equality(ExpressionSyntax other, SemanticModel model, ExpressionSyntax kind)
        {
            var (comparand, text) = Compared(other, model);

            switch (comparand)
            {
                case Comparand.Constant:
                    Constants.Add(new Sighting(text!, Where(kind)));
                    return;

                case Comparand.Nothing:
                    return;

                case Comparand.Runtime:
                    Note(PassedThrough, kind, $"compared with runtime text `{other}`");
                    return;

                default:
                    Note(Opaque, kind, $"compared with `{other}`, which the gate cannot resolve to a constant");
                    return;
            }
        }

        /// <summary>What one expression the kind is compared with amounts to.</summary>
        private (Comparand Kind, string? Text) Compared(ExpressionSyntax other, SemanticModel model)
        {
            var value = model.GetConstantValue(other);

            if (value.HasValue)
            {
                return value.Value switch
                {
                    string text => (Comparand.Constant, text),
                    null => (Comparand.Nothing, null),
                    _ => (Comparand.Unknown, null),
                };
            }

            var symbol = model.GetSymbolInfo(other).Symbol?.OriginalDefinition;

            if (symbol is not null && _tracked.Contains(symbol))
            {
                return (Comparand.Runtime, null);
            }

            switch (symbol)
            {
                case IFieldSymbol { IsReadOnly: false }:
                    return (Comparand.Runtime, null);

                case IFieldSymbol field:
                    return Initialised(field);

                case IPropertySymbol { SetMethod: not null }:
                    return (Comparand.Runtime, null);

                case IPropertySymbol property:
                    return Initialised(property);

                case ILocalSymbol local:
                    return Initialised(local, runtimeWhenBare: true);

                default:
                    return (Comparand.Unknown, null);
            }
        }

        /// <summary>
        /// A field, property or local stands for its initializer: a constant if that is one, and
        /// runtime text if a local was declared with none — `out var`, a pattern variable.
        /// </summary>
        private (Comparand Kind, string? Text) Initialised(ISymbol symbol, bool runtimeWhenBare = false)
        {
            var declaration = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();

            var initializer = declaration switch
            {
                VariableDeclaratorSyntax variable => variable.Initializer?.Value,
                PropertyDeclarationSyntax property => property.Initializer?.Value ?? property.ExpressionBody?.Expression,
                _ => null,
            };

            if (initializer is null)
            {
                return (runtimeWhenBare ? Comparand.Runtime : Comparand.Unknown, null);
            }

            var value = _compilation.GetSemanticModel(initializer.SyntaxTree).GetConstantValue(initializer);

            return value.HasValue && value.Value is string text
                ? (Comparand.Constant, text)
                : (Comparand.Unknown, null);
        }

        private void Pattern(PatternSyntax pattern, SemanticModel model, string where)
        {
            switch (pattern)
            {
                case ConstantPatternSyntax constant:
                    var value = model.GetConstantValue(constant.Expression);

                    if (value.HasValue && value.Value is string text)
                    {
                        Constants.Add(new Sighting(text, where));
                    }
                    else if (!(value.HasValue && value.Value is null))
                    {
                        Opaque.Add($"{where}: the pattern `{constant}` is not a string constant");
                    }

                    break;

                case BinaryPatternSyntax binary:
                    Pattern(binary.Left, model, where);
                    Pattern(binary.Right, model, where);
                    break;

                case UnaryPatternSyntax unary:
                    Pattern(unary.Pattern, model, where);
                    break;

                case ParenthesizedPatternSyntax parenthesised:
                    Pattern(parenthesised.Pattern, model, where);
                    break;

                // Matches every name, so it names none: the `_ =>` arm, `var other`, `string s`.
                case DiscardPatternSyntax or VarPatternSyntax or DeclarationPatternSyntax or TypePatternSyntax:
                    break;

                default:
                    Opaque.Add($"{where}: the pattern `{pattern}` on the kind is not a name");
                    break;
            }
        }

        private void Argument(ArgumentSyntax argument, ExpressionSyntax kind, SemanticModel model, int depth)
        {
            switch (argument.Parent)
            {
                case TupleExpressionSyntax tuple:
                    Tuple(tuple, tuple.Arguments.IndexOf(argument), model, kind);
                    return;

                case BracketedArgumentListSyntax { Parent: ElementAccessExpressionSyntax element }:
                    Membership(element.Expression, model, kind);
                    return;

                case ArgumentListSyntax { Parent: InvocationExpressionSyntax call }:
                    Invocation(call, argument, kind, model, depth);
                    return;

                case ArgumentListSyntax { Parent: BaseObjectCreationExpressionSyntax creation }:
                    Stored(creation, argument, kind, model, depth);
                    return;

                default:
                    Note(PassedThrough, kind, $"an argument in a {argument.Parent?.Parent?.Kind()}");
                    return;
            }
        }

        private void Invocation(
            InvocationExpressionSyntax call, ArgumentSyntax argument, ExpressionSyntax kind, SemanticModel model, int depth)
        {
            var info = model.GetSymbolInfo(call);
            var method = info.Symbol as IMethodSymbol ?? info.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

            var methodName = method?.Name ?? call.Expression switch
            {
                MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                _ => call.Expression.ToString(),
            };

            // string.Equals(kind, "Docked", StringComparison.Ordinal), in either order.
            if (methodName == "Equals" && method?.ContainingType.SpecialType == SpecialType.System_String)
            {
                var other = call.ArgumentList.Arguments.FirstOrDefault(candidate =>
                    candidate != argument && model.GetTypeInfo(candidate.Expression).Type?.SpecialType == SpecialType.System_String);

                if (other is null)
                {
                    Note(Opaque, kind, "string.Equals with no other string to compare against");
                    return;
                }

                Equality(other.Expression, model, kind);
                return;
            }

            // Membership: the names are in the collection's initializer.
            if (call.Expression is MemberAccessExpressionSyntax member
                && methodName is "Contains" or "ContainsKey" or "TryGetValue" or "GetValueOrDefault" or "Remove")
            {
                Membership(member.Expression, model, kind);
                return;
            }

            // One of d47's own methods: follow the argument into the parameter.
            if (method is not null && Declaration(method) is { } declaration)
            {
                var parameter = ParameterFor(method, call.ArgumentList, argument);

                if (parameter is null)
                {
                    Note(Opaque, kind, $"passed to {methodName} at a position the gate could not map to a parameter");
                    return;
                }

                Follow(parameter, declaration, depth);
                return;
            }

            Note(PassedThrough, kind, $"passed to {methodName}");
        }

        /// <summary>
        /// The kind is a constructor argument. A positional record makes it a property, which is
        /// tracked from here on; a class with a body gets its constructor followed instead.
        /// </summary>
        private void Stored(
            BaseObjectCreationExpressionSyntax creation, ArgumentSyntax argument, ExpressionSyntax kind, SemanticModel model, int depth)
        {
            var info = model.GetSymbolInfo(creation);
            var constructor = info.Symbol as IMethodSymbol ?? info.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

            if (constructor is null || creation.ArgumentList is null)
            {
                Note(Opaque, kind, $"stored by `{creation}`, whose constructor the gate could not resolve");
                return;
            }

            var parameter = ParameterFor(constructor, creation.ArgumentList, argument);

            if (parameter is null)
            {
                Note(Opaque, kind, $"stored by `{creation}` at a position the gate could not map to a parameter");
                return;
            }

            var property = constructor.ContainingType.GetMembers(parameter.Name).OfType<IPropertySymbol>().FirstOrDefault();

            if (property is not null)
            {
                Track(property, kind);
                return;
            }

            if (Declaration(constructor) is { } declaration)
            {
                Follow(parameter, declaration, depth);
                return;
            }

            Note(PassedThrough, kind, $"stored by {constructor.ContainingType.Name}, outside the source");
        }

        /// <summary>The kind is what a member returns: every call of that member is a read of it.</summary>
        private void Returned(ExpressionSyntax kind, SemanticModel model)
        {
            var scope = kind.Ancestors().FirstOrDefault(node =>
                node is MemberDeclarationSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax);

            if (scope is MemberDeclarationSyntax member)
            {
                Track(model.GetDeclaredSymbol(member), kind);
                return;
            }

            Note(PassedThrough, kind, "returned from a lambda or local function");
        }

        /// <summary>
        /// A place the kind is copied to. From here on a read of it is a read of the kind, which is
        /// what the outer loop in <see cref="Run"/> goes back for.
        /// </summary>
        private void Track(ISymbol? destination, ExpressionSyntax kind)
        {
            if (destination is not (IPropertySymbol or IFieldSymbol or IMethodSymbol))
            {
                Note(Opaque, kind, $"stored somewhere the gate could not resolve: `{kind.Parent}`");
                return;
            }

            var canonical = destination.OriginalDefinition;

            if (canonical.DeclaringSyntaxReferences.Length == 0)
            {
                Note(PassedThrough, kind, $"stored in {canonical.ToDisplayString()}, outside the source");
                return;
            }

            if (_tracked.Add(canonical))
            {
                _queue.Add(canonical);
            }

            Note(PassedThrough, kind, $"stored in {canonical.ToDisplayString()}, which is followed");
        }

        private static SyntaxNode? Declaration(IMethodSymbol method) =>
            (method.ReducedFrom ?? method.OriginalDefinition).DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();

        private static IParameterSymbol? ParameterFor(IMethodSymbol method, BaseArgumentListSyntax arguments, ArgumentSyntax argument)
        {
            if (argument.NameColon is { } named)
            {
                return method.Parameters.FirstOrDefault(p => p.Name == named.Name.Identifier.ValueText);
            }

            var index = arguments.Arguments.IndexOf(argument);

            return index < method.Parameters.Length
                ? method.Parameters[index]
                : method.Parameters.LastOrDefault(p => p.IsParams);
        }

        /// <summary>Every use of a parameter inside its method, read as a use of the kind.</summary>
        private void Follow(IParameterSymbol parameter, SyntaxNode declaration, int depth)
        {
            var model = _compilation.GetSemanticModel(declaration.SyntaxTree);

            var declared = declaration.DescendantNodes()
                .OfType<ParameterSyntax>()
                .FirstOrDefault(p => p.Identifier.ValueText == parameter.Name);

            if (declared is null)
            {
                Opaque.Add($"{Where(declaration)}: the parameter {parameter.Name} has no declaration the gate can find");
                return;
            }

            var canonical = model.GetDeclaredSymbol(declared);

            foreach (var use in declaration.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (use.Identifier.ValueText == parameter.Name
                    && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(use).Symbol, canonical))
                {
                    Classify(use, model, depth + 1);
                }
            }
        }

        /// <summary>Every use of a local the kind was copied into, read as a use of the kind.</summary>
        private void Local(VariableDeclaratorSyntax declarator, SemanticModel model, int depth)
        {
            var local = model.GetDeclaredSymbol(declarator);

            var scope = declarator.Ancestors().FirstOrDefault(node =>
                            node is MemberDeclarationSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
                        ?? declarator.SyntaxTree.GetRoot();

            foreach (var use in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (use.Identifier.ValueText == declarator.Identifier.ValueText
                    && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(use).Symbol, local))
                {
                    Classify(use, model, depth + 1);
                }
            }
        }

        /// <summary>
        /// The kind is looked up in a collection: the names are whatever its initializer lists. A
        /// collection that starts empty is a tally being kept, not a dispatch.
        /// </summary>
        private void Membership(ExpressionSyntax collection, SemanticModel model, ExpressionSyntax kind)
        {
            var symbol = model.GetSymbolInfo(collection).Symbol;
            var declaration = symbol?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();

            if (declaration is null)
            {
                Note(Opaque, kind, $"membership in `{collection}`, whose declaration the gate cannot see");
                return;
            }

            var initializer = declaration switch
            {
                VariableDeclaratorSyntax variable => variable.Initializer?.Value,
                PropertyDeclarationSyntax property => property.Initializer?.Value ?? property.ExpressionBody?.Expression,
                _ => null,
            };

            if (initializer is null)
            {
                Note(Opaque, kind, $"membership in `{collection}`, which has no initializer the gate can read");
                return;
            }

            var declarationModel = _compilation.GetSemanticModel(declaration.SyntaxTree);
            var keys = Keys(initializer).ToList();

            if (keys.Count == 0)
            {
                if (initializer is BaseObjectCreationExpressionSyntax or CollectionExpressionSyntax)
                {
                    Note(PassedThrough, kind, $"keyed into `{collection}`, which starts empty");
                    return;
                }

                Note(Opaque, kind, $"membership in `{collection}`, whose initializer `{initializer}` lists no names the gate can read");
                return;
            }

            foreach (var key in keys)
            {
                var value = declarationModel.GetConstantValue(key);

                if (value.HasValue && value.Value is string text)
                {
                    Constants.Add(new Sighting(text, Where(kind)));
                }
                else
                {
                    Note(Opaque, kind, $"membership in `{collection}`, whose element `{key}` is not a string constant");
                }
            }
        }

        private static IEnumerable<ExpressionSyntax> Keys(ExpressionSyntax initializer) => initializer switch
        {
            CollectionExpressionSyntax collection => collection.Elements.Select(element => element switch
            {
                ExpressionElementSyntax expression => expression.Expression,
                SpreadElementSyntax spread => spread.Expression,
                _ => (ExpressionSyntax)collection,
            }),
            BaseObjectCreationExpressionSyntax { Initializer: { } initial } => initial.Expressions.Select(KeyOf),
            ArrayCreationExpressionSyntax { Initializer: { } initial } => initial.Expressions,
            ImplicitArrayCreationExpressionSyntax { Initializer: var initial } => initial.Expressions,
            InvocationExpressionSyntax call => call.DescendantNodes()
                .FirstOrDefault(node => node is CollectionExpressionSyntax or ArrayCreationExpressionSyntax
                    or ImplicitArrayCreationExpressionSyntax or BaseObjectCreationExpressionSyntax { Initializer: not null })
                is ExpressionSyntax inner ? Keys(inner) : [],
            _ => [],
        };

        /// <summary>The key of one initializer element: <c>["Docked"] = …</c>, <c>{ "Docked", … }</c>, or the element itself.</summary>
        private static ExpressionSyntax KeyOf(ExpressionSyntax element) => element switch
        {
            AssignmentExpressionSyntax { Left: ImplicitElementAccessSyntax access } => access.ArgumentList.Arguments[0].Expression,
            InitializerExpressionSyntax pair when pair.Expressions.Count > 0 => pair.Expressions[0],
            _ => element,
        };

        private void Tuple(TupleExpressionSyntax tuple, int index, SemanticModel model, ExpressionSyntax kind)
        {
            switch (tuple.Parent)
            {
                case SwitchExpressionSyntax expression when expression.GoverningExpression == tuple:
                    foreach (var arm in expression.Arms)
                    {
                        Positional(arm.Pattern, index, model, kind);
                    }

                    return;

                case IsPatternExpressionSyntax test when test.Expression == tuple:
                    Positional(test.Pattern, index, model, kind);
                    return;

                default:
                    Note(PassedThrough, kind, "an element of a tuple that is not matched");
                    return;
            }
        }

        private void Positional(PatternSyntax pattern, int index, SemanticModel model, ExpressionSyntax kind)
        {
            switch (pattern)
            {
                case RecursivePatternSyntax { PositionalPatternClause: { } positional } when index < positional.Subpatterns.Count:
                    Pattern(positional.Subpatterns[index].Pattern, model, Where(kind));
                    break;

                case BinaryPatternSyntax binary:
                    Positional(binary.Left, index, model, kind);
                    Positional(binary.Right, index, model, kind);
                    break;

                case UnaryPatternSyntax unary:
                    Positional(unary.Pattern, index, model, kind);
                    break;

                case ParenthesizedPatternSyntax parenthesised:
                    Positional(parenthesised.Pattern, index, model, kind);
                    break;

                case DiscardPatternSyntax or VarPatternSyntax or DeclarationPatternSyntax:
                    break;

                default:
                    Note(Opaque, kind, $"a tuple pattern `{pattern}` the gate cannot take apart");
                    break;
            }
        }

        private void Note(List<string> into, SyntaxNode at, string what) => into.Add($"{Where(at)}: {what}");

        private string Where(SyntaxNode node)
        {
            var span = node.GetLocation().GetLineSpan();
            var path = Path.GetRelativePath(_root, node.SyntaxTree.FilePath).Replace('\\', '/');
            return $"{path}:{span.StartLinePosition.Line + 1}";
        }
    }
}
