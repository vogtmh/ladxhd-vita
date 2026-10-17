using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cs2Cpp;

partial class BodyWriter
{
    // ------------------------------------------------------------------ operators

    string StrOperand(ExpressionSyntax e)
    {
        var cv = model.GetConstantValue(e);
        if (cv.HasValue)
        {
            if (cv.Value == null) return "::System::String::Empty";
            if (cv.Value is string s) return $"STR(u{Names.StringLit(s)})";
            return $"STR(u{Names.StringLit(Convert.ToString(cv.Value, System.Globalization.CultureInfo.InvariantCulture))})";
        }
        var t = model.GetTypeInfo(e).Type;
        var raw = ExprNoConv(e);
        if (IsString(t)) return raw;
        return $"::System::ToStrC({raw})";
    }

    void CollectConcat(ExpressionSyntax e, List<ExpressionSyntax> parts)
    {
        if (e is BinaryExpressionSyntax b && b.IsKind(SyntaxKind.AddExpression) && IsString(model.GetTypeInfo(b).Type) && !model.GetConstantValue(b).HasValue)
        {
            CollectConcat(b.Left, parts);
            CollectConcat(b.Right, parts);
        }
        else if (e is ParenthesizedExpressionSyntax p && p.Expression is BinaryExpressionSyntax pb && pb.IsKind(SyntaxKind.AddExpression) && IsString(model.GetTypeInfo(pb).Type) && !model.GetConstantValue(pb).HasValue)
            CollectConcat(p.Expression, parts);
        else parts.Add(e);
    }

    string Binary(BinaryExpressionSyntax b)
    {
        var kind = b.Kind();
        switch (kind)
        {
            case SyntaxKind.IsExpression:
            {
                var tt = model.GetTypeInfo(b.Right).Type;
                var lt = model.GetTypeInfo(b.Left).Type;
                return TypeTest(ExprNoConv(b.Left), lt, tt);
            }
            case SyntaxKind.AsExpression:
            {
                var tt = model.GetTypeInfo(b.Right).Type;
                var inner = Expr(b.Left);
                if (IsNullable(tt))
                {
                    var it = ((INamedTypeSymbol)tt).TypeArguments[0];
                    var t = Tmp();
                    return $"({{ auto {t} = {inner}; ::System::isBoxed<{Names.TypeRef(it)}>({t}) ? {Names.TypeName(tt)}(::System::unbox<{Names.TypeRef(it)}>({t})) : {Names.TypeName(tt)}(); }})";
                }
                return $"::System::as<{Names.TypeName(tt)}>({inner})";
            }
            case SyntaxKind.CoalesceExpression:
                return Coalesce(b);
            case SyntaxKind.LogicalAndExpression:
                return $"({Expr(b.Left)} && {Expr(b.Right)})";
            case SyntaxKind.LogicalOrExpression:
                return $"({Expr(b.Left)} || {Expr(b.Right)})";
        }
        var rt = model.GetTypeInfo(b).Type;
        var lt2 = model.GetTypeInfo(b.Left).Type;
        var rt2 = model.GetTypeInfo(b.Right).Type;
        if (kind == SyntaxKind.AddExpression && IsString(rt))
        {
            var parts = new List<ExpressionSyntax>();
            CollectConcat(b, parts);
            var codes = parts.Select(StrOperand).ToList();
            if (codes.Count == 2) return $"::System::String::Concat({codes[0]}, {codes[1]})";
            if (codes.Count == 3) return $"::System::String::Concat({codes[0]}, {codes[1]}, {codes[2]})";
            return $"::System::String::ConcatN({{{string.Join(", ", codes)}}})";
        }
        var op = model.GetOperation(b) as IBinaryOperation;
        var opStr = b.OperatorToken.Text;
        if ((kind == SyntaxKind.EqualsExpression || kind == SyntaxKind.NotEqualsExpression))
        {
            bool eq = kind == SyntaxKind.EqualsExpression;
            var lcv = model.GetConstantValue(b.Left);
            var rcv = model.GetConstantValue(b.Right);
            // null comparisons on nullable
            if (IsNullable(lt2) && rcv.HasValue && rcv.Value == null) return eq ? $"!({ExprNoConv(b.Left)}).hasValue" : $"({ExprNoConv(b.Left)}).hasValue";
            if (IsNullable(rt2) && lcv.HasValue && lcv.Value == null) return eq ? $"!({ExprNoConv(b.Right)}).hasValue" : $"({ExprNoConv(b.Right)}).hasValue";
            if (op?.OperatorMethod != null && op.OperatorMethod.ContainingType.SpecialType == SpecialType.System_String || IsString(lt2) && IsString(rt2))
                return $"{(eq ? "" : "!")}::System::String::op_Equality({Expr(b.Left)}, {Expr(b.Right)})";
            if (op?.OperatorMethod != null && op.OperatorMethod.ContainingType.TypeKind == TypeKind.Delegate)
                return $"({Expr(b.Left)} {opStr} {Expr(b.Right)})";
        }
        if (op?.OperatorMethod != null && Names.IsSource(op.OperatorMethod, comp))
            return $"{Names.TypeName(op.OperatorMethod.ContainingType)}::{op.OperatorMethod.MetadataName}({Expr(b.Left)}, {Expr(b.Right)})";
        var l = Expr(b.Left);
        var r = Expr(b.Right);
        if (kind == SyntaxKind.ModuloExpression && IsFloat(rt))
            return $"static_cast<{Names.TypeRef(rt)}>(std::fmod({l}, {r}))";
        // enum arithmetic
        if ((kind == SyntaxKind.AddExpression || kind == SyntaxKind.SubtractExpression) && (lt2?.TypeKind == TypeKind.Enum || rt2?.TypeKind == TypeKind.Enum))
        {
            var et = lt2?.TypeKind == TypeKind.Enum ? lt2 : rt2;
            var u = Names.EnumUnderlying((INamedTypeSymbol)et);
            string lc = lt2?.TypeKind == TypeKind.Enum ? $"static_cast<{u}>({l})" : l;
            string rc = rt2?.TypeKind == TypeKind.Enum ? $"static_cast<{u}>({r})" : r;
            var expr = $"({lc} {opStr} {rc})";
            return rt?.TypeKind == TypeKind.Enum ? $"static_cast<{Names.TypeName(rt)}>{expr}" : expr;
        }
        if ((kind == SyntaxKind.BitwiseAndExpression || kind == SyntaxKind.BitwiseOrExpression || kind == SyntaxKind.ExclusiveOrExpression) && rt?.SpecialType == SpecialType.System_Boolean)
            return $"((bool)({l} {opStr} {r}))";
        if (kind == SyntaxKind.LeftShiftExpression || kind == SyntaxKind.RightShiftExpression)
        {
            if (kind == SyntaxKind.RightShiftExpression && lt2 != null && IsIntegral(lt2))
                return $"static_cast<{Names.TypeRef(rt)}>({l} >> ({r} & {(rt.SpecialType is SpecialType.System_Int64 or SpecialType.System_UInt64 ? 63 : 31)}))";
            return $"static_cast<{Names.TypeRef(rt)}>({l} << ({r} & {(rt.SpecialType is SpecialType.System_Int64 or SpecialType.System_UInt64 ? 63 : 31)}))";
        }
        return $"({l} {opStr} {r})";
    }

    string Coalesce(BinaryExpressionSyntax b)
    {
        var lt = model.GetTypeInfo(b.Left).Type;
        var rt = model.GetTypeInfo(b).ConvertedType ?? model.GetTypeInfo(b).Type;
        var t = Tmp();
        var l = ExprNoConv(b.Left);
        var r = Expr(b.Right);
        if (IsNullable(lt))
        {
            var resType = model.GetTypeInfo(b).Type;
            if (IsNullable(resType)) return $"({{ auto {t} = {l}; {t}.hasValue ? {t} : {Names.TypeName(resType)}({r}); }})";
            return $"({{ auto {t} = {l}; {t}.hasValue ? {t}.value : {r}; }})";
        }
        var res = model.GetTypeInfo(b).Type ?? rt;
        var resRef = Names.TypeRef(res);
        return $"({{ {resRef} {t} = {l}; {t} ? {t} : ({resRef})({r}); }})";
    }

    string TypeTest(string subj, ITypeSymbol subjType, ITypeSymbol target)
    {
        if (Names.IsRefType(target) || Names.IsTypeParamRef(target))
            return $"::System::is<{Names.TypeName(target)}>({subj})";
        if (IsNullable(subjType)) return $"({subj}).hasValue";
        if (subjType != null && !Names.IsRefType(subjType) && SymbolEqualityComparer.Default.Equals(subjType, target)) return "true";
        return $"::System::isBoxed<{Names.TypeRef(target)}>({subj})";
    }

    string PrefixUnary(PrefixUnaryExpressionSyntax pu)
    {
        switch (pu.Kind())
        {
            case SyntaxKind.PreIncrementExpression:
            case SyntaxKind.PreDecrementExpression:
                return IncDec(pu.Operand, pu.IsKind(SyntaxKind.PreIncrementExpression), true, true);
            case SyntaxKind.LogicalNotExpression:
                return "!" + Expr(pu.Operand);
            case SyntaxKind.UnaryMinusExpression:
            {
                var op = model.GetOperation(pu) as IUnaryOperation;
                if (op?.OperatorMethod != null && Names.IsSource(op.OperatorMethod, comp))
                    return $"{Names.TypeName(op.OperatorMethod.ContainingType)}::{op.OperatorMethod.MetadataName}({Expr(pu.Operand)})";
                return "(-" + Expr(pu.Operand) + ")";
            }
            case SyntaxKind.UnaryPlusExpression:
                return "(+" + Expr(pu.Operand) + ")";
            case SyntaxKind.BitwiseNotExpression:
                return "(~" + Expr(pu.Operand) + ")";
            case SyntaxKind.IndexExpression:
                Warn(pu, "index-from-end");
                return Expr(pu.Operand);
            case SyntaxKind.AddressOfExpression:
                return "&" + Expr(pu.Operand);
            case SyntaxKind.PointerIndirectionExpression:
                return "*" + Expr(pu.Operand);
        }
        Warn(pu, "unsupported unary " + pu.Kind());
        return Expr(pu.Operand);
    }

    string Cast(CastExpressionSyntax ce)
    {
        var target = model.GetTypeInfo(ce.Type).Type;
        var src = model.GetTypeInfo(ce.Expression).Type;
        var inner = Expr(ce.Expression);
        var srcConv = model.GetTypeInfo(ce.Expression).ConvertedType ?? src;
        if (src == null) return inner;
        var conv = model.ClassifyConversion(ce.Expression, target, true);
        return ConvertCode(inner, srcConv, target, conv, true, ce);
    }

    // ------------------------------------------------------------------ creation

    string ObjectCreation(BaseObjectCreationExpressionSyntax oc, InitializerExpressionSyntax init)
    {
        var t = model.GetTypeInfo(oc).Type;
        var op = model.GetOperation(oc);
        if (t.TypeKind == TypeKind.Delegate)
        {
            var a = oc.ArgumentList.Arguments[0].Expression;
            return Expr(a);
        }
        string create;
        if (op is IObjectCreationOperation oco)
        {
            var ctor = oco.Constructor;
            var args = ctor != null ? Args(oco.Arguments, ctor) : "";
            if (t.SpecialType == SpecialType.System_String)
            {
                create = oco.Arguments.Length == 2 ? $"::System::String::FromChar({args})" : $"::System::String::FromChars({args})";
            }
            else if (Names.IsRefType(t))
                create = $"::System::New<{Names.TypeName(t)}>({args})";
            else if (IsNullable(t))
                create = $"{Names.TypeName(t)}({args})";
            else if (args.Length == 0)
                create = $"{Names.TypeName(t)}{{}}";
            else
                create = $"{Names.TypeName(t)}({args})";
        }
        else if (op is ITypeParameterObjectCreationOperation)
        {
            create = $"::System::New<std::remove_pointer_t<{Names.TypeRef(t)}>>()";
        }
        else
        {
            Warn(oc, "unknown object creation");
            create = $"::System::New<{Names.TypeName(t)}>()";
        }
        if (init == null) return create;
        var tmp = Tmp("o");
        var sb = new StringBuilder();
        sb.Append($"({{ auto {tmp} = {create}; ");
        var acc = Names.IsRefType(t) ? "->" : ".";
        foreach (var ex in init.Expressions)
        {
            switch (ex)
            {
                case AssignmentExpressionSyntax asg when asg.Left is IdentifierNameSyntax lid:
                {
                    var ms = model.GetSymbolInfo(lid).Symbol;
                    if (asg.Right is InitializerExpressionSyntax nested)
                    {
                        // nested collection initializer on a member: Member = { a, b }
                        var mcode = MemberOn(tmp, t, ms, lid, false);
                        var mt = model.GetTypeInfo(lid).Type;
                        foreach (var ne in nested.Expressions)
                            sb.Append($"{mcode}{Access(mt)}Add({Expr(ne)}); ");
                        break;
                    }
                    var val = Expr(asg.Right);
                    if (ms is IPropertySymbol ps && !em.IsFieldLike(ps))
                        sb.Append($"{tmp}{acc}set_{Names.MemberName(ps)}({val}); ");
                    else if (ms is IFieldSymbol fs)
                        sb.Append($"{tmp}{acc}{FieldName(fs)} = {val}; ");
                    else
                        sb.Append($"{tmp}{acc}{Names.MemberName(ms)} = {val}; ");
                    break;
                }
                case AssignmentExpressionSyntax asg2 when asg2.Left is ImplicitElementAccessSyntax iea:
                {
                    var keys = string.Join(", ", iea.ArgumentList.Arguments.Select(x => Expr(x.Expression)));
                    sb.Append($"{tmp}{acc}set_Item({keys}, {Expr(asg2.Right)}); ");
                    break;
                }
                case InitializerExpressionSyntax cie when cie.IsKind(SyntaxKind.ComplexElementInitializerExpression):
                {
                    var addOp = model.GetCollectionInitializerSymbolInfo(cie).Symbol as IMethodSymbol;
                    var parts = cie.Expressions.Select(Expr);
                    sb.Append($"{tmp}{acc}{(addOp != null ? Names.MemberName(addOp) : "Add")}({string.Join(", ", parts)}); ");
                    break;
                }
                default:
                {
                    var addOp = model.GetCollectionInitializerSymbolInfo(ex).Symbol as IMethodSymbol;
                    sb.Append($"{tmp}{acc}{(addOp != null ? Names.MemberName(addOp) : "Add")}({Expr(ex)}); ");
                    break;
                }
            }
        }
        sb.Append($"{tmp}; }})");
        return sb.ToString();
    }

    string ArrayCreation(ArrayCreationExpressionSyntax ac)
    {
        var at = (IArrayTypeSymbol)model.GetTypeInfo(ac).Type;
        if (ac.Initializer != null) return ArrayInit(at, ac.Initializer);
        var rank = ac.Type.RankSpecifiers[0];
        var sizes = rank.Sizes.Select(s => Expr(s)).ToList();
        return $"{Names.TypeName(at)}::New({string.Join(", ", sizes)})";
    }

    string ArrayInit(IArrayTypeSymbol at, InitializerExpressionSyntax init)
    {
        if (at == null)
        {
            Warn(init, "array initializer without type");
            return "nullptr";
        }
        if (at.Rank == 2)
        {
            var rows = init.Expressions.OfType<InitializerExpressionSyntax>().ToList();
            int cols = rows.Count > 0 ? rows[0].Expressions.Count : 0;
            var elems = rows.SelectMany(r => r.Expressions).Select(Expr);
            return $"::System::MakeArray2<{Names.TypeRef(at.ElementType)}>({rows.Count}, {cols}, {{{string.Join(", ", elems)}}})";
        }
        var items = init.Expressions.Select(x => x is InitializerExpressionSyntax sub && at.ElementType is IArrayTypeSymbol sat ? ArrayInit(sat, sub) : Expr(x));
        return $"{Names.TypeName(at)}::Of({{{string.Join(", ", items)}}})";
    }

    string CollectionExpr(CollectionExpressionSyntax ce)
    {
        var t = model.GetTypeInfo(ce).ConvertedType;
        var elems = ce.Elements.OfType<ExpressionElementSyntax>().Select(e => Expr(e.Expression)).ToList();
        if (t is IArrayTypeSymbol at) return $"{Names.TypeName(at)}::Of({{{string.Join(", ", elems)}}})";
        var tmp = Tmp("c");
        var nt = (INamedTypeSymbol)t;
        var concrete = nt.TypeKind == TypeKind.Interface
            ? $"::System::Collections::Generic::List<{Names.TypeRef(nt.TypeArguments[0])}>"
            : Names.TypeName(nt);
        var sb = new StringBuilder($"({{ auto {tmp} = ::System::New<{concrete}>(); ");
        foreach (var e in elems) sb.Append($"{tmp}->Add({e}); ");
        sb.Append($"static_cast<{Names.TypeRef(t)}>({tmp}); }})");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ lambdas

    string Lambda(AnonymousFunctionExpressionSyntax lam)
    {
        var target = model.GetTypeInfo(lam).ConvertedType as INamedTypeSymbol;
        var sym = model.GetSymbolInfo(lam).Symbol as IMethodSymbol;
        if (target == null || target.TypeKind != TypeKind.Delegate || sym == null)
        {
            Warn(lam, "lambda without delegate target");
            return "nullptr";
        }
        var inv = target.DelegateInvokeMethod;
        var ps = sym.Parameters.Select((p, i) =>
        {
            var pt = Names.ParamType(inv.Parameters[i]);
            var n = p.Name == "_" || p.IsDiscard ? $"__unused{i}" : Names.Escape(p.Name);
            return $"{pt} {n}";
        });
        var capture = isStaticCtx || type.IsValueType ? "[=]" : "[=, this]";
        if (type.IsValueType && !isStaticCtx) capture = "[=, *this]";
        var ret = Names.ReturnType(inv);
        lambdaDepth++;
        var oldIter = iteratorElem;
        iteratorElem = null;
        returnTypes.Push(inv.ReturnsVoid ? null : inv.ReturnType);
        string body;
        var block = lam switch
        {
            ParenthesizedLambdaExpressionSyntax pl => pl.Block,
            SimpleLambdaExpressionSyntax sl => sl.Block,
            AnonymousMethodExpressionSyntax am => am.Block,
            _ => null,
        };
        if (block != null) body = "{\n" + Statements(block.Statements, 2) + "    }";
        else
        {
            var exprBody = lam switch
            {
                ParenthesizedLambdaExpressionSyntax pl => pl.ExpressionBody,
                SimpleLambdaExpressionSyntax sl => sl.ExpressionBody,
                _ => null,
            };
            var old = pending;
            pending = new List<string>();
            string e = inv.ReturnsVoid ? StmtExpr(exprBody) : Expr(exprBody);
            var sb = new StringBuilder("{ ");
            foreach (var d in pending) sb.Append(d).Append(' ');
            pending = old;
            sb.Append(inv.ReturnsVoid ? e + "; }" : "return " + e + "; }");
            body = sb.ToString();
        }
        returnTypes.Pop();
        iteratorElem = oldIter;
        lambdaDepth--;
        var mutableKw = type.IsValueType && !isStaticCtx ? " mutable" : "";
        return $"{Names.DelegateSig(target)}::Make({capture}({string.Join(", ", ps)}){mutableKw} -> {ret} {body})";
    }

    // ------------------------------------------------------------------ patterns

    string IsPattern(IsPatternExpressionSyntax ip)
    {
        var st = model.GetTypeInfo(ip.Expression).Type;
        string subj;
        if (IsSimpleSubject(ip.Expression)) subj = ExprNoConv(ip.Expression);
        else
        {
            subj = Tmp("p");
            pending.Add($"{(st != null ? Names.TypeRef(st) : "auto")} {subj}{{}};");
            var cond0 = Pattern(ip.Pattern, subj, st);
            return $"(({subj} = {ExprNoConv(ip.Expression)}), {cond0})";
        }
        return "(" + Pattern(ip.Pattern, subj, st) + ")";
    }

    bool IsSimpleSubject(ExpressionSyntax e) => e switch
    {
        IdentifierNameSyntax => true,
        ThisExpressionSyntax => true,
        MemberAccessExpressionSyntax ma => model.GetSymbolInfo(ma).Symbol is IFieldSymbol && IsSimpleSubject(ma.Expression),
        ParenthesizedExpressionSyntax p => IsSimpleSubject(p.Expression),
        _ => false,
    };

    string DeclareDesignation(VariableDesignationSyntax d, ITypeSymbol t)
    {
        if (d is SingleVariableDesignationSyntax sv)
        {
            var ls = model.GetDeclaredSymbol(sv) as ILocalSymbol;
            var n = Names.Escape(sv.Identifier.Text);
            pending.Add($"{Names.TypeRef(ls?.Type ?? t)} {n}{{}};");
            return n;
        }
        return null;
    }

    string Pattern(PatternSyntax p, string subj, ITypeSymbol st)
    {
        switch (p)
        {
            case ConstantPatternSyntax cp when model.GetSymbolInfo(cp.Expression).Symbol is ITypeSymbol cpt:
                return TypeTest(subj, st, cpt);
            case ConstantPatternSyntax cp:
            {
                var cv = model.GetConstantValue(cp.Expression);
                if (cv.HasValue) return ConstCompare(subj, st, cv.Value, model.GetTypeInfo(cp.Expression).Type);
                return $"{subj} == {Expr(cp.Expression)}";
            }
            case DiscardPatternSyntax:
                return "true";
            case VarPatternSyntax vp:
            {
                var n = DeclareDesignation(vp.Designation, st);
                return n != null ? $"(({n} = {subj}), true)" : "true";
            }
            case TypePatternSyntax tp:
                return TypeTest(subj, st, model.GetTypeInfo(tp.Type).Type);
            case DeclarationPatternSyntax dp:
            {
                var tt = model.GetTypeInfo(dp.Type).Type;
                var n = DeclareDesignation(dp.Designation, tt);
                if (n == null) return TypeTest(subj, st, tt);
                if (Names.IsRefType(tt) || Names.IsTypeParamRef(tt))
                    return $"(({n} = ::System::as<{Names.TypeName(tt)}>({subj})) != nullptr)";
                if (IsNullable(st)) return $"(({subj}).hasValue && (({n} = ({subj}).value), true))";
                if (st != null && SymbolEqualityComparer.Default.Equals(st, tt)) return $"(({n} = {subj}), true)";
                return $"(::System::isBoxed<{Names.TypeRef(tt)}>({subj}) && (({n} = ::System::unbox<{Names.TypeRef(tt)}>({subj})), true))";
            }
            case RelationalPatternSyntax rp:
            {
                var cv = model.GetConstantValue(rp.Expression);
                var lit = cv.HasValue ? Names.Literal(cv.Value, IsNullable(st) ? ((INamedTypeSymbol)st).TypeArguments[0] : st) : Expr(rp.Expression);
                if (IsNullable(st)) return $"(({subj}).hasValue && ({subj}).value {rp.OperatorToken.Text} {lit})";
                return $"({subj} {rp.OperatorToken.Text} {lit})";
            }
            case BinaryPatternSyntax bp:
                return bp.IsKind(SyntaxKind.AndPattern)
                    ? $"({Pattern(bp.Left, subj, st)} && {Pattern(bp.Right, subj, st)})"
                    : $"({Pattern(bp.Left, subj, st)} || {Pattern(bp.Right, subj, st)})";
            case UnaryPatternSyntax up:
                return $"!({Pattern(up.Pattern, subj, st)})";
            case ParenthesizedPatternSyntax pp:
                return Pattern(pp.Pattern, subj, st);
            case RecursivePatternSyntax rp:
            {
                var conds = new List<string>();
                string cur = subj;
                ITypeSymbol ct = st;
                if (rp.Type != null)
                {
                    var tt = model.GetTypeInfo(rp.Type).Type;
                    var tmp = Tmp("rp");
                    pending.Add($"{Names.TypeRef(tt)} {tmp}{{}};");
                    if (Names.IsRefType(tt)) conds.Add($"(({tmp} = ::System::as<{Names.TypeName(tt)}>({subj})) != nullptr)");
                    else conds.Add($"(::System::isBoxed<{Names.TypeRef(tt)}>({subj}) && (({tmp} = ::System::unbox<{Names.TypeRef(tt)}>({subj})), true))");
                    cur = tmp;
                    ct = tt;
                }
                else if (st != null && Names.IsRefType(st)) conds.Add($"({subj} != nullptr)");
                else if (IsNullable(st))
                {
                    conds.Add($"({subj}).hasValue");
                    cur = $"({subj}).value";
                    ct = ((INamedTypeSymbol)st).TypeArguments[0];
                }
                if (rp.PositionalPatternClause != null)
                {
                    var subs = rp.PositionalPatternClause.Subpatterns;
                    for (int i = 0; i < subs.Count; i++)
                    {
                        ITypeSymbol et = ct is INamedTypeSymbol nt && nt.IsTupleType ? nt.TupleElements[i].Type : null;
                        conds.Add(Pattern(subs[i].Pattern, $"{cur}.Item{i + 1}", et));
                    }
                }
                if (rp.PropertyPatternClause != null)
                {
                    foreach (var sp in rp.PropertyPatternClause.Subpatterns)
                    {
                        var ms = sp.NameColon != null ? model.GetSymbolInfo(sp.NameColon.Name).Symbol : model.GetSymbolInfo(sp.ExpressionColon.Expression).Symbol;
                        var mt = ms switch { IPropertySymbol ps => ps.Type, IFieldSymbol fs => fs.Type, _ => null };
                        var access = MemberOn(cur, ct, ms, (ExpressionSyntax)sp.NameColon?.Name ?? sp.ExpressionColon.Expression, false);
                        conds.Add(Pattern(sp.Pattern, access, mt));
                    }
                }
                if (rp.Designation != null)
                {
                    var n = DeclareDesignation(rp.Designation, ct);
                    if (n != null) conds.Add($"(({n} = {cur}), true)");
                }
                return conds.Count == 0 ? "true" : "(" + string.Join(" && ", conds) + ")";
            }
        }
        Warn(p, "unsupported pattern " + p.Kind());
        return "false";
    }

    string SwitchExpr(SwitchExpressionSyntax sw)
    {
        var st = model.GetTypeInfo(sw.GoverningExpression).Type;
        var rt = model.GetTypeInfo(sw).ConvertedType ?? model.GetTypeInfo(sw).Type;
        var subj = Tmp("s");
        var res = Tmp("r");
        var sb = new StringBuilder();
        sb.Append($"({{ {(st != null ? Names.TypeRef(st) : "auto")} {subj} = {ExprNoConv(sw.GoverningExpression)}; {Names.TypeRef(rt)} {res}{{}}; do {{ ");
        foreach (var arm in sw.Arms)
        {
            var old = pending;
            pending = new List<string>();
            var cond = Pattern(arm.Pattern, subj, st);
            if (arm.WhenClause != null) cond = $"({cond}) && ({Expr(arm.WhenClause.Condition)})";
            var val = Expr(arm.Expression);
            var decls = pending;
            pending = old;
            sb.Append("{ ");
            foreach (var d in decls) sb.Append(d).Append(' ');
            sb.Append($"if ({cond}) {{ {res} = {val}; break; }} }} ");
        }
        sb.Append($"::System::ThrowSwitch(); }} while (0); {res}; }})");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ misc expressions

    string ConditionalAccess(ConditionalAccessExpressionSyntax ca, bool statement)
    {
        var recvType = model.GetTypeInfo(ca.Expression).Type;
        var resType = model.GetTypeInfo(ca).Type;
        var t = Tmp("c");
        var recv = ExprNoConv(ca.Expression);
        var oldR = bindingReceiver;
        var oldT = bindingType;
        bool nullableRecv = IsNullable(recvType);
        bindingReceiver = nullableRecv ? $"{t}.value" : t;
        bindingType = nullableRecv ? ((INamedTypeSymbol)recvType).TypeArguments[0] : recvType;
        string test = nullableRecv ? $"{t}.hasValue" : t;
        string result;
        if (statement || resType == null || resType.SpecialType == SpecialType.System_Void)
        {
            var inner = StmtExpr(ca.WhenNotNull);
            result = $"({{ auto {t} = {recv}; if ({test}) {{ {inner}; }} }})";
        }
        else
        {
            var inner = ExprNoConv(ca.WhenNotNull);
            if (IsNullable(resType))
            {
                var innerType = model.GetTypeInfo(ca.WhenNotNull).Type;
                var wrap = IsNullable(innerType) ? inner : $"{Names.TypeName(resType)}({inner})";
                result = $"({{ auto {t} = {recv}; {test} ? {wrap} : {Names.TypeName(resType)}(); }})";
            }
            else
                result = $"({{ auto {t} = {recv}; {test} ? static_cast<{Names.TypeRef(resType)}>({inner}) : static_cast<{Names.TypeRef(resType)}>(nullptr); }})";
        }
        bindingReceiver = oldR;
        bindingType = oldT;
        return result;
    }

    string Interpolated(InterpolatedStringExpressionSyntax istr)
    {
        var parts = new List<string>();
        foreach (var c in istr.Contents)
        {
            if (c is InterpolatedStringTextSyntax txt)
                parts.Add($"STR(u{Names.StringLit(txt.TextToken.ValueText)})");
            else if (c is InterpolationSyntax ip)
            {
                string v;
                var raw = ExprNoConv(ip.Expression);
                var cv = model.GetConstantValue(ip.Expression);
                if (cv.HasValue) raw = Names.Literal(cv.Value, model.GetTypeInfo(ip.Expression).Type);
                if (ip.FormatClause != null)
                    v = $"::System::Fmt({raw}, STR(u{Names.StringLit(ip.FormatClause.FormatStringToken.ValueText)}))";
                else v = $"::System::ToStrC({raw})";
                if (ip.AlignmentClause != null)
                    v = $"::System::PadAlign({v}, {Expr(ip.AlignmentClause.Value)})";
                parts.Add(v);
            }
        }
        if (parts.Count == 0) return "::System::String::Empty";
        if (parts.Count == 1) return parts[0];
        return $"::System::String::ConcatN({{{string.Join(", ", parts)}}})";
    }

    string Tuple(TupleExpressionSyntax tu)
    {
        var t = model.GetTypeInfo(tu).ConvertedType as INamedTypeSymbol ?? model.GetTypeInfo(tu).Type as INamedTypeSymbol;
        if (t == null || !t.IsTupleType)
        {
            Warn(tu, "tuple without tuple type");
            return "/*tuple*/0";
        }
        var elems = tu.Arguments.Select((a, i) =>
        {
            var et = t.TupleElements[i].Type;
            var code = Expr(a.Expression);
            var at = model.GetTypeInfo(a.Expression).ConvertedType ?? model.GetTypeInfo(a.Expression).Type;
            if (at != null && !SymbolEqualityComparer.Default.Equals(at, et) && !model.GetConstantValue(a.Expression).HasValue)
                code = $"static_cast<{Names.TypeRef(et)}>({code})";
            else if (model.GetConstantValue(a.Expression) is { HasValue: true } cv)
                code = Names.Literal(cv.Value, et);
            return code;
        });
        return $"{Names.TypeName(t)}{{{string.Join(", ", elems)}}}";
    }
}
