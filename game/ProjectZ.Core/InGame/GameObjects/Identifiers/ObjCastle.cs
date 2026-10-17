using ProjectZ.InGame.GameObjects.Base;

namespace ProjectZ.InGame.GameObjects.Identifiers
{
    internal class ObjCastle : GameObject
    {
        public ObjCastle() : base("editor dungeon") { }

        public ObjCastle(Map.Map map, int posX, int posY, string dungeonName, bool updatePosition) : base(map)
        {
            if (!string.IsNullOrEmpty(dungeonName))
                Game1.GameManager.SetCastle(dungeonName);
        }
    }
}
