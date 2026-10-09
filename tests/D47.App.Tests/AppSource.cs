using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace D47.App.Tests;

/// <summary>A parsed <c>.cs</c> file under <c>src/D47.App</c>.</summary>
public sealed record SourceFile(string Path, string Text, SyntaxTree Tree)
{
    public string Name => System.IO.Path.GetFileName(Path);
}

/// <summary>A code line with where it came from.</summary>
public sealed record CodeLine(string File, int Line, string Text)
{
    public override string ToString() => $"{File}:{Line} {Text}";
}

/// <summary>A method found by name, with its declaration, file and code lines.</summary>
public sealed record SourceMethod(SourceFile File, MethodDeclarationSyntax Node)
{
    public int Line => Node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    public string Text => Node.ToString();

    public IReadOnlyList<string> Parameters => [.. Node.ParameterList.Parameters.Select(parameter => parameter.ToString())];

    public IReadOnlyList<CodeLine> CodeLines()
    {
        var span = Node.GetLocation().GetLineSpan();
        var lines = File.Text.ReplaceLineEndings("\n").Split('\n');

        return [.. Enumerable.Range(span.StartLinePosition.Line, span.EndLinePosition.Line - span.StartLinePosition.Line + 1)
            .Select(index => new CodeLine(File.Name, index + 1, lines[index].Trim()))
            .Where(line => line.Text.Length > 0 && !line.Text.StartsWith("//", StringComparison.Ordinal))];
    }
}

/// <summary>
/// Every <c>.cs</c> file under <c>src/D47.App</c>, parsed once per test run, syntax only. A gate
/// that reads a member finds it by name; a gate that counts or forbids a fragment reads the tree.
/// </summary>
public static class AppSource
{
    private static readonly Lazy<IReadOnlyList<SourceFile>> Parsed = new(Parse);

    public static IReadOnlyList<SourceFile> Files => Parsed.Value;

    /// <summary>The one method called <paramref name="name"/>, or <c>Type.Name</c> to name its type.</summary>
    public static SourceMethod Method(string name)
    {
        var (type, member) = name.Contains('.', StringComparison.Ordinal)
            ? (name[..name.LastIndexOf('.')], name[(name.LastIndexOf('.') + 1)..])
            : (null, name);

        var found = Methods()
            .Where(method => method.Node.Identifier.ValueText == member)
            .Where(method => type is null || EnclosingTypeName(method.Node) == type)
            .ToList();

        Assert.True(
            found.Count == 1,
            found.Count == 0
                ? $"No method named {name} under src/D47.App."
                : $"{found.Count} methods named {name}; expected one:\n"
                  + string.Join(Environment.NewLine, found.Select(method => $"{method.File.Name}:{method.Line}")));

        return found[0];
    }

    public static IEnumerable<SourceMethod> Methods() =>
        Files.SelectMany(file => file.Tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Select(node => new SourceMethod(file, node)));

    /// <summary>Trimmed lines of the tree containing any fragment, with <c>//</c> lines dropped.</summary>
    public static List<CodeLine> CodeLines(params string[] fragments) =>
        [.. Files.SelectMany(file => CodeLinesOf(file.Name, file.Text, fragments))];

    /// <summary>Code lines matching <paramref name="pattern"/> that sit outside the type called <paramref name="type"/>.</summary>
    public static List<CodeLine> CodeLinesMatchingOutside(string type, System.Text.RegularExpressions.Regex pattern) =>
        [.. Files.SelectMany(file =>
        {
            var inside = file.Tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
                .Where(declaration => declaration.Identifier.ValueText == type)
                .Select(declaration => declaration.GetLocation().GetLineSpan())
                .ToList();

            return CodeLinesOf(file.Name, file.Text, [""])
                .Where(line => pattern.IsMatch(line.Text))
                .Where(line => !inside.Any(span => line.Line - 1 >= span.StartLinePosition.Line && line.Line - 1 <= span.EndLinePosition.Line));
        })];

    /// <summary>The same, for one file outside the tree.</summary>
    public static List<CodeLine> CodeLinesIn(string path, params string[] fragments) =>
        [.. CodeLinesOf(System.IO.Path.GetFileName(path), File.ReadAllText(path), fragments)];

    public static string RepositoryRoot()
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

    public static string EnclosingTypeName(SyntaxNode node) =>
        node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "";

    private static IEnumerable<CodeLine> CodeLinesOf(string file, string text, string[] fragments) =>
        text.ReplaceLineEndings("\n").Split('\n')
            .Select((line, index) => new CodeLine(file, index + 1, line.Trim()))
            .Where(line => !line.Text.StartsWith("//", StringComparison.Ordinal))
            .Where(line => fragments.Any(fragment => line.Text.Contains(fragment, StringComparison.Ordinal)));

    private static IReadOnlyList<SourceFile> Parse()
    {
        var root = Path.Combine(RepositoryRoot(), "src", "D47.App");
        var skipped = new[] { Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar };

        return [.. Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !skipped.Any(segment => file.Contains(segment, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .Select(file =>
            {
                var text = File.ReadAllText(file);
                return new SourceFile(file, text, CSharpSyntaxTree.ParseText(text, path: file));
            })];
    }
}
