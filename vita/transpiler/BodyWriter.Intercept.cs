using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cs2Cpp;

partial class BodyWriter
{
    static bool IsPrimitive(ITypeSymbol t) => t != null && (t.SpecialType is >= SpecialType.System_Boolean and <= SpecialType.System_Double);

    // Calls that need a different shape in C++.
    string Intercept(InvocationExpressionSyntax inv, IMethodSymbol m, IInvocationOperation op)
    {
        var ct = m.ContainingType?.ToDisplayString();
        ExpressionSyntax recvSyntax = inv.Expression is MemberAccessExpressionSyntax ma ? ma.Expression : null;
        ITypeSymbol recvType = recvSyntax != null ? model.GetTypeInfo(recvSyntax).Type : (inv.Expression is MemberBindingExpressionSyntax ? bindingType : null);
        string Recv() => recvSyntax != null ? ExprNoConv(recvSyntax) : inv.Expression is MemberBindingExpressionSyntax ? bindingReceiver : (type.IsValueType ? "(*this)" : "this");
        string A() => op != null ? Args(op.Arguments, m) : "";

        if (TryEnumGetValues(inv, out var ev)) return ev;

        if (m.IsStatic)
        {
            switch (ct)
            {
                case "System.GC":
                    // GetTotalMemory reports bytes allocated since startup (used for load profiling).
                    // Collect(generation) and the no-GC region drive the port's collection policy (rt/core.cpp).
                    return m.Name switch
                    {
                        "Collect" when m.Parameters.Length == 0 => "::rt::Collect()",
                        "Collect" => $"::rt::CollectGeneration({A()})",
                        "TryStartNoGCRegion" => $"::rt::BeginNoGC({A()})",
                        "GetTotalMemory" => "::rt::AllocatedBytes()",
                        _ => "0"
                    };
                case "System.Enum" when m.Name == "Parse" || m.Name == "TryParse":
                    break;
                case "System.String" when m.Name == "Concat":
                {
                    var args = inv.ArgumentList.Arguments.Select(a => StrOperand(a.Expression)).ToList();
                    if (args.Count == 1) return $"::System::String::Join(::System::String::Empty, {Expr(inv.ArgumentList.Arguments[0].Expression)})";
                    return $"::System::String::ConcatN({{{string.Join(", ", args)}}})";
                }
                case "System.String" when m.Name == "Format":
                    return $"::System::String::Format({A()})";
                case "System.Object" when m.Name == "Equals" && m.Parameters.Length == 2:
                    return $"::System::Object::Equals({A()})";
            }
            return null;
        }

        // Instance calls on value-type/primitive/enum receivers.
        var rt = recvType ?? m.ContainingType;
        bool valueRecv = rt != null && !Names.IsRefType(rt) && rt is not ITypeParameterSymbol;
        if (m.Name == "GetType" && m.Parameters.Length == 0)
        {
            if (valueRecv) return $"::System::TypeOf<{Names.TypeName(rt)}>()";
            if (rt != null && rt.TypeKind == TypeKind.Interface) return $"::System::AsObject({Recv()})->GetType()";
            return null;
        }
        if (m.Name == "ToString" && valueRecv && !IsNullable(rt))
        {
            bool userDefined = rt.TypeKind == TypeKind.Struct && !IsPrimitive(rt) && m.ContainingType.Equals(rt, SymbolEqualityComparer.Default) && !m.IsOverride && false;
            if (m.Parameters.Length == 0)
            {
                if (rt.TypeKind == TypeKind.Struct && !IsPrimitive(rt) && SymbolEqualityComparer.Default.Equals(m.ContainingType, rt))
                    return null;
                return $"::System::ToStr({Recv()})";
            }
            var first = m.Parameters[0].Type;
            if (IsString(first)) return $"::System::Fmt({Recv()}, {Expr(inv.ArgumentList.Arguments[0].Expression)})";
            return $"::System::ToStr({Recv()})";
        }
        if (m.Name == "ToString" && rt != null && rt.TypeKind == TypeKind.Interface)
            return $"::System::AsObject({Recv()})->ToString()";
        if (m.Name == "Equals" && m.Parameters.Length == 1 && valueRecv && (IsPrimitive(rt) || rt.TypeKind == TypeKind.Enum))
        {
            var arg = inv.ArgumentList.Arguments[0].Expression;
            var at = model.GetTypeInfo(arg).Type;
            if (SymbolEqualityComparer.Default.Equals(at, rt))
                return $"::System::Eq<{Names.TypeRef(rt)}>::eq({Recv()}, {ExprNoConv(arg)})";
            return $"::System::box({Recv()})->Equals({Expr(arg)})";
        }
        if (m.Name == "GetHashCode" && m.Parameters.Length == 0 && valueRecv && (IsPrimitive(rt) || rt.TypeKind == TypeKind.Enum))
            return $"::System::Eq<{Names.TypeRef(rt)}>::hash({Recv()})";
        if (ct == "System.Enum" && m.Name == "HasFlag")
        {
            var et = rt;
            var u = et.TypeKind == TypeKind.Enum ? Names.EnumUnderlying((INamedTypeSymbol)et) : "int64_t";
            var f = Tmp("f");
            return $"({{ auto {f} = static_cast<{u}>({ExprNoConv(inv.ArgumentList.Arguments[0].Expression)}); (static_cast<{u}>({Recv()}) & {f}) == {f}; }})";
        }
        if (ct == "System.Enum" && m.Name == "CompareTo")
            return $"::System::Int64::CompareTo((int64_t){Recv()}, (int64_t)::System::unbox<int64_t>({Expr(inv.ArgumentList.Arguments[0].Expression)}))";
        if (IsPrimitive(rt) && valueRecv)
        {
            // int.CompareTo(x) -> Int32::CompareTo(recv, x)
            var a = A();
            return $"{StaticTypeName(rt)}::{Names.MemberName(m)}({Recv()}{(a.Length > 0 ? ", " + a : "")})";
        }
        if (rt != null && rt.TypeKind == TypeKind.Enum && (m.Name == "Equals" || m.Name == "GetHashCode"))
            return null;
        return null;
    }
}
