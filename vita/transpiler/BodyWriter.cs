using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Cs2Cpp;

// Translates method bodies (statements and expressions) to C++.
partial class BodyWriter
{
    readonly Emitter em;
    readonly CSharpCompilation comp;
    SemanticModel model;
    readonly INamedTypeSymbol type;
    ISymbol member;               // method/property/ctor being translated
    bool isStaticCtx;
    int tempCounter;
    List<string> pending = new();  // declarations hoisted before the current statement
    readonly HashSet<ISymbol> boxed = new(SymbolEqualityComparer.Default);
    readonly Dictionary<ISymbol, string> rename = new(SymbolEqualityComparer.Default);
    string bindingReceiver;        // for ?. member bindings
    ITypeSymbol bindingType;
    ITypeSymbol iteratorElem;      // non-null inside iterator methods
    int lambdaDepth;
    readonly Stack<ITypeSymbol> returnTypes = new();
    public readonly HashSet<string> Warnings = new();

    public BodyWriter(Emitter em, INamedTypeSymbol type, ISymbol member)
    {
        this.em = em;
        comp = em.Comp;
        this.type = type;
        this.member = member;
        isStaticCtx = member?.IsStatic ?? true;
    }

    SemanticModel ModelFor(SyntaxNode n) => model = comp.GetSemanticModel(n.SyntaxTree);

    string Tmp(string p = "t") => $"__{p}{++tempCounter}";

    void Warn(SyntaxNode n, string msg)
    {
        var loc = n.GetLocation().GetLineSpan();
        Warnings.Add($"{Path.GetFileName(loc.Path)}:{loc.StartLinePosition.Line + 1}: {msg}");
    }

    // ------------------------------------------------------------------ entry points

    public string MethodBody(SyntaxNode body, IMethodSymbol m)
    {
        ModelFor(body);
        AnalyzeCaptures(body, m);
        var sb = new StringBuilder();
        sb.Append("{\n");
        foreach (var p in m.Parameters)
            if (boxed.Contains(p))
            {
                var n = "__ref_" + Names.Escape(p.Name);
                sb.Append($"    auto* {n} = ::System::MakeRef<{Names.TypeRef(p.Type)}>({Names.Escape(p.Name)});\n");
                rename[p] = n + "->v";
            }
        returnTypes.Push(m.ReturnsVoid ? null : m.ReturnType);
        bool iterator = IsIterator(body);
        if (iterator)
        {
            iteratorElem = ((INamedTypeSymbol)m.ReturnType).TypeArguments[0];
            sb.Append($"    auto* __yield = ::System::New<::System::Collections::Generic::List<{Names.TypeRef(iteratorElem)}>>();\n");
        }
        if (body is BlockSyntax block)
            sb.Append(Statements(block.Statements, 1));
        else if (body is ArrowExpressionClauseSyntax arrow)
        {
            var old = pending;
            pending = new List<string>();
            string e = m.ReturnsVoid || m.MethodKind == MethodKind.Constructor ? StmtExpr(arrow.Expression) : Expr(arrow.Expression);
            foreach (var d in pending) sb.Append("    ").Append(d).Append('\n');
            pending = old;
            sb.Append(m.ReturnsVoid || m.MethodKind == MethodKind.Constructor ? $"    {e};\n" : $"    return {e};\n");
        }
        if (iterator) sb.Append("    return __yield;\n");
        returnTypes.Pop();
        sb.Append('}');
        return sb.ToString();
    }

    public string InitExpr(ExpressionSyntax e, out List<string> decls)
    {
        ModelFor(e);
        var old = pending;
        pending = new List<string>();
        var r = Expr(e);
        decls = pending;
        pending = old;
        return r;
    }

    public string ConstructorInitArgs(ConstructorInitializerSyntax init, out IMethodSymbol ctor, out List<string> decls)
    {
        ModelFor(init);
        var old = pending;
        pending = new List<string>();
        ctor = model.GetSymbolInfo(init).Symbol as IMethodSymbol;
        var op = model.GetOperation(init) as IInvocationOperation;
        string args = op != null ? Args(op.Arguments, ctor) : "";
        decls = pending;
        pending = old;
        return args;
    }

    static bool IsIterator(SyntaxNode body) =>
        body.DescendantNodes(n => n is not (LambdaExpressionSyntax or LocalFunctionStatementSyntax or AnonymousMethodExpressionSyntax))
            .Any(n => n is YieldStatementSyntax);

    // Variables captured by lambdas and written after initialization get a shared heap cell.
    void AnalyzeCaptures(SyntaxNode body, IMethodSymbol m)
    {
        var lambdas = body.DescendantNodes().Where(n => n is AnonymousFunctionExpressionSyntax).ToList();
        if (lambdas.Count == 0) return;
        var captured = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var l in lambdas)
        {
            try
            {
                var df = model.AnalyzeDataFlow(l);
                if (df.Succeeded)
                    foreach (var s in df.CapturedInside) captured.Add(s);
            }
            catch { }
        }
        if (captured.Count == 0) return;
        foreach (var n in body.DescendantNodesAndSelf())
        {
            ExpressionSyntax target = n switch
            {
                AssignmentExpressionSyntax a => a.Left,
                PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression } p => p.Operand,
                PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression } p => p.Operand,
                ArgumentSyntax { RefKindKeyword.RawKind: (int)SyntaxKind.OutKeyword or (int)SyntaxKind.RefKeyword } a => a.Expression,
                _ => null,
            };
            if (target == null) continue;
            var targets = target is TupleExpressionSyntax te ? te.Arguments.Select(a => a.Expression) : new[] { target };
            foreach (var t in targets)
            {
                var s = model.GetSymbolInfo(t).Symbol;
                if (s is ILocalSymbol or IParameterSymbol && captured.Contains(s)) boxed.Add(s);
            }
        }
    }

    // ------------------------------------------------------------------ statements

    public string Statements(SyntaxList<StatementSyntax> stmts, int ind)
    {
        var sb = new StringBuilder();
        // Local functions: std::function declared at the block top, assigned before the first statement
        // that uses them (captured locals are declared by then).
        var localFns = stmts.OfType<LocalFunctionStatementSyntax>().ToList();
        foreach (var lf in localFns) sb.Append(LocalFunctionDecl(lf, ind));
        var defineAt = new Dictionary<int, List<LocalFunctionStatementSyntax>>();
        foreach (var lf in localFns)
        {
            int own = stmts.IndexOf(lf);
            int pos = own;
            var name = lf.Identifier.Text;
            for (int i = 0; i < own; i++)
                if (stmts[i].DescendantNodes().OfType<IdentifierNameSyntax>().Any(n => n.Identifier.Text == name)) { pos = i; break; }
            if (!defineAt.TryGetValue(pos, out var l)) defineAt[pos] = l = new List<LocalFunctionStatementSyntax>();
            l.Add(lf);
        }
        for (int i = 0; i < stmts.Count; i++)
        {
            if (defineAt.TryGetValue(i, out var fns))
                foreach (var lf in fns) sb.Append(LocalFunctionDef(lf, ind));
            if (stmts[i] is LocalFunctionStatementSyntax) continue;
            sb.Append(Stmt(stmts[i], ind));
        }
        return sb.ToString();
    }

    static string Ind(int n) => new string(' ', n * 4);

    string Stmt(StatementSyntax s, int ind)
    {
        var old = pending;
        pending = new List<string>();
        string body;
        try
        {
            body = StmtInner(s, ind);
        }
        catch (Exception ex)
        {
            Warn(s, "translator error: " + ex.Message);
            body = Ind(ind) + $"/* TRANSLATION ERROR: {ex.Message.Replace("*/", "")} */ ::System::ThrowInvalidOperation(\"untranslated\");\n";
        }
        var decls = pending;
        pending = old;
        if (decls.Count == 0) return body;
        var sb = new StringBuilder();
        foreach (var d in decls) sb.Append(Ind(ind)).Append(d).Append('\n');
        sb.Append(body);
        return sb.ToString();
    }

    // Statement used as a nested body (if/while): wrap hoisted declarations in a block.
    string SubStmt(StatementSyntax s, int ind)
    {
        if (s is BlockSyntax) return Stmt(s, ind);
        var inner = Stmt(s, ind + 1);
        return Ind(ind) + "{\n" + inner + Ind(ind) + "}\n";
    }

    string StmtInner(StatementSyntax s, int ind)
    {
        var I = Ind(ind);
        switch (s)
        {
            case BlockSyntax b:
                return I + "{\n" + Statements(b.Statements, ind + 1) + I + "}\n";
            case EmptyStatementSyntax:
                return I + ";\n";
            case LocalDeclarationStatementSyntax ld:
                return LocalDecl(ld, ind);
            case ExpressionStatementSyntax es:
                return I + StmtExpr(es.Expression) + ";\n";
            case IfStatementSyntax ifs:
            {
                var sb = new StringBuilder();
                sb.Append(I).Append("if (").Append(Cond(ifs.Condition)).Append(")\n");
                sb.Append(SubStmt(ifs.Statement, ind));
                if (ifs.Else != null)
                {
                    sb.Append(I).Append("else\n");
                    sb.Append(SubStmt(ifs.Else.Statement, ind));
                }
                return sb.ToString();
            }
            case WhileStatementSyntax ws:
            {
                var cond = Cond(ws.Condition);
                var body = SubStmt(ws.Statement, ind + (pending.Count > 0 ? 1 : 0));
                return WrapPending(ind, (i2) => Ind(i2) + "while (" + cond + ")\n" + body);
            }
            case DoStatementSyntax ds:
            {
                var body = SubStmt(ds.Statement, ind + 1);
                var cond = Cond(ds.Condition);
                return WrapPending(ind, (i2) => Ind(i2) + "do\n" + body + Ind(i2) + "while (" + cond + ");\n");
            }
            case ForStatementSyntax fs:
                return ForStmt(fs, ind);
            case ForEachStatementSyntax fe:
                return ForEach(fe, ind);
            case ForEachVariableStatementSyntax fev:
                return ForEachVar(fev, ind);
            case ReturnStatementSyntax rs:
            {
                if (iteratorElem != null && lambdaDepth == 0) return I + "return __yield;\n";
                if (rs.Expression == null) return I + "return;\n";
                return I + "return " + Expr(rs.Expression) + ";\n";
            }
            case YieldStatementSyntax ys:
                if (ys.IsKind(SyntaxKind.YieldBreakStatement)) return I + "return __yield;\n";
                return I + "__yield->Add(" + Expr(ys.Expression) + ");\n";
            case BreakStatementSyntax:
                return I + "break;\n";
            case ContinueStatementSyntax:
                return I + "continue;\n";
            case ThrowStatementSyntax ts:
                return ts.Expression == null ? I + "throw;\n" : I + "throw " + Expr(ts.Expression) + ";\n";
            case TryStatementSyntax tr:
                return TryStmt(tr, ind);
            case SwitchStatementSyntax sw:
                return SwitchStmt(sw, ind);
            case UsingStatementSyntax us:
                return UsingStmt(us, ind);
            case LockStatementSyntax lk:
            {
                var t = Tmp("lk");
                return I + "{\n" + Ind(ind + 1) + $"::System::Threading::LockGuard {t}(::System::AsObject({Expr(lk.Expression)}));\n" +
                       SubStmt(lk.Statement, ind + 1) + I + "}\n";
            }
            case CheckedStatementSyntax cs:
                return StmtInner(cs.Block, ind);
            case UnsafeStatementSyntax us2:
                return StmtInner(us2.Block, ind);
            case LocalFunctionStatementSyntax:
                return "";
            case LabeledStatementSyntax lab:
                return I + Names.Escape(lab.Identifier.Text) + ":\n" + StmtInner(lab.Statement, ind);
            case GotoStatementSyntax gs:
                if (gs.IsKind(SyntaxKind.GotoStatement)) return I + "goto " + ((IdentifierNameSyntax)gs.Expression).Identifier.Text + ";\n";
                Warn(s, "goto case unsupported");
                return I + "/* goto case */\n";
        }
        Warn(s, "unsupported statement " + s.Kind());
        return I + $"/* unsupported statement {s.Kind()} */\n";
    }

    string WrapPending(int ind, Func<int, string> make)
    {
        if (pending.Count == 0) return make(ind);
        var sb = new StringBuilder();
        sb.Append(Ind(ind)).Append("{\n");
        foreach (var d in pending) sb.Append(Ind(ind + 1)).Append(d).Append('\n');
        pending.Clear();
        sb.Append(make(ind + 1));
        sb.Append(Ind(ind)).Append("}\n");
        return sb.ToString();
    }

    string Cond(ExpressionSyntax e) => Expr(e);

    string LocalDecl(LocalDeclarationStatementSyntax ld, int ind)
    {
        var I = Ind(ind);
        var sb = new StringBuilder();
        foreach (var v in ld.Declaration.Variables)
        {
            var sym = (ILocalSymbol)model.GetDeclaredSymbol(v);
            var name = Names.Escape(v.Identifier.Text);
            var t = Names.TypeRef(sym.Type);
            if (sym.IsConst) continue;  // constants are inlined at use sites
            string init = v.Initializer != null ? Expr(v.Initializer.Value) : null;
            if (boxed.Contains(sym))
            {
                rename[sym] = name + "->v";
                sb.Append(I).Append($"auto* {name} = ::System::MakeRef<{t}>({init ?? ""});\n");
                continue;
            }
            sb.Append(I).Append(t).Append(' ').Append(name);
            sb.Append(init != null ? " = " + init : "{}").Append(";\n");
            if (ld.UsingKeyword.RawKind != 0)
                sb.Append(I).Append($"::System::Finally {Tmp("using")}([&]() {{ if ({name}) {DisposeCall(name, sym.Type)}; }});\n");
        }
        return sb.ToString();
    }

    string DisposeCall(string v, ITypeSymbol t)
    {
        return Names.IsRefType(t) ? $"{v}->Dispose()" : $"{v}.Dispose()";
    }

    string ForStmt(ForStatementSyntax fs, int ind)
    {
        var sb = new StringBuilder();
        string init = "";
        if (fs.Declaration != null)
        {
            var parts = new List<string>();
            string typeStr = null;
            foreach (var v in fs.Declaration.Variables)
            {
                var sym = (ILocalSymbol)model.GetDeclaredSymbol(v);
                typeStr ??= Names.TypeRef(sym.Type);
                var name = Names.Escape(v.Identifier.Text);
                if (boxed.Contains(sym))
                {
                    Warn(fs, "captured+mutated for-loop variable");
                    rename[sym] = name + "->v";
                    typeStr = "auto*";
                    parts.Add($"{name} = ::System::MakeRef<{Names.TypeRef(sym.Type)}>({(v.Initializer != null ? Expr(v.Initializer.Value) : "")})");
                    continue;
                }
                parts.Add(name + (v.Initializer != null ? " = " + Expr(v.Initializer.Value) : "{}"));
            }
            init = typeStr + " " + string.Join(", ", parts);
        }
        else if (fs.Initializers.Count > 0)
            init = string.Join(", ", fs.Initializers.Select(StmtExpr));
        var cond = fs.Condition != null ? Cond(fs.Condition) : "";
        var incr = string.Join(", ", fs.Incrementors.Select(StmtExpr));
        var hasPending = pending.Count > 0;
        var body = SubStmt(fs.Statement, ind + (hasPending ? 1 : 0));
        return WrapPending(ind, i2 => Ind(i2) + $"for ({init}; {cond}; {incr})\n" + body);
    }

    string ForEach(ForEachStatementSyntax fe, int ind)
    {
        var info = model.GetForEachStatementInfo(fe);
        var sym = (ILocalSymbol)model.GetDeclaredSymbol(fe);
        var coll = ForEachSource(fe.Expression);
        var name = Names.Escape(fe.Identifier.Text);
        var elem = info.ElementType;
        var I = Ind(ind);
        if (TryEnumGetValues(fe.Expression, out _)) elem = sym.Type;
        if (elem != null && !SymbolEqualityComparer.Default.Equals(elem, sym.Type))
        {
            var tmp = Tmp("e");
            var conv = ConvertCode(tmp, elem, sym.Type, info.ElementConversion, true);
            var inner = Stmt(fe.Statement, ind + 1);
            return WrapPending(ind, i2 => Ind(i2) + $"for (auto {tmp} : {coll})\n" + Ind(i2) + "{\n" + Ind(i2 + 1) + $"{Names.TypeRef(sym.Type)} {name} = {conv};\n" + inner + Ind(i2) + "}\n");
        }
        var body = SubStmt(fe.Statement, ind + (pending.Count > 0 ? 1 : 0));
        return WrapPending(ind, i2 => Ind(i2) + $"for ({Names.TypeRef(sym.Type)} {name} : {coll})\n" + body);
    }

    string ForEachSource(ExpressionSyntax e)
    {
        // Enum.GetValues(typeof(E)) -> typed array
        if (TryEnumGetValues(e, out var ev)) return $"::System::Each({ev})";
        var t = model.GetTypeInfo(e).Type;
        var code = ExprNoConv(e);
        return $"::System::Each({code})";
    }

    bool TryEnumGetValues(ExpressionSyntax e, out string code)
    {
        code = null;
        if (e is CastExpressionSyntax ce) e = ce.Expression;
        if (e is InvocationExpressionSyntax inv && model.GetSymbolInfo(inv).Symbol is IMethodSymbol m &&
            m.Name == "GetValues" && m.ContainingType.ToDisplayString() == "System.Enum" &&
            inv.ArgumentList.Arguments.Count == 1 && inv.ArgumentList.Arguments[0].Expression is TypeOfExpressionSyntax to)
        {
            var et = model.GetTypeInfo(to.Type).Type;
            code = $"::System::Enum::GetValues<{Names.TypeName(et)}>()";
            return true;
        }
        return false;
    }

    string ForEachVar(ForEachVariableStatementSyntax fe, int ind)
    {
        var coll = ForEachSource(fe.Expression);
        var tmp = Tmp("kv");
        var I = Ind(ind);
        var sb = new StringBuilder();
        var decls = new List<string>();
        var elemType = model.GetForEachStatementInfo(fe).ElementType;
        string Deconstruct(ExpressionSyntax target, string src, ITypeSymbol srcType)
        {
            var parts = new StringBuilder();
            IList<ExpressionSyntax> targets = target switch
            {
                DeclarationExpressionSyntax { Designation: ParenthesizedVariableDesignationSyntax pvd } =>
                    pvd.Variables.Select(v => (ExpressionSyntax)SyntaxFactory.IdentifierName(v is SingleVariableDesignationSyntax sv ? sv.Identifier.Text : "_")).ToList(),
                TupleExpressionSyntax tex => tex.Arguments.Select(a => a.Expression).ToList(),
                _ => new List<ExpressionSyntax>(),
            };
            var designations = target is DeclarationExpressionSyntax { Designation: ParenthesizedVariableDesignationSyntax pv } ? pv.Variables.ToList() : null;
            for (int i = 0; i < targets.Count; i++)
            {
                string access;
                ITypeSymbol ct;
                if (srcType is INamedTypeSymbol nt && nt.IsTupleType)
                {
                    access = $"{src}.Item{i + 1}";
                    ct = nt.TupleElements[i].Type;
                }
                else
                {
                    access = i == 0 ? $"{src}.get_Key()" : $"{src}.get_Value()";
                    ct = ((INamedTypeSymbol)srcType).TypeArguments[i];
                }
                if (designations != null)
                {
                    if (designations[i] is SingleVariableDesignationSyntax sv)
                    {
                        var ls = (ILocalSymbol)model.GetDeclaredSymbol(sv);
                        parts.Append(Ind(ind + 1)).Append($"{Names.TypeRef(ls.Type)} {Names.Escape(sv.Identifier.Text)} = {access};\n");
                    }
                }
                else
                    parts.Append(Ind(ind + 1)).Append($"{ExprNoConv(targets[i])} = {access};\n");
            }
            return parts.ToString();
        }
        var dec = Deconstruct(fe.Variable, tmp, elemType);
        var inner = Stmt(fe.Statement, ind + 1);
        return WrapPending(ind, i2 => Ind(i2) + $"for (auto {tmp} : {coll})\n" + Ind(i2) + "{\n" + dec + inner + Ind(i2) + "}\n");
    }

    string TryStmt(TryStatementSyntax tr, int ind)
    {
        var I = Ind(ind);
        var sb = new StringBuilder();
        int inner = ind;
        if (tr.Finally != null)
        {
            sb.Append(I).Append("{\n");
            inner = ind + 1;
            var fin = Statements(tr.Finally.Block.Statements, inner + 1);
            sb.Append(Ind(inner)).Append($"::System::Finally {Tmp("fin")}([&]() {{\n{fin}{Ind(inner)}}});\n");
        }
        if (tr.Catches.Count > 0)
        {
            sb.Append(Ind(inner)).Append("try\n");
            sb.Append(Stmt(tr.Block, inner));
            foreach (var c in tr.Catches)
            {
                string decl;
                if (c.Declaration == null) decl = "::System::Exception* __ex";
                else
                {
                    var ct = model.GetTypeInfo(c.Declaration.Type).Type;
                    var n = c.Declaration.Identifier.RawKind != 0 ? Names.Escape(c.Declaration.Identifier.Text) : Tmp("ex");
                    decl = $"{Names.TypeRef(ct)} {n}";
                }
                sb.Append(Ind(inner)).Append($"catch ({decl})\n");
                if (c.Filter != null)
                {
                    sb.Append(Ind(inner)).Append("{\n");
                    sb.Append(Ind(inner + 1)).Append($"if (!({Expr(c.Filter.FilterExpression)})) throw;\n");
                    sb.Append(Stmt(c.Block, inner + 1));
                    sb.Append(Ind(inner)).Append("}\n");
                }
                else sb.Append(Stmt(c.Block, inner));
            }
        }
        else sb.Append(Stmt(tr.Block, inner));
        if (tr.Finally != null) sb.Append(I).Append("}\n");
        return sb.ToString();
    }

    string UsingStmt(UsingStatementSyntax us, int ind)
    {
        var I = Ind(ind);
        var sb = new StringBuilder();
        sb.Append(I).Append("{\n");
        var disposes = new List<string>();
        if (us.Declaration != null)
        {
            foreach (var v in us.Declaration.Variables)
            {
                var sym = (ILocalSymbol)model.GetDeclaredSymbol(v);
                var name = Names.Escape(v.Identifier.Text);
                sb.Append(Ind(ind + 1)).Append($"{Names.TypeRef(sym.Type)} {name} = {Expr(v.Initializer.Value)};\n");
                disposes.Add($"if ({name}) {DisposeCall(name, sym.Type)};");
            }
        }
        else if (us.Expression != null)
        {
            var t = Tmp("u");
            var ty = model.GetTypeInfo(us.Expression).Type;
            sb.Append(Ind(ind + 1)).Append($"auto {t} = {Expr(us.Expression)};\n");
            disposes.Add($"if ({t}) {DisposeCall(t, ty)};");
        }
        foreach (var d in pending) sb.Append(Ind(ind + 1)).Append(d).Append('\n');
        pending.Clear();
        disposes.Reverse();
        sb.Append(Ind(ind + 1)).Append($"::System::Finally {Tmp("using")}([&]() {{ {string.Join(" ", disposes)} }});\n");
        sb.Append(SubStmt(us.Statement, ind + 1));
        sb.Append(I).Append("}\n");
        return sb.ToString();
    }

    string SwitchStmt(SwitchStatementSyntax sw, int ind)
    {
        var I = Ind(ind);
        var gt = model.GetTypeInfo(sw.Expression).Type;
        bool simple = gt != null && (gt.TypeKind == TypeKind.Enum || IsIntegral(gt) || gt.SpecialType == SpecialType.System_Char || gt.SpecialType == SpecialType.System_Boolean)
                      && sw.Sections.All(sec => sec.Labels.All(l => l is DefaultSwitchLabelSyntax || l is CaseSwitchLabelSyntax));
        var sb = new StringBuilder();
        if (simple)
        {
            sb.Append(I).Append($"switch ({ExprNoConv(sw.Expression)})\n").Append(I).Append("{\n");
            foreach (var sec in sw.Sections)
            {
                foreach (var l in sec.Labels)
                {
                    if (l is DefaultSwitchLabelSyntax) sb.Append(I).Append("default:\n");
                    else if (l is CaseSwitchLabelSyntax cl)
                    {
                        var cv = model.GetConstantValue(cl.Value);
                        sb.Append(I).Append("case ").Append(cv.HasValue ? Names.Literal(cv.Value, gt) : ExprNoConv(cl.Value)).Append(":\n");
                    }
                }
                sb.Append(I).Append("{\n").Append(Statements(sec.Statements, ind + 1)).Append(I).Append("}\n");
            }
            sb.Append(I).Append("}\n");
            return sb.ToString();
        }
        // General form: if-chain inside switch(0) so that `break` leaves the switch and `continue` reaches the loop.
        var subj = Tmp("sw");
        sb.Append(I).Append("switch (0)\n").Append(I).Append("{\n").Append(I).Append("default:\n").Append(I).Append("{\n");
        sb.Append(Ind(ind + 1)).Append($"auto {subj} = {ExprNoConv(sw.Expression)};\n");
        SwitchSectionSyntax defaultSec = null;
        bool first = true;
        foreach (var sec in sw.Sections)
        {
            var conds = new List<string>();
            var old = pending;
            pending = new List<string>();
            foreach (var l in sec.Labels)
            {
                switch (l)
                {
                    case DefaultSwitchLabelSyntax:
                        defaultSec = sec;
                        break;
                    case CaseSwitchLabelSyntax cl:
                        conds.Add(ConstantMatch(cl.Value, subj, gt));
                        break;
                    case CasePatternSwitchLabelSyntax cp:
                    {
                        var c = Pattern(cp.Pattern, subj, gt);
                        if (cp.WhenClause != null) c = $"({c}) && ({Expr(cp.WhenClause.Condition)})";
                        conds.Add(c);
                        break;
                    }
                }
            }
            var decls = pending;
            pending = old;
            if (conds.Count == 0) continue;
            foreach (var d in decls) sb.Append(Ind(ind + 1)).Append(d).Append('\n');
            sb.Append(Ind(ind + 1)).Append(first ? "if (" : "else if (").Append(string.Join(" || ", conds.Select(c => "(" + c + ")"))).Append(")\n");
            sb.Append(Ind(ind + 1)).Append("{\n").Append(Statements(sec.Statements, ind + 2)).Append(Ind(ind + 1)).Append("}\n");
            first = false;
        }
        if (defaultSec != null)
        {
            if (!first) sb.Append(Ind(ind + 1)).Append("else\n");
            sb.Append(Ind(ind + 1)).Append("{\n").Append(Statements(defaultSec.Statements, ind + 2)).Append(Ind(ind + 1)).Append("}\n");
        }
        sb.Append(I).Append("}\n").Append(I).Append("}\n");
        return sb.ToString();
    }

    string ConstantMatch(ExpressionSyntax value, string subj, ITypeSymbol subjType)
    {
        var cv = model.GetConstantValue(value);
        if (!cv.HasValue) return $"{subj} == {Expr(value)}";
        return ConstCompare(subj, subjType, cv.Value, model.GetTypeInfo(value).Type);
    }

    string ConstCompare(string subj, ITypeSymbol subjType, object value, ITypeSymbol constType)
    {
        if (value == null)
        {
            if (IsNullable(subjType)) return $"!{subj}.hasValue";
            return $"{subj} == nullptr";
        }
        if (value is string s)
            return $"::System::String::op_Equality({AsString(subj, subjType)}, STR(u{Names.StringLit(s)}))";
        if (IsNullable(subjType))
        {
            var inner = ((INamedTypeSymbol)subjType).TypeArguments[0];
            return $"({subj}.hasValue && {subj}.value == {Names.Literal(value, inner)})";
        }
        if (subjType == null || subjType.SpecialType == SpecialType.System_Object || subjType.TypeKind == TypeKind.Interface)
        {
            var ct = constType ?? subjType;
            return $"(::System::isBoxed<{Names.TypeRef(ct)}>({subj}) && ::System::unbox<{Names.TypeRef(ct)}>({subj}) == {Names.Literal(value, ct)})";
        }
        return $"{subj} == {Names.Literal(value, subjType)}";
    }

    string AsString(string subj, ITypeSymbol t) =>
        t != null && t.SpecialType == SpecialType.System_String ? subj : $"::System::as<::System::String>({subj})";

    // ------------------------------------------------------------------ local functions

    string LocalFunctionDecl(LocalFunctionStatementSyntax lf, int ind)
    {
        var sym = (IMethodSymbol)model.GetDeclaredSymbol(lf);
        var sig = $"{Names.ReturnType(sym)}({string.Join(", ", sym.Parameters.Select(Names.ParamType))})";
        return Ind(ind) + $"std::function<{sig}> {Names.Escape(lf.Identifier.Text)};\n";
    }

    string LocalFunctionDef(LocalFunctionStatementSyntax lf, int ind)
    {
        var sym = (IMethodSymbol)model.GetDeclaredSymbol(lf);
        var ps = string.Join(", ", sym.Parameters.Select(p => Names.ParamType(p) + " " + Names.Escape(p.Name)));
        var oldIter = iteratorElem;
        iteratorElem = null;
        lambdaDepth++;
        returnTypes.Push(sym.ReturnsVoid ? null : sym.ReturnType);
        string body;
        if (lf.Body != null) body = "{\n" + Statements(lf.Body.Statements, ind + 1) + Ind(ind) + "}";
        else
        {
            var old = pending;
            pending = new List<string>();
            var e = sym.ReturnsVoid ? StmtExpr(lf.ExpressionBody.Expression) : Expr(lf.ExpressionBody.Expression);
            var sb2 = new StringBuilder("{\n");
            foreach (var d in pending) sb2.Append(Ind(ind + 1)).Append(d).Append('\n');
            pending = old;
            sb2.Append(Ind(ind + 1)).Append(sym.ReturnsVoid ? e + ";\n" : "return " + e + ";\n").Append(Ind(ind)).Append('}');
            body = sb2.ToString();
        }
        returnTypes.Pop();
        lambdaDepth--;
        iteratorElem = oldIter;
        return Ind(ind) + $"{Names.Escape(lf.Identifier.Text)} = [&]({ps}) -> {Names.ReturnType(sym)} {body};\n";
    }

    // ------------------------------------------------------------------ helpers

    static bool IsIntegral(ITypeSymbol t) => t.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Int16
        or SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_UInt16 or SpecialType.System_UInt32 or SpecialType.System_UInt64;

    static bool IsFloat(ITypeSymbol t) => t != null && t.SpecialType is SpecialType.System_Single or SpecialType.System_Double;

    public static bool IsNullable(ITypeSymbol t) => t is INamedTypeSymbol nt && nt.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    static bool IsString(ITypeSymbol t) => t != null && t.SpecialType == SpecialType.System_String;

    bool IsDelegate(ITypeSymbol t) => t != null && t.TypeKind == TypeKind.Delegate;

    string Access(ITypeSymbol recvType) => recvType != null && (Names.IsRefType(recvType) || Names.IsTypeParamRef(recvType)) ? "->" : ".";
}
