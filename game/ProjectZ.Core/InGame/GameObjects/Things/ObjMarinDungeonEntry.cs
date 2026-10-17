using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.NPCs;
using ProjectZ.InGame.Map;

namespace ProjectZ.InGame.GameObjects.Things
{
    class ObjMarinDungeonEntry : GameObject
    {
        private Rectangle _rectangle;
        private int _offsetX;
        private int _offsetY;

        public ObjMarinDungeonEntry(Map.Map map, int posX, int posY, int offsetX, int offsetY) : base(map)
        {
            EditorIconSource = new Rectangle(0, 0, 16, 16);
            EditorColor = Color.Blue;

            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _offsetX = offsetX;
            _offsetY = offsetY;

            _rectangle = new Rectangle(posX, posY, 16, 16);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
        }

        public override void Init()
        {
            if (MapManager.ObjLink.NextMapPositionStart == null)
                return;

            // Check if Link is currently standing in the dungeon entry tile.
            var linkPosition = MapManager.ObjLink.NextMapPositionStart.Value;
            if (_rectangle.Contains(new Point((int)linkPosition.X, (int)linkPosition.Y)))
            {
                // Check if Marin is a follower by checking the key-value pair.
                var itemMarin = Game1.GameManager.SaveManager.GetString("has_marin");
                if (!string.IsNullOrEmpty(itemMarin) && itemMarin == "1")
                {
                    // If she is, get the NPC version of Marin.
                    ObjMarin marin = MapManager.ObjLink.GetMarin();

                    // Set the dungeon sequence if she is on the map.
                    if (marin != null)
                        marin.LeaveDungeonSequence(EntityPosition.Position, _offsetX, _offsetY);
                }
            }
        }

        private void Update()
        {
            // Check if Link is currently standing in the dungeon entry tile.
            if (MapManager.ObjLink.BodyRectangle.Intersects(_rectangle))
            {
                // Check if Marin is a follower by checking the key-value pair.
                var itemMarin = Game1.GameManager.SaveManager.GetString("has_marin");
                if (!string.IsNullOrEmpty(itemMarin) && itemMarin == "1")
                {
                    // If she is, get the NPC version of Marin.
                    ObjMarin marin = MapManager.ObjLink.GetMarin();

                    // Set the dungeon sequence if she is on the map.
                    if (marin != null)
                        marin.EnterDungeonMessage = true;
                }
            }
        }
    }
}
