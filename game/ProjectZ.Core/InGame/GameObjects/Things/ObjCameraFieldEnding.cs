using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;

namespace ProjectZ.InGame.GameObjects.Things
{
    public class ObjCameraFieldEnding : GameObject
    {
        public Vector2 Position => EntityPosition?.Position ?? Vector2.Zero;
        public int Index;

        public ObjCameraFieldEnding() : base("editor field") 
        { 
            EditorColor = Color.Green * 1.00f;
        }

        public ObjCameraFieldEnding(Map.Map map, int posX, int posY, int index) : base(map)
        {
            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);
            Index = index;
        }
    }
}
