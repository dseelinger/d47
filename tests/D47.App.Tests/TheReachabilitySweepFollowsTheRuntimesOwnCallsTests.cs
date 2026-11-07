using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Each rule <see cref="ReachabilitySweep"/> applies beyond the IL it reads, run over a small assembly compiled in
/// memory against the metadata of assemblies this process has already loaded.
/// </summary>
public sealed class TheReachabilitySweepFollowsTheRuntimesOwnCallsTests
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(Loaded);

    [Fact]
    public void ARecordComparedWithEqualsReadsItsProperties()
    {
        var unreached = Sweep(
            """
            public sealed record Compared(int A, string B);
            public sealed record Built(int C);
            public static class Program
            {
                public static void Main()
                {
                    System.Console.WriteLine(new Compared(1, "x") == new Compared(1, "x"));
                    System.Console.WriteLine(new Built(2));
                }
            }
            """);

        Assert.DoesNotContain("Compared.A", unreached);
        Assert.DoesNotContain("Compared.B", unreached);
        Assert.Contains("Built.C", unreached);
    }

    [Fact]
    public void ACallOfObjectEqualsReadsEveryLiveRecordsProperties()
    {
        var unreached = Sweep(
            """
            public sealed record Boxed(int A);
            public static class Program
            {
                public static void Main() => System.Console.WriteLine(Same(new Boxed(1), new Boxed(1)));

                private static bool Same(object was, object now) => Equals(was, now);
            }
            """);

        Assert.DoesNotContain("Boxed.A", unreached);
    }

    [Fact]
    public void ARecordKeyingAHashSetReadsItsProperties()
    {
        var unreached = Sweep(
            """
            public sealed record Key(int A);
            public sealed record Listed(int B);
            public static class Program
            {
                public static void Main()
                {
                    var keys = new System.Collections.Generic.HashSet<Key> { new(1) };
                    var listed = new System.Collections.Generic.List<Listed> { new(2) };
                    System.Console.WriteLine(keys.Count + listed.Count);
                }
            }
            """);

        Assert.DoesNotContain("Key.A", unreached);
        Assert.Contains("Listed.B", unreached);
    }

    [Fact]
    public void ATypeJsonSerializerReadsHasEveryPropertyLive()
    {
        var unreached = Sweep(
            """
            public sealed class Outer { public int A { get; set; } public Inner[] Items { get; set; } = []; }
            public sealed class Inner { public string Name { get; set; } = ""; }
            public sealed class ByType { public int Count { get; set; } }
            public sealed class Plain { public int P { get; set; } }
            public sealed class Wrapped { public int W { get; set; } }
            public sealed class Store<T> { public string Save(T value) => System.Text.Json.JsonSerializer.Serialize(new Box { Value = value }); public sealed class Box { public T? Value { get; set; } } }
            public static class Program
            {
                public static void Main()
                {
                    System.Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new Outer()));
                    System.Console.WriteLine(System.Text.Json.JsonSerializer.Deserialize("{}", typeof(ByType)));
                    System.Console.WriteLine(new Plain { P = 1 });
                    System.Console.WriteLine(new Store<Wrapped>().Save(new Wrapped()));
                }
            }
            """);

        Assert.DoesNotContain("Outer.A", unreached);
        Assert.DoesNotContain("Inner.Name", unreached);
        Assert.DoesNotContain("ByType.Count", unreached);
        Assert.DoesNotContain("ByType.ByType()", unreached);
        Assert.DoesNotContain("Wrapped.W", unreached);
        Assert.Contains("Plain.P", unreached);
    }

    [Fact]
    public void ATypeHandedToNativeCodeHasEveryMemberLive()
    {
        var unreached = Sweep(
            """
            using System.Runtime.InteropServices;
            [StructLayout(LayoutKind.Sequential)]
            public struct Point { public int X; public int Y; public int Sum => X + Y; }
            [StructLayout(LayoutKind.Sequential)]
            public struct Managed { public int X; public int Twice => X * 2; }
            public static class Program
            {
                [DllImport("user32.dll")]
                private static extern bool GetCursorPos(out Point point);

                public static void Main()
                {
                    GetCursorPos(out var point);
                    System.Console.WriteLine(point.X);
                    Show(new Managed { X = 1 });
                }

                private static void Show(Managed managed) => System.Console.WriteLine(managed.X);
            }
            """);

        Assert.DoesNotContain("Point.Sum", unreached);
        Assert.Contains("Managed.Twice", unreached);
    }

    [Fact]
    public void ASafeHandleHasTheConstructorTheMarshallerCallsLive()
    {
        var unreached = Sweep(
            """
            public sealed class Opened : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
            {
                public Opened() : base(true) { }
                public Opened(bool owns) : base(owns) { }
                protected override bool ReleaseHandle() => true;
            }
            public static class Program
            {
                private static Opened? held;

                public static void Main() => System.Console.WriteLine(held);
            }
            """);

        Assert.DoesNotContain("Opened.Opened()", unreached);
        Assert.DoesNotContain("Opened.ReleaseHandle()", unreached);
        Assert.Contains("Opened.Opened(bool)", unreached);
    }

    [Fact]
    public void AnOverrideIsReachedThroughTheMemberItOverrides()
    {
        var unreached = Sweep(
            """
            public abstract class Shape { public abstract int Area(); public abstract int Unused(); }
            public sealed class Square : Shape { public override int Area() => 4; public override int Unused() => 0; }
            public interface IGreeter { string Greet(); string Ignored(); }
            public sealed class Greeter : IGreeter { public string Greet() => "hi"; public string Ignored() => ""; }
            public sealed class Comparer : System.Collections.Generic.IComparer<int> { public int Compare(int x, int y) => x - y; }
            public sealed class Writer : System.IO.TextWriter
            {
                public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
                public override void Write(char value) { }
            }
            public static class Program
            {
                public static void Main()
                {
                    Shape shape = new Square();
                    IGreeter greeter = new Greeter();
                    System.Console.WriteLine(shape.Area() + greeter.Greet() + new Comparer() + new Writer());
                }
            }
            """);

        Assert.DoesNotContain("Square.Area()", unreached);
        Assert.DoesNotContain("Greeter.Greet()", unreached);
        Assert.DoesNotContain("Comparer.Compare(int, int)", unreached);
        Assert.DoesNotContain("Writer.Write(char)", unreached);
        Assert.DoesNotContain("Writer.Encoding", unreached);
        Assert.Contains("Shape.Unused()", unreached);
        Assert.Contains("Square.Unused()", unreached);
        Assert.Contains("IGreeter.Ignored()", unreached);
        Assert.Contains("Greeter.Ignored()", unreached);
    }

    [Fact]
    public void APropertySetAndNeverReadIsUnreachable()
    {
        var unreached = Sweep(
            """
            public sealed class Settings { public int Read { get; set; } public int Written { get; set; } }
            public sealed class Nobody { public void Call() { } }
            public static class Program
            {
                public static void Main()
                {
                    var settings = new Settings { Read = 1, Written = 2 };
                    System.Console.WriteLine(settings.Read);
                }
            }
            """);

        Assert.DoesNotContain("Settings.Read", unreached);
        Assert.Contains("Settings.Written", unreached);
        Assert.Contains("Nobody", unreached);
        Assert.DoesNotContain("Nobody.Call()", unreached);
    }

    private static IReadOnlyList<string> Sweep(string source)
    {
        var compilation = CSharpCompilation.Create(
            "swept",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        using var image = new MemoryStream();
        var emitted = compilation.Emit(image);

        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));

        return ReachabilitySweep.Unreached([ImmutableArray.Create(image.ToArray())], [("Program", "Main")], []);
    }

    private static IReadOnlyList<MetadataReference> Loaded() =>
    [
        .. new[]
            {
                typeof(object).Assembly, Assembly.Load("System.Runtime"), typeof(Console).Assembly,
                typeof(JsonSerializer).Assembly, typeof(HashSet<>).Assembly, typeof(Enumerable).Assembly,
                typeof(SafeHandle).Assembly, typeof(Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid).Assembly,
                typeof(Marshal).Assembly, Assembly.Load("System.Runtime.InteropServices"), Assembly.Load("System.Memory"),
            }
            .Distinct()
            .Select(Reference),
    ];

    /// <summary>A reference built from a loaded assembly's metadata in memory, so nothing is read from disk.</summary>
    private static unsafe MetadataReference Reference(Assembly assembly)
    {
        if (!System.Reflection.Metadata.AssemblyExtensions.TryGetRawMetadata(assembly, out var blob, out var length))
        {
            throw new InvalidOperationException($"{assembly.GetName().Name} has no metadata in memory.");
        }

        return AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((nint)blob, length)).GetReference();
    }
}
