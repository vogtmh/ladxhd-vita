using System;
using System.IO;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.SaveLoad
{
    internal static class SharedSaveSync
    {
        public static void SyncFromSharedIfEnabled()
        {
            var sharedSaves = Game1.SharedSaveService;
            if (!GameSettings.SharedStorage || sharedSaves == null || !sharedSaves.IsSupported || !sharedSaves.HasAccess)
                return;

            SyncSaveSlots(sharedSaves);
            AchievementManager.SyncWithShared();
        }

        private static void SyncSaveSlots(ISharedSaveService sharedSaves)
        {
            try
            {
                var sharedDirectory = sharedSaves.SharedSaveDirectory;
                if (!Directory.Exists(sharedDirectory))
                    return;

                var scopedDirectory = Game1.UserDataPaths.SaveDirectory;
                sharedSaves.EnsureDirectory(scopedDirectory);
                for (var slot = 0; slot < SaveStateManager.SaveCount; slot++)
                    SyncSlot(slot, sharedDirectory, scopedDirectory, sharedSaves);
            }
            catch { }
        }

        private static void SyncSlot(int slot, string sharedDirectory, string scopedDirectory, ISharedSaveService sharedSaves)
        {
            var sharedSave = Path.Combine(sharedDirectory, SaveGameSaveLoad.SaveFileName + slot);
            var sharedSaveGame = Path.Combine(sharedDirectory, SaveGameSaveLoad.SaveFileNameGame + slot);
            var scopedSave = Path.Combine(scopedDirectory, SaveGameSaveLoad.SaveFileName + slot);
            var scopedSaveGame = Path.Combine(scopedDirectory, SaveGameSaveLoad.SaveFileNameGame + slot);

            if (!sharedSaves.FileExists(sharedSave) || !sharedSaves.FileExists(sharedSaveGame))
                return;

            if (!sharedSaves.FileExists(scopedSave) || !sharedSaves.FileExists(scopedSaveGame) ||
                MinWriteTime(sharedSave, sharedSaveGame, sharedSaves) > MaxWriteTime(scopedSave, scopedSaveGame, sharedSaves))
            {
                // A damaged shared slot must not prevent later slots from syncing.
                try
                {
                    sharedSaves.CopyFile(sharedSave, scopedSave);
                    sharedSaves.CopyFile(sharedSaveGame, scopedSaveGame);
                }
                catch { }
            }
        }

        private static DateTime MinWriteTime(string first, string second, ISharedSaveService sharedSaves) =>
            sharedSaves.GetLastWriteTimeUtc(first) < sharedSaves.GetLastWriteTimeUtc(second)
                ? sharedSaves.GetLastWriteTimeUtc(first)
                : sharedSaves.GetLastWriteTimeUtc(second);

        private static DateTime MaxWriteTime(string first, string second, ISharedSaveService sharedSaves) =>
            sharedSaves.GetLastWriteTimeUtc(first) > sharedSaves.GetLastWriteTimeUtc(second)
                ? sharedSaves.GetLastWriteTimeUtc(first)
                : sharedSaves.GetLastWriteTimeUtc(second);
    }
}
