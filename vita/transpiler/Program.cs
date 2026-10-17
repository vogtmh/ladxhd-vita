using Cs2Cpp;

var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var core = Path.Combine(repo, "game", "ProjectZ.Core");
var vita = Path.Combine(repo, "vita");
var cmd = args.Length > 0 ? args[0] : "survey";
var comp = Loader.Load(core, Path.Combine(vita, "port", "patches"));
switch (cmd)
{
    case "survey":
        Survey.Run(comp, Path.Combine(vita, "build", "survey"));
        break;
    case "gen":
        new Emitter(comp, Path.Combine(vita, "build", "gen"), Path.Combine(vita, "port")).Run();
        break;
    default:
        Console.Error.WriteLine($"unknown command {cmd}");
        return 1;
}
return 0;
