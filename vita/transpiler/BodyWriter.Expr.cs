using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cs2Cpp;

partial class BodyWriter
{
    // Expression with its implicit conversion (to the converted type) applied.
    public string Expr(ExpressionSyntax e)
    {
        var ti = model.GetTypeInfo(e);
        var cv = model.GetConstantValue(e);
        var to = ti.ConvertedType ?? ti.Type;
        if (cv.HasValue && !(e is InterpolatedStringExpressionSyntax && cv.Value == null))
        {
            if (to != null && (to.SpecialType == SpecialType.System_Object || to.TypeKind == TypeKind.Interface) && cv.Value != null)
                return $"::System::box({Names.Literal(cv.Value, ti.Type)})";
            if (to != null && to.TypeKind == TypeKind.Enum && cv.Value != null && model.GetSymbolInfo(e).Symbol is IFieldSymbol ef && ef.ContainingType.TypeKind == TypeKind.Enum
                && SymbolEqualityComparer.Default.Equals(ef.ContainingType, to))
                return $"{Names.TypeName(to)}::{Names.Escape(ef.Name)}";
            return Names.Literal(cv.Value, to);
        }
        var raw = ExprNoConv(e);
        var conv = model.GetConversion(e);
        if (ti.Type == null || ti.ConvertedType == null || conv.IsIdentity) return raw;
        return ConvertCode(raw, ti.Type, ti.ConvertedType, conv, false, e);
    }

    // Code for converting `code` of type `from` to `to`.
    string ConvertCode(string code, ITypeSymbol from, ITypeSymbol to, Conversion conv, bool isExplicit, SyntaxNode node = null)
    {
        if (from == null || to == null) return code;
        if (SymbolEqualityComparer.Default.Equals(from, to)) return code;
        var toRef = Names.TypeRef(to);
        if (conv.IsUserDefined && conv.MethodSymbol != null)
        {
            var m = conv.MethodSymbol;
            if (Names.IsSource(m, comp))
                return $"{Names.TypeName(m.ContainingType)}::{m.MetadataName}({code})";
            return $"static_cast<{toRef}>({code})";
        }
        if (conv.IsNullLiteral || conv.IsDefaultLiteral) return Names.Literal(null, to);
        if (conv.IsBoxing) return $"::System::box({code})";
        if (conv.IsUnboxing) return $"::System::unbox<{toRef}>({code})";
        if (conv.IsNullable)
        {
            if (IsNullable(to) && !IsNullable(from))
            {
                var inner = ((INamedTypeSymbol)to).TypeArguments[0];
                var ic = SymbolEqualityComparer.Default.Equals(inner, from) ? code : $"static_cast<{Names.TypeRef(inner)}>({code})";
                return $"{Names.TypeName(to)}({ic})";
            }
            if (IsNullable(from) && !IsNullable(to)) return $"static_cast<{toRef}>(({code}).get_Value())";
            if (IsNullable(from) && IsNullable(to))
            {
                var ti2 = ((INamedTypeSymbol)to).TypeArguments[0];
                var t = Tmp();
                return $"({{ auto {t} = {code}; {t}.hasValue ? {Names.TypeName(to)}(static_cast<{Names.TypeRef(ti2)}>({t}.value)) : {Names.TypeName(to)}(); }})";
            }
        }
        if (conv.IsNumeric || conv.IsEnumeration || conv.IsConstantExpression && !Names.IsRefType(to))
            return $"static_cast<{toRef}>({code})";
        if (conv.IsTupleLiteralConversion || conv.IsTupleConversion)
        {
            var ft = (INamedTypeSymbol)from;
            var tt = (INamedTypeSymbol)to;
            if (ft.IsTupleType && tt.IsTupleType)
            {
                var t = Tmp();
                var parts = tt.TupleElements.Select((el, i) => $"static_cast<{Names.TypeRef(el.Type)}>({t}.Item{i + 1})");
                return $"({{ auto {t} = {code}; {Names.TypeName(to)}{{{string.Join(", ", parts)}}}; }})";
            }
        }
        if (conv.IsReference)
        {
            if (from.TypeKind == TypeKind.Array && to.TypeKind == TypeKind.Interface)
                return $"::System::AsEnumerable({code})";
            if (IsString(from) && to.TypeKind == TypeKind.Interface)
                return $"({{ auto* __se = new ::System::StringEnumerable(); __se->s = {code}; static_cast<{toRef}>(__se); }})";
            if (from.TypeKind == TypeKind.Interface && (to.SpecialType == SpecialType.System_Object))
                return $"::System::AsObject({code})";
            if (from is ITypeParameterSymbol)
                return $"::System::cast<{Names.TypeName(to)}>({code})";
            if (to is ITypeParameterSymbol tp)
                return $"::System::cast<std::remove_pointer_t<{Names.Escape(tp.Name)}>>({code})";
            if (conv.IsImplicit)
            {
                if (from.TypeKind == TypeKind.Delegate && to.TypeKind == TypeKind.Delegate) return code;
                if (from.TypeKind == TypeKind.Interface && to.TypeKind == TypeKind.Interface) return $"dynamic_cast<{toRef}>({code})";
                if (from.TypeKind == TypeKind.Interface) return $"dynamic_cast<{toRef}>({code})";
                return $"static_cast<{toRef}>({code})";
            }
            return $"::System::cast<{Names.TypeName(to)}>({code})";
        }
        if (conv.IsMethodGroup) return code;  // handled by ExprNoConv via converted type
        if (conv.IsAnonymousFunction) return code;
        if (conv.IsImplicit && from is ITypeParameterSymbol && to.SpecialType == SpecialType.System_Object)
            return $"::System::box({code})";
        if (conv.IsIdentity) return code;
        if (conv.IsThrow) return code;
        if (conv.IsConditionalExpression || conv.IsSwitchExpression || conv.IsObjectCreation) return code;
        if (isExplicit)
        {
            if (Names.IsRefType(to)) return $"::System::cast<{Names.TypeName(to)}>({code})";
            return $"static_cast<{toRef}>({code})";
        }
        return code;
    }

    // Expression translated as a statement (no value needed): allows setters and events.
    string StmtExpr(ExpressionSyntax e)
    {
        switch (e)
        {
            case AssignmentExpressionSyntax a:
                return Assignment(a, false);
            case PostfixUnaryExpressionSyntax p when p.IsKind(SyntaxKind.PostIncrementExpression) || p.IsKind(SyntaxKind.PostDecrementExpression):
                return IncDec(p.Operand, p.IsKind(SyntaxKind.PostIncrementExpression), false, false);
            case PrefixUnaryExpressionSyntax p when p.IsKind(SyntaxKind.PreIncrementExpression) || p.IsKind(SyntaxKind.PreDecrementExpression):
                return IncDec(p.Operand, p.IsKind(SyntaxKind.PreIncrementExpression), true, false);
            case ConditionalAccessExpressionSyntax ca:
                return ConditionalAccess(ca, true);
        }
        return ExprNoConv(e);
    }

    public string ExprNoConv(ExpressionSyntax e)
    {
        switch (e)
        {
            case ParenthesizedExpressionSyntax p:
                return "(" + ExprNoConv(p.Expression) + ")";
            case LiteralExpressionSyntax lit:
            {
                var cv = model.GetConstantValue(lit);
                var t = model.GetTypeInfo(lit).Type;
                if (lit.IsKind(SyntaxKind.DefaultLiteralExpression))
                    return Names.Literal(null, model.GetTypeInfo(lit).ConvertedType);
                return cv.HasValue ? Names.Literal(cv.Value, t) : "nullptr";
            }
            case IdentifierNameSyntax id:
                return Identifier(id);
            case GenericNameSyntax gn:
                return Identifier(gn);
            case MemberAccessExpressionSyntax ma:
                return MemberAccess(ma);
            case InvocationExpressionSyntax inv:
                return Invocation(inv);
            case ElementAccessExpressionSyntax ea:
                return ElementAccess(ea);
            case AssignmentExpressionSyntax a:
                return Assignment(a, true);
            case BinaryExpressionSyntax b:
                return Binary(b);
            case PrefixUnaryExpressionSyntax pu:
                return PrefixUnary(pu);
            case PostfixUnaryExpressionSyntax po:
                if (po.IsKind(SyntaxKind.SuppressNullableWarningExpression)) return ExprNoConv(po.Operand);
                return IncDec(po.Operand, po.IsKind(SyntaxKind.PostIncrementExpression), false, true);
            case CastExpressionSyntax ce:
                return Cast(ce);
            case ConditionalExpressionSyntax c:
                return $"({Cond(c.Condition)} ? {Expr(c.WhenTrue)} : {Expr(c.WhenFalse)})";
            case ThisExpressionSyntax:
                return type.IsValueType ? "(*this)" : "this";
            case BaseExpressionSyntax:
                return "this";
            case ObjectCreationExpressionSyntax oc:
                return ObjectCreation(oc, oc.Initializer);
            case ImplicitObjectCreationExpressionSyntax ioc:
                return ObjectCreation(ioc, ioc.Initializer);
            case ArrayCreationExpressionSyntax ac:
                return ArrayCreation(ac);
            case ImplicitArrayCreationExpressionSyntax iac:
                return ArrayInit((IArrayTypeSymbol)model.GetTypeInfo(iac).Type, iac.Initializer);
            case InitializerExpressionSyntax ie when ie.IsKind(SyntaxKind.ArrayInitializerExpression):
                return ArrayInit((IArrayTypeSymbol)model.GetTypeInfo(ie).ConvertedType, ie);
            case CollectionExpressionSyntax ce2:
                return CollectionExpr(ce2);
            case AnonymousFunctionExpressionSyntax lam:
                return Lambda(lam);
            case TypeOfExpressionSyntax to:
                return $"::System::TypeOf<{Names.TypeName(model.GetTypeInfo(to.Type).Type)}>()";
            case DefaultExpressionSyntax de:
                return Names.Literal(null, model.GetTypeInfo(de.Type).Type);
            case SizeOfExpressionSyntax so:
                return $"(int32_t)sizeof({Names.TypeRef(model.GetTypeInfo(so.Type).Type)})";
            case IsPatternExpressionSyntax ip:
                return IsPattern(ip);
            case ConditionalAccessExpressionSyntax ca:
                return ConditionalAccess(ca, false);
            case MemberBindingExpressionSyntax mb:
                return MemberOn(bindingReceiver, bindingType, model.GetSymbolInfo(mb).Symbol, mb, false);
            case ElementBindingExpressionSyntax eb:
                return ElementOn(bindingReceiver, bindingType, eb.ArgumentList.Arguments, eb);
            case InterpolatedStringExpressionSyntax istr:
                return Interpolated(istr);
            case SwitchExpressionSyntax sw:
                return SwitchExpr(sw);
            case TupleExpressionSyntax tu:
                return Tuple(tu);
            case ThrowExpressionSyntax th:
            {
                var tt = model.GetTypeInfo(th).ConvertedType;
                return $"::System::ThrowExpr<{(tt != null ? Names.TypeRef(tt) : "void")}>({Expr(th.Expression)})";
            }
            case DeclarationExpressionSyntax de:
                return Declaration(de);
            case CheckedExpressionSyntax ch:
                return ExprNoConv(ch.Expression);
            case PredefinedTypeSyntax pt:
                return StaticTypeName(model.GetTypeInfo(pt).Type);
            case QualifiedNameSyntax qn:
            {
                var s = model.GetSymbolInfo(qn).Symbol;
                if (s is ITypeSymbol ts) return StaticTypeName(ts);
                return MemberOn(null, null, s, qn, false);
            }
            case RefExpressionSyntax re:
                return ExprNoConv(re.Expression);
            case RangeExpressionSyntax:
                Warn(e, "range expression outside element access");
                return "/*range*/0";
        }
        Warn(e, "unsupported expression " + e.Kind());
        return $"/* unsupported {e.Kind()} */0";
    }

    // Name used for static member access on a type (primitives map to runtime structs).
    string StaticTypeName(ITypeSymbol t)
    {
        switch (t.SpecialType)
        {
            case SpecialType.System_Int32: return "::System::Int32";
            case SpecialType.System_Int64: return "::System::Int64";
            case SpecialType.System_Int16: return "::System::Int16";
            case SpecialType.System_UInt32: return "::System::UInt32";
            case SpecialType.System_Byte: return "::System::Byte";
            case SpecialType.System_Single: return "::System::Single";
            case SpecialType.System_Double: return "::System::Double";
            case SpecialType.System_Boolean: return "::System::Boolean";
            case SpecialType.System_Char: return "::System::Char";
            case SpecialType.System_Enum: return "::System::Enum";
        }
        if (t.TypeKind == TypeKind.Delegate) return "::System::Delegate";
        return Names.TypeName(t);
    }

    string LocalName(ISymbol s)
    {
        if (rename.TryGetValue(s, out var r)) return r;
        return Names.Escape(s.Name);
    }

    string Identifier(SimpleNameSyntax id)
    {
        var info = model.GetSymbolInfo(id);
        var s = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
        switch (s)
        {
            case ILocalSymbol or IParameterSymbol:
                return LocalName(s);
            case IRangeVariableSymbol:
                return Names.Escape(s.Name);
            case IMethodSymbol m when m.MethodKind == MethodKind.LocalFunction:
                return MethodGroup(id, null, m);
            case IMethodSymbol m:
                return MethodGroup(id, m.IsStatic ? null : (type.IsValueType ? "(*this)" : "this"), m, m.IsStatic ? null : type);
            case ITypeSymbol ts:
                return StaticTypeName(ts);
            case INamespaceSymbol ns:
                return Names.NamespacePrefix(ns);
            case null:
                Warn(id, "unresolved identifier " + id.Identifier.Text);
                return Names.Escape(id.Identifier.Text);
        }
        return MemberOn(null, null, s, id, false);
    }

    // Access to a field/property/event `s` on receiver code (null = implicit this / static).
    string MemberOn(string recv, ITypeSymbol recvType, ISymbol s, ExpressionSyntax node, bool isBase)
    {
        switch (s)
        {
            case IFieldSymbol f:
            {
                if (f.IsConst) return Names.Literal(f.ConstantValue, f.Type);
                if (f.ContainingType.IsTupleType || f.CorrespondingTupleField != null)
                {
                    var tf = f.CorrespondingTupleField ?? f;
                    return $"{recv}.{tf.Name}";
                }
                var name = FieldName(f);
                if (f.IsStatic) return $"{StaticTypeName(f.ContainingType)}::{name}";
                if (recv == null || isBase) return $"this->{name}";
                return recv + Access(recvType ?? f.ContainingType) + name;
            }
            case IPropertySymbol p:
            {
                if (em.IsFieldLike(p))
                {
                    if (p.IsStatic) return $"{StaticTypeName(p.ContainingType)}::{Names.MemberName(p)}";
                    if (recv == null || isBase) return $"this->{Names.MemberName(p)}";
                    return recv + Access(recvType ?? p.ContainingType) + Names.MemberName(p);
                }
                var getter = "get_" + Names.MemberName(p) + "()";
                if (p.IsStatic) return $"{StaticTypeName(p.ContainingType)}::{getter}";
                if (isBase) return $"{Names.TypeName(type.BaseType)}::{getter}";
                if (recv == null) return $"this->{getter}";
                if (recvType != null && recvType.TypeKind == TypeKind.Interface && p.ContainingType.SpecialType == SpecialType.System_Object)
                    return $"::System::AsObject({recv})->{getter}";
                return recv + Access(recvType ?? p.ContainingType) + getter;
            }
            case IEventSymbol ev:
            {
                var name = Names.MemberName(ev);
                if (ev.IsStatic) return $"{StaticTypeName(ev.ContainingType)}::{name}";
                if (recv == null || isBase) return $"this->{name}";
                return recv + Access(recvType ?? ev.ContainingType) + name;
            }
            case IMethodSymbol m:
                return MethodGroup(node, recv, m, recvType);
            case ITypeSymbol t:
                return StaticTypeName(t);
            case ILocalSymbol or IParameterSymbol:
                return LocalName(s);
        }
        Warn(node, "unsupported member " + s?.Kind);
        return "/*member?*/0";
    }

    public string FieldName(IFieldSymbol f)
    {
        if (f.AssociatedSymbol is IPropertySymbol ap) return em.IsFieldLike(ap) ? Names.MemberName(ap) : "__b_" + Names.MemberName(ap);
        return Names.MemberName(f);
    }

    string MemberAccess(MemberAccessExpressionSyntax ma)
    {
        var info = model.GetSymbolInfo(ma);
        var s = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
        if (s is INamespaceSymbol ns) return Names.NamespacePrefix(ns);
        if (s is ITypeSymbol ts) return StaticTypeName(ts);
        if (s == null)
        {
            Warn(ma, "unresolved member " + ma);
            return "/*unresolved*/0";
        }
        bool isStatic = s.IsStatic || s is IFieldSymbol { IsConst: true };
        if (isStatic) return MemberOn(null, null, s, ma, false);
        if (ma.Expression is BaseExpressionSyntax) return MemberOn(null, type.BaseType, s, ma, true);
        var recvType = model.GetTypeInfo(ma.Expression).Type;
        var recv = ExprNoConv(ma.Expression);
        if (recvType != null && IsNullable(recvType) && s is IPropertySymbol np)
            return np.Name == "HasValue" ? $"({recv}).get_HasValue()" : $"({recv}).get_Value()";
        return MemberOn(recv, recvType, s, ma, false);
    }

    // Method group converted to a delegate.
    string MethodGroup(SyntaxNode node, string recv, IMethodSymbol m, ITypeSymbol recvType = null)
    {
        var target = node is ExpressionSyntax es ? model.GetTypeInfo(es).ConvertedType as INamedTypeSymbol : null;
        if (target == null || target.TypeKind != TypeKind.Delegate)
        {
            Warn(node, "method group without delegate target");
            return "/*methodgroup*/nullptr";
        }
        var inv = target.DelegateInvokeMethod;
        var sig = Names.DelegateSig(target);
        var ps = inv.Parameters.Select((p, i) => $"{Names.ParamType(p)} __a{i}").ToList();
        var args = inv.Parameters.Select((p, i) => $"__a{i}").ToList();
        var ret = inv.ReturnsVoid ? "" : "return ";
        var targs = m.IsGenericMethod ? "<" + string.Join(", ", m.TypeArguments.Select(Names.TypeRef)) + ">" : "";
        var key = m.ContainingType?.ToDisplayString() + "." + m.Name;
        if (m.MethodKind == MethodKind.LocalFunction)
            return $"{sig}::Make({Names.Escape(m.Name)})";
        if (m.IsStatic || m.IsExtensionMethod && m.ReducedFrom != null)
        {
            if (m.ReducedFrom != null)
            {
                var r = Tmp("r");
                var rm = m.ReducedFrom;
                return $"({{ auto {r} = {recv}; {sig}::Bind(nullptr, \"{key}\", [=]({string.Join(", ", ps)}) -> {Names.ReturnType(inv)} {{ {ret}{Names.TypeName(rm.ContainingType)}::{Names.MemberName(rm)}{targs}({string.Join(", ", new[] { r }.Concat(args))}); }}); }})";
            }
            return $"{sig}::Bind(nullptr, \"{key}\", []({string.Join(", ", ps)}) -> {Names.ReturnType(inv)} {{ {ret}{StaticTypeName(m.ContainingType)}::{Names.MemberName(m)}{targs}({string.Join(", ", args)}); }})";
        }
        if (recv == null || recv == "this")
        {
            if (isStaticCtx) Warn(node, "instance method group in static context");
            return $"{sig}::Bind(this, \"{key}\", [this]({string.Join(", ", ps)}) -> {Names.ReturnType(inv)} {{ {ret}this->{Names.MemberName(m)}{targs}({string.Join(", ", args)}); }})";
        }
        var t = Tmp("r");
        var acc = Access(recvType ?? m.ContainingType);
        var keyObj = Names.IsRefType(recvType ?? m.ContainingType) ? $"::System::AsObject({t})" : "nullptr";
        return $"({{ auto {t} = {recv}; {sig}::Bind({keyObj}, \"{key}\", [=]({string.Join(", ", ps)}) mutable -> {Names.ReturnType(inv)} {{ {ret}{t}{acc}{Names.MemberName(m)}{targs}({string.Join(", ", args)}); }}); }})";
    }

    // ------------------------------------------------------------------ invocation

    string Args(IEnumerable<IArgumentOperation> args, IMethodSymbol m, bool skipThis = false)
    {
        var list = args.Where(a => a.Parameter != null).OrderBy(a => a.Parameter.Ordinal).ToList();
        var parts = new List<string>();
        foreach (var a in list)
        {
            if (skipThis && a.Parameter.Ordinal == 0 && m.IsExtensionMethod) continue;
            parts.Add(Arg(a));
        }
        return string.Join(", ", parts);
    }

    string Arg(IArgumentOperation a)
    {
        var p = a.Parameter;
        switch (a.ArgumentKind)
        {
            case ArgumentKind.DefaultValue:
                if (p.HasExplicitDefaultValue) return Names.Literal(p.ExplicitDefaultValue, p.Type);
                return Names.Literal(null, p.Type);
            case ArgumentKind.ParamArray:
                if (a.Value is IArrayCreationOperation arr && arr.IsImplicit)
                {
                    var at = (IArrayTypeSymbol)arr.Type;
                    var elems = arr.Initializer?.ElementValues.Select(v => OpExpr(v)) ?? Enumerable.Empty<string>();
                    return $"{Names.TypeName(at)}::Of({{{string.Join(", ", elems)}}})";
                }
                break;
        }
        if (a.Value.Syntax is ExpressionSyntax es)
        {
            if (a.Syntax is ArgumentSyntax asx && asx.Expression == es || a.Value.Syntax == a.Syntax)
                return ArgExpr(es, p);
            return OpExpr(a.Value);
        }
        if (a.Syntax is ArgumentSyntax asy) return ArgExpr(asy.Expression, p);
        return OpExpr(a.Value);
    }

    string ArgExpr(ExpressionSyntax e, IParameterSymbol p)
    {
        if (p.RefKind is RefKind.Out or RefKind.Ref)
        {
            if (e is DeclarationExpressionSyntax) return ExprNoConv(e);
            if (e is IdentifierNameSyntax { Identifier.Text: "_" } && model.GetSymbolInfo(e).Symbol is null or IDiscardSymbol)
            {
                var t = Tmp("discard");
                pending.Add($"{Names.TypeRef(p.Type)} {t}{{}};");
                return t;
            }
            return ExprNoConv(e);
        }
        return Expr(e);
    }

    // Operation without syntax of its own (implicit param-array elements etc.).
    string OpExpr(IOperation op)
    {
        if (op.Syntax is ExpressionSyntax es && !op.IsImplicit) return Expr(es);
        if (op is IConversionOperation conv)
        {
            var inner = OpExpr(conv.Operand);
            var c = conv.GetConversion();
            return ConvertCode(inner, conv.Operand.Type, conv.Type, c, !conv.IsImplicit);
        }
        if (op.ConstantValue.HasValue) return Names.Literal(op.ConstantValue.Value, op.Type);
        if (op.Syntax is ExpressionSyntax es2) return Expr(es2);
        Warn(op.Syntax, "unhandled implicit operation " + op.Kind);
        return "/*op*/0";
    }

    string Invocation(InvocationExpressionSyntax inv)
    {
        var info = model.GetSymbolInfo(inv);
        var m = info.Symbol as IMethodSymbol ?? info.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        if (m == null)
        {
            // nameof etc.
            var cv = model.GetConstantValue(inv);
            if (cv.HasValue) return Names.Literal(cv.Value, model.GetTypeInfo(inv).Type);
            Warn(inv, "unresolved invocation " + inv.Expression);
            return "/*unresolved call*/0";
        }
        var op = model.GetOperation(inv) as IInvocationOperation;
        if (m.MethodKind == MethodKind.DelegateInvoke)
        {
            string target;
            if (inv.Expression is MemberAccessExpressionSyntax dm && dm.Name.Identifier.Text == "Invoke" &&
                model.GetSymbolInfo(dm).Symbol is IMethodSymbol { MethodKind: MethodKind.DelegateInvoke })
                target = ExprNoConv(dm.Expression);
            else if (inv.Expression is MemberBindingExpressionSyntax mb && mb.Name.Identifier.Text == "Invoke")
                target = bindingReceiver;
            else target = ExprNoConv(inv.Expression);
            return $"{target}->Invoke({(op != null ? Args(op.Arguments, m) : "")})";
        }
        var special = Intercept(inv, m, op);
        if (special != null) return special;

        var name = Names.MemberName(m);
        var targs = m.IsGenericMethod ? "<" + string.Join(", ", m.TypeArguments.Select(Names.TypeRef)) + ">" : "";
        if (m.MethodKind == MethodKind.LocalFunction)
            return $"{Names.Escape(m.Name)}({(op != null ? Args(op.Arguments, m) : "")})";

        // Extension method: static call with receiver as first argument.
        if (m.ReducedFrom != null || m.IsExtensionMethod && op != null && op.Instance == null && m.IsStatic)
        {
            var def = m.ReducedFrom ?? m;
            var full = def.IsGenericMethod ? "<" + string.Join(", ", (m.ReducedFrom != null ? m.TypeArguments : m.TypeArguments).Select(Names.TypeRef)) + ">" : "";
            return $"{StaticTypeName(def.ContainingType)}::{Names.MemberName(def)}{full}({Args(op.Arguments, def)})";
        }
        var args = op != null ? Args(op.Arguments, m) : "";
        if (m.IsStatic)
            return $"{StaticTypeName(m.ContainingType)}::{name}{targs}({args})";

        // Instance call.
        ExpressionSyntax recvSyntax = inv.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Expression,
            _ => null,
        };
        if (inv.Expression is MemberBindingExpressionSyntax)
            return bindingReceiver + Access(bindingType) + (targs != "" ? "template " : "") + name + targs + "(" + args + ")";
        if (recvSyntax == null)
            return (type.IsValueType ? "this->" : "this->") + (targs != "" ? "template " : "") + name + targs + "(" + args + ")";
        if (recvSyntax is BaseExpressionSyntax)
        {
            var bt = m.ContainingType;
            return $"{Names.TypeName(type.BaseType)}::{name}{targs}({args})";
        }
        var recvType = model.GetTypeInfo(recvSyntax).Type;
        var recv = ExprNoConv(recvSyntax);
        if (recvType != null && recvType.TypeKind == TypeKind.Interface && m.ContainingType.SpecialType == SpecialType.System_Object)
            return $"::System::AsObject({recv})->{name}({args})";
        return recv + Access(recvType ?? m.ContainingType) + (targs != "" ? "template " : "") + name + targs + "(" + args + ")";
    }

    // ------------------------------------------------------------------ element access

    string ElementAccess(ElementAccessExpressionSyntax ea)
    {
        var recvType = model.GetTypeInfo(ea.Expression).Type;
        var recv = ExprNoConv(ea.Expression);
        return ElementOn(recv, recvType, ea.ArgumentList.Arguments, ea);
    }

    string ElementOn(string recv, ITypeSymbol recvType, SeparatedSyntaxList<ArgumentSyntax> args, ExpressionSyntax node)
    {
        if (recvType is IArrayTypeSymbol)
            return $"{recv}->at({string.Join(", ", args.Select(a => Expr(a.Expression)))})";
        if (IsString(recvType))
        {
            if (args.Count == 1 && args[0].Expression is RangeExpressionSyntax rg)
            {
                if (rg.RightOperand == null) return $"{recv}->Substring({ExprNoConv(rg.LeftOperand)})";
                Warn(node, "string range with end");
            }
            return $"{recv}->get_Chars({Expr(args[0].Expression)})";
        }
        var s = model.GetSymbolInfo(node).Symbol as IPropertySymbol;
        var acc = Access(recvType);
        if (s != null)
        {
            var op = model.GetOperation(node) as IPropertyReferenceOperation;
            var a = op != null ? Args(op.Arguments, s.GetMethod ?? s.SetMethod) : string.Join(", ", args.Select(x => Expr(x.Expression)));
            return $"{recv}{acc}get_Item({a})";
        }
        Warn(node, "unknown element access");
        return $"{recv}{acc}get_Item({string.Join(", ", args.Select(a => Expr(a.Expression)))})";
    }

    // ------------------------------------------------------------------ assignment

    // Describes an assignment target.
    record LValue(string Get, Func<string, string> Set, bool IsSimple, ITypeSymbol Type);

    LValue Target(ExpressionSyntax e)
    {
        var s = model.GetSymbolInfo(e).Symbol;
        var t = model.GetTypeInfo(e).Type;
        switch (e)
        {
            case ParenthesizedExpressionSyntax p:
                return Target(p.Expression);
            case ElementAccessExpressionSyntax ea:
            {
                var recvType = model.GetTypeInfo(ea.Expression).Type;
                if (recvType is IArrayTypeSymbol) break;
                var recv = ExprNoConv(ea.Expression);
                if (s is IPropertySymbol ip)
                {
                    var op = model.GetOperation(ea) as IPropertyReferenceOperation;
                    var a = op != null ? Args(op.Arguments, ip.SetMethod ?? ip.GetMethod) : "";
                    var acc = Access(recvType);
                    var r = recv;
                    return new LValue($"{r}{acc}get_Item({a})", v => $"{r}{acc}set_Item({a}{(a.Length > 0 ? ", " : "")}{v})", false, t);
                }
                break;
            }
            case MemberBindingExpressionSyntax mb when s is IPropertySymbol bp && !em.IsFieldLike(bp):
            {
                var r = bindingReceiver;
                var acc = Access(bindingType);
                return new LValue($"{r}{acc}get_{Names.MemberName(bp)}()", v => $"{r}{acc}set_{Names.MemberName(bp)}({v})", false, t);
            }
        }
        if (s is IPropertySymbol pa && !em.IsFieldLike(pa) && !pa.IsIndexer && pa.SetMethod == null && Names.IsSource(pa, comp) && Emitter.IsAutoProperty(pa))
        {
            string owner = pa.IsStatic ? $"{StaticTypeName(pa.ContainingType)}::" : (e is MemberAccessExpressionSyntax mae && mae.Expression is not ThisExpressionSyntax ? ExprNoConv(mae.Expression) + Access(model.GetTypeInfo(mae.Expression).Type) : "this->");
            var code0 = owner + "__b_" + Names.MemberName(pa);
            return new LValue(code0, v => $"{code0} = {v}", true, t);
        }
        if (s is IPropertySymbol p2 && !em.IsFieldLike(p2) && !p2.IsIndexer)
        {
            string recv = null;
            ITypeSymbol recvType = null;
            bool isBase = false;
            if (e is MemberAccessExpressionSyntax ma)
            {
                if (ma.Expression is BaseExpressionSyntax) isBase = true;
                else if (!p2.IsStatic)
                {
                    recv = ExprNoConv(ma.Expression);
                    recvType = model.GetTypeInfo(ma.Expression).Type;
                }
            }
            var n = Names.MemberName(p2);
            string prefix;
            if (p2.IsStatic) prefix = $"{StaticTypeName(p2.ContainingType)}::";
            else if (isBase) prefix = $"{Names.TypeName(type.BaseType)}::";
            else if (recv == null) prefix = "this->";
            else prefix = recv + Access(recvType ?? p2.ContainingType);
            return new LValue($"{prefix}get_{n}()", v => $"{prefix}set_{n}({v})", false, t);
        }
        var code = ExprNoConv(e);
        return new LValue(code, v => $"{code} = {v}", true, t);
    }

    string Assignment(AssignmentExpressionSyntax a, bool needValue)
    {
        var kind = a.Kind();
        var lsym = model.GetSymbolInfo(a.Left).Symbol;
        var ltype = model.GetTypeInfo(a.Left).Type;
        if (lsym is IDiscardSymbol) return $"(void)({Expr(a.Right)})";

        // Deconstruction
        if (kind == SyntaxKind.SimpleAssignmentExpression && (a.Left is TupleExpressionSyntax || a.Left is DeclarationExpressionSyntax { Designation: ParenthesizedVariableDesignationSyntax }))
            return Deconstruction(a);

        // Events
        if (lsym is IEventSymbol ev && (kind == SyntaxKind.AddAssignmentExpression || kind == SyntaxKind.SubtractAssignmentExpression))
        {
            var h = Expr(a.Right);
            if (!Names.IsSource(ev, comp))
            {
                string recv = a.Left is MemberAccessExpressionSyntax ma ? ExprNoConv(ma.Expression) + Access(model.GetTypeInfo(ma.Expression).Type) : "this->";
                return $"{recv}{(kind == SyntaxKind.AddAssignmentExpression ? "add_" : "remove_")}{Names.MemberName(ev)}({h})";
            }
            var target = ExprNoConv(a.Left);
            var fn = kind == SyntaxKind.AddAssignmentExpression ? "Combine" : "Remove";
            return $"{target} = ::System::Delegate::{fn}({target}, {h})";
        }

        var lv = Target(a.Left);
        if (kind == SyntaxKind.SimpleAssignmentExpression)
        {
            var rhs = Expr(a.Right);
            if (lv.IsSimple) return $"{lv.Get} = {rhs}";
            if (!needValue) return lv.Set(rhs);
            var t = Tmp();
            return $"({{ auto {t} = {rhs}; {lv.Set(t)}; {t}; }})";
        }
        if (kind == SyntaxKind.CoalesceAssignmentExpression)
        {
            var rhs = Expr(a.Right);
            string test = IsNullable(ltype) ? $"!({lv.Get}).hasValue" : $"!({lv.Get})";
            if (!needValue) return $"({{ if ({test}) {lv.Set(rhs)}; }})";
            return $"({{ if ({test}) {lv.Set(rhs)}; {lv.Get}; }})";
        }
        // Compound
        var opKind = kind switch
        {
            SyntaxKind.AddAssignmentExpression => "+",
            SyntaxKind.SubtractAssignmentExpression => "-",
            SyntaxKind.MultiplyAssignmentExpression => "*",
            SyntaxKind.DivideAssignmentExpression => "/",
            SyntaxKind.ModuloAssignmentExpression => "%",
            SyntaxKind.AndAssignmentExpression => "&",
            SyntaxKind.OrAssignmentExpression => "|",
            SyntaxKind.ExclusiveOrAssignmentExpression => "^",
            SyntaxKind.LeftShiftAssignmentExpression => "<<",
            SyntaxKind.RightShiftAssignmentExpression => ">>",
            _ => "?",
        };
        var cop = model.GetOperation(a) as ICompoundAssignmentOperation;
        string value;
        if (IsDelegate(ltype) && (opKind == "+" || opKind == "-"))
            value = $"::System::Delegate::{(opKind == "+" ? "Combine" : "Remove")}({lv.Get}, {Expr(a.Right)})";
        else if (IsString(ltype) && opKind == "+")
            value = $"::System::String::Concat({lv.Get}, {StrOperand(a.Right)})";
        else if (cop?.OperatorMethod != null && Names.IsSource(cop.OperatorMethod, comp))
            value = $"{Names.TypeName(cop.OperatorMethod.ContainingType)}::{cop.OperatorMethod.MetadataName}({lv.Get}, {Expr(a.Right)})";
        else if (ltype != null && ltype.TypeKind == TypeKind.Enum && (opKind == "+" || opKind == "-"))
        {
            var u = Names.EnumUnderlying((INamedTypeSymbol)ltype);
            value = $"static_cast<{Names.TypeName(ltype)}>(static_cast<{u}>({lv.Get}) {opKind} {Expr(a.Right)})";
        }
        else if (opKind == "%" && IsFloat(ltype))
            value = $"static_cast<{Names.TypeRef(ltype)}>(std::fmod({lv.Get}, {Expr(a.Right)}))";
        else if (IsNullable(ltype))
            value = $"({lv.Get} {opKind} {Expr(a.Right)})";
        else if (lv.IsSimple && !IsNullable(ltype))
            return $"{lv.Get} {opKind}= {Expr(a.Right)}";
        else
            value = $"static_cast<{Names.TypeRef(ltype)}>({lv.Get} {opKind} {Expr(a.Right)})";
        if (lv.IsSimple) return $"{lv.Get} = {value}";
        if (!needValue) return lv.Set(value);
        var tmp = Tmp();
        return $"({{ auto {tmp} = {value}; {lv.Set(tmp)}; {tmp}; }})";
    }

    string IncDec(ExpressionSyntax operand, bool inc, bool prefix, bool needValue)
    {
        var t = model.GetTypeInfo(operand).Type;
        var lv = Target(operand);
        var op = inc ? "+" : "-";
        bool isEnum = t != null && t.TypeKind == TypeKind.Enum;
        var sop = model.GetOperation(operand.Parent) as IIncrementOrDecrementOperation;
        if (lv.IsSimple && !isEnum && sop?.OperatorMethod == null)
            return prefix ? $"{(inc ? "++" : "--")}{lv.Get}" : $"{lv.Get}{(inc ? "++" : "--")}";
        string next;
        if (isEnum)
        {
            var u = Names.EnumUnderlying((INamedTypeSymbol)t);
            next = $"static_cast<{Names.TypeName(t)}>(static_cast<{u}>({{0}}) {op} 1)";
        }
        else if (sop?.OperatorMethod != null && Names.IsSource(sop.OperatorMethod, comp))
            next = $"{Names.TypeName(sop.OperatorMethod.ContainingType)}::{sop.OperatorMethod.MetadataName}({{0}})";
        else next = $"static_cast<{Names.TypeRef(t)}>({{0}} {op} 1)";
        if (!needValue)
        {
            var cur = lv.Get;
            return lv.IsSimple ? $"{cur} = {string.Format(next, cur)}" : lv.Set(string.Format(next, cur));
        }
        var tmp = Tmp();
        var n2 = Tmp();
        if (prefix)
            return $"({{ auto {tmp} = {string.Format(next, lv.Get)}; {(lv.IsSimple ? $"{lv.Get} = {tmp}" : lv.Set(tmp))}; {tmp}; }})";
        return $"({{ auto {tmp} = {lv.Get}; auto {n2} = {string.Format(next, tmp)}; {(lv.IsSimple ? $"{lv.Get} = {n2}" : lv.Set(n2))}; {tmp}; }})";
    }

    string Deconstruction(AssignmentExpressionSyntax a)
    {
        var t = Tmp("d");
        var rhsType = model.GetTypeInfo(a.Right).Type as INamedTypeSymbol;
        var sb = new StringBuilder();
        sb.Append($"({{ auto {t} = {Expr(a.Right)}; ");
        void Assign(ExpressionSyntax target, string src, VariableDesignationSyntax des)
        {
            if (des is SingleVariableDesignationSyntax sv)
            {
                var ls = (ILocalSymbol)model.GetDeclaredSymbol(sv);
                pending.Add($"{Names.TypeRef(ls.Type)} {Names.Escape(sv.Identifier.Text)}{{}};");
                sb.Append($"{Names.Escape(sv.Identifier.Text)} = {src}; ");
                return;
            }
            if (des is DiscardDesignationSyntax) return;
            if (target is IdentifierNameSyntax { Identifier.Text: "_" } && model.GetSymbolInfo(target).Symbol is null or IDiscardSymbol) return;
            if (target is DeclarationExpressionSyntax de) { Assign(null, src, de.Designation); return; }
            var lv = Target(target);
            sb.Append(lv.IsSimple ? $"{lv.Get} = {src}; " : lv.Set(src) + "; ");
        }
        if (a.Left is TupleExpressionSyntax te)
        {
            for (int i = 0; i < te.Arguments.Count; i++) Assign(te.Arguments[i].Expression, $"{t}.Item{i + 1}", null);
        }
        else if (a.Left is DeclarationExpressionSyntax { Designation: ParenthesizedVariableDesignationSyntax pvd })
        {
            for (int i = 0; i < pvd.Variables.Count; i++) Assign(null, $"{t}.Item{i + 1}", pvd.Variables[i]);
        }
        sb.Append("})");
        return sb.ToString();
    }

    string Declaration(DeclarationExpressionSyntax de)
    {
        if (de.Designation is SingleVariableDesignationSyntax sv)
        {
            var ls = (ILocalSymbol)model.GetDeclaredSymbol(sv);
            var n = Names.Escape(sv.Identifier.Text);
            pending.Add($"{Names.TypeRef(ls.Type)} {n}{{}};");
            if (boxed.Contains(ls))
            {
                Warn(de, "captured out var");
            }
            return n;
        }
        if (de.Designation is DiscardDesignationSyntax)
        {
            var t = model.GetTypeInfo(de).Type;
            var n = Tmp("discard");
            pending.Add($"{Names.TypeRef(t)} {n}{{}};");
            return n;
        }
        Warn(de, "unsupported declaration expression");
        return "/*decl*/0";
    }
}
