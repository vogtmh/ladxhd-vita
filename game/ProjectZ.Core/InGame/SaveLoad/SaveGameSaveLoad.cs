using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.SaveLoad
{
    public class SaveGameSaveLoad
    {
        private static SaveManager playerSaveState;

        public static string SaveFileName = "save";
        public static string SaveFileNameGame = "saveGame";

        public static bool SaveExists(int slot)
        {
            return SaveManager.FileExists(Path.Combine(Values.PathSaveFolder, SaveFileName + slot)) &&
                   SaveManager.FileExists(Path.Combine(Values.PathSaveFolder, SaveFileNameGame + slot));
        }

        public static bool CopySaveFile(int from, int to)
        {
            var success =
                CopySaveFile(Path.Combine(Values.PathSaveFolder, SaveFileName + from),     Path.Combine(Values.PathSaveFolder, SaveFileName + to)) &&
                CopySaveFile(Path.Combine(Values.PathSaveFolder, SaveFileNameGame + from), Path.Combine(Values.PathSaveFolder, SaveFileNameGame + to));

            // Mirror the resulting slot to shared storage if enabled.
            if (success)
                MirrorPairToShared(to);

            return success;
        }

        public static bool CopySaveFile(string fromFile, string toFile)
        {
            if (!File.Exists(fromFile))
                return false;

            var fromFull = Path.GetFullPath(fromFile);
            var toFull = Path.GetFullPath(toFile);
            if (string.Equals(fromFull, toFull, StringComparison.OrdinalIgnoreCase))
                return false;

            var dir = Path.GetDirectoryName(toFull);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var temp = toFull + ".tmp";

            File.Copy(fromFull, temp, overwrite: true);

            if (File.Exists(toFull))
                File.Delete(toFull);

            File.Move(temp, toFull);

            return true;
        }

        public static bool DeleteSaveFile(int slot)
        {
            // Delete the save files and store if they were deleted.
            var success = DeleteSaveFile(Path.Combine(Values.PathSaveFolder, SaveFileName + slot)) && 
                DeleteSaveFile(Path.Combine(Values.PathSaveFolder, SaveFileNameGame + slot));

            // Mirror the deletion to shared storage if enabled.
            if (success)
                MirrorDeleteToShared(slot);

            return success;
        }

        private static bool DeleteSaveFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void SaveGame(GameManager gameManager, bool showIcon)
        {
            Directory.CreateDirectory(Values.PathSaveFolder);

            var saveFilePath = Path.Combine(Values.PathSaveFolder, SaveFileNameGame + gameManager.SaveSlot);

            gameManager.SaveManager.Save(saveFilePath, Values.SaveRetries);

            if (playerSaveState == null)
                FillSaveState(ref playerSaveState, gameManager);

            playerSaveState?.Save(Path.Combine(Values.PathSaveFolder, SaveFileName + gameManager.SaveSlot), Values.SaveRetries);
            playerSaveState = null;

            // Mirror the saved slot to shared storage if enabled.
            MirrorPairToShared(gameManager.SaveSlot);

            if (showIcon)
                Game1.GameManager.InGameOverlay.InGameHud.ShowSaveIcon();
        }

        public static void FillSaveState(GameManager gameManager)
        {
            FillSaveState(ref playerSaveState, gameManager);
        }

        public static void ClearSaveState()
        {
            playerSaveState = null;
        }

        private static void FillSaveState(ref SaveManager sm, GameManager gm)
        {
            // A list of "equippable" items plus the flippers.
            string[] equipTypes = new string[] { "sword1", "sword2", "shield", "mirrorShield", "feather", "stonelifter", "stonelifter2",
                "pegasusBoots", "shovel", "magicRod", "hookshot", "boomerang", "ocarina", "bow", "bomb", "powder", "flippers" };

            // Equippable items that have tiers to them. Use the name and get their corresponding pair.
            // Example: "sword1 > sword1,sword2" and "sword2 > sword1,sword2"
            string[] TierGroup(string name)
            {
                return name switch
                {
                    var n when n.StartsWith("sword")     => new[] { "sword1", "sword2" },
                    var n when n.EndsWith("hield")       => new[] { "shield", "mirrorShield" },
                    var n when n.StartsWith("stonelift") => new[] { "stonelifter", "stonelifter2" },
                    _ => null
                };
            }
            // Create a new instance of SaveManager.
            sm = new SaveManager();

            // Save basic properties.
            sm.SetString("savename", gm.RealSaveName);
            sm.SetBool("ThiefState", gm.ThiefState);
            sm.SetInt("maxHearts", gm.MaxHearts);
            sm.SetInt("deathCount", gm.DeathCount);
            sm.SetInt("killCount", gm.KillCount);
            sm.SetInt("currentHealth", gm.CurrentHealth);
            sm.SetInt("cloak", gm.CloakType);
            sm.SetInt("ocarinaSong", gm.SelectedOcarinaSong);
            sm.SetInt("guardianAcornCount", gm.GuardianAcornCount);
            sm.SetInt("pieceOfPowerCount", gm.PieceOfPowerCount);
            sm.SetFloat("totalPlaytime", gm.TotalPlaytime + gm.CurrentSessionPlaytime);
            sm.SetBool("cleared", gm.GameCleared);
            sm.SetBool("debugMode", gm.DebugMode);
            sm.SetString("currentMap", MapManager.ObjLink.SaveMap);
            sm.SetInt("posX", (int)MapManager.ObjLink.SavePosition.X);
            sm.SetInt("posY", (int)MapManager.ObjLink.SavePosition.Y);
            sm.SetInt("dir", MapManager.ObjLink.SaveDirection);

            // This instance of rupees is only used for the file selection screen.
            var rubyObject = gm.GetItem("ruby");
            if (rubyObject != null)
                sm.SetInt("rubyCount", rubyObject.Count);

            // Save the player's position on the map.
            if (gm.PlayerMapPosition != null)
            {
                sm.SetInt("mapPosX", gm.PlayerMapPosition.Value.X);
                sm.SetInt("mapPosY", gm.PlayerMapPosition.Value.Y);
            }
            // Save the dungeon keys.
            var dsKeys = "";
            foreach (var strKey in gm.DungeonMaps.Keys)
                dsKeys += strKey + ",";
            sm.SetString("dungeonKeyNames", dsKeys);

            // Save dungeon minimap progress.
            foreach (var miniMap in gm.DungeonMaps)
            {
                for (var y = 0; y < miniMap.Value.Tiles.GetLength(1); y++)
                {
                    var strLine = "";
                    for (var x = 0; x < miniMap.Value.Tiles.GetLength(0); x++)
                        strLine += (miniMap.Value.Tiles[x, y].DiscoveryState ? "1" : "0") + ",";
                    sm.SetString(miniMap.Key + "line" + y, strLine);
                }
            }
            // Save equipped items: sword, shield, feather, bombs, etc.
            for (var i = 0; i < gm.Equipment.Length; i++)
            {
                // Set up the string as it is written to the save file.
                var strItem = "";
                if (gm.Equipment[i] != null)
                    strItem += gm.Equipment[i].Name + ":" + gm.Equipment[i].Count;

                // Get the name of the of the equipment item.
                var name = gm.Equipment[i]?.Name ?? "";

                // If it's an equip item and "Give All Items" is enabled.
                if (GameSettings.ChGiveAllItems && equipTypes.Contains(name))
                {
                    // If it's a tiered item then get the pair.
                    var tiers = TierGroup(name);
                    if (tiers != null)
                    {
                        // Find the tier that the player actually owns.
                        string owned = null;
                        foreach (var tier in tiers)
                            if (gm.SaveManager.GetString("store_" + tier) == "1")
                                owned = tier;

                        // Write the tier that the player actually owns to the save file.
                        sm.SetString("equipment" + i, owned != null ? owned + ":1" : "");
                    }
                    else
                    {
                        // Non-pair tracked item: keep the existing rule (persist if legitimately owned).
                        var skip = gm.SaveManager.GetString("store_" + name) != "1";
                        sm.SetString("equipment" + i, skip ? "" : strItem);
                    }
                }
                // If the "Give All Items" cheat is diabled just store the item normally.
                else
                {
                    sm.SetString("equipment" + i, strItem);
                }
            }

            // Save all the collected objects: keys, relicts, flippers, etc.
            var objIndex = 0;
            for (var i = 0; i < gm.CollectedItems.Count; i++)
            {
                // Get the name of the item.
                var name = gm.CollectedItems[i].Name;

                // Skip saving the item if "Get All Items" is enabled, it's an equip type (Flippers), and the player did legit collect them yet.
                if (GameSettings.ChGiveAllItems && equipTypes.Contains(name) && gm.SaveManager.GetString("store_" + name) != "1")
                    continue;

                // Set the string to how it's stored in the save file.
                var strItem = name + ":" + gm.CollectedItems[i].Count;

                // If the item is bound to a certain map, add the bounded location to the string.
                if (gm.CollectedItems[i].LocationBounding != null)
                    strItem += ":" + gm.CollectedItems[i].LocationBounding;

                // Store the concatenated string in the save file.
                sm.SetString("object" + objIndex, strItem);
                objIndex++;
            }

            // Save the discovered map areas on the overworld.
            var values = new int[8];
            if (gm.MapVisibility != null)
            {
                for (var y = 0; y < 16; y++)
                {
                    var index = y / 2;
                    for (var x = 0; x < 16; x++)
                        values[index] = values[index] << 1 | (gm.MapVisibility[x, y] ? 0x1 : 0x0);
                }
            }
            for (var i = 0; i < values.Length; i++)
                sm.SetInt("map" + i, values[i]);
        }

        public static void LoadSaveFile(GameManager gm, int slot)
        {
            if (!gm.SaveManager.LoadFile(Path.Combine(Values.PathSaveFolder, SaveFileNameGame + slot)))
                return;

            var sm = new SaveManager();

            gm.SaveSlot = slot;
            gm.Equipment = new GameItemCollected[GameManager.EquipmentSlots];
            gm.CollectedItems.Clear();
            gm.DungeonMaps = new Dictionary<string, GameManager.MiniMap>();

            if (!sm.LoadFile(Path.Combine(Values.PathSaveFolder, SaveFileName + slot)))
                return;

            gm.SaveName = sm.GetString("savename");
            gm.ThiefState = sm.GetBool("ThiefState", false);
            gm.MaxHearts = sm.GetInt("maxHearts");
            gm.CurrentHealth = sm.GetInt("currentHealth");
            gm.CloakType = sm.GetInt("cloak", 0);
            gm.SelectedOcarinaSong = sm.GetInt("ocarinaSong", -1);
            gm.GuardianAcornCount = sm.GetInt("guardianAcornCount", 0);
            gm.PieceOfPowerCount = sm.GetInt("pieceOfPowerCount", 0);
            gm.DeathCount = sm.GetInt("deathCount", 0);
            gm.KillCount = sm.GetInt("killCount", 0);
            gm.TotalPlaytime = sm.GetFloat("totalPlaytime", 0.0f);
            gm.GameCleared = sm.GetBool("cleared", false);
            gm.CurrentSessionPlaytime = 0.0f;

            gm.DebugMode = sm.GetBool("debugMode", false);

            // so the map positions is still shown right even if the game was saved outside of the overworld
            if (sm.ContainsValue("mapPosX"))
                gm.PlayerMapPosition = new Point(
                    sm.GetInt("mapPosX"),
                    sm.GetInt("mapPosY"));
            else
                gm.PlayerMapPosition = null;

            // load the dungeon discovery state
            var strDungeonKeys = sm.GetString("dungeonKeyNames");
            if (!string.IsNullOrEmpty(strDungeonKeys))
            {
                var keys = strDungeonKeys.Split(',');

                for (var i = 0; i < keys.Length - 1; i++)
                {
                    // make sure the mini map is loaded
                    gm.LoadMiniMap(keys[i]);

                    // should never happen
                    if (!gm.DungeonMaps.TryGetValue(keys[i], out var map))
                        continue;

                    var width = map.Tiles.GetLength(0);
                    var height = map.Tiles.GetLength(1);

                    for (var y = 0; y < height; y++)
                    {
                        var line = sm.GetString(keys[i] + "line" + y);

                        // should never happen
                        if (line == null)
                            continue;

                        var splitLine = line.Split(',');

                        // should never happen
                        if (splitLine.Length - 1 != width)
                            continue;

                        for (var x = 0; x < width; x++)
                            map.Tiles[x, y].DiscoveryState = splitLine[x] == "1";
                    }
                }
            }

            // load equipped items
            for (var i = 0; i < gm.Equipment.Length; i++)
            {
                var strItem = sm.GetString("equipment" + i);

                if (!string.IsNullOrEmpty(strItem))
                {
                    // load the collected item
                    gm.CollectItem(GetGameItem(strItem), i, skipAchievements:true);
                }
                else
                {
                    gm.Equipment[i] = null;
                }
            }

            // load all the collected items
            string strObject;
            var counter = 0;
            while ((strObject = sm.GetString("object" + counter)) != null)
            {
                // add the collected object
                gm.CollectItem(GetGameItem(strObject), skipAchievements:true);
                counter++;
            }

            // load the discovered map data map
            gm.MapVisibility = new bool[16, 16];
            var values = new int[8];

            for (var i = 0; i < values.Length; i++)
                values[i] = sm.GetInt("map" + i);

            for (var y = 0; y < 16; y++)
            {
                var index = y / 2;

                for (var x = 0; x < 16; x++)
                {
                    // check the first bit of the 32bit value
                    gm.MapVisibility[x, y] = (values[index] & 0x80000000) != 0;
                    values[index] = values[index] << 1;
                }
            }
            // Get the current map and position of Link.
            gm.LoadedMap = sm.GetString("currentMap");
            gm.SavePositionX = sm.GetInt("posX");
            gm.SavePositionY = sm.GetInt("posY");
            gm.SaveDirection = sm.GetInt("dir");

            // Crash protection: If the save file somehow loses track of the last map, then force
            // the overworld map with Link's position set just outside of Tarin and Marin's house.
            if (string.IsNullOrEmpty(gm.LoadedMap))
            {
                gm.LoadedMap = "overworld.map";
                gm.SavePositionX = 424;
                gm.SavePositionY = 1370;
                gm.SaveDirection = 3;
            }
        }

        public static GameItemCollected GetGameItem(string strItem)
        {
            var strSplit = strItem.Split(':');
            if (strSplit.Length < 2)
                return new GameItemCollected(strSplit.Length > 0 ? strSplit[0] : "");

            var item = new GameItemCollected(strSplit[0]);

            if (short.TryParse(strSplit[1], out var count))
                item.Count = count;

            if (strSplit.Length > 2)
                item.LocationBounding = strSplit[2];

            return item;
        }

        //-------------------------------------------------------------------------------------------------------------------------------------------------
        //
        //  SHARED STORAGE MIRRORING
        //
        //-------------------------------------------------------------------------------------------------------------------------------------------------

        public static void MirrorPairToShared(int slot)
        {
            var sharedSaves = Game1.SharedSaveService;
            if (!GameSettings.SharedStorage || !sharedSaves.IsSupported || !sharedSaves.HasAccess)
                return;
            try
            {
                var sharedDir = sharedSaves.SharedSaveDirectory;
                sharedSaves.EnsureDirectory(sharedDir);

                var scopedSave = Path.Combine(Game1.UserDataPaths.SaveDirectory, SaveFileName + slot);
                var scopedSaveGame = Path.Combine(Game1.UserDataPaths.SaveDirectory, SaveFileNameGame + slot);
                var sharedSave = Path.Combine(sharedDir, SaveFileName + slot);
                var sharedSaveGame = Path.Combine(sharedDir, SaveFileNameGame + slot);

                if (sharedSaves.FileExists(scopedSave))
                    sharedSaves.CopyFile(scopedSave, sharedSave);
                if (sharedSaves.FileExists(scopedSaveGame))
                    sharedSaves.CopyFile(scopedSaveGame, sharedSaveGame);
            }
            catch { }
        }

        private static void MirrorDeleteToShared(int slot)
        {
            var sharedSaves = Game1.SharedSaveService;
            if (!GameSettings.SharedStorage || !sharedSaves.IsSupported || !sharedSaves.HasAccess)
                return;
            try
            {
                var sharedDir = sharedSaves.SharedSaveDirectory;
                var sharedSave = Path.Combine(sharedDir, SaveFileName + slot);
                var sharedSaveGame = Path.Combine(sharedDir, SaveFileNameGame + slot);

                if (sharedSaves.FileExists(sharedSave))
                    sharedSaves.DeleteFile(sharedSave);
                if (sharedSaves.FileExists(sharedSaveGame))
                    sharedSaves.DeleteFile(sharedSaveGame);
            }
            catch { }
        }
    }
}
