using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace Cs2Cpp;

// Maps C# symbols to C++ spellings. Reference types become pointers; nested types are flattened as Outer__Inner.
static class Names
{
    static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "alignas","alignof","and","and_eq","asm","auto","bitand","bitor","compl","concept","consteval","constexpr",
        "constinit","const_cast","co_await","co_return","co_yield","decltype","delete","dynamic_cast","export","extern",
        "friend","inline","mutable","noexcept","not","not_eq","nullptr","or","or_eq","register","reinterpret_cast",
        "requires","signed","static_assert","static_cast","template","thread_local","typeid","typename","union",
        "unsigned","xor","xor_eq","wchar_t","char8_t","char16_t","char32_t","NULL","errno","assert",
        "stdin","stdout","stderr","EOF","TRUE","FALSE","DEBUG","linux","unix","major","minor","environ",
        "int8_t","int16_t","int32_t","int64_t","uint8_t","uint16_t","uint32_t","uint64_t","size_t",
        "ptrdiff_t","INFINITY","NAN","M_PI","HUGE_VAL","FLT_MAX","INT_MAX","INT_MIN","RAND_MAX","BUFSIZ",
        "unix__","vita","__unused","class","new","this","throw","try","catch","operator","private","protected","public","virtual","void","volatile","while","bool","break","case","char","const","continue","default","do","double","else","enum","explicit","false","float","for","goto","if","int","long","namespace","return","short","sizeof","static","struct","switch","true","typedef","using","char16_t","double","int","float","long","short","bool","delete","goto",
    };

    public static string Escape(string id)
    {
        if (Reserved.Contains(id) || Regex.IsMatch(id, "^_[A-Z]") || id.StartsWith("__")) return id + "_";
        if (id.StartsWith("@")) return Escape(id.Substring(1));
        return id;
    }

    public static string MemberName(ISymbol s)
    {
        var n = s.Name;
        int dot = n.LastIndexOf('.');
        if (dot >= 0) n = n.Substring(dot + 1);
        return Escape(n);
    }

    public static bool IsSource(ISymbol s, Compilation c) => SymbolEqualityComparer.Default.Equals(s.ContainingAssembly, c.Assembly);

    public static bool IsRefType(ITypeSymbol t)
    {
        if (t == null) return false;
        if (t is ITypeParameterSymbol tp)
            return false;  // template parameter already includes the pointer
        if (t.TypeKind == TypeKind.Dynamic) return true;
        if (t.IsTupleType) return false;
        return t.IsReferenceType;
    }

    public static bool IsTypeParamRef(ITypeSymbol t) =>
        t is ITypeParameterSymbol tp && (tp.HasReferenceTypeConstraint || tp.ConstraintTypes.Any(c => c.IsReferenceType));

    // Type as used for variables/fields/params (pointer for reference types).
    public static string TypeRef(ITypeSymbol t)
    {
        if (t is ITypeParameterSymbol) return Escape(t.Name);
        var n = TypeName(t);
        return IsRefType(t) ? n + "*" : n;
    }

    public static string NamespacePrefix(INamespaceSymbol ns)
    {
        if (ns == null || ns.IsGlobalNamespace) return "::";
        var parts = new Stack<string>();
        for (var n = ns; n != null && !n.IsGlobalNamespace; n = n.ContainingNamespace) parts.Push(Escape(n.Name));
        return "::" + string.Join("::", parts) + "::";
    }

    // Flattened C++ identifier of a named type without namespace (Outer__Inner).
    public static string FlatName(INamedTypeSymbol t)
    {
        var name = Escape(t.Name);
        for (var o = t.ContainingType; o != null; o = o.ContainingType) name = Escape(o.Name) + "__" + name;
        return name;
    }

    static IEnumerable<ITypeSymbol> AllTypeArgs(INamedTypeSymbol t)
    {
        var chain = new List<INamedTypeSymbol>();
        for (var o = t; o != null; o = o.ContainingType) chain.Insert(0, o);
        foreach (var c in chain)
            foreach (var a in c.TypeArguments) yield return a;
    }

    // Type name without pointer.
    public static string TypeName(ITypeSymbol t)
    {
        switch (t.SpecialType)
        {
            case SpecialType.System_Object: return "::System::Object";
            case SpecialType.System_String: return "::System::String";
            case SpecialType.System_Boolean: return "bool";
            case SpecialType.System_Char: return "char16_t";
            case SpecialType.System_SByte: return "int8_t";
            case SpecialType.System_Byte: return "uint8_t";
            case SpecialType.System_Int16: return "int16_t";
            case SpecialType.System_UInt16: return "uint16_t";
            case SpecialType.System_Int32: return "int32_t";
            case SpecialType.System_UInt32: return "uint32_t";
            case SpecialType.System_Int64: return "int64_t";
            case SpecialType.System_UInt64: return "uint64_t";
            case SpecialType.System_Single: return "float";
            case SpecialType.System_Double: return "double";
            case SpecialType.System_Decimal: return "double";
            case SpecialType.System_Void: return "void";
            case SpecialType.System_IntPtr: return "intptr_t";
            case SpecialType.System_UIntPtr: return "uintptr_t";
            case SpecialType.System_Array: return "::System::Array";
            case SpecialType.System_Enum: return "int32_t";
        }
        if (t.TypeKind == TypeKind.Dynamic) return "::System::Object";
        if (t is ITypeParameterSymbol tp) return $"std::remove_pointer_t<{Escape(tp.Name)}>";
        if (t is IArrayTypeSymbol at)
            return $"::System::Array{at.Rank}<{TypeRef(at.ElementType)}>";
        if (t is INamedTypeSymbol nt)
        {
            if (nt.IsTupleType)
                return "::System::ValueTuple<" + string.Join(", ", nt.TupleElements.Select(e => TypeRef(e.Type))) + ">";
            if (nt.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                return $"::System::Nullable<{TypeRef(nt.TypeArguments[0])}>";
            if (nt.TypeKind == TypeKind.Delegate)
                return DelegateSig(nt);
            var full = nt.OriginalDefinition.ToDisplayString();
            switch (full)
            {
                case "System.Collections.IEnumerable": return "::System::Collections::IEnumerable";
                case "System.IComparable<T>": return $"::System::IComparableT<{TypeRef(nt.TypeArguments[0])}>";
                case "System.IEquatable<T>": return $"::System::IEquatable<{TypeRef(nt.TypeArguments[0])}>";
            }
            var sb = new StringBuilder();
            sb.Append(NamespacePrefix(nt.ContainingNamespace));
            sb.Append(FlatName(nt));
            var args = AllTypeArgs(nt).ToList();
            if (args.Count > 0) sb.Append('<').Append(string.Join(", ", args.Select(TypeRef))).Append('>');
            return sb.ToString();
        }
        if (t is IPointerTypeSymbol pt) return TypeRef(pt.PointedAtType) + "*";
        return "/*?" + t.ToDisplayString() + "*/void";
    }

    public static string DelegateSig(INamedTypeSymbol d)
    {
        var inv = d.DelegateInvokeMethod;
        return $"::System::Fn<{ReturnType(inv)}({string.Join(", ", inv.Parameters.Select(ParamType))})>";
    }

    public static string ReturnType(IMethodSymbol m) => m.ReturnsVoid ? "void" : TypeRef(m.ReturnType) + (m.RefKind != RefKind.None ? "&" : "");

    public static string ParamType(IParameterSymbol p)
    {
        var t = TypeRef(p.Type);
        return p.RefKind is RefKind.Ref or RefKind.Out ? t + "&" : t;
    }

    // Name for out-of-class definitions (a leading :: would merge with the preceding type).
    public static string DefName(ITypeSymbol t) => TypeName(t).TrimStart(':');

    public static string EnumUnderlying(INamedTypeSymbol e) => TypeName(e.EnumUnderlyingType);

    // C++ literal for a constant of the given type.
    public static string Literal(object v, ITypeSymbol t)
    {
        if (t != null && t.TypeKind == TypeKind.Enum && v != null)
            return $"static_cast<{TypeName(t)}>({Convert.ToInt64(v)})";
        if (t is INamedTypeSymbol nn && nn.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return v == null ? $"{TypeName(t)}()" : $"{TypeName(t)}({Literal(v, nn.TypeArguments[0])})";
        if (v == null)
        {
            if (t == null) return "nullptr";
            if (IsRefType(t)) return $"static_cast<{TypeRef(t)}>(nullptr)";
            if (t is ITypeParameterSymbol) return $"{TypeRef(t)}{{}}";
            return $"{TypeName(t)}{{}}";
        }
        var st = t?.SpecialType ?? SpecialType.None;
        switch (v)
        {
            case string s: return $"STR(u{StringLit(s)})";
            case bool b: return b ? "true" : "false";
            case char c:
                if (st is SpecialType.System_Char or SpecialType.None) return CharLit(c);
                return Literal((int)c, t);
        }
        switch (st)
        {
            case SpecialType.System_Single: return FloatLit(Convert.ToSingle(v));
            case SpecialType.System_Double: return DoubleLit(Convert.ToDouble(v));
            case SpecialType.System_Decimal: return DoubleLit(Convert.ToDouble(v));
            case SpecialType.System_Char: return CharLit((char)Convert.ToInt32(v));
            case SpecialType.System_Boolean: return Convert.ToBoolean(v) ? "true" : "false";
            case SpecialType.System_Int64: return $"INT64_C({Convert.ToInt64(v)})";
            case SpecialType.System_UInt64: return $"UINT64_C({Convert.ToUInt64(v)})";
            case SpecialType.System_UInt32: return $"{Convert.ToUInt32(v)}u";
            case SpecialType.System_Int32:
            {
                long l = Convert.ToInt64(v);
                return l == int.MinValue ? "(-2147483647 - 1)" : l.ToString();
            }
            case SpecialType.System_Byte: return $"(uint8_t){Convert.ToInt64(v)}";
            case SpecialType.System_SByte: return $"(int8_t){Convert.ToInt64(v)}";
            case SpecialType.System_Int16: return $"(int16_t){Convert.ToInt64(v)}";
            case SpecialType.System_UInt16: return $"(uint16_t){Convert.ToInt64(v)}";
            case SpecialType.System_Object:
                return $"::System::box({Literal(v, null)})";
        }
        return v switch
        {
            float f => FloatLit(f),
            double d => DoubleLit(d),
            long l => $"INT64_C({l})",
            ulong ul => $"UINT64_C({ul})",
            uint u => $"{u}u",
            int i => i == int.MinValue ? "(-2147483647 - 1)" : i.ToString(),
            _ => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    public static string FloatLit(float f)
    {
        if (float.IsNaN(f)) return "std::numeric_limits<float>::quiet_NaN()";
        if (float.IsPositiveInfinity(f)) return "std::numeric_limits<float>::infinity()";
        if (float.IsNegativeInfinity(f)) return "(-std::numeric_limits<float>::infinity())";
        var s = f.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (!s.Contains('.') && !s.Contains('E')) s += ".0";
        if (s.Contains('E') && !s.Contains('.')) s = s.Replace("E", ".0E");
        return s + "f";
    }

    public static string DoubleLit(double d)
    {
        if (double.IsNaN(d)) return "std::numeric_limits<double>::quiet_NaN()";
        if (double.IsPositiveInfinity(d)) return "std::numeric_limits<double>::infinity()";
        if (double.IsNegativeInfinity(d)) return "(-std::numeric_limits<double>::infinity())";
        var s = d.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (!s.Contains('.') && !s.Contains('E')) s += ".0";
        if (s.Contains('E') && !s.Contains('.')) s = s.Replace("E", ".0E");
        return s;
    }

    public static string CharLit(char c)
    {
        if (c == '\'') return "u'\\''";
        if (c == '\\') return "u'\\\\'";
        if (c >= 0x20 && c < 0x7f) return $"u'{c}'";
        return $"(char16_t)0x{(int)c:X4}";
    }

    public static string StringLit(string s)
    {
        var sb = new StringBuilder("\"");
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '?': sb.Append("\\?"); break;
                default:
                    if (c >= 0x20 && c < 0x7f) sb.Append(c);
                    else if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                    {
                        sb.Append($"\\U{char.ConvertToUtf32(c, s[i + 1]):X8}");
                        i++;
                    }
                    else if (c >= 0xD800 && c <= 0xDFFF) sb.Append("\\uFFFD");
                    else if (c < 0x20 || c == 0x7f) sb.Append($"\\x{(int)c:X2}\"\"");
                    else sb.Append($"\\u{(int)c:X4}");
                    break;
            }
        }
        return sb.Append('"').ToString();
    }
}
