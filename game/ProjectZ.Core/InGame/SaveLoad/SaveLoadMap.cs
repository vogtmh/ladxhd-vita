using System;
using System.Collections.Generic;
using System.IO;
using ProjectZ.InGame.GameObjects;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;
namespace ProjectZ.InGame.SaveLoad
{
    public class SaveLoadMap
    {
        public static void LoadMap(Map.Map map)
        {
            Game1.UiManager.EnqueueDeferredAction(() => LoadMapInternal(map));
        }

        private static void LoadMapInternal(Map.Map map)
        {
            var defaultPath = string.IsNullOrEmpty(map.MapFileName)
                ? null
                : Path.GetDirectoryName(Path.GetFullPath(map.MapFileName));

            if (Game1.FileDialogService.TryOpen("map", defaultPath, out var path))
                EditorLoadMap(path, map);
        }

        public static void EditorLoadMap(string filePath, Map.Map map)
        {
            var safeFileName = Path.GetFileName(filePath);
            map.MapName = safeFileName;

            // load the map file
            LoadMapFile(filePath, map);

            // create the objects
            map.Objects.LoadObjects();

            Game1.GameManager.MapManager.FinishLoadingMap(map);
        }

        public static void SaveMapDialog(Map.Map map)
        {
            Game1.UiManager.EnqueueDeferredAction(() => SaveMapDialogInternal(map));
        }

        private static void SaveMapDialogInternal(Map.Map map)
        {
            var defaultPath = string.IsNullOrEmpty(map.MapFileName)
                ? null
                : Path.GetDirectoryName(Path.GetFullPath(map.MapFileName));

            if (Game1.FileDialogService.TrySave("map", defaultPath, out var path))
                SaveMapFile(path, map);
        }

        public static void SaveMap(Map.Map map)
        {
            SaveMapFile(map.MapFileName, map);
        }

        // this function is used to update the file format of existing maps
        public static void UpdateMaps()
        {
            Game1.UiManager.EnqueueDeferredAction(UpdateMapsInternal);
        }

        private static void UpdateMapsInternal()
        {
            var newMap = new Map.Map();

            foreach (var fileName in Game1.FileDialogService.OpenMultiple("map"))
            {
                // load the map file
                LoadMapFile(fileName, newMap);
                // save the map file
                SaveMapFile(fileName, newMap);
            }
        }

        public static void ImportTilemap()
        {
            Game1.UiManager.EnqueueDeferredAction(ImportTilemapInternal);
        }

        private static void ImportTilemapInternal()
        {
            if (!Game1.FileDialogService.TryOpen("txt", null, out var filePath))
                return;

            var reader = new StreamReader(filePath);

            var tilesetName = Path.GetFileName(filePath).Replace(".txt", "") + ".png";

            var mapWidth = Convert.ToInt32(reader.ReadLine());
            var mapDepth = 3;

            var mapHeight = Convert.ToInt32(reader.ReadLine());

            // create a new map
            Game1.GameManager.MapManager.CurrentMap = Map.Map.CreateEmptyMap();
            Game1.GameManager.MapManager.CurrentMap.MapFileName = filePath.Replace(".txt", ".map");
            Game1.GameManager.MapManager.CurrentMap.TileMap.TilesetPath = tilesetName;

            Game1.GameManager.MapManager.CurrentMap.Objects.SpawnObject(MapManager.ObjLink);
            MapManager.ObjLink.Map = Game1.GameManager.MapManager.CurrentMap;

            Game1.GameManager.MapManager.CurrentMap.TileMap.ArrayTileMap = new int[mapWidth, mapHeight, mapDepth];

            for (var y = 0; y < mapHeight; y++)
            {
                var strLine = reader.ReadLine();

                if (strLine == null) continue;

                var strTiles = strLine.Split(',');

                for (var x = 0; x < mapWidth; x++)
                    Game1.GameManager.MapManager.CurrentMap.TileMap.ArrayTileMap[x, y, 0] =
                        strTiles[x] == "" ? -1 : Int32.Parse(strTiles[x]);
            }

            reader.Close();

            Game1.GameManager.MapManager.CurrentMap.HoleMap.ArrayTileMap = new int[mapWidth, mapHeight, 1];
            for (var y = 0; y < mapHeight; y++)
                for (var x = 0; x < mapWidth; x++)
                    Game1.GameManager.MapManager.CurrentMap.HoleMap.ArrayTileMap[x, y, 0] = -1;

            // load the tileset texture
            Game1.GameManager.MapManager.CurrentMap.TileMap.SetTileset(Resources.GetTexture(tilesetName));
            Game1.GameManager.MapManager.CurrentMap.HoleMap.SetTileset(Resources.GetTexture("hole.png"));

            // empty 2 and 3 layer
            for (var z = 1; z < mapDepth; z++)
                for (var y = 0; y < mapHeight; y++)
                    for (var x = 0; x < mapWidth; x++)
                        Game1.GameManager.MapManager.CurrentMap.TileMap.ArrayTileMap[x, y, z] = -1;

            Game1.GameManager.MapManager.CurrentMap.DigMap = new string[mapWidth, mapHeight];
        }

        public static void SaveMapFile(string savePath, Map.Map map)
        {
            var strTempFile = savePath + ".temp";
            var strOldFile = savePath + ".delete";
            var writer = new StreamWriter(strTempFile);

            // write down the map format version
            writer.WriteLine("3");

            writer.WriteLine(map.MapOffsetX);
            writer.WriteLine(map.MapOffsetY);

            // save the tilemap
            SaveTileMap(writer, map.TileMap);

            // save the map objects
            SaveObjects(writer, map.Objects);

            writer.Close();

            // change the file to the right one
            if (File.Exists(savePath))
            {
                File.Move(savePath, strOldFile);
                File.Move(strTempFile, savePath);
                File.Delete(strOldFile);
            }
            else
            {
                File.Move(strTempFile, savePath);
            }

            // save the dig map
            // could be included into the map file in the future
            if (map.DigMap != null)
                SaveDigMap(savePath, map);
        }

        private static void SaveDigMap(string savePath, Map.Map map)
        {
            savePath += ".data";

            var pathTemp = savePath + ".temp";
            var pathDelete = savePath + ".delete";

            DataMapSerializer.SaveData(pathTemp, map.DigMap);

            // change the file to the right one
            if (File.Exists(savePath))
            {
                File.Move(savePath, pathDelete);
                File.Move(pathTemp, savePath);
                File.Delete(pathDelete);
            }
            else
            {
                File.Move(pathTemp, savePath);
            }
        }

        public static void LoadMap(string mapName, Map.Map map)
        {
            map.MapName = mapName;

            var modFile = Path.Combine(Values.PathMapMods, mapName);
            var mapFile = GameFS.Exists(modFile)
                ? modFile
                : GameFS.NormalizePath(Path.Combine(Values.PathDataFolder, "Maps", mapName));

            LoadMapFile(mapFile, map);
        }

        public static void LoadMapFile(string fileName, Map.Map map)
        {
            var ap = GameFS.ToAssetPath(fileName);
            using var stream = GameFS.OpenRead(ap);
            using var reader = new StreamReader(stream);

            // reset map variables
            map.Reset();
            map.MapFileName = fileName;

            var fileVersion = int.Parse(reader.ReadLine());

            if (fileVersion > 2)
            {
                map.MapOffsetX = int.Parse(reader.ReadLine());
                map.MapOffsetY = int.Parse(reader.ReadLine());
            }
            // load the tilemap
            LoadTileMap(reader, map.TileMap);
            CreateEmptyHoleMap(map.HoleMap, map.MapWidth, map.MapHeight);

            map.HoleMap.SetTileset(Resources.GetTexture("hole.png"));
            map.StateMap = new MapStates.FieldStates[map.MapWidth, map.MapHeight];
            map.UpdateMap = new int[map.MapWidth, map.MapHeight];

            // load the objects
            LoadObjects(reader, map);

            // load the dig map
            var digPath = fileName + ".data";

            if (GameFS.Exists(digPath))
                map.DigMap = DataMapSerializer.LoadData(digPath);
            else
                map.DigMap = new string[map.MapWidth, map.MapHeight];
        }

        public static void CreateEmptyHoleMap(TileMap holeMap, int width, int height)
        {
            holeMap.ArrayTileMap = new int[width, height, 1];

            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    holeMap.ArrayTileMap[x, y, 0] = -1;
        }

        public static void SaveObjects(StreamWriter writer, ObjectManager objectManager)
        {
            // write down the directory of the objects
            writer.WriteLine(GameObjectTemplates.ObjectTemplates.Count);

            var keyToIndexDictionary = new Dictionary<string, int>();

            var counter = 0;
            foreach (var entry in GameObjectTemplates.ObjectTemplates)
            {
                writer.WriteLine(entry.Key);
                keyToIndexDictionary.Add(entry.Key, counter);
                counter++;
            }

            // write the free objects
            var objectList = Game1.GameManager.MapManager.CurrentMap.Objects.GetMergedObjectLists();

            objectList.Sort();
            writer.WriteLine(objectList.Count);

            foreach (var gameObject in objectList)
            {
                // don't save objects that are not in the game
                if (!GameObjectTemplates.ObjectTemplates.ContainsKey(gameObject.Index))
                    continue;

                // create a string from the parameter of the object
                var strObjectLine = MapData.GetObjectString(
                    keyToIndexDictionary[gameObject.Index], gameObject.Index, gameObject.Parameter);
                writer.WriteLine(strObjectLine);
            }
        }

        private static void LoadObjects(StreamReader reader, Map.Map map)
        {
            // read the objects used on this map
            var objCount = Convert.ToInt32(reader.ReadLine());
            var objectList = new string[objCount];

            for (var i = 0; i < objCount; i++)
                objectList[i] = reader.ReadLine();

            // read objects
            map.Objects.Clear();

            var objectCount = Convert.ToInt32(reader.ReadLine());
            for (var i = 0; i < objectCount; i++)
            {
                var objectSplit = reader.ReadLine().Split(';');
                var objectIndex = Convert.ToInt32(objectSplit[0]);
                var strIndex = objectList[objectIndex];

                // check if the object exists
                if (GameObjectTemplates.ObjectTemplates.ContainsKey(strIndex))
                {
                    // "Alternative objects" are a 2nd list of game objects used to draw objects on top of "dig holes", or holes that
                    // are created with the shovel. We can't draw the entire batch of game objects before or after drawing the hole
                    // map because some objects should be over the holes (pushable rocks) while some under the holes (flower patches).

                    bool isAltObject = strIndex.Contains("moveStone");
                    MapData.AddObject(map, new GameObjectItem(strIndex, MapData.StringToParameter(strIndex, objectSplit)), isAltObject);
                }
            }
        }

        private static void SaveTileMap(StreamWriter writer, TileMap tileMap)
        {
            // tileset path
            writer.WriteLine(tileMap.TilesetPath);

            // tilemap dimensions
            writer.WriteLine(tileMap.ArrayTileMap.GetLength(0));
            writer.WriteLine(tileMap.ArrayTileMap.GetLength(1));
            writer.WriteLine(tileMap.ArrayTileMap.GetLength(2));

            // write the tilemap
            for (var z = 0; z < tileMap.ArrayTileMap.GetLength(2); z++)
                for (var y = 0; y < tileMap.ArrayTileMap.GetLength(1); y++)
                {
                    var strLine = "";
                    for (var x = 0; x < tileMap.ArrayTileMap.GetLength(0); x++)
                    {
                        if (tileMap.ArrayTileMap[x, y, z] >= 0)
                            strLine += tileMap.ArrayTileMap[x, y, z];

                        strLine += ",";
                    }
                    writer.WriteLine(strLine);
                }
        }

        public static void LoadTileMap(StreamReader reader, TileMap tileMap)
        {
            var textureName = reader.ReadLine();

            var tileSize = 16;
            if (Resources.TilesetSizes.ContainsKey(textureName))
                tileSize = Resources.TilesetSizes[textureName];

            // load the tileset texture
            tileMap.TilesetPath = textureName;
            tileMap.SetTileset(Resources.GetTexture(textureName), tileSize);

            tileMap.BlurLayer = true;

            var width = Convert.ToInt32(reader.ReadLine());
            var height = Convert.ToInt32(reader.ReadLine());
            var depth = Convert.ToInt32(reader.ReadLine());

            tileMap.ArrayTileMap = new int[width, height, depth];

            // load the tile map
            for (var z = 0; z < depth; z++)
                for (var y = 0; y < height; y++)
                {
                    var strLine = reader.ReadLine();
                    var strTiles = strLine?.Split(',');

                    for (var x = 0; x < width; x++)
                        tileMap.ArrayTileMap[x, y, z] = strTiles[x] == "" ? -1 : int.Parse(strTiles[x]);
                }
        }

        public static void SaveMiniMapDiscovery(string fileName, int[,] map)
        {
            using (var writer = new StreamWriter(fileName))
            {
                writer.WriteLine(map.GetLength(0));
                writer.WriteLine(map.GetLength(1));

                for (var y = 0; y < map.GetLength(1); y++)
                {
                    var strLine = "";

                    for (var x = 0; x < map.GetLength(0); x++)
                        strLine += map[x, y] + ",";

                    writer.WriteLine(strLine);
                }
            }
        }

        public static GameManager.MiniMap LoadMiniMap(string fileName)
        {
            var assets = GameFS.ToAssetPath(fileName);

            if (!GameFS.Exists(assets))
                return null;

            using var stream = GameFS.OpenRead(assets);
            using var reader = new StreamReader(stream);

            var miniMap = new GameManager.MiniMap();

            miniMap.OffsetX = Convert.ToInt32(reader.ReadLine());
            miniMap.OffsetY = Convert.ToInt32(reader.ReadLine());

            var width = Convert.ToInt32(reader.ReadLine());
            var height = Convert.ToInt32(reader.ReadLine());
            miniMap.Tiles = new GameManager.MiniMapTile[width, height];

            // read the tile map
            for (var y = 0; y < height; y++)
            {
                var strLine = reader.ReadLine();
                var split = strLine.Split(',');

                for (var x = 0; x < width; x++)
                {
                    var tileIndex = Convert.ToInt32(split[x]);
                    miniMap.Tiles[x, y] = new GameManager.MiniMapTile { TileIndex = tileIndex };
                }
            }

            reader.ReadLine();

            // read the hint map
            for (var y = 0; y < height; y++)
            {
                var strLine = reader.ReadLine();
                var split = strLine.Split(',');

                for (var x = 0; x < width; x++)
                {
                    var hintSplit = split[x].Split(".");
                    if (hintSplit.Length != 2)
                        continue;

                    miniMap.Tiles[x, y].HintTileIndex = Convert.ToInt32(hintSplit[0]);
                    miniMap.Tiles[x, y].HintKey = hintSplit[1];
                }
            }

            reader.ReadLine();

            // read the tile overrides
            int.TryParse(reader.ReadLine(), out var overrideCount);
            if (overrideCount > 0)
            {
                miniMap.Overrides = new GameManager.MiniMapOverrides[overrideCount];

                for (var i = 0; i < overrideCount; i++)
                {
                    var strOverride = reader.ReadLine();
                    var split = strOverride.Split(',');
                    if (split.Length != 4)
                        continue;

                    var saveKey = split[0];
                    int.TryParse(split[1], out var posX);
                    int.TryParse(split[2], out var posY);
                    int.TryParse(split[3], out var tileIndex);

                    miniMap.Overrides[i] = new GameManager.MiniMapOverrides { SaveKey = saveKey, PosX = posX, PosY = posY, TileIndex = tileIndex };
                }
            }

            reader.Close();

            return miniMap;
        }
    }
}
