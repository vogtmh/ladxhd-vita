using System;
using System.IO;
using ProjectZ.InGame.Overlay;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.SaveLoad
{
    public static class AchievementManager
    {
        public static int Count = 110;
        private static bool[] _earned = new bool[Count];

        private const string SharedFileName = "achievements";

        public static bool IsEarned(int index) => index >= 0 && index < _earned.Length && _earned[index];

        public static void SetEarned(int index, bool value)
        {
            if (index >= 0 && index < _earned.Length)
                _earned[index] = value;
        }

        public static void Earn(int index, bool save = true)
        {
            if (index < 0 || index >= _earned.Length || _earned[index])
                return;

            _earned[index] = true;

            if (save)
                Save();

            Game1.AudioManager.PlaySoundEffect("D360-25-19");

            if (!GameSettings.HideAchievement)
                AchievementOverlay.Push(index);
        }

        public static void Reset()
        {
            _earned = new bool[Count];
        }

        public static string GetFilePath()
        {
            return Game1.UserDataPaths.AchievementsFilePath;
        }

        public static void Save()
        {
            for (var i = 0; i < Values.SaveRetries; i++)
            {
                try
                {
                    SaveOnce();
                    MirrorToShared();
                    return;
                }
                catch (Exception) { }
            }
            System.Diagnostics.Debug.WriteLine("Error while saving achievements.");
        }

        private static void SaveOnce()
        {
            var filePath = GetFilePath();

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tempPath = filePath + ".tmp";

            using (var fs = File.Open(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fs))
            {
                for (var i = 0; i < _earned.Length; i++)
                    writer.WriteLine("achievement" + i + " " + (_earned[i] ? "true" : "false"));
            }

            if (File.Exists(filePath))
                File.Delete(filePath);

            File.Move(tempPath, filePath);
        }

        public static bool Load()
        {
            Reset();

            var values = ReadFile(GetFilePath(), Values.LoadRetries);
            if (values == null)
                return false;

            _earned = values;
            return true;
        }

        private static bool[] ReadFile(string filePath, int retries)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            for (var attempt = 0; attempt < retries; attempt++)
            {
                try
                {
                    var result = new bool[Count];

                    using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var reader = new StreamReader(fs);

                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        var parts = line.Split(new[] { ' ' }, 2, StringSplitOptions.None);
                        if (parts.Length < 2)
                            continue;

                        var key = parts[0];
                        if (!key.StartsWith("achievement", StringComparison.Ordinal))
                            continue;

                        if (!int.TryParse(key.Substring("achievement".Length), out var index))
                            continue;

                        if (!bool.TryParse(parts[1], out var value))
                            continue;

                        if (index >= 0 && index < result.Length)
                            result[index] = value;
                    }
                    return result;
                }
                catch { }
            }
            return null;
        }

        private static bool SharedAvailable(out ISharedSaveService sharedSaves)
        {
            sharedSaves = Game1.SharedSaveService;
            return GameSettings.SharedStorage && sharedSaves != null &&
                   sharedSaves.IsSupported && sharedSaves.HasAccess;
        }

        // The scoped achievements file sits in the user-data root, one level above SaveFiles.
        // Mirror that on the shared side so it lands next to the shared SaveFiles folder.
        private static string GetSharedPath(ISharedSaveService sharedSaves)
        {
            var root = sharedSaves.SharedRootDirectory;
            return string.IsNullOrEmpty(root) ? null : Path.Combine(root, SharedFileName);
        }

        // Builds prior to this change wrote the file inside the shared SaveFiles folder.
        private static string GetLegacySharedPath(ISharedSaveService sharedSaves)
        {
            var saveDir = sharedSaves.SharedSaveDirectory;
            return string.IsNullOrEmpty(saveDir) ? null : Path.Combine(saveDir, SharedFileName);
        }

        public static void MirrorToShared()
        {
            // Do not try to mirror if the user has not enabled it.
            if (!SharedAvailable(out var sharedSaves))
                return;

            try
            {
                var root = sharedSaves.SharedRootDirectory;
                if (string.IsNullOrEmpty(root))
                    return;

                sharedSaves.EnsureDirectory(root);

                var scoped = GetFilePath();
                if (!sharedSaves.FileExists(scoped))
                    return;

                sharedSaves.CopyFile(scoped, Path.Combine(root, SharedFileName));
            }
            catch { }
        }

        public static void SyncWithShared()
        {
            // Do not try to sync if the user has not enabled it.
            if (!SharedAvailable(out var sharedSaves))
                return;

            // As with many other "try" statements, we don't want to ever crash due to anything going wrong.
            try
            {
                // Attempt to merge the achievements file from shared storage to scoped storage.
                var changed = Union(ReadFile(GetFilePath(), Values.LoadRetries));
                changed |= MergeShared(GetSharedPath(sharedSaves), sharedSaves);
                changed |= MergeShared(GetLegacySharedPath(sharedSaves), sharedSaves);
                Save();

                // Used for testing only, but doesn't hurt to keep as release builds won't try to run this.
                if (changed)
                    System.Diagnostics.Debug.WriteLine("Achievements merged from shared storage.");
            }
            catch { }
        }

        private static bool MergeShared(string sharedFile, ISharedSaveService sharedSaves)
        {
            // If the achievements file is empty or doesn't exist don't try to merge.
            if (string.IsNullOrEmpty(sharedFile) || !sharedSaves.FileExists(sharedFile))
                return false;

            var staging = GetFilePath() + ".shared";

            try
            {
                var dir = Path.GetDirectoryName(staging);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                sharedSaves.CopyFile(sharedFile, staging);
                return Union(ReadFile(staging, Values.LoadRetries));
            }
            catch
            {
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(staging))
                        File.Delete(staging);
                }
                catch { }
            }
        }

        private static bool Union(bool[] other)
        {
            if (other == null)
                return false;

            var changed = false;
            for (var i = 0; i < _earned.Length && i < other.Length; i++)
            {
                if (other[i] && !_earned[i])
                {
                    _earned[i] = true;
                    changed = true;
                }
            }
            return changed;
        }

        public static void Delete()
        {
            Reset();

            // When deleting achievements we try to delete both the shared and scoped version.
            for (var i = 0; i < Values.SaveRetries; i++)
            {
                try
                {
                    DeleteOnce();
                    DeleteShared();
                    return;
                }
                catch (Exception) { }
            }
            System.Diagnostics.Debug.WriteLine("Error while deleting achievements.");
        }

        private static void DeleteOnce()
        {
            var filePath = GetFilePath();

            if (File.Exists(filePath))
                File.Delete(filePath);

            var tempPath = filePath + ".tmp";
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            var stagingPath = filePath + ".shared";
            if (File.Exists(stagingPath))
                File.Delete(stagingPath);
        }

        private static void DeleteShared()
        {
            // Do not try to delete if the user has not enabled it.
            if (!SharedAvailable(out var sharedSaves))
                return;

            // Try to delete the achievements file in shared storage.
            try
            {
                DeleteSharedFile(GetSharedPath(sharedSaves), sharedSaves);
                DeleteSharedFile(GetLegacySharedPath(sharedSaves), sharedSaves);
            }
            catch { }
        }

        private static void DeleteSharedFile(string shared, ISharedSaveService sharedSaves)
        {
            if (string.IsNullOrEmpty(shared))
                return;

            var tmp = shared + ".tmp";

            if (sharedSaves.FileExists(shared))
                sharedSaves.DeleteFile(shared);
            if (sharedSaves.FileExists(tmp))
                sharedSaves.DeleteFile(tmp);
        }
    }
}
