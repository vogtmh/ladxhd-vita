using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cs2Cpp;

// Lists every external API member the game touches: this is the surface the C++ runtime must provide.
static class Survey
{
    public static void Run(CSharpCompilation comp, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Console.WriteLine($"compile errors: {errors.Count}");
        foreach (var e in errors.Take(20)) Console.WriteLine("  " + e);

        var members = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var kinds = new Dictionary<SyntaxKind, int>();
        foreach (var tree in comp.SyntaxTrees)
        {
            var model = comp.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                var k = node.Kind();
                kinds[k] = kinds.GetValueOrDefault(k) + 1;
                ISymbol extra = node switch
                {
                    BaseObjectCreationExpressionSyntax => model.GetSymbolInfo(node).Symbol,
                    BinaryExpressionSyntax or PrefixUnaryExpressionSyntax or AssignmentExpressionSyntax => (model.GetOperation(node) switch
                    {
                        Microsoft.CodeAnalysis.Operations.IBinaryOperation b => b.OperatorMethod,
                        Microsoft.CodeAnalysis.Operations.IUnaryOperation u => u.OperatorMethod,
                        Microsoft.CodeAnalysis.Operations.ICompoundAssignmentOperation c => c.OperatorMethod,
                        _ => null,
                    }),
                    MethodDeclarationSyntax or PropertyDeclarationSyntax => (model.GetDeclaredSymbol(node) switch
                    {
                        IMethodSymbol dm => dm.OverriddenMethod,
                        IPropertySymbol dp => dp.OverriddenProperty,
                        _ => null,
                    }),
                    _ => null,
                };
                if (extra != null && !SymbolEqualityComparer.Default.Equals(extra.ContainingAssembly, comp.Assembly))
                {
                    var key2 = extra.ContainingType.ToDisplayString();
                    if (!members.TryGetValue(key2, out var set2)) members[key2] = set2 = new SortedSet<string>(StringComparer.Ordinal);
                    set2.Add(extra.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                }
                if (node is not (IdentifierNameSyntax or GenericNameSyntax)) continue;
                var sym = model.GetSymbolInfo(node).Symbol;
                if (sym == null) continue;
                if (sym is IMethodSymbol m && m.ReducedFrom != null) sym = m.ReducedFrom;
                sym = sym.OriginalDefinition;
                if (SymbolEqualityComparer.Default.Equals(sym.ContainingAssembly, comp.Assembly)) continue;
                if (sym is INamespaceSymbol || sym is ILocalSymbol || sym is IParameterSymbol) continue;
                var type = sym is ITypeSymbol ts ? ts : sym.ContainingType;
                if (type == null) continue;
                var key = type.ToDisplayString();
                if (!members.TryGetValue(key, out var set)) members[key] = set = new SortedSet<string>(StringComparer.Ordinal);
                if (sym is not ITypeSymbol) set.Add(sym.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            }
        }
        using (var w = new StreamWriter(Path.Combine(outDir, "external_api.txt")))
            foreach (var (t, set) in members)
            {
                w.WriteLine(t);
                foreach (var s in set) w.WriteLine("    " + s);
            }
        using (var w = new StreamWriter(Path.Combine(outDir, "syntax_kinds.txt")))
            foreach (var (k, n) in kinds.OrderByDescending(p => p.Value))
                w.WriteLine($"{n,8} {k}");
        Console.WriteLine($"external types: {members.Count}, members: {members.Values.Sum(s => s.Count)}");
    }
}
