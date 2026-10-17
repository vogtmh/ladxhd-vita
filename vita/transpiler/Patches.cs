namespace Cs2Cpp;

// Port-only source edits, applied in memory before compiling (the game source stays untouched).
// Format of port/patches/*.txt, repeated per edit:
//   FILE <path relative to ProjectZ.Core>
//   FIND
//   <exact lines>
//   REPLACE
//   <new lines>
//   END
static class Patches
{
    public sealed class Edit
    {
        public string File = "", Find = "", Replace = "", Source = "";
        public bool Applied;
    }

    public static List<Edit> Read(string dir)
    {
        var edits = new List<Edit>();
        if (!Directory.Exists(dir)) return edits;
        foreach (var path in Directory.GetFiles(dir, "*.txt").OrderBy(p => p, StringComparer.Ordinal))
        {
            Edit cur = null;
            string mode = null;
            var buf = new List<string>();
            foreach (var line in System.IO.File.ReadAllLines(path))
            {
                if (line.StartsWith("FILE "))
                {
                    cur = new Edit { File = line[5..].Trim().Replace('\\', '/'), Source = Path.GetFileName(path) };
                    mode = null;
                }
                else if (line == "FIND" || line == "REPLACE" || line == "END")
                {
                    if (cur == null) throw new Exception($"{path}: {line} without FILE");
                    if (mode == "FIND") cur.Find = string.Join("\n", buf);
                    if (mode == "REPLACE") cur.Replace = string.Join("\n", buf);
                    buf.Clear();
                    mode = line == "END" ? null : line;
                    if (line == "END") edits.Add(cur);
                }
                else if (mode != null) buf.Add(line);
            }
        }
        return edits;
    }

    public static string Apply(List<Edit> edits, string coreDir, string path, string text)
    {
        var rel = Path.GetRelativePath(coreDir, path).Replace('\\', '/');
        foreach (var e in edits.Where(e => e.File == rel))
        {
            var normalized = text.Replace("\r\n", "\n");
            int at = normalized.IndexOf(e.Find, StringComparison.Ordinal);
            if (at < 0) throw new Exception($"patch {e.Source}: text not found in {rel}:\n{e.Find}");
            if (normalized.IndexOf(e.Find, at + 1, StringComparison.Ordinal) >= 0)
                throw new Exception($"patch {e.Source}: text is ambiguous in {rel}:\n{e.Find}");
            text = normalized[..at] + e.Replace + normalized[(at + e.Find.Length)..];
            e.Applied = true;
        }
        return text;
    }

    public static void CheckAllApplied(List<Edit> edits)
    {
        foreach (var e in edits.Where(e => !e.Applied))
            throw new Exception($"patch {e.Source}: file {e.File} not found");
    }
}
