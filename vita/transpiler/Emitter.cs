using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cs2Cpp;

// Emits C++ headers and sources for all types of the compilation.
class Emitter
{
    public readonly CSharpCompilation Comp;
    readonly string outDir;
    readonly string portDir;
    public List<INamedTypeSymbol> Types = new();
    readonly Dictionary<string, string> bodyOverrides = new();
    readonly HashSet<string> skipBodies = new();
    readonly HashSet<INamedTypeSymbol> reflectTypes = new(SymbolEqualityComparer.Default);
    readonly Dictionary<IPropertySymbol, bool> fieldLike = new(SymbolEqualityComparer.Default);
    public readonly List<string> Warnings = new();
    readonly List<string> memberKeys = new();

    public Emitter(CSharpCompilation comp, string outDir, string portDir)
    {
        Comp = comp;
        this.outDir = outDir;
        this.portDir = portDir;
    }

    // ------------------------------------------------------------------ analysis

    public bool IsFieldLike(IPropertySymbol p)
    {
        if (fieldLike.TryGetValue(p, out var r)) return r;
        r = ComputeFieldLike(p);
        fieldLike[p] = r;
        return r;
    }

    bool ComputeFieldLike(IPropertySymbol p)
    {
        p = p.OriginalDefinition;
        if (!Names.IsSource(p, Comp) || p.IsIndexer || p.IsVirtual || p.IsAbstract || p.IsOverride) return false;
        if (p.ExplicitInterfaceImplementations.Length > 0) return false;
        if (p.ContainingType.TypeKind == TypeKind.Interface) return false;
        foreach (var i in p.ContainingType.AllInterfaces)
            foreach (var im in i.GetMembers().OfType<IPropertySymbol>())
                if (SymbolEqualityComparer.Default.Equals(p.ContainingType.FindImplementationForInterfaceMember(im), p)) return false;
        return IsAutoProperty(p);
    }

    public static bool IsAutoProperty(IPropertySymbol p)
    {
        foreach (var r in p.DeclaringSyntaxReferences)
        {
            if (r.GetSyntax() is not PropertyDeclarationSyntax pd) return false;
            if (pd.ExpressionBody != null || pd.AccessorList == null) return false;
            if (pd.AccessorList.Accessors.Any(a => a.Body != null || a.ExpressionBody != null)) return false;
            return true;
        }
        return false;
    }

    public string MethodKey(IMethodSymbol m) => m.ContainingType.ToDisplayString() + "." + m.Name;

    void LoadConfig()
    {
        var bodies = Path.Combine(portDir, "bodies");
        if (Directory.Exists(bodies))
            foreach (var f in Directory.GetFiles(bodies, "*.cpp"))
                bodyOverrides[Path.GetFileNameWithoutExtension(f)] = File.ReadAllText(f).Trim();
        var cfg = Path.Combine(portDir, "config.txt");
        if (File.Exists(cfg))
            foreach (var raw in File.ReadAllLines(cfg))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts[0] == "skipbody") skipBodies.Add(parts[1]);
            }
    }

    string OverrideBody(IMethodSymbol m)
    {
        var key = MethodKey(m);
        if (bodyOverrides.TryGetValue(key + "#" + m.Parameters.Length, out var b)) return b;
        if (bodyOverrides.TryGetValue(key, out b)) return b;
        return null;
    }

    bool SkipBody(IMethodSymbol m) => skipBodies.Contains(MethodKey(m)) || skipBodies.Contains(MethodKey(m) + "#" + m.Parameters.Length);

    void CollectTypes(INamespaceOrTypeSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers())
        {
            if (t.Name.StartsWith("<") || t.IsImplicitlyDeclared) continue;
            if (!Names.IsSource(t, Comp)) continue;
            Types.Add(t);
            CollectTypes(t);
        }
        if (ns is INamespaceSymbol n)
            foreach (var sub in n.GetNamespaceMembers()) CollectTypes(sub);
    }

    // ------------------------------------------------------------------ run

    public void Run()
    {
        LoadConfig();
        CollectTypes(Comp.Assembly.GlobalNamespace);
        CollectReflection();
        Directory.CreateDirectory(outDir);
        foreach (var f in Directory.GetFiles(outDir, "impl_*.cpp")) File.Delete(f);
        var ordered = OrderTypes();
        EmitForward();
        EmitTypes(ordered);
        EmitImpl(ordered);
        EmitEnums();
        EmitInit();
        File.WriteAllLines(Path.Combine(outDir, "..", "members.txt"), memberKeys.Distinct().OrderBy(k => k));
        File.WriteAllLines(Path.Combine(outDir, "..", "warnings.txt"), Warnings.Distinct().OrderBy(w => w));
        Console.WriteLine($"types: {Types.Count}, warnings: {Warnings.Distinct().Count()}");
    }

    void CollectReflection()
    {
        foreach (var tree in Comp.SyntaxTrees)
        {
            var model = Comp.GetSemanticModel(tree);
            foreach (var to in tree.GetRoot().DescendantNodes().OfType<TypeOfExpressionSyntax>())
                if (model.GetTypeInfo(to.Type).Type is INamedTypeSymbol nt && Names.IsSource(nt, Comp) && nt.TypeKind == TypeKind.Class)
                    reflectTypes.Add(nt);
        }
    }

    IEnumerable<INamedTypeSymbol> ClassLike => Types.Where(t => t.TypeKind is TypeKind.Class or TypeKind.Struct or TypeKind.Interface);

    // Topological order: base types, interfaces and by-value struct fields first.
    List<INamedTypeSymbol> OrderTypes()
    {
        var result = new List<INamedTypeSymbol>();
        var state = new Dictionary<INamedTypeSymbol, int>(SymbolEqualityComparer.Default);
        var set = new HashSet<INamedTypeSymbol>(ClassLike, SymbolEqualityComparer.Default);
        void Visit(INamedTypeSymbol t)
        {
            t = t.OriginalDefinition;
            if (!set.Contains(t)) return;
            if (state.TryGetValue(t, out var s))
            {
                if (s == 1) Warnings.Add("type dependency cycle at " + t);
                return;
            }
            state[t] = 1;
            foreach (var d in TypeDeps(t)) Visit(d);
            state[t] = 2;
            result.Add(t);
        }
        foreach (var t in ClassLike) Visit(t);
        return result;
    }

    IEnumerable<INamedTypeSymbol> TypeDeps(INamedTypeSymbol t)
    {
        if (t.BaseType != null) yield return t.BaseType;
        foreach (var i in t.Interfaces) yield return i;
        foreach (var f in t.GetMembers().OfType<IFieldSymbol>())
        {
            if (f.IsConst || f.IsStatic) continue;
            foreach (var d in ValueDeps(f.Type)) yield return d;
        }
        foreach (var p in t.GetMembers().OfType<IPropertySymbol>())
        {
            if (p.IsStatic || p.IsIndexer) continue;
            if (IsFieldLike(p) || IsAutoProperty(p) && Names.IsSource(p, Comp))
                foreach (var d in ValueDeps(p.Type)) yield return d;
        }
        // Covariant overrides need complete return types.
        foreach (var m in t.GetMembers().OfType<IMethodSymbol>())
            if (m.IsOverride && m.OverriddenMethod != null && !SymbolEqualityComparer.Default.Equals(m.ReturnType, m.OverriddenMethod.ReturnType) && m.ReturnType is INamedTypeSymbol rt)
                yield return rt;
    }

    IEnumerable<INamedTypeSymbol> ValueDeps(ITypeSymbol t)
    {
        if (t is not INamedTypeSymbol nt) yield break;
        if (Names.IsRefType(nt)) yield break;
        if (nt.IsTupleType)
        {
            foreach (var e in nt.TupleElements)
                foreach (var d in ValueDeps(e.Type)) yield return d;
            yield break;
        }
        if (nt.IsGenericType)
            foreach (var a in nt.TypeArguments)
                foreach (var d in ValueDeps(a)) yield return d;
        if (nt.TypeKind == TypeKind.Struct) yield return nt;
    }

    // ------------------------------------------------------------------ forward.h

    static string NsOpen(INamespaceSymbol ns)
    {
        if (ns == null || ns.IsGlobalNamespace) return "";
        return "namespace " + Names.NamespacePrefix(ns).Trim(':') + " {\n";
    }

    static string NsClose(INamespaceSymbol ns) => ns == null || ns.IsGlobalNamespace ? "" : "}\n";

    static string TemplateHeader(INamedTypeSymbol t)
    {
        var args = new List<string>();
        for (var o = t; o != null; o = o.ContainingType) args.InsertRange(0, o.TypeParameters.Select(tp => "class " + Names.Escape(tp.Name)));
        return args.Count > 0 ? "template <" + string.Join(", ", args) + ">\n" : "";
    }

    void EmitForward()
    {
        var sb = new StringBuilder();
        sb.Append("#pragma once\n// Generated by Cs2Cpp. Do not edit.\n#include \"rt/all.h\"\n#include \"fw/all.h\"\n#include <functional>\n\n");
        foreach (var g in Types.GroupBy(t => Names.NamespacePrefix(t.ContainingNamespace)))
        {
            var ns = g.First().ContainingNamespace;
            sb.Append(NsOpen(ns));
            foreach (var t in g)
            {
                if (t.TypeKind == TypeKind.Enum) EmitEnumDecl(sb, t);
                else if (t.TypeKind is TypeKind.Class or TypeKind.Struct or TypeKind.Interface)
                    sb.Append(TemplateHeader(t)).Append("struct ").Append(Names.FlatName(t)).Append(";\n");
            }
            sb.Append(NsClose(ns));
        }
        File.WriteAllText(Path.Combine(outDir, "forward.h"), sb.ToString());
    }

    void EmitEnumDecl(StringBuilder sb, INamedTypeSymbol e)
    {
        var n = Names.FlatName(e);
        var u = Names.EnumUnderlying(e);
        sb.Append($"enum class {n} : {u} {{\n");
        foreach (var f in e.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue))
            sb.Append($"    {Names.Escape(f.Name)} = {Names.Literal(f.ConstantValue, e.EnumUnderlyingType)},\n");
        sb.Append("};\n");
        foreach (var op in new[] { "|", "&", "^" })
        {
            sb.Append($"inline constexpr {n} operator{op}({n} a, {n} b) {{ return static_cast<{n}>(static_cast<{u}>(a) {op} static_cast<{u}>(b)); }}\n");
            sb.Append($"inline {n}& operator{op}=({n}& a, {n} b) {{ return a = a {op} b; }}\n");
        }
        sb.Append($"inline constexpr {n} operator~({n} a) {{ return static_cast<{n}>(~static_cast<{u}>(a)); }}\n");
        sb.Append($"::System::String* __EnumToString({n} v);\n::System::Array1<{n}>* __EnumValues({n}*);\n::System::Type* __EnumType({n}*);\n");
    }

    void EmitEnums()
    {
        var sb = new StringBuilder("// Generated by Cs2Cpp.\n#include \"types.h\"\n\n");
        foreach (var e in Types.Where(t => t.TypeKind == TypeKind.Enum))
        {
            var n = Names.TypeName(e);
            var ns = Names.NamespacePrefix(e.ContainingNamespace);
            var fields = e.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).ToList();
            bool flags = e.GetAttributes().Any(a => a.AttributeClass?.Name == "FlagsAttribute");
            var vals = fields.Select(f => Convert.ToInt64(f.ConstantValue)).ToList();
            sb.Append($"::System::String* {ns}__EnumToString({n} v) {{\n");
            if (fields.Count == 0) sb.Append("    return ::System::ToStr((int64_t)v);\n}\n");
            else
            {
                sb.Append($"    static const int64_t vals[] = {{{string.Join(", ", vals.Select(v => $"INT64_C({v})"))}}};\n");
                sb.Append($"    static const char16_t* const names[] = {{{string.Join(", ", fields.Select(f => "u\"" + f.Name + "\""))}}};\n");
                sb.Append($"    return ::System::EnumToStringImpl((int64_t)v, vals, names, {fields.Count}, {(flags ? "true" : "false")});\n}}\n");
            }
            var sorted = fields.Select((f, i) => (f, v: vals[i])).OrderBy(p => (ulong)p.v).Select(p => $"{n}::{Names.Escape(p.f.Name)}");
            sb.Append($"::System::Array1<{n}>* {ns}__EnumValues({n}*) {{ return ::System::Array1<{n}>::Of({{{string.Join(", ", sorted)}}}); }}\n");
            sb.Append($"::System::Type* {ns}__EnumType({n}*) {{ static ::System::Type* t = ::System::EnumTypeCreate(u\"{e.Name}\", u\"{e.ToDisplayString()}\", Names__Box<{n}>()); return t; }}\n\n".Replace("Names__Box", "::System::EnumBoxer"));
        }
        File.WriteAllText(Path.Combine(outDir, "enums.cpp"), sb.ToString());
    }

    // ------------------------------------------------------------------ types.h

    string BaseList(INamedTypeSymbol t)
    {
        var bases = new List<string>();
        if (t.TypeKind == TypeKind.Class)
        {
            var bt = t.BaseType;
            bases.Add("public " + (bt == null || bt.SpecialType == SpecialType.System_Object ? "::System::Object" : Names.TypeName(bt)));
            foreach (var i in t.Interfaces) bases.Add("public virtual " + Names.TypeName(i));
        }
        else if (t.TypeKind == TypeKind.Interface)
        {
            foreach (var i in t.Interfaces) bases.Add("public virtual " + Names.TypeName(i));
        }
        return bases.Count > 0 ? " : " + string.Join(", ", bases) : "";
    }

    static string Params(IMethodSymbol m) => string.Join(", ", m.Parameters.Select(p => Names.ParamType(p) + " " + ParamName(p)));
    static string ParamName(IParameterSymbol p) => string.IsNullOrEmpty(p.Name) || p.Name == "_" ? $"__p{p.Ordinal}" : Names.Escape(p.Name);

    static bool IsOrdinaryMethod(IMethodSymbol m) => m.MethodKind is MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation;

    string MethodDecl(IMethodSymbol m, INamedTypeSymbol t)
    {
        var sb = new StringBuilder();
        if (m.IsGenericMethod) sb.Append("template <" + string.Join(", ", m.TypeParameters.Select(tp => "class " + Names.Escape(tp.Name))) + "> ");
        bool iface = t.TypeKind == TypeKind.Interface;
        bool isStruct = t.TypeKind == TypeKind.Struct;
        if (m.IsStatic) sb.Append("static ");
        else if ((m.IsVirtual || m.IsAbstract || m.IsOverride || iface) && !m.IsGenericMethod && !isStruct) sb.Append("virtual ");
        sb.Append(Names.ReturnType(m)).Append(' ').Append(Names.MemberName(m)).Append('(').Append(Params(m)).Append(')');
        if (m.IsOverride && !m.IsGenericMethod && !isStruct) sb.Append(" override");
        if ((m.IsAbstract || iface && !m.IsStatic && m.DeclaringSyntaxReferences.All(r => r.GetSyntax() is MethodDeclarationSyntax { Body: null, ExpressionBody: null })) && !m.IsGenericMethod) sb.Append(" = 0");
        sb.Append(';');
        return sb.ToString();
    }

    void EmitTypes(List<INamedTypeSymbol> ordered)
    {
        var sb = new StringBuilder();
        sb.Append("#pragma once\n// Generated by Cs2Cpp. Do not edit.\n#include \"forward.h\"\n\n");
        foreach (var t in ordered)
        {
            sb.Append(NsOpen(t.ContainingNamespace));
            sb.Append(TemplateHeader(t));
            sb.Append("struct ").Append(Names.FlatName(t)).Append(BaseList(t)).Append(" {\n");
            if (t.TypeKind == TypeKind.Interface) EmitInterfaceBody(sb, t);
            else EmitClassBody(sb, t);
            sb.Append("};\n");
            sb.Append(NsClose(t.ContainingNamespace));
        }
        sb.Append("\n#include \"templates.h\"\n");
        File.WriteAllText(Path.Combine(outDir, "types.h"), sb.ToString());
    }

    void EmitInterfaceBody(StringBuilder sb, INamedTypeSymbol t)
    {
        if (t.Interfaces.Length == 0) sb.Append("    virtual ~").Append(Names.FlatName(t)).Append("() = default;\n");
        foreach (var m in t.GetMembers().OfType<IMethodSymbol>())
            if (IsOrdinaryMethod(m)) sb.Append("    ").Append(MethodDecl(m, t)).Append('\n');
        foreach (var p in t.GetMembers().OfType<IPropertySymbol>())
        {
            var n = p.IsIndexer ? "Item" : Names.MemberName(p);
            var ip = p.IsIndexer ? string.Join(", ", p.Parameters.Select(x => Names.ParamType(x) + " " + ParamName(x))) : "";
            if (p.GetMethod != null) sb.Append($"    virtual {Names.TypeRef(p.Type)} get_{n}({ip}) = 0;\n");
            if (p.SetMethod != null) sb.Append($"    virtual void set_{n}({ip}{(ip.Length > 0 ? ", " : "")}{Names.TypeRef(p.Type)} value) = 0;\n");
        }
        sb.Append("    static ::System::Type* __type();\n");
    }

    public IEnumerable<IFieldSymbol> InstanceFields(INamedTypeSymbol t) =>
        t.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsStatic && !f.IsConst && f.AssociatedSymbol == null);

    void EmitClassBody(StringBuilder sb, INamedTypeSymbol t)
    {
        bool isStruct = t.TypeKind == TypeKind.Struct;
        var flat = Names.FlatName(t);
        string init = isStruct ? "{}" : "";
        // Fields
        foreach (var f in t.GetMembers().OfType<IFieldSymbol>())
        {
            if (f.IsConst) continue;
            if (f.AssociatedSymbol != null) continue;  // backing fields handled via properties
            sb.Append("    ").Append(f.IsStatic ? "static " : "").Append(Names.TypeRef(f.Type)).Append(' ').Append(Names.MemberName(f)).Append(f.IsStatic ? "" : init).Append(";\n");
        }
        foreach (var e in t.GetMembers().OfType<IEventSymbol>())
        {
            sb.Append("    ").Append(e.IsStatic ? "static " : "").Append(Names.TypeRef(e.Type)).Append(' ').Append(Names.MemberName(e)).Append(e.IsStatic ? "" : init).Append(";\n");
        }
        foreach (var p in t.GetMembers().OfType<IPropertySymbol>())
        {
            if (IsFieldLike(p))
            {
                sb.Append("    ").Append(p.IsStatic ? "static " : "").Append(Names.TypeRef(p.Type)).Append(' ').Append(Names.MemberName(p)).Append(p.IsStatic ? "" : init).Append(";\n");
                continue;
            }
            var n = p.IsIndexer ? "Item" : Names.MemberName(p);
            var ip = p.IsIndexer ? string.Join(", ", p.Parameters.Select(x => Names.ParamType(x) + " " + ParamName(x))) : "";
            if (!p.IsAbstract && IsAutoProperty(p))
                sb.Append("    ").Append(p.IsStatic ? "static " : "").Append(Names.TypeRef(p.Type)).Append(" __b_").Append(n).Append(p.IsStatic ? "" : init).Append(";\n");
            string mods = p.IsStatic ? "static " : (p.IsVirtual || p.IsAbstract || p.IsOverride) ? "virtual " : "";
            bool isOverride = p.IsOverride;
            string suffix = (isOverride ? " override" : "") + (p.IsAbstract ? " = 0" : "");
            // An override may only redefine one accessor; C++ needs both declared only where defined.
            if (p.GetMethod != null) sb.Append($"    {mods}{Names.TypeRef(p.Type)} get_{n}({ip}){suffix};\n");
            if (p.SetMethod != null) sb.Append($"    {mods}void set_{n}({ip}{(ip.Length > 0 ? ", " : "")}{Names.TypeRef(p.Type)} value){suffix};\n");
        }
        // Constructors
        if (isStruct)
        {
            sb.Append($"    {flat}() = default;\n");
            foreach (var c in t.InstanceConstructors.Where(c => !c.IsImplicitlyDeclared && c.Parameters.Length > 0))
                sb.Append($"    {flat}({Params(c)});\n");
        }
        else if (!t.IsStatic && !t.IsAbstract || t.IsAbstract)
        {
            foreach (var c in t.InstanceConstructors)
                sb.Append($"    void __init({Params(c)});\n");
        }
        // Methods
        var declaredNames = new HashSet<string>();
        foreach (var m in t.GetMembers().OfType<IMethodSymbol>())
        {
            if (IsOrdinaryMethod(m))
            {
                sb.Append("    ").Append(MethodDecl(m, t)).Append('\n');
                declaredNames.Add(Names.MemberName(m));
                memberKeys.Add(MethodKey(m) + "#" + m.Parameters.Length);
            }
            else if (m.MethodKind is MethodKind.UserDefinedOperator or MethodKind.Conversion)
                sb.Append($"    static {Names.ReturnType(m)} {m.MetadataName}({Params(m)});\n");
        }
        // Interface members implemented by inherited methods
        if (!isStruct)
            foreach (var stub in ForwardingStubs(t)) sb.Append("    ").Append(stub.decl).Append('\n');
        // Unhide base overloads
        if (!isStruct && t.BaseType != null)
            foreach (var n in declaredNames)
                if (BaseHasMethod(t.BaseType, n)) sb.Append($"    using {BaseCppName(t)}::{n};\n");
        sb.Append("    static void __cctor();\n");
        sb.Append("    static ::System::Type* __type();\n");
        if (reflectTypes.Contains(t)) sb.Append("    static void __reflect(::System::Type* t);\n");
        if (!isStruct) sb.Append("    ::System::Type* GetType() override;\n");
        if (isStruct)
        {
            bool hasTypedEquals = t.GetMembers("Equals").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, t));
            if (!hasTypedEquals) sb.Append($"    bool Equals(const {flat}& o) const;\n");
            if (!t.GetMembers("GetHashCode").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 0)) sb.Append("    int32_t GetHashCode() const;\n");
            if (!t.GetMembers("ToString").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 0)) sb.Append("    ::System::String* ToString() const;\n");
        }
    }

    string BaseCppName(INamedTypeSymbol t) =>
        t.BaseType == null || t.BaseType.SpecialType == SpecialType.System_Object ? "::System::Object" : Names.TypeName(t.BaseType);

    static bool BaseHasMethod(INamedTypeSymbol bt, string name)
    {
        for (var b = bt; b != null; b = b.BaseType)
            foreach (var m in b.GetMembers().OfType<IMethodSymbol>())
                if (m.MethodKind == MethodKind.Ordinary && Names.MemberName(m) == name && m.DeclaredAccessibility != Accessibility.Private) return true;
        return false;
    }

    record Stub(string decl, string def);

    IEnumerable<Stub> ForwardingStubs(INamedTypeSymbol t)
    {
        var seen = new HashSet<string>();
        foreach (var i in t.AllInterfaces)
        {
            if (t.BaseType != null && t.BaseType.AllInterfaces.Contains(i, SymbolEqualityComparer.Default) && !t.Interfaces.Contains(i, SymbolEqualityComparer.Default)) continue;
            foreach (var im in i.GetMembers())
            {
                var impl = t.FindImplementationForInterfaceMember(im);
                if (impl == null || SymbolEqualityComparer.Default.Equals(impl.ContainingType, t)) continue;
                if (impl.ContainingType.AllInterfaces.Contains(i, SymbolEqualityComparer.Default) && impl.ContainingType.TypeKind == TypeKind.Class) continue;
                var owner = Names.TypeName(impl.ContainingType);
                if (im is IMethodSymbol mm && impl is IMethodSymbol implM && IsOrdinaryMethod(mm))
                {
                    var key = "m:" + mm.Name + mm.Parameters.Length;
                    if (!seen.Add(key)) continue;
                    var args = string.Join(", ", mm.Parameters.Select(ParamName));
                    var ret = mm.ReturnsVoid ? "" : "return ";
                    yield return new Stub($"{Names.ReturnType(mm)} {Names.MemberName(mm)}({Params(mm)}) override {{ {ret}{owner}::{Names.MemberName(implM)}({args}); }}", null);
                }
                else if (im is IPropertySymbol ip && impl is IPropertySymbol implP)
                {
                    var n = Names.MemberName(ip);
                    if (!seen.Add("p:" + n)) continue;
                    var getAcc = IsFieldLike(implP) ? $"this->{Names.MemberName(implP)}" : $"{owner}::get_{Names.MemberName(implP)}()";
                    if (ip.GetMethod != null) yield return new Stub($"{Names.TypeRef(ip.Type)} get_{n}() override {{ return {getAcc}; }}", null);
                    if (ip.SetMethod != null)
                    {
                        var setAcc = IsFieldLike(implP) ? $"this->{Names.MemberName(implP)} = value" : $"{owner}::set_{Names.MemberName(implP)}(value)";
                        yield return new Stub($"void set_{n}({Names.TypeRef(ip.Type)} value) override {{ {setAcc}; }}", null);
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------ impl_*.cpp

    void EmitImpl(List<INamedTypeSymbol> ordered)
    {
        var templates = new StringBuilder("#pragma once\n// Generated by Cs2Cpp. Do not edit.\n\n");
        var classes = ordered.Where(t => t.TypeKind != TypeKind.Interface).ToList();
        const int perFile = 12;
        int fileIndex = 0;
        for (int i = 0; i < classes.Count; i += perFile)
        {
            var sb = new StringBuilder("// Generated by Cs2Cpp. Do not edit.\n#include \"types.h\"\n\n");
            foreach (var t in classes.Skip(i).Take(perFile))
            {
                try { EmitTypeImpl(sb, templates, t); }
                catch (Exception ex) { Warnings.Add($"{t}: emitter error {ex.Message}"); }
            }
            File.WriteAllText(Path.Combine(outDir, $"impl_{fileIndex++:D3}.cpp"), sb.ToString());
        }
        // interfaces: __type
        var isb = new StringBuilder("// Generated by Cs2Cpp. Do not edit.\n#include \"types.h\"\n\n");
        foreach (var t in ordered.Where(t => t.TypeKind == TypeKind.Interface && !t.IsGenericType))
        {
            var full = Names.DefName(t);
            isb.Append($"::System::Type* {full}::__type() {{ static ::System::Type* t = ([]() {{ auto* t = ::System::Type::Create(u\"{t.Name}\", u\"{t.ToDisplayString()}\", nullptr); t->isInterface = true; t->isInstance = [](::System::Object* o) {{ return dynamic_cast<{full}*>(o) != nullptr; }}; return t; }})(); return t; }}\n");
        }
        // Default interface method bodies
        foreach (var t in ordered.Where(t => t.TypeKind == TypeKind.Interface && !t.IsGenericType))
            foreach (var m in t.GetMembers().OfType<IMethodSymbol>().Where(m => IsOrdinaryMethod(m) && !m.IsAbstract))
            {
                var syntax = m.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;
                SyntaxNode body = (SyntaxNode)syntax?.Body ?? syntax?.ExpressionBody;
                if (body == null) continue;
                var bw = new BodyWriter(this, t, m);
                isb.Append($"{Names.ReturnType(m)} {Names.DefName(t)}::{Names.MemberName(m)}({Params(m)}) ").Append(bw.MethodBody(body, m)).Append('\n');
                Warnings.AddRange(bw.Warnings);
            }
        File.WriteAllText(Path.Combine(outDir, $"impl_{fileIndex++:D3}.cpp"), isb.ToString());
        File.WriteAllText(Path.Combine(outDir, "templates.h"), templates.ToString());
    }

    void EmitTypeImpl(StringBuilder sb, StringBuilder templates, INamedTypeSymbol t)
    {
        var full = Names.DefName(t);
        bool isStruct = t.TypeKind == TypeKind.Struct;
        bool generic = t.IsGenericType;
        var outSb = generic ? templates : sb;
        var th = TemplateHeader(t);
        sb.Append($"// ---- {t.ToDisplayString()}\n");

        // Static field definitions
        foreach (var f in t.GetMembers().OfType<IFieldSymbol>().Where(f => f.IsStatic && !f.IsConst && f.AssociatedSymbol == null))
            outSb.Append($"{th}{Names.TypeRef(f.Type)} {full}::{Names.MemberName(f)}{{}};\n");
        foreach (var e in t.GetMembers().OfType<IEventSymbol>().Where(e => e.IsStatic))
            outSb.Append($"{th}{Names.TypeRef(e.Type)} {full}::{Names.MemberName(e)}{{}};\n");
        foreach (var p in t.GetMembers().OfType<IPropertySymbol>().Where(p => p.IsStatic))
        {
            if (IsFieldLike(p)) outSb.Append($"{th}{Names.TypeRef(p.Type)} {full}::{Names.MemberName(p)}{{}};\n");
            else if (IsAutoProperty(p)) outSb.Append($"{th}{Names.TypeRef(p.Type)} {full}::__b_{Names.MemberName(p)}{{}};\n");
        }

        // __cctor
        outSb.Append($"{th}void {full}::__cctor() {{\n");
        foreach (var (target, init) in StaticInitializers(t))
        {
            var bw = new BodyWriter(this, t, null);
            var code = bw.InitExpr(init, out var decls);
            foreach (var d in decls) outSb.Append("    ").Append(d).Append('\n');
            outSb.Append($"    {target} = {code};\n");
            Warnings.AddRange(bw.Warnings);
        }
        var cctor = t.StaticConstructors.FirstOrDefault(c => !c.IsImplicitlyDeclared);
        if (cctor != null && cctor.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is ConstructorDeclarationSyntax cds)
        {
            var bw = new BodyWriter(this, t, cctor);
            outSb.Append("    ").Append(bw.MethodBody((SyntaxNode)cds.Body ?? cds.ExpressionBody, cctor).Replace("\n", "\n    ")).Append('\n');
            Warnings.AddRange(bw.Warnings);
        }
        outSb.Append("}\n");

        // Type info
        var baseTypeExpr = isStruct ? "nullptr" : (t.BaseType == null || t.BaseType.SpecialType == SpecialType.System_Object ? "::System::Object::__type()" : Names.TypeName(t.BaseType) + "::__type()");
        var reflect = reflectTypes.Contains(t) ? $" t->reflect = &{full}::__reflect;" : "";
        var isInst = isStruct ? $"::System::isBoxed<{full}>(o)" : $"dynamic_cast<{full}*>(o) != nullptr";
        outSb.Append($"{th}::System::Type* {full}::__type() {{ static ::System::Type* t = ([]() {{ auto* t = ::System::Type::Create(u\"{t.Name}\", u\"{t.ToDisplayString()}\", {baseTypeExpr}); t->isValueType = {(isStruct ? "true" : "false")}; t->isInstance = [](::System::Object* o) {{ return {isInst}; }};{reflect} return t; }})(); return t; }}\n");
        if (!isStruct) outSb.Append($"{th}::System::Type* {full}::GetType() {{ return __type(); }}\n");
        if (reflectTypes.Contains(t)) EmitReflection(outSb, t, th);

        // Struct helpers
        if (isStruct)
        {
            var flat = Names.FlatName(t);
            bool hasTypedEquals = t.GetMembers("Equals").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, t));
            var fields = StructFields(t).ToList();
            if (!hasTypedEquals)
            {
                var objEquals = t.GetMembers("Equals").OfType<IMethodSymbol>().FirstOrDefault(m => m.Parameters.Length == 1 && m.Parameters[0].Type.SpecialType == SpecialType.System_Object);
                if (objEquals != null)
                    outSb.Append($"{th}bool {full}::Equals(const {flat}& o) const {{ return const_cast<{flat}*>(this)->Equals(::System::box(o)); }}\n");
                else
                {
                    var cmp = fields.Count == 0 ? "true" : string.Join(" && ", fields.Select(f => $"::System::Eq<{f.type}>::eq({f.name}, o.{f.name})"));
                    outSb.Append($"{th}bool {full}::Equals(const {flat}& o) const {{ return {cmp}; }}\n");
                }
            }
            if (!t.GetMembers("GetHashCode").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 0))
            {
                var h = fields.Count == 0 ? "0" : string.Join(" ^ ", fields.Select((f, i) => $"(::System::Eq<{f.type}>::hash({f.name}) * {31 + i * 2})"));
                outSb.Append($"{th}int32_t {full}::GetHashCode() const {{ return {h}; }}\n");
            }
            if (!t.GetMembers("ToString").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 0))
                outSb.Append($"{th}::System::String* {full}::ToString() const {{ return STR(u\"{t.ToDisplayString()}\"); }}\n");
        }

        // Constructors
        foreach (var c in t.InstanceConstructors)
        {
            try { EmitConstructor(outSb, t, c, th); }
            catch (Exception ex) { Warnings.Add($"{t}: ctor error {ex.Message}"); }
        }
        // Methods
        foreach (var m in t.GetMembers().OfType<IMethodSymbol>())
        {
            if (!IsOrdinaryMethod(m) && m.MethodKind is not (MethodKind.UserDefinedOperator or MethodKind.Conversion)) continue;
            if (m.IsAbstract) continue;
            if (SkipBody(m)) continue;
            var syntax = m.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
            SyntaxNode body = syntax switch
            {
                MethodDeclarationSyntax md => (SyntaxNode)md.Body ?? md.ExpressionBody,
                OperatorDeclarationSyntax od => (SyntaxNode)od.Body ?? od.ExpressionBody,
                ConversionOperatorDeclarationSyntax cd => (SyntaxNode)cd.Body ?? cd.ExpressionBody,
                _ => null,
            };
            var name = m.MethodKind is MethodKind.UserDefinedOperator or MethodKind.Conversion ? m.MetadataName : Names.MemberName(m);
            var target = m.IsGenericMethod ? templates : outSb;
            var mth = m.IsGenericMethod ? th + "template <" + string.Join(", ", m.TypeParameters.Select(tp => "class " + Names.Escape(tp.Name))) + ">\n" : th;
            target.Append($"{mth}{Names.ReturnType(m)} {full}::{name}({Params(m)}) ");
            var ov = OverrideBody(m);
            if (ov != null) { target.Append(ov).Append("\n"); continue; }
            if (body == null)
            {
                target.Append("{ ::System::ThrowInvalidOperation(\"extern method\"); }\n");
                continue;
            }
            var bw = new BodyWriter(this, t, m);
            try { target.Append(bw.MethodBody(body, m)).Append('\n'); }
            catch (Exception ex)
            {
                target.Append($"{{ /* TRANSLATION ERROR {ex.Message.Replace("*/", "")} */ ::System::ThrowInvalidOperation(\"untranslated\"); }}\n");
                Warnings.Add($"{MethodKey(m)}: {ex.Message}");
            }
            Warnings.AddRange(bw.Warnings);
        }
        // Properties
        foreach (var p in t.GetMembers().OfType<IPropertySymbol>())
        {
            if (IsFieldLike(p) || p.IsAbstract) continue;
            EmitPropertyImpl(outSb, t, p, th);
        }
    }

    IEnumerable<(string type, string name)> StructFields(INamedTypeSymbol t)
    {
        foreach (var f in t.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsStatic && !f.IsConst && f.AssociatedSymbol == null))
            yield return (Names.TypeRef(f.Type), Names.MemberName(f));
        foreach (var p in t.GetMembers().OfType<IPropertySymbol>().Where(p => !p.IsStatic && !p.IsIndexer))
        {
            if (IsFieldLike(p)) yield return (Names.TypeRef(p.Type), Names.MemberName(p));
            else if (IsAutoProperty(p)) yield return (Names.TypeRef(p.Type), "__b_" + Names.MemberName(p));
        }
    }

    // Static initializers in declaration order: (target, initializer).
    IEnumerable<(string, ExpressionSyntax)> StaticInitializers(INamedTypeSymbol t)
    {
        var full = Names.TypeName(t);
        var list = new List<(int, string, ExpressionSyntax, SyntaxTree)>();
        foreach (var m in t.GetMembers())
        {
            if (!m.IsStatic) continue;
            foreach (var r in m.DeclaringSyntaxReferences)
            {
                var syn = r.GetSyntax();
                if (m is IFieldSymbol f && !f.IsConst && f.AssociatedSymbol == null && syn is VariableDeclaratorSyntax vd && vd.Initializer != null)
                    list.Add((vd.SpanStart, $"{full}::{Names.MemberName(f)}", vd.Initializer.Value, syn.SyntaxTree));
                else if (m is IPropertySymbol p && syn is PropertyDeclarationSyntax pd && pd.Initializer != null)
                    list.Add((pd.SpanStart, $"{full}::{(IsFieldLike(p) ? Names.MemberName(p) : "__b_" + Names.MemberName(p))}", pd.Initializer.Value, syn.SyntaxTree));
                else if (m is IEventSymbol ev && syn is VariableDeclaratorSyntax evd && evd.Initializer != null)
                    list.Add((evd.SpanStart, $"{full}::{Names.MemberName(ev)}", evd.Initializer.Value, syn.SyntaxTree));
            }
        }
        return list.OrderBy(x => x.Item4.FilePath).ThenBy(x => x.Item1).Select(x => (x.Item2, x.Item3));
    }

    IEnumerable<(string, ExpressionSyntax)> InstanceInitializers(INamedTypeSymbol t)
    {
        var list = new List<(int, string, ExpressionSyntax, SyntaxTree)>();
        foreach (var m in t.GetMembers())
        {
            if (m.IsStatic) continue;
            foreach (var r in m.DeclaringSyntaxReferences)
            {
                var syn = r.GetSyntax();
                if (m is IFieldSymbol f && !f.IsConst && f.AssociatedSymbol == null && syn is VariableDeclaratorSyntax vd && vd.Initializer != null)
                    list.Add((vd.SpanStart, $"this->{Names.MemberName(f)}", vd.Initializer.Value, syn.SyntaxTree));
                else if (m is IPropertySymbol p && syn is PropertyDeclarationSyntax pd && pd.Initializer != null)
                    list.Add((pd.SpanStart, $"this->{(IsFieldLike(p) ? Names.MemberName(p) : "__b_" + Names.MemberName(p))}", pd.Initializer.Value, syn.SyntaxTree));
                else if (m is IEventSymbol ev && syn is VariableDeclaratorSyntax evd && evd.Initializer != null)
                    list.Add((evd.SpanStart, $"this->{Names.MemberName(ev)}", evd.Initializer.Value, syn.SyntaxTree));
            }
        }
        return list.OrderBy(x => x.Item4.FilePath).ThenBy(x => x.Item1).Select(x => (x.Item2, x.Item3));
    }

    void EmitConstructor(StringBuilder sb, INamedTypeSymbol t, IMethodSymbol c, string th)
    {
        var full = Names.DefName(t);
        bool isStruct = t.TypeKind == TypeKind.Struct;
        if (isStruct && c.IsImplicitlyDeclared) return;
        if (isStruct && c.Parameters.Length == 0) return;
        var syn = c.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as ConstructorDeclarationSyntax;
        var bw = new BodyWriter(this, t, c);
        bool chainsThis = syn?.Initializer != null && syn.Initializer.IsKind(SyntaxKind.ThisConstructorInitializer);
        var ov = OverrideBody(c);
        if (isStruct)
        {
            sb.Append($"{th}{full}::{Names.FlatName(t)}({Params(c)})");
            if (chainsThis)
            {
                var args = bw.ConstructorInitArgs(syn.Initializer, out _, out var d0);
                sb.Append($" : {Names.FlatName(t)}({args})");
            }
            sb.Append(' ');
        }
        else sb.Append($"{th}void {full}::__init({Params(c)}) ");
        if (ov != null) { sb.Append(ov).Append('\n'); return; }
        var pre = new StringBuilder();
        if (!chainsThis)
        {
            foreach (var (target, init) in InstanceInitializers(t))
            {
                var fw = new BodyWriter(this, t, c);
                var code = fw.InitExpr(init, out var decls);
                foreach (var d in decls) pre.Append("    ").Append(d).Append('\n');
                pre.Append($"    {target} = {code};\n");
                Warnings.AddRange(fw.Warnings);
            }
        }
        if (!isStruct)
        {
            if (syn?.Initializer != null)
            {
                var args = bw.ConstructorInitArgs(syn.Initializer, out var target, out var decls);
                foreach (var d in decls) pre.Append("    ").Append(d).Append('\n');
                if (chainsThis) pre.Append($"    {full}::__init({args});\n");
                else pre.Append($"    {BaseCppName(t)}::__init({args});\n");
            }
            else if (t.BaseType != null && t.BaseType.SpecialType != SpecialType.System_Object)
                pre.Append($"    {BaseCppName(t)}::__init();\n");
        }
        string body = "{\n}";
        if (syn != null && (syn.Body != null || syn.ExpressionBody != null))
            body = bw.MethodBody((SyntaxNode)syn.Body ?? syn.ExpressionBody, c);
        // insert prologue after the opening brace (and after boxed-param setup)
        var idx = body.IndexOf('\n') + 1;
        sb.Append(body.Substring(0, idx)).Append(pre).Append(body.Substring(idx)).Append('\n');
        Warnings.AddRange(bw.Warnings);
    }

    void EmitPropertyImpl(StringBuilder sb, INamedTypeSymbol t, IPropertySymbol p, string th)
    {
        var full = Names.DefName(t);
        var n = p.IsIndexer ? "Item" : Names.MemberName(p);
        var ip = p.IsIndexer ? string.Join(", ", p.Parameters.Select(x => Names.ParamType(x) + " " + ParamName(x))) : "";
        var syn = p.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as BasePropertyDeclarationSyntax;
        if (syn == null) return;
        if (IsAutoProperty(p))
        {
            if (p.GetMethod != null) sb.Append($"{th}{Names.TypeRef(p.Type)} {full}::get_{n}() {{ return __b_{n}; }}\n");
            if (p.SetMethod != null) sb.Append($"{th}void {full}::set_{n}({Names.TypeRef(p.Type)} value) {{ __b_{n} = value; }}\n");
            return;
        }
        ArrowExpressionClauseSyntax arrow = syn switch
        {
            PropertyDeclarationSyntax pd => pd.ExpressionBody,
            IndexerDeclarationSyntax id => id.ExpressionBody,
            _ => null,
        };
        if (arrow != null && p.GetMethod != null)
        {
            var bw = new BodyWriter(this, t, p.GetMethod);
            sb.Append($"{th}{Names.TypeRef(p.Type)} {full}::get_{n}({ip}) ").Append(bw.MethodBody(arrow, p.GetMethod)).Append('\n');
            Warnings.AddRange(bw.Warnings);
            return;
        }
        foreach (var acc in syn.AccessorList?.Accessors ?? default)
        {
            bool isGet = acc.IsKind(SyntaxKind.GetAccessorDeclaration);
            var m = isGet ? p.GetMethod : p.SetMethod;
            if (m == null) continue;
            var sig = isGet ? $"{Names.TypeRef(p.Type)} {full}::get_{n}({ip})" : $"void {full}::set_{n}({ip}{(ip.Length > 0 ? ", " : "")}{Names.TypeRef(p.Type)} value)";
            SyntaxNode body = (SyntaxNode)acc.Body ?? acc.ExpressionBody;
            sb.Append(th).Append(sig).Append(' ');
            if (body == null) { sb.Append("{ }\n"); continue; }
            var bw = new BodyWriter(this, t, m);
            sb.Append(bw.MethodBody(body, m)).Append('\n');
            Warnings.AddRange(bw.Warnings);
        }
    }

    void EmitReflection(StringBuilder sb, INamedTypeSymbol t, string th)
    {
        var full = Names.DefName(t);
        sb.Append($"{th}void {full}::__reflect(::System::Type* t) {{\n");
        if (!t.IsAbstract && !t.IsStatic)
        {
            var ctors = new List<string>();
            foreach (var c in t.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public))
            {
                var ps = c.Parameters.Select(p =>
                    $"::System::Reflection::MakeParam(u\"{p.Name}\", ::System::TypeOf<{Names.TypeName(p.Type)}>(), {(p.HasExplicitDefaultValue ? "true" : "false")}, {(p.HasExplicitDefaultValue ? BoxLiteral(p) : "nullptr")})");
                var args = c.Parameters.Select((p, i) => Names.IsRefType(p.Type) ? $"::System::cast<{Names.TypeName(p.Type)}>(a->at({i}))" : $"::System::unbox<{Names.TypeRef(p.Type)}>(a->at({i}))");
                ctors.Add($"::System::Reflection::MakeCtor({{{string.Join(", ", ps)}}}, [](::System::Array1<::System::Object*>* a) -> ::System::Object* {{ return ::System::New<{full}>({string.Join(", ", args)}); }})");
            }
            sb.Append($"    t->ctors = ::System::Array1<::System::Reflection::ConstructorInfo*>::Of({{{string.Join(",\n        ", ctors)}}});\n");
        }
        var fields = new List<string>();
        foreach (var f in t.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsConst && f.AssociatedSymbol == null && f.DeclaredAccessibility == Accessibility.Public))
        {
            var ft = Names.TypeRef(f.Type);
            string get, set;
            var n = Names.MemberName(f);
            if (f.IsStatic)
            {
                get = $"[](::System::Object*) -> ::System::Object* {{ return ::System::box({full}::{n}); }}";
                set = Names.IsRefType(f.Type) ? $"[](::System::Object*, ::System::Object* v) {{ {full}::{n} = ::System::cast<{Names.TypeName(f.Type)}>(v); }}" : $"[](::System::Object*, ::System::Object* v) {{ {full}::{n} = ::System::unbox<{ft}>(v); }}";
            }
            else
            {
                get = $"[](::System::Object* o) -> ::System::Object* {{ return ::System::box(::System::cast<{full}>(o)->{n}); }}";
                set = Names.IsRefType(f.Type) ? $"[](::System::Object* o, ::System::Object* v) {{ ::System::cast<{full}>(o)->{n} = ::System::cast<{Names.TypeName(f.Type)}>(v); }}" : $"[](::System::Object* o, ::System::Object* v) {{ ::System::cast<{full}>(o)->{n} = ::System::unbox<{ft}>(v); }}";
            }
            fields.Add($"::System::Reflection::MakeField(u\"{f.Name}\", ::System::TypeOf<{Names.TypeName(f.Type)}>(), {(f.IsStatic ? "true" : "false")}, {get}, {set})");
        }
        sb.Append($"    t->fields = ::System::Array1<::System::Reflection::FieldInfo*>::Of({{{string.Join(",\n        ", fields)}}});\n");
        sb.Append("}\n");
    }

    static string BoxLiteral(IParameterSymbol p)
    {
        if (p.ExplicitDefaultValue == null) return "nullptr";
        return $"::System::box({Names.Literal(p.ExplicitDefaultValue, p.Type)})";
    }

    // ------------------------------------------------------------------ init.cpp

    void EmitInit()
    {
        var order = StaticInitOrder();
        var sb = new StringBuilder("// Generated by Cs2Cpp. Do not edit.\n#include \"types.h\"\n\nvoid __RunStaticInit() {\n");
        foreach (var t in order) sb.Append($"    {Names.TypeName(t)}::__cctor();\n");
        sb.Append("}\n");
        File.WriteAllText(Path.Combine(outDir, "init.cpp"), sb.ToString());
    }

    // Types whose static initializers read other types' statics must run after them.
    List<INamedTypeSymbol> StaticInitOrder()
    {
        var classes = ClassLike.Where(t => t.TypeKind != TypeKind.Interface && !t.IsGenericType).ToList();
        var deps = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
        var methodMemo = new Dictionary<ISymbol, HashSet<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
        foreach (var t in classes)
        {
            var set = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var nodes = new List<SyntaxNode>();
            nodes.AddRange(StaticInitializers(t).Select(x => (SyntaxNode)x.Item2));
            foreach (var c in t.StaticConstructors)
                foreach (var r in c.DeclaringSyntaxReferences) nodes.Add(r.GetSyntax());
            foreach (var n in nodes) CollectStaticDeps(n, set, methodMemo, 0);
            set.Remove(t);
            deps[t] = set;
        }
        var result = new List<INamedTypeSymbol>();
        var state = new Dictionary<INamedTypeSymbol, int>(SymbolEqualityComparer.Default);
        void Visit(INamedTypeSymbol t)
        {
            if (state.TryGetValue(t, out var s)) return;
            state[t] = 1;
            if (deps.TryGetValue(t, out var ds))
                foreach (var d in ds) Visit(d);
            state[t] = 2;
            result.Add(t);
        }
        foreach (var t in classes) Visit(t);
        return result.Where(t => classes.Contains(t, SymbolEqualityComparer.Default)).ToList();
    }

    void CollectStaticDeps(SyntaxNode node, HashSet<INamedTypeSymbol> set, Dictionary<ISymbol, HashSet<INamedTypeSymbol>> memo, int depth)
    {
        if (depth > 8) return;
        var model = Comp.GetSemanticModel(node.SyntaxTree);
        foreach (var n in node.DescendantNodesAndSelf())
        {
            ISymbol s = n switch
            {
                IdentifierNameSyntax or GenericNameSyntax => model.GetSymbolInfo(n).Symbol,
                BaseObjectCreationExpressionSyntax => model.GetSymbolInfo(n).Symbol,
                _ => null,
            };
            if (s == null) continue;
            s = s.OriginalDefinition;
            if (!Names.IsSource(s, Comp)) continue;
            if (s is IFieldSymbol { IsStatic: true, IsConst: false } or IPropertySymbol { IsStatic: true } or IEventSymbol { IsStatic: true })
                set.Add(s.ContainingType.OriginalDefinition);
            if (s is IMethodSymbol m && (m.MethodKind is MethodKind.Ordinary or MethodKind.Constructor or MethodKind.PropertyGet) || s is IPropertySymbol)
            {
                ISymbol key = s;
                if (s is IMethodSymbol { IsStatic: true } sm) set.Add(sm.ContainingType.OriginalDefinition);
                if (!memo.TryGetValue(key, out var sub))
                {
                    sub = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                    memo[key] = sub;
                    var targets = s is IPropertySymbol ps ? new ISymbol[] { ps.GetMethod } : new[] { s };
                    foreach (var tsym in targets.Where(x => x != null))
                        foreach (var r in tsym.DeclaringSyntaxReferences)
                            CollectStaticDeps(r.GetSyntax(), sub, memo, depth + 1);
                    if (s is IMethodSymbol { MethodKind: MethodKind.Constructor } ctor)
                        foreach (var (_, init) in InstanceInitializers(ctor.ContainingType))
                            CollectStaticDeps(init, sub, memo, depth + 1);
                }
                set.UnionWith(sub);
            }
        }
    }
}
