using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;

namespace ProjectZ.InGame.GameObjects.Dungeon
{
    internal class ObjDungeonTilePuzzle : GameObject
    {
        private List<ObjButtonOrder> _buttonOrderTiles = new List<ObjButtonOrder>();

        public ObjDungeonTilePuzzle() : base("editor tile puzzle")
        {
            EditorColor = Color.Blue;
        }

        public ObjDungeonTilePuzzle (Map.Map map, int posX, int posY, string puzzleKey, string strStateKey, string strKey, bool drawSprite) : base(map)
        {
            // If the puzzle key has not yet been set, create a new puzzle key.
            if (Game1.GameManager.SaveManager.GetString(puzzleKey, "0") == "0")
                Game1.GameManager.SaveManager.SetString(puzzleKey, Game1.RandomNumber.Next(1, 5).ToString());

            // Load whatever the puzzle key has been set to.
            var variation = Game1.GameManager.SaveManager.GetString(puzzleKey);

            // The puzzle key determines the button order that must be stepped on.
            var stepOrder = variation switch
            {
                // Tile order: Top-Left, Top Right, Middle, Bottom-Left, Bottom-Right
                "1" => new int[] { 3, 2, 0, 1, 4 },
                "2" => new int[] { 0, 4, 2, 3, 1 },
                "3" => new int[] { 2, 3, 4, 0, 1 },
                "4" => new int[] { 4, 1, 0, 3, 2 },
                _   => new int[] { 3, 2, 0, 1, 4 }
            };
            // The positions of the tiles. Puzzle tile should be placed on center tile.
            Point[] tilePos = new Point[5];
            tilePos[0] = new Point(posX - 48, posY - 16);
            tilePos[1] = new Point(posX + 32, posY - 16);
            tilePos[2] = new Point(posX, posY);
            tilePos[3] = new Point(posX - 48, posY + 16);
            tilePos[4] = new Point(posX + 32, posY + 16);

            // String array where all but one will be empty and the last will have the "strKey".
            string[] finalStrKey = new string[5];

            // Loop through and create and set up the buttons.
            for (int i = 0; i < tilePos.Length; i++)
            {
                // Only the final button should have "strKey" set the rest should be empty strings.
                finalStrKey[i] = stepOrder[i] == 4 ? strKey : "";

                // Create the button at the position and set up the associated keys.
                _buttonOrderTiles.Add(new ObjButtonOrder(map, tilePos[i].X, tilePos[i].Y, stepOrder[i], strStateKey, finalStrKey[i], drawSprite));

                // Spawn the buttons into the map.
                Map.Objects.SpawnObject(_buttonOrderTiles[i]);
            }
        }
    }
}
