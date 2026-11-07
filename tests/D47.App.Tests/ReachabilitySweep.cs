using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace D47.App.Tests;

/// <summary>
/// Reads the IL of a set of assemblies, marks everything their entry points reach, and names each symbol left unmarked
/// in C# display form.
/// </summary>
internal sealed class ReachabilitySweep
{
    /// <summary>Generic types whose type arguments are compared through their equality.</summary>
    private static readonly HashSet<string> HashingCollections =
    [
        "System.Collections.Generic.Dictionary`2", "System.Collections.Generic.HashSet`1",
        "System.Collections.Concurrent.ConcurrentDictionary`2",
        "System.Collections.Immutable.ImmutableDictionary`2", "System.Collections.Immutable.IImmutableDictionary`2",
        "System.Collections.Immutable.ImmutableHashSet`1", "System.Collections.Immutable.IImmutableSet`1",
        "System.Collections.Frozen.FrozenDictionary`2", "System.Collections.Frozen.FrozenSet`1",
        "System.Collections.Generic.EqualityComparer`1",
    ];

    /// <summary>Generic methods whose type arguments are compared through their equality.</summary>
    private static readonly HashSet<string> HashingOperators =
    [
        "ToDictionary", "ToHashSet", "ToLookup", "GroupBy", "Distinct", "DistinctBy", "Union", "UnionBy", "Intersect",
        "IntersectBy", "Except", "ExceptBy", "ToImmutableDictionary", "ToImmutableHashSet", "ToFrozenDictionary",
        "ToFrozenSet",
    ];

    /// <summary>Framework methods that compare their own type's type arguments, or their own, through equality.</summary>
    private static readonly HashSet<string> EqualityOperators = ["Contains", "IndexOf", "LastIndexOf", "Remove", "SequenceEqual"];

    /// <summary>Record members the compiler writes, whose bodies mark nothing.</summary>
    private static readonly HashSet<string> RecordGenerated =
        ["Equals", "GetHashCode", "PrintMembers", "ToString", "<Clone>$", "get_EqualityContract", "op_Equality", "op_Inequality"];

    private static readonly HashSet<string> LiveWithType = ["ToString", "Equals", "GetHashCode", "Dispose"];

    private static readonly Dictionary<string, string> Keywords = new(StringComparer.Ordinal)
    {
        ["System.Boolean"] = "bool", ["System.Byte"] = "byte", ["System.SByte"] = "sbyte", ["System.Char"] = "char",
        ["System.Int16"] = "short", ["System.UInt16"] = "ushort", ["System.Int32"] = "int", ["System.UInt32"] = "uint",
        ["System.Int64"] = "long", ["System.UInt64"] = "ulong", ["System.Single"] = "float", ["System.Double"] = "double",
        ["System.Decimal"] = "decimal", ["System.String"] = "string", ["System.Object"] = "object", ["System.Void"] = "void",
        ["System.IntPtr"] = "nint", ["System.UIntPtr"] = "nuint",
    };

    private readonly List<Image> images = [];
    private readonly Dictionary<MetadataReader, Image> byReader = [];
    private readonly Dictionary<string, Image> byAssembly = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TypeNode> byFullName = new(StringComparer.Ordinal);
    private readonly Signatures signatures;
    private readonly Queue<object> work = new();
    private readonly Dictionary<MethodNode, List<MethodNode>> waiters = [];
    private readonly HashSet<TypeNode> equality = [];
    private bool untypedEquality;
    private readonly HashSet<TypeNode> json = [];
    private readonly HashSet<(TypeNode Type, int Index)> jsonParameters = [];
    private readonly Dictionary<TypeNode, List<List<Sig>>> instantiations = [];
    private MethodNode? scanning;
    private readonly HashSet<TypeNode> native = [];

    private ReachabilitySweep(IEnumerable<ImmutableArray<byte>> assemblies)
    {
        signatures = new Signatures(this);

        foreach (var bytes in assemblies)
        {
            var pe = new PEReader(bytes);
            var image = new Image(pe, pe.GetMetadataReader());
            images.Add(image);
            byReader[image.Md] = image;
            byAssembly[image.Md.GetString(image.Md.GetAssemblyDefinition().Name)] = image;
        }

        foreach (var image in images)
        {
            Index(image);
        }

        foreach (var image in images)
        {
            foreach (var type in image.Types.Values)
            {
                Link(type);
            }
        }
    }

    /// <summary>The display names of every symbol the roots cannot reach, sorted ordinally.</summary>
    /// <param name="roots">Methods reached from outside, by declaring type's full name and method name.</param>
    /// <param name="unreported">Namespaces whose types are swept but never named.</param>
    public static IReadOnlyList<string> Unreached(
        IEnumerable<ImmutableArray<byte>> assemblies,
        IEnumerable<(string Type, string Method)> roots,
        IEnumerable<string> unreported)
    {
        var sweep = new ReachabilitySweep(assemblies);

        foreach (var (type, method) in roots)
        {
            var node = sweep.byFullName.TryGetValue(type, out var found)
                ? found
                : throw new InvalidOperationException($"No type {type} to start from.");

            var matches = node.Methods.Where(candidate => candidate.Name == method).ToList();

            if (matches.Count == 0)
            {
                throw new InvalidOperationException($"No method {type}.{method} to start from.");
            }

            matches.ForEach(sweep.Reach);
        }

        sweep.Drain();

        var skipped = unreported.ToList();
        var names = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var type in sweep.images.SelectMany(image => image.Types.Values).Where(type => type.Enclosing is null))
        {
            if (!skipped.Any(space => type.FullName.StartsWith(space + ".", StringComparison.Ordinal)))
            {
                Report(type, names);
            }
        }

        foreach (var image in sweep.images)
        {
            image.Pe.Dispose();
        }

        return [.. names];
    }

    private static void Report(TypeNode type, SortedSet<string> names)
    {
        if (type.MetadataName.Contains('<', StringComparison.Ordinal) || type.CompilerGenerated)
        {
            return;
        }

        if (!AnyLive(type))
        {
            if (!HoldsOnlyConstants(type))
            {
                names.Add(type.DisplayName);
            }

            return;
        }

        foreach (var method in type.Methods.Where(method => !method.Reached && Reportable(method)))
        {
            names.Add(method.DisplayName);
        }

        foreach (var property in type.Properties.Where(property => !property.Reached && Reportable(property)))
        {
            names.Add(property.DisplayName);
        }

        foreach (var @event in type.Events.Where(@event => !@event.Reached && !@event.Name.Contains('<', StringComparison.Ordinal)))
        {
            names.Add($"{type.DisplayName}.{@event.Name}");
        }

        foreach (var nested in type.Nested)
        {
            Report(nested, names);
        }
    }

    private static bool AnyLive(TypeNode type) => type.Live || type.Nested.Any(AnyLive);

    /// <summary>A type of constants alone, which IL never references because each read is compiled in as its value.</summary>
    private static bool HoldsOnlyConstants(TypeNode type) =>
        type.Fields.Count > 0
        && type.Fields.All(field => field.Constant)
        && type.Methods.Count == 0
        && type.Properties.Count == 0
        && type.Events.Count == 0
        && type.Nested.Count == 0;

    private static bool Reportable(MethodNode method) =>
        method.Property is null
        && method.Event is null
        && !method.Name.Contains('<', StringComparison.Ordinal)
        && !method.CompilerGenerated
        && method.Name != ".cctor"
        && (method.Def.ImplAttributes & MethodImplAttributes.CodeTypeMask) != MethodImplAttributes.Runtime
        && !(method.Owner.IsRecord && (RecordGenerated.Contains(method.Name) || method.IsCopyConstructor));

    private static bool Reportable(PropertyNode property) =>
        !property.Name.Contains('<', StringComparison.Ordinal)
        && !property.CompilerGenerated
        && !(property.Owner.IsRecord && property.Name == "EqualityContract");

    private void Index(Image image)
    {
        var md = image.Md;

        foreach (var handle in md.TypeDefinitions)
        {
            var def = md.GetTypeDefinition(handle);
            var type = new TypeNode(image, handle, def, md.GetString(def.Name))
            {
                CompilerGenerated = HasAttribute(md, def.GetCustomAttributes(), "System.Runtime.CompilerServices.CompilerGeneratedAttribute"),
                GenericNames = [.. def.GetGenericParameters().Select(parameter => md.GetString(md.GetGenericParameter(parameter).Name))],
            };

            image.Types[handle] = type;
        }

        foreach (var type in image.Types.Values)
        {
            var enclosing = type.Def.GetDeclaringType();

            if (!enclosing.IsNil)
            {
                type.Enclosing = image.Types[enclosing];
                type.Enclosing.Nested.Add(type);
            }
        }

        foreach (var type in image.Types.Values)
        {
            type.FullName = FullName(type);
            image.ByName[type.FullName] = type;
            byFullName.TryAdd(type.FullName, type);
        }

        foreach (var type in image.Types.Values)
        {
            foreach (var handle in type.Def.GetMethods())
            {
                var def = md.GetMethodDefinition(handle);
                var method = new MethodNode(type, handle, def, md.GetString(def.Name))
                {
                    CompilerGenerated = HasAttribute(md, def.GetCustomAttributes(), "System.Runtime.CompilerServices.CompilerGeneratedAttribute"),
                    GenericNames = [.. def.GetGenericParameters().Select(parameter => md.GetString(md.GetGenericParameter(parameter).Name))],
                };

                type.Methods.Add(method);
                image.Methods[handle] = method;
            }

            foreach (var handle in type.Def.GetFields())
            {
                var definition = md.GetFieldDefinition(handle);
                var field = new FieldNode(type, md.GetString(definition.Name)) { Constant = (definition.Attributes & FieldAttributes.Literal) != 0 };
                type.Fields.Add(field);
                image.Fields[handle] = field;
            }

            foreach (var handle in type.Def.GetProperties())
            {
                var def = md.GetPropertyDefinition(handle);
                var accessors = def.GetAccessors();
                var property = new PropertyNode(type, handle, md.GetString(def.Name))
                {
                    Getter = accessors.Getter.IsNil ? null : image.Methods[accessors.Getter],
                    Setter = accessors.Setter.IsNil ? null : image.Methods[accessors.Setter],
                    CompilerGenerated = HasAttribute(md, def.GetCustomAttributes(), "System.Runtime.CompilerServices.CompilerGeneratedAttribute"),
                };

                property.Getter?.Property = property;
                property.Setter?.Property = property;
                type.Properties.Add(property);
            }

            foreach (var handle in type.Def.GetEvents())
            {
                var def = md.GetEventDefinition(handle);
                var accessors = def.GetAccessors();
                var @event = new EventNode(type, md.GetString(def.Name))
                {
                    Adder = accessors.Adder.IsNil ? null : image.Methods[accessors.Adder],
                };

                foreach (var accessor in new[] { accessors.Adder, accessors.Remover, accessors.Raiser }.Where(accessor => !accessor.IsNil))
                {
                    image.Methods[accessor].Event = @event;
                }

                type.Events.Add(@event);
            }

            foreach (var field in type.Fields)
            {
                if (field.Name.StartsWith('<') && field.Name.EndsWith(">k__BackingField", StringComparison.Ordinal))
                {
                    var name = field.Name[1..field.Name.IndexOf('>', StringComparison.Ordinal)];
                    field.BackingOf = type.Properties.FirstOrDefault(property => property.Name == name);
                }
            }

            type.IsRecord = type.Methods.Any(method => method.Name == "<Clone>$")
                            || type.Properties.Any(property => property.Name == "EqualityContract")
                            || type.Methods.Any(method => method.Name == "PrintMembers" && method.CompilerGenerated);
        }
    }

    private static string FullName(TypeNode type)
    {
        if (type.Enclosing is { } enclosing)
        {
            return FullName(enclosing) + "+" + type.MetadataName;
        }

        var space = type.Image.Md.GetString(type.Def.Namespace);
        return space.Length == 0 ? type.MetadataName : space + "." + type.MetadataName;
    }

    /// <summary>Resolves a type's base, interfaces and signatures once every image is indexed.</summary>
    private void Link(TypeNode type)
    {
        var md = type.Image.Md;

        if (!type.Def.BaseType.IsNil)
        {
            var base_ = Definition(TypeOf(type.Image, type.Def.BaseType));
            type.SrcBase = base_?.Src;
            type.ExternalBase = base_ is { Src: null } ? base_.Name : null;
        }

        foreach (var handle in type.Def.GetInterfaceImplementations())
        {
            var face = Definition(TypeOf(type.Image, md.GetInterfaceImplementation(handle).Interface));

            if (face?.Src is { } src)
            {
                type.SrcInterfaces.Add(src);
            }
            else
            {
                type.HasFrameworkInterface = true;
            }
        }

        type.IsInterface = (type.Def.Attributes & TypeAttributes.Interface) != 0;
        type.IsCom = type.IsInterface
                     && ((type.Def.Attributes & TypeAttributes.Import) != 0
                         || HasAttribute(md, type.Def.GetCustomAttributes(), "System.Runtime.InteropServices.Marshalling.GeneratedComInterfaceAttribute"));

        foreach (var method in type.Methods)
        {
            method.Signature = method.Def.DecodeSignature(signatures, type.Image);
            method.Key = Key(method.Signature);
            method.IsCopyConstructor = method.Name == ".ctor"
                                       && method.Signature.ParameterTypes.Length == 1
                                       && Definition(method.Signature.ParameterTypes[0])?.Src == type;
        }
    }

    private void Drain()
    {
        while (work.TryDequeue(out var next))
        {
            switch (next)
            {
                case TypeNode type:
                    Process(type);
                    break;
                case MethodNode method:
                    Process(method);
                    break;
            }
        }
    }

    private void MarkType(TypeNode type)
    {
        if (!type.Live)
        {
            type.Live = true;
            work.Enqueue(type);
        }
    }

    private void Reach(MethodNode method)
    {
        if (!method.Reached)
        {
            method.Reached = true;
            work.Enqueue(method);
        }
    }

    /// <summary>Reaches <paramref name="method"/> once <paramref name="on"/> is reached.</summary>
    private void Depend(MethodNode method, MethodNode on)
    {
        if (on.Reached)
        {
            Reach(method);
            return;
        }

        if (!waiters.TryGetValue(on, out var list))
        {
            waiters[on] = list = [];
        }

        list.Add(method);
    }

    private void Process(TypeNode type)
    {
        var image = type.Image;
        var md = image.Md;

        if (!type.Def.BaseType.IsNil)
        {
            MarkToken(image, type.Def.BaseType);
        }

        foreach (var handle in type.Def.GetInterfaceImplementations())
        {
            MarkToken(image, md.GetInterfaceImplementation(handle).Interface);
        }

        MarkAttributes(image, type.Def.GetCustomAttributes());
        MarkConstraints(image, type.Def.GetGenericParameters());

        if (untypedEquality)
        {
            Equality(type);
        }

        foreach (var handle in type.Def.GetFields())
        {
            var field = md.GetFieldDefinition(handle);
            MarkSig(field.DecodeSignature(signatures, image));
            MarkAttributes(image, field.GetCustomAttributes());
        }

        foreach (var handle in type.Def.GetProperties())
        {
            MarkAttributes(image, md.GetPropertyDefinition(handle).GetCustomAttributes());
        }

        foreach (var handle in type.Def.GetEvents())
        {
            MarkAttributes(image, md.GetEventDefinition(handle).GetCustomAttributes());
        }

        foreach (var method in type.Methods)
        {
            if (method.Name == ".cctor" || LiveWithType.Contains(method.Name) || method.Name.StartsWith("op_", StringComparison.Ordinal))
            {
                Reach(method);
            }
        }

        if (IsSafeHandle(type))
        {
            type.Methods.Where(method => method.Name == ".ctor" && method.Signature.ParameterTypes.Length == 0).ToList().ForEach(Reach);
        }

        if (type.IsInterface)
        {
            if (type.IsCom)
            {
                type.Methods.ForEach(Reach);
            }

            return;
        }

        var explicitly = new HashSet<MethodNode>();

        foreach (var handle in type.Def.GetMethodImplementations())
        {
            var implementation = md.GetMethodImplementation(handle);

            if (implementation.MethodBody.Kind != HandleKind.MethodDefinition)
            {
                continue;
            }

            var body = image.Methods[(MethodDefinitionHandle)implementation.MethodBody];
            explicitly.Add(body);

            if (Resolve(image, implementation.MethodDeclaration).Src is { } declared)
            {
                Depend(body, declared);
            }
            else
            {
                Reach(body);
            }
        }

        foreach (var method in type.Methods.Where(method => (method.Attributes & MethodAttributes.Virtual) != 0 && !explicitly.Contains(method)))
        {
            if ((method.Attributes & MethodAttributes.NewSlot) == 0)
            {
                var overridden = Overridden(type, method);

                if (overridden.Count == 0)
                {
                    Reach(method);
                }

                overridden.ForEach(origin => Depend(method, origin));
                continue;
            }

            var implemented = Implemented(type, method);

            if (implemented.Count > 0)
            {
                implemented.ForEach(member => Depend(method, member));
            }
            else if ((method.Attributes & MethodAttributes.Final) != 0 || ImplementsFrameworkInterface(type))
            {
                Reach(method);
            }
        }
    }

    private void Process(MethodNode method)
    {
        var image = method.Owner.Image;
        var md = image.Md;

        MarkType(method.Owner);
        MarkSig(method.Signature.ReturnType);
        method.Signature.ParameterTypes.ToList().ForEach(MarkSig);
        MarkAttributes(image, method.Def.GetCustomAttributes());
        MarkConstraints(image, method.Def.GetGenericParameters());

        foreach (var handle in method.Def.GetParameters())
        {
            MarkAttributes(image, md.GetParameter(handle).GetCustomAttributes());
        }

        if (method.Property is { } property && (method == property.Getter || property.Getter is null))
        {
            property.Reached = true;
        }

        if (method.Event is { } @event && method == @event.Adder)
        {
            @event.Reached = true;
        }

        var external = (method.Attributes & MethodAttributes.PinvokeImpl) != 0
                       || (method.Def.ImplAttributes & MethodImplAttributes.InternalCall) != 0;

        var invoked = method.Name == "Invoke" && method.Owner.ExternalBase == "System.MulticastDelegate";

        if (external || method.Owner.IsCom || invoked)
        {
            foreach (var passed in method.Signature.ParameterTypes.Append(method.Signature.ReturnType))
            {
                SrcTypes(passed).Where(type => !invoked || type.IsValueType).ToList().ForEach(Native);
            }
        }

        if (waiters.Remove(method, out var waiting))
        {
            waiting.ForEach(Reach);
        }

        if (!(method.Owner.IsRecord && (RecordGenerated.Contains(method.Name) && method.CompilerGenerated || method.IsCopyConstructor)))
        {
            Scan(method);
        }
    }

    private void Scan(MethodNode method)
    {
        var image = method.Owner.Image;
        var rva = method.Def.RelativeVirtualAddress;

        if (rva == 0)
        {
            return;
        }

        var body = image.Pe.GetMethodBody(rva);

        if (!body.LocalSignature.IsNil)
        {
            image.Md.GetStandaloneSignature(body.LocalSignature).DecodeLocalSignature(signatures, image).ToList().ForEach(MarkSig);
        }

        foreach (var region in body.ExceptionRegions.Where(region => !region.CatchType.IsNil))
        {
            MarkToken(image, region.CatchType);
        }

        var tokens = new List<Sig>();
        var jsonByType = false;
        scanning = method;
        var il = body.GetILReader();

        while (il.RemainingBytes > 0)
        {
            var first = il.ReadByte();
            var op = first == 0xFE ? (ILOpCode)(0xFE00 | il.ReadByte()) : (ILOpCode)first;

            if (op == ILOpCode.Switch)
            {
                var targets = il.ReadInt32();
                il.Offset += 4 * targets;
                continue;
            }

            if (op.IsBranch())
            {
                il.Offset += op.GetBranchOperandSize();
                continue;
            }

            if (!TakesToken(op))
            {
                il.Offset += OperandSize(op);
                continue;
            }

            var raw = il.ReadInt32();

            if (op == ILOpCode.Ldstr)
            {
                continue;
            }

            var handle = MetadataTokens.EntityHandle(raw);

            switch (op)
            {
                case ILOpCode.Calli:
                    var called = image.Md.GetStandaloneSignature((StandaloneSignatureHandle)handle).DecodeMethodSignature(signatures, image);
                    MarkSig(called.ReturnType);
                    called.ParameterTypes.ToList().ForEach(MarkSig);

                    if (called.Header.CallingConvention != SignatureCallingConvention.Default)
                    {
                        called.ParameterTypes.Append(called.ReturnType).SelectMany(SrcTypes).ToList().ForEach(Native);
                    }

                    break;
                case ILOpCode.Call or ILOpCode.Callvirt or ILOpCode.Newobj or ILOpCode.Ldftn or ILOpCode.Ldvirtftn or ILOpCode.Jmp:
                    jsonByType |= CallTo(image, handle);
                    break;
                case ILOpCode.Ldfld or ILOpCode.Ldflda or ILOpCode.Ldsfld or ILOpCode.Ldsflda:
                    UseField(image, handle, load: true);
                    break;
                case ILOpCode.Stfld or ILOpCode.Stsfld:
                    UseField(image, handle, load: false);
                    break;
                case ILOpCode.Ldtoken when handle.Kind is HandleKind.MethodDefinition or HandleKind.MethodSpecification
                                           || handle.Kind == HandleKind.MemberReference && image.Md.GetMemberReference((MemberReferenceHandle)handle).GetKind() == MemberReferenceKind.Method:
                    CallTo(image, handle);
                    break;
                case ILOpCode.Ldtoken when handle.Kind is HandleKind.FieldDefinition or HandleKind.MemberReference:
                    UseField(image, handle, load: false);
                    break;
                case ILOpCode.Ldtoken:
                    var token = TypeOf(image, handle);
                    MarkSig(token);
                    tokens.Add(token);
                    break;
                default:
                    MarkToken(image, handle);
                    break;
            }
        }

        if (jsonByType)
        {
            tokens.SelectMany(SrcTypes).ToList().ForEach(Json);
        }
    }

    private static bool TakesToken(ILOpCode op) => op is
        ILOpCode.Jmp or ILOpCode.Call or ILOpCode.Calli or ILOpCode.Callvirt or ILOpCode.Cpobj or ILOpCode.Ldobj
        or ILOpCode.Ldstr or ILOpCode.Newobj or ILOpCode.Castclass or ILOpCode.Isinst or ILOpCode.Unbox
        or ILOpCode.Ldfld or ILOpCode.Ldflda or ILOpCode.Stfld or ILOpCode.Ldsfld or ILOpCode.Ldsflda
        or ILOpCode.Stsfld or ILOpCode.Stobj or ILOpCode.Box or ILOpCode.Newarr or ILOpCode.Ldelema
        or ILOpCode.Ldelem or ILOpCode.Stelem or ILOpCode.Unbox_any or ILOpCode.Refanyval or ILOpCode.Mkrefany
        or ILOpCode.Ldtoken or ILOpCode.Ldftn or ILOpCode.Ldvirtftn or ILOpCode.Initobj or ILOpCode.Constrained
        or ILOpCode.Sizeof;

    private static int OperandSize(ILOpCode op) => op switch
    {
        ILOpCode.Ldarg_s or ILOpCode.Ldarga_s or ILOpCode.Starg_s or ILOpCode.Ldloc_s or ILOpCode.Ldloca_s
            or ILOpCode.Stloc_s or ILOpCode.Ldc_i4_s or ILOpCode.Unaligned => 1,
        ILOpCode.Ldarg or ILOpCode.Ldarga or ILOpCode.Starg or ILOpCode.Ldloc or ILOpCode.Ldloca or ILOpCode.Stloc => 2,
        ILOpCode.Ldc_i4 or ILOpCode.Ldc_r4 => 4,
        ILOpCode.Ldc_i8 or ILOpCode.Ldc_r8 => 8,
        _ => 0,
    };

    /// <summary>Marks a call, a delegate load or a construction; true when it is a JsonSerializer call taking a Type.</summary>
    private bool CallTo(Image image, EntityHandle handle)
    {
        var arguments = ImmutableArray<Sig>.Empty;

        if (handle.Kind == HandleKind.MethodSpecification)
        {
            var spec = image.Md.GetMethodSpecification((MethodSpecificationHandle)handle);
            arguments = spec.DecodeSignature(signatures, image);
            arguments.ToList().ForEach(MarkSig);
            handle = spec.Method;
        }

        var target = Resolve(image, handle);

        if (target.Src is { } src)
        {
            Reach(src);

            if (src.Owner.IsRecord
                && (src.Name is "op_Equality" or "op_Inequality"
                    || src.Name == "Equals" && src.Signature.ParameterTypes.Length == 1
                                            && Definition(src.Signature.ParameterTypes[0])?.Src == src.Owner))
            {
                Equality(src.Owner);
            }

            var parameters = src.Def.GetGenericParameters();

            for (var i = 0; i < parameters.Count && i < arguments.Length; i++)
            {
                if ((src.Owner.Image.Md.GetGenericParameter(parameters[i]).Attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
                {
                    SrcTypes(arguments[i]).ForEach(Constructed);
                }
            }

            return false;
        }

        if (target is { DeclaringType: "System.Object" or "System.ValueType", Name: "Equals" } && !untypedEquality)
        {
            untypedEquality = true;
            images.SelectMany(each => each.Types.Values).Where(type => type.Live).ToList().ForEach(Equality);
        }

        if (HashingOperators.Contains(target.Name) || EqualityOperators.Contains(target.Name))
        {
            arguments.ToList().ForEach(Equality);
        }

        if (EqualityOperators.Contains(target.Name) && target.Parent is { Kind: SigKind.Generic } instance)
        {
            instance.Args.ForEach(Equality);
        }

        if (target.DeclaringType is not { } declaring
            || declaring != "System.Text.Json.JsonSerializer" && !declaring.StartsWith("System.Net.Http.Json.", StringComparison.Ordinal))
        {
            return false;
        }

        arguments.SelectMany(SrcTypes).ToList().ForEach(Json);
        arguments.SelectMany(TypeParameters).ToList().ForEach(JsonParameter);
        return target.Signature is { } signature && signature.ParameterTypes.Any(parameter => parameter is { Kind: SigKind.Named, Name: "System.Type" });
    }

    private void UseField(Image image, EntityHandle handle, bool load)
    {
        if (ResolveField(image, handle) is not { } field)
        {
            return;
        }

        MarkType(field.Owner);

        if (load && field.BackingOf is { } property)
        {
            property.Reached = true;
        }
    }

    private void Constructed(TypeNode type)
    {
        type.Methods.Where(method => method.Name == ".ctor" && method.Signature.ParameterTypes.Length == 0).ToList().ForEach(Reach);
    }

    /// <summary>
    /// A record whose equality is used reads every property that has a backing field, its base records' included, and
    /// uses the equality of each record its fields hold. Once reached code calls <c>object.Equals</c>, which compares by
    /// a runtime type the IL does not name, every live record's equality is used.
    /// </summary>
    private void Equality(TypeNode type)
    {
        for (var record = type; record is not null && record.IsRecord; record = record.SrcBase)
        {
            if (!equality.Add(record))
            {
                return;
            }

            foreach (var property in record.Properties.Where(property => record.Fields.Any(field => field.BackingOf == property)))
            {
                property.Reached = true;
            }

            var md = record.Image.Md;

            foreach (var handle in record.Def.GetFields())
            {
                var field = md.GetFieldDefinition(handle);

                if ((field.Attributes & FieldAttributes.Static) == 0)
                {
                    ComparedBy(field.DecodeSignature(signatures, record.Image));
                }
            }
        }
    }

    private void Equality(Sig argument) => SrcTypes(argument).ForEach(Equality);

    /// <summary>Uses the equality of the records <c>EqualityComparer.Default</c> reaches through a field of this type.</summary>
    private void ComparedBy(Sig sig)
    {
        if (sig.Kind == SigKind.Named && sig.Src is { } src)
        {
            Equality(src);
        }

        if (sig.Kind == SigKind.Generic
            && (sig.Element!.Name == "System.Nullable`1" || sig.Element.Name.StartsWith("System.ValueTuple`", StringComparison.Ordinal)))
        {
            sig.Args.ForEach(ComparedBy);
        }
    }

    /// <summary>A type System.Text.Json reads or writes has every property and constructor live, and so do its property types.</summary>
    private void Json(TypeNode type)
    {
        if (!json.Add(type))
        {
            return;
        }

        MarkType(type);

        for (var current = type; current is not null; current = current.SrcBase)
        {
            foreach (var property in current.Properties)
            {
                property.Reached = true;

                if (property.Getter is { } getter)
                {
                    Reach(getter);
                }

                if (property.Setter is { } setter)
                {
                    Reach(setter);
                }

                var signature = current.Image.Md.GetPropertyDefinition(property.Handle).DecodeSignature(signatures, current.Image);
                SrcTypes(signature.ReturnType).ForEach(Json);
            }

            current.Methods.Where(method => method.Name == ".ctor").ToList().ForEach(Reach);
        }
    }

    /// <summary>A JSON call over the scanned method's own type parameter makes that argument a JSON type at every instantiation.</summary>
    private void JsonParameter(int index)
    {
        for (var owner = scanning?.Owner; owner is not null && index < owner.GenericNames.Length; owner = owner.Enclosing)
        {
            if (jsonParameters.Add((owner, index)))
            {
                foreach (var arguments in instantiations.GetValueOrDefault(owner) ?? [])
                {
                    SrcTypes(arguments[index]).ForEach(Json);
                }
            }
        }
    }

    private static IEnumerable<int> TypeParameters(Sig sig) =>
        (sig.Kind == SigKind.TypeParameter ? [sig.Index] : Enumerable.Empty<int>())
        .Concat(sig.Element is { } element ? TypeParameters(element) : [])
        .Concat(sig.Args.SelectMany(TypeParameters));

    /// <summary>A type handed to native code has every member live.</summary>
    private void Native(TypeNode type)
    {
        if (!native.Add(type))
        {
            return;
        }

        MarkType(type);
        type.Methods.ForEach(Reach);
        type.Properties.ForEach(property => property.Reached = true);
        type.Events.ForEach(@event => @event.Reached = true);
    }

    private static bool IsSafeHandle(TypeNode type)
    {
        var current = type;

        while (current.SrcBase is { } next)
        {
            current = next;
        }

        return current.ExternalBase is { } name
               && (name == "System.Runtime.InteropServices.SafeHandle" || name.StartsWith("Microsoft.Win32.SafeHandles.", StringComparison.Ordinal));
    }

    private static bool ImplementsFrameworkInterface(TypeNode type)
    {
        for (var current = type; current is not null; current = current.SrcBase)
        {
            if (current.HasFrameworkInterface)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The nearest virtuals in src base types that <paramref name="method"/> overrides.</summary>
    private static List<MethodNode> Overridden(TypeNode type, MethodNode method)
    {
        for (var current = type.SrcBase; current is not null; current = current.SrcBase)
        {
            var candidates = Matching(current, method, virtualOnly: true);

            if (candidates.Count > 0)
            {
                return candidates;
            }
        }

        return [];
    }

    /// <summary>The members of src interfaces that <paramref name="method"/> implements.</summary>
    private static List<MethodNode> Implemented(TypeNode type, MethodNode method)
    {
        var found = new List<MethodNode>();

        for (var current = type; current is not null; current = current.SrcBase)
        {
            foreach (var face in current.SrcInterfaces)
            {
                found.AddRange(Matching(face, method, virtualOnly: false));
            }
        }

        return found;
    }

    private static List<MethodNode> Matching(TypeNode type, MethodNode method, bool virtualOnly)
    {
        var candidates = type.Methods
            .Where(candidate => candidate.Name == method.Name
                                && (!virtualOnly || (candidate.Attributes & MethodAttributes.Virtual) != 0)
                                && candidate.Signature.ParameterTypes.Length == method.Signature.ParameterTypes.Length
                                && candidate.Signature.GenericParameterCount == method.Signature.GenericParameterCount)
            .ToList();

        var exact = candidates.Where(candidate => candidate.Key == method.Key).ToList();
        return exact.Count > 0 ? exact : candidates;
    }

    private void MarkAttributes(Image image, CustomAttributeHandleCollection handles)
    {
        foreach (var handle in handles)
        {
            var attribute = image.Md.GetCustomAttribute(handle);
            CallTo(image, attribute.Constructor);

            CustomAttributeValue<Sig> value;

            try
            {
                value = attribute.DecodeValue(signatures);
            }
            catch (Exception exception) when (exception is BadImageFormatException or ArgumentException or InvalidOperationException)
            {
                continue;
            }

            foreach (var argument in value.FixedArguments.Concat(value.NamedArguments.Select(named => new CustomAttributeTypedArgument<Sig>(named.Type, named.Value))))
            {
                MarkValue(argument.Value);
            }
        }
    }

    private void MarkValue(object? value)
    {
        switch (value)
        {
            case Sig sig:
                MarkSig(sig);
                SrcTypes(sig).ForEach(Constructed);
                break;
            case ImmutableArray<CustomAttributeTypedArgument<Sig>> array:
                foreach (var item in array)
                {
                    MarkValue(item.Value);
                }

                break;
        }
    }

    private void MarkConstraints(Image image, GenericParameterHandleCollection parameters)
    {
        foreach (var parameter in parameters)
        {
            foreach (var constraint in image.Md.GetGenericParameter(parameter).GetConstraints())
            {
                MarkToken(image, image.Md.GetGenericParameterConstraint(constraint).Type);
            }
        }
    }

    private void MarkToken(Image image, EntityHandle handle)
    {
        if (handle.Kind is HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification)
        {
            MarkSig(TypeOf(image, handle));
        }
    }

    /// <summary>Marks every src type a signature names, and applies the hashing-collection rule to its instantiations.</summary>
    private void MarkSig(Sig sig)
    {
        if (sig.Src is { } src)
        {
            MarkType(src);
        }

        if (sig.Kind == SigKind.Generic)
        {
            if (sig.Element!.Name is { } name && HashingCollections.Contains(name))
            {
                sig.Args.ForEach(Equality);
            }

            if (sig.Element.Src is { } generic)
            {
                if (!instantiations.TryGetValue(generic, out var seen))
                {
                    instantiations[generic] = seen = [];
                }

                seen.Add(sig.Args);

                for (var i = 0; i < sig.Args.Count; i++)
                {
                    if (jsonParameters.Contains((generic, i)))
                    {
                        SrcTypes(sig.Args[i]).ForEach(Json);
                    }
                }

                var parameters = generic.Def.GetGenericParameters();

                for (var i = 0; i < parameters.Count && i < sig.Args.Count; i++)
                {
                    if ((generic.Image.Md.GetGenericParameter(parameters[i]).Attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
                    {
                        SrcTypes(sig.Args[i]).ForEach(Constructed);
                    }
                }
            }
        }

        if (sig.Element is { } element)
        {
            MarkSig(element);
        }

        sig.Args.ForEach(MarkSig);
    }

    private static List<TypeNode> SrcTypes(Sig sig)
    {
        var found = new List<TypeNode>();
        Collect(sig);
        return found;

        void Collect(Sig current)
        {
            if (current.Src is { } src)
            {
                found.Add(src);
            }

            if (current.Element is { } element)
            {
                Collect(element);
            }

            current.Args.ForEach(Collect);
        }
    }

    /// <summary>The named type a signature instantiates or is.</summary>
    private static Sig? Definition(Sig sig) => sig.Kind switch
    {
        SigKind.Named => sig,
        SigKind.Generic => sig.Element,
        _ => null,
    };

    private Sig TypeOf(Image image, EntityHandle handle) => handle.Kind switch
    {
        HandleKind.TypeDefinition => signatures.GetTypeFromDefinition(image.Md, (TypeDefinitionHandle)handle, 0),
        HandleKind.TypeReference => signatures.GetTypeFromReference(image.Md, (TypeReferenceHandle)handle, 0),
        HandleKind.TypeSpecification => signatures.GetTypeFromSpecification(image.Md, image, (TypeSpecificationHandle)handle, 0),
        _ => Sig.Of(SigKind.Named, "?"),
    };

    private Target Resolve(Image image, EntityHandle handle)
    {
        switch (handle.Kind)
        {
            case HandleKind.MethodDefinition:
                var method = image.Methods[(MethodDefinitionHandle)handle];
                return new Target(method, method.Owner.FullName, method.Name, method.Signature);
            case HandleKind.MethodSpecification:
                return Resolve(image, image.Md.GetMethodSpecification((MethodSpecificationHandle)handle).Method);
            case HandleKind.MemberReference:
                var member = image.Md.GetMemberReference((MemberReferenceHandle)handle);

                if (member.GetKind() != MemberReferenceKind.Method)
                {
                    return new Target(null, null, "", null);
                }

                var name = image.Md.GetString(member.Name);
                var signature = member.DecodeMethodSignature(signatures, image);

                if (member.Parent.Kind == HandleKind.MethodDefinition)
                {
                    return Resolve(image, member.Parent);
                }

                if (member.Parent.Kind is not (HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification))
                {
                    return new Target(null, null, name, signature);
                }

                var parent = TypeOf(image, member.Parent);
                MarkSig(parent);

                var owner = Definition(parent);

                return owner?.Src is { } src
                    ? new Target(Find(src, name, signature), src.FullName, name, signature)
                    : new Target(null, owner?.Name, name, signature, parent);
            default:
                return new Target(null, null, "", null);
        }
    }

    private static MethodNode? Find(TypeNode type, string name, MethodSignature<Sig> signature)
    {
        var key = Key(signature);

        for (var current = type; current is not null; current = current.SrcBase)
        {
            var candidates = current.Methods
                .Where(method => method.Name == name
                                 && method.Signature.ParameterTypes.Length == signature.ParameterTypes.Length
                                 && method.Signature.GenericParameterCount == signature.GenericParameterCount)
                .ToList();

            if (candidates.Count == 1)
            {
                return candidates[0];
            }

            if (candidates.FirstOrDefault(method => method.Key == key) is { } exact)
            {
                return exact;
            }
        }

        return null;
    }

    private FieldNode? ResolveField(Image image, EntityHandle handle)
    {
        if (handle.Kind == HandleKind.FieldDefinition)
        {
            return image.Fields[(FieldDefinitionHandle)handle];
        }

        if (handle.Kind != HandleKind.MemberReference)
        {
            return null;
        }

        var member = image.Md.GetMemberReference((MemberReferenceHandle)handle);

        if (member.GetKind() != MemberReferenceKind.Field
            || member.Parent.Kind is not (HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification))
        {
            return null;
        }

        var parent = TypeOf(image, member.Parent);
        MarkSig(parent);

        var name = image.Md.GetString(member.Name);

        for (var current = Definition(parent)?.Src; current is not null; current = current.SrcBase)
        {
            if (current.Fields.FirstOrDefault(field => field.Name == name) is { } field)
            {
                return field;
            }
        }

        return null;
    }

    private static bool HasAttribute(MetadataReader md, CustomAttributeHandleCollection handles, string fullName) =>
        handles.Any(handle => AttributeName(md, md.GetCustomAttribute(handle)) == fullName);

    private static string? AttributeName(MetadataReader md, CustomAttribute attribute)
    {
        var type = attribute.Constructor.Kind switch
        {
            HandleKind.MethodDefinition => md.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType(),
            HandleKind.MemberReference => md.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent,
            _ => default(EntityHandle),
        };

        return type.Kind switch
        {
            HandleKind.TypeDefinition => Qualified(md, md.GetTypeDefinition((TypeDefinitionHandle)type).Namespace, md.GetTypeDefinition((TypeDefinitionHandle)type).Name),
            HandleKind.TypeReference => Qualified(md, md.GetTypeReference((TypeReferenceHandle)type).Namespace, md.GetTypeReference((TypeReferenceHandle)type).Name),
            _ => null,
        };
    }

    private static string Qualified(MetadataReader md, StringHandle space, StringHandle name) =>
        space.IsNil || md.GetString(space).Length == 0 ? md.GetString(name) : md.GetString(space) + "." + md.GetString(name);

    private static string Key(MethodSignature<Sig> signature) =>
        $"{signature.GenericParameterCount}({string.Join(",", signature.ParameterTypes.Select(Key))}){Key(signature.ReturnType)}";

    private static string Key(Sig sig) => sig.Kind switch
    {
        SigKind.Named => sig.Name,
        SigKind.Generic => $"{Key(sig.Element!)}[{string.Join(",", sig.Args.Select(Key))}]",
        SigKind.SzArray => Key(sig.Element!) + "[]",
        SigKind.Array => $"{Key(sig.Element!)}[{sig.Index}]",
        SigKind.ByRef => Key(sig.Element!) + "&",
        SigKind.Pointer => Key(sig.Element!) + "*",
        SigKind.TypeParameter => "!" + sig.Index,
        SigKind.MethodParameter => "!!" + sig.Index,
        _ => "fnptr",
    };

    /// <summary>A C# display name for a metadata full name, with the type arguments spread over its nesting.</summary>
    private static string Render(string fullName, IReadOnlyList<string> arguments)
    {
        var next = 0;
        var segments = new List<string>();

        foreach (var segment in fullName.Split('+'))
        {
            var tick = segment.IndexOf('`', StringComparison.Ordinal);

            if (tick < 0 || !int.TryParse(segment[(tick + 1)..], out var arity))
            {
                segments.Add(segment);
                continue;
            }

            var taken = arguments.Skip(next).Take(arity).ToList();
            next += arity;
            segments.Add($"{segment[..tick]}<{string.Join(", ", taken)}>");
        }

        return string.Join(".", segments);
    }

    private static string Display(Sig sig, IReadOnlyList<string> typeNames, IReadOnlyList<string> methodNames)
    {
        string Show(Sig inner) => Display(inner, typeNames, methodNames);

        switch (sig.Kind)
        {
            case SigKind.Named:
                return Keywords.TryGetValue(sig.Name, out var keyword) ? keyword : Render(sig.Name, []);
            case SigKind.Generic:
                var name = sig.Element!.Name;

                if (name == "System.Nullable`1")
                {
                    return Show(sig.Args[0]) + "?";
                }

                if (name.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
                {
                    return $"({string.Join(", ", sig.Args.Select(Show))})";
                }

                return Render(name, [.. sig.Args.Select(Show)]);
            case SigKind.SzArray:
                return Show(sig.Element!) + "[]";
            case SigKind.Array:
                return $"{Show(sig.Element!)}[{new string(',', sig.Index - 1)}]";
            case SigKind.ByRef:
                return "ref " + Show(sig.Element!);
            case SigKind.Pointer:
                return Show(sig.Element!) + "*";
            case SigKind.TypeParameter:
                return sig.Index < typeNames.Count ? typeNames[sig.Index] : "!" + sig.Index;
            case SigKind.MethodParameter:
                return sig.Index < methodNames.Count ? methodNames[sig.Index] : "!!" + sig.Index;
            default:
                return "delegate*";
        }
    }

    private enum SigKind
    {
        Named,
        Generic,
        SzArray,
        Array,
        ByRef,
        Pointer,
        TypeParameter,
        MethodParameter,
        FunctionPointer,
    }

    /// <summary>A decoded type: a named type (with its src definition when it has one), an instantiation or a constructed type.</summary>
    private sealed class Sig
    {
        public SigKind Kind { get; private init; }

        public string Name { get; private init; } = "";

        public TypeNode? Src { get; private init; }

        public Sig? Element { get; private init; }

        public List<Sig> Args { get; private init; } = [];

        public int Index { get; private init; }

        public static Sig Of(SigKind kind, string name, TypeNode? src = null) => new() { Kind = kind, Name = name, Src = src };

        public static Sig Wrap(SigKind kind, Sig element, int index = 0) => new() { Kind = kind, Element = element, Index = index };

        public static Sig Generic(Sig definition, IEnumerable<Sig> arguments) => new() { Kind = SigKind.Generic, Element = definition, Args = [.. arguments] };

        public static Sig Parameter(SigKind kind, int index) => new() { Kind = kind, Index = index };
    }

    private sealed class Signatures(ReachabilitySweep sweep) : ISignatureTypeProvider<Sig, Image>, ICustomAttributeTypeProvider<Sig>
    {
        private readonly Dictionary<(MetadataReader, TypeReferenceHandle), Sig> references = [];

        public Sig GetArrayType(Sig elementType, ArrayShape shape) => Sig.Wrap(SigKind.Array, elementType, shape.Rank);

        public Sig GetByReferenceType(Sig elementType) => Sig.Wrap(SigKind.ByRef, elementType);

        public Sig GetFunctionPointerType(MethodSignature<Sig> signature) =>
            Sig.Generic(Sig.Of(SigKind.FunctionPointer, "fnptr"), signature.ParameterTypes.Append(signature.ReturnType));

        public Sig GetGenericInstantiation(Sig genericType, ImmutableArray<Sig> typeArguments) => Sig.Generic(genericType, typeArguments);

        public Sig GetGenericMethodParameter(Image genericContext, int index) => Sig.Parameter(SigKind.MethodParameter, index);

        public Sig GetGenericTypeParameter(Image genericContext, int index) => Sig.Parameter(SigKind.TypeParameter, index);

        public Sig GetModifiedType(Sig modifier, Sig unmodifiedType, bool isRequired) => unmodifiedType;

        public Sig GetPinnedType(Sig elementType) => elementType;

        public Sig GetPointerType(Sig elementType) => Sig.Wrap(SigKind.Pointer, elementType);

        public Sig GetPrimitiveType(PrimitiveTypeCode typeCode) => Sig.Of(SigKind.Named, typeCode switch
        {
            PrimitiveTypeCode.IntPtr => "System.IntPtr",
            PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
            PrimitiveTypeCode.TypedReference => "System.TypedReference",
            _ => "System." + typeCode,
        });

        public Sig GetSZArrayType(Sig elementType) => Sig.Wrap(SigKind.SzArray, elementType);

        public Sig GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var type = sweep.byReader[reader].Types[handle];
            return Sig.Of(SigKind.Named, type.FullName, type);
        }

        public Sig GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            if (references.TryGetValue((reader, handle), out var known))
            {
                return known;
            }

            var reference = reader.GetTypeReference(handle);
            var name = reader.GetString(reference.Name);
            Sig resolved;

            switch (reference.ResolutionScope.Kind)
            {
                case HandleKind.TypeReference:
                    var outer = GetTypeFromReference(reader, (TypeReferenceHandle)reference.ResolutionScope, rawTypeKind);
                    var full = outer.Name + "+" + name;
                    resolved = Sig.Of(SigKind.Named, full, outer.Src?.Image.ByName.GetValueOrDefault(full));
                    break;
                case HandleKind.AssemblyReference:
                    var assembly = reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)reference.ResolutionScope).Name);
                    var qualified = Qualified(reader, reference.Namespace, reference.Name);
                    resolved = Sig.Of(SigKind.Named, qualified, sweep.byAssembly.GetValueOrDefault(assembly)?.ByName.GetValueOrDefault(qualified));
                    break;
                default:
                    var local = Qualified(reader, reference.Namespace, reference.Name);
                    resolved = Sig.Of(SigKind.Named, local, sweep.byReader[reader].ByName.GetValueOrDefault(local));
                    break;
            }

            references[(reader, handle)] = resolved;
            return resolved;
        }

        public Sig GetTypeFromSpecification(MetadataReader reader, Image genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

        public Sig GetSystemType() => Sig.Of(SigKind.Named, "System.Type");

        public bool IsSystemType(Sig type) => type is { Kind: SigKind.Named, Name: "System.Type" };

        /// <summary>Resolves an attribute's typeof argument, which the blob holds as an assembly-qualified name.</summary>
        public Sig GetTypeFromSerializedName(string name)
        {
            var parts = name.Split(['[', ']', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var head = parts.Length > 0 ? parts[0] : name;
            var arguments = parts.Skip(1).Select(part => sweep.byFullName.GetValueOrDefault(part)).OfType<TypeNode>()
                .Select(type => Sig.Of(SigKind.Named, type.FullName, type)).ToList();
            var named = Sig.Of(SigKind.Named, head, sweep.byFullName.GetValueOrDefault(head));
            return arguments.Count == 0 ? named : Sig.Generic(named, arguments);
        }

        public PrimitiveTypeCode GetUnderlyingEnumType(Sig type)
        {
            if (type.Src is not { } src)
            {
                return PrimitiveTypeCode.Int32;
            }

            var md = src.Image.Md;
            var value = src.Def.GetFields().Select(md.GetFieldDefinition).FirstOrDefault(field => md.GetString(field.Name) == "value__");
            var underlying = value.Signature.IsNil ? null : value.DecodeSignature(this, src.Image).Name;

            return underlying switch
            {
                "System.Byte" => PrimitiveTypeCode.Byte,
                "System.SByte" => PrimitiveTypeCode.SByte,
                "System.Int16" => PrimitiveTypeCode.Int16,
                "System.UInt16" => PrimitiveTypeCode.UInt16,
                "System.UInt32" => PrimitiveTypeCode.UInt32,
                "System.Int64" => PrimitiveTypeCode.Int64,
                "System.UInt64" => PrimitiveTypeCode.UInt64,
                _ => PrimitiveTypeCode.Int32,
            };
        }
    }

    private sealed record Target(MethodNode? Src, string? DeclaringType, string Name, MethodSignature<Sig>? Signature, Sig? Parent = null);

    private sealed class Image(PEReader pe, MetadataReader md)
    {
        public PEReader Pe { get; } = pe;

        public MetadataReader Md { get; } = md;

        public Dictionary<TypeDefinitionHandle, TypeNode> Types { get; } = [];

        public Dictionary<MethodDefinitionHandle, MethodNode> Methods { get; } = [];

        public Dictionary<FieldDefinitionHandle, FieldNode> Fields { get; } = [];

        public Dictionary<string, TypeNode> ByName { get; } = new(StringComparer.Ordinal);
    }

    private sealed class TypeNode(Image image, TypeDefinitionHandle handle, TypeDefinition def, string metadataName)
    {
        public Image Image { get; } = image;

        public TypeDefinitionHandle Handle { get; } = handle;

        public TypeDefinition Def { get; } = def;

        public string MetadataName { get; } = metadataName;

        public string FullName { get; set; } = "";

        public string[] GenericNames { get; init; } = [];

        public bool CompilerGenerated { get; init; }

        public TypeNode? Enclosing { get; set; }

        public List<TypeNode> Nested { get; } = [];

        public List<MethodNode> Methods { get; } = [];

        public List<FieldNode> Fields { get; } = [];

        public List<PropertyNode> Properties { get; } = [];

        public List<EventNode> Events { get; } = [];

        public TypeNode? SrcBase { get; set; }

        public string? ExternalBase { get; set; }

        public List<TypeNode> SrcInterfaces { get; } = [];

        public bool HasFrameworkInterface { get; set; }

        public bool IsInterface { get; set; }

        public bool IsCom { get; set; }

        public bool IsRecord { get; set; }

        public bool IsValueType => ExternalBase is "System.ValueType" or "System.Enum";

        public bool Live { get; set; }

        public string DisplayName => Render(FullName, GenericNames);

        /// <summary>The name a constructor is displayed under.</summary>
        public string SimpleName => MetadataName.Split('`')[0];
    }

    private sealed class MethodNode(TypeNode owner, MethodDefinitionHandle handle, MethodDefinition def, string name)
    {
        public TypeNode Owner { get; } = owner;

        public MethodDefinitionHandle Handle { get; } = handle;

        public MethodDefinition Def { get; } = def;

        public string Name { get; } = name;

        public MethodAttributes Attributes => Def.Attributes;

        public string[] GenericNames { get; init; } = [];

        public bool CompilerGenerated { get; init; }

        public MethodSignature<Sig> Signature { get; set; }

        public string Key { get; set; } = "";

        public bool IsCopyConstructor { get; set; }

        public PropertyNode? Property { get; set; }

        public EventNode? Event { get; set; }

        public bool Reached { get; set; }

        public string DisplayName
        {
            get
            {
                var md = Owner.Image.Md;
                var flags = new Dictionary<int, ParameterAttributes>();

                foreach (var handle in Def.GetParameters())
                {
                    var parameter = md.GetParameter(handle);
                    flags[parameter.SequenceNumber] = parameter.Attributes;
                }

                var parameters = Signature.ParameterTypes.Select((type, i) =>
                {
                    if (type.Kind != SigKind.ByRef)
                    {
                        return Display(type, Owner.GenericNames, GenericNames);
                    }

                    var attributes = flags.GetValueOrDefault(i + 1);
                    var prefix = (attributes & ParameterAttributes.Out) != 0 && (attributes & ParameterAttributes.In) == 0 ? "out "
                        : (attributes & ParameterAttributes.In) != 0 ? "in "
                        : "ref ";
                    return prefix + Display(type.Element!, Owner.GenericNames, GenericNames);
                });

                var shown = Name == ".ctor" ? Owner.SimpleName : Name;
                var generic = GenericNames.Length == 0 ? "" : $"<{string.Join(", ", GenericNames)}>";
                return $"{Owner.DisplayName}.{shown}{generic}({string.Join(", ", parameters)})";
            }
        }
    }

    private sealed class FieldNode(TypeNode owner, string name)
    {
        public TypeNode Owner { get; } = owner;

        public string Name { get; } = name;

        public PropertyNode? BackingOf { get; set; }

        public bool Constant { get; init; }
    }

    private sealed class PropertyNode(TypeNode owner, PropertyDefinitionHandle handle, string name)
    {
        public TypeNode Owner { get; } = owner;

        public PropertyDefinitionHandle Handle { get; } = handle;

        public string Name { get; } = name;

        public MethodNode? Getter { get; init; }

        public MethodNode? Setter { get; init; }

        public bool CompilerGenerated { get; init; }

        public bool Reached { get; set; }

        public string DisplayName
        {
            get
            {
                var accessor = Getter ?? Setter;
                var indexes = accessor is null ? [] : accessor.Signature.ParameterTypes.Take(accessor.Signature.ParameterTypes.Length - (accessor == Setter ? 1 : 0)).ToList();

                return indexes.Count == 0
                    ? $"{Owner.DisplayName}.{Name}"
                    : $"{Owner.DisplayName}.this[{string.Join(", ", indexes.Select(type => Display(type, Owner.GenericNames, [])))}]";
            }
        }
    }

    private sealed class EventNode(TypeNode owner, string name)
    {
        public TypeNode Owner { get; } = owner;

        public string Name { get; } = name;

        public MethodNode? Adder { get; init; }

        public bool Reached { get; set; }
    }
}
