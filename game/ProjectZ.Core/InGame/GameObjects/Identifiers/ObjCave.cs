using ProjectZ.InGame.GameObjects.Base;

namespace ProjectZ.InGame.GameObjects.Identifiers
{
    internal class ObjCave : GameObject
    {
        public ObjCave() : base("editor cave") { }

        public ObjCave(Map.Map map, int posX, int posY) : base(map)
        {
            map.IsCave = true;
        }
    }
}
