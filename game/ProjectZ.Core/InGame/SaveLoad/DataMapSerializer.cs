using System.IO;
using ProjectZ.InGame.Things;
using System;
namespace ProjectZ.InGame.SaveLoad
{
    public class DataMapSerializer
    {
        public static void SaveDialog(string[,] data)
        {
            Game1.UiManager.EnqueueDeferredAction(() => SaveDialogInternal(data));
        }

        private static void SaveDialogInternal(string[,] data)
        {
            if (Game1.FileDialogService.TrySave("data", null, out var path))
                SaveData(path, data);
        }

        public static void LoadDialog(Action<string[,]> onLoad)
        {
            Game1.UiManager.EnqueueDeferredAction(() => LoadDialogInternal(onLoad));
        }

        private static void LoadDialogInternal(Action<string[,]> onLoad)
        {
            if (Game1.FileDialogService.TryOpen("data", null, out var path))
                onLoad(LoadData(path));
        }

        public static void SaveData(string path, string[,] data)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            using var writer = new StreamWriter(path);
            writer.WriteLine(data.GetLength(0));
            writer.WriteLine(data.GetLength(1));
            for (var y = 0; y < data.GetLength(1); y++)
            {
                var line = "";
                for (var x = 0; x < data.GetLength(0); x++)
                    line += (data[x, y] ?? "") + ";";
                writer.WriteLine(line);
            }
        }

        public static string[,] LoadData(string path)
        {
            using var stream = GameFS.OpenReadAny(path);
            using var reader = new StreamReader(stream);
            if (!int.TryParse(reader.ReadLine(), out var lengthX) ||
                !int.TryParse(reader.ReadLine(), out var lengthY) ||
                lengthX < 0 || lengthY < 0)
                throw new InvalidDataException($"Invalid .data header: '{path}'");
            var output = new string[lengthX, lengthY];
            for (int y = 0; y < lengthY; y++)
                for (int x = 0; x < lengthX; x++)
                    output[x, y] = "";
            for (int y = 0; y < lengthY; y++)
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;
                var split = line.Split(';');
                int max = Math.Min(lengthX, split.Length);
                for (int x = 0; x < max; x++)
                    output[x, y] = split[x] ?? "";
            }
            return output;
        }
    }
}
