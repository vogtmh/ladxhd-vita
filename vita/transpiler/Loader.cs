using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cs2Cpp;

static class Loader
{
    public static CSharpCompilation Load(string coreDir, string patchDir)
    {
        var patches = Patches.Read(patchDir);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var nuget = Path.Combine(home, ".nuget", "packages");
        var refPack = Directory.GetDirectories(Path.Combine(home, ".dotnet", "packs", "Microsoft.NETCore.App.Ref"))
            .OrderBy(d => d).Last();
        var refDir = Path.Combine(refPack, "ref", "net9.0");

        var refs = Directory.GetFiles(refDir, "*.dll").Select(p => MetadataReference.CreateFromFile(p)).ToList();
        refs.Add(MetadataReference.CreateFromFile(Path.Combine(nuget, "monogame.framework.native", "3.8.5.1", "lib", "netstandard2.1", "MonoGame.Framework.dll")));
        refs.Add(MetadataReference.CreateFromFile(Path.Combine(nuget, "monogame.extended", "6.1.1", "lib", "net8.0", "MonoGame.Extended.dll")));
        refs.Add(MetadataReference.CreateFromFile(Path.Combine(nuget, "nvorbis", "0.10.5", "lib", "netstandard2.0", "NVorbis.dll")));

        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp13, preprocessorSymbols: new[] { "RELEASE", "NET", "NET9_0" });
        var trees = Directory.EnumerateFiles(coreDir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => CSharpSyntaxTree.ParseText(Patches.Apply(patches, coreDir, p, File.ReadAllText(p)), parseOptions, p))
            .ToList();
        Patches.CheckAllApplied(patches);

        return CSharpCompilation.Create("ProjectZ.Core", trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Disable));
    }
}
