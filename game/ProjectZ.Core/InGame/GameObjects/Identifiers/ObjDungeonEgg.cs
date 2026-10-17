using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.Map;

namespace ProjectZ.InGame.GameObjects.Identifiers
{
    internal class ObjDungeonEgg : GameObject
    {
        public ObjDungeonEgg() : base("editor dungeon") { }

        public ObjDungeonEgg(Map.Map map, int posX, int posY, string dungeonName, bool updatePosition) : base(map)
        {
            if (!string.IsNullOrEmpty(dungeonName))
                Game1.GameManager.SetDungeonEgg(dungeonName);

            // The camera method applied depends on the currently active camera.
            if (dungeonName == "final stairs")
            {
                if (Camera.ClassicMode)
                    MapManager.Camera.StartPan(new Vector2(112, 212), new Vector2(112, -16), 9250);
                else
                    MapManager.Camera.StartOffset(new Vector2(0, -32), 8600);
            }
        }
    }
}
