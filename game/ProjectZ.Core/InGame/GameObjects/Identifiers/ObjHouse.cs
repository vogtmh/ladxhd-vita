using ProjectZ.InGame.GameObjects.Base;

namespace ProjectZ.InGame.GameObjects.Identifiers
{
    internal class ObjHouse : GameObject
    {
        public ObjHouse() : base("editor house") { }

        public ObjHouse(Map.Map map, int posX, int posY) : base(map)
        {
            map.IsHouse = true;
        }
    }
}
