using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Identifiers
{
    internal class ObjOverworld : GameObject
    {
        private readonly Vector2 _offset;
        public  static   Vector2 GridOrigin;

        public ObjOverworld() : base("editor overworld") { }

        public ObjOverworld(Map.Map map, int posX, int posY) : base(map)
        {
            // Tells "Map" that this is the overworld map.
            map.IsOverworld = true;

            // Pull the offset from the object's location.
            _offset = new Vector2(posX, posY);

            // A reference to the offset that is public.
            GridOrigin = _offset;

            // Update component used to update Link's position marker on the map.
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));

            // When loading the overworld, if the signpost game was not complete then reset the state of the signs.
            if (Game1.GameManager.SaveManager.GetString("shieldGameStairs", "0") != "1")
                Game1.GameManager.SaveManager.SetString("sgame", "0");
        }

        public void Update()
        {
            // Updates the player's position on the map.
            var offsetX = (int)(MapManager.ObjLink.CenterPosition.X - _offset.X) / Values.FieldWidth;
            var offsetY = (int)(MapManager.ObjLink.CenterPosition.Y - _offset.Y) / Values.FieldHeight;
            Game1.GameManager.SetMapPosition(new Point(offsetX, offsetY), true);
        }
    }
}
