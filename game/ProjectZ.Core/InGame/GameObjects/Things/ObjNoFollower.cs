using ProjectZ.InGame.GameObjects.Base;

namespace ProjectZ.InGame.GameObjects.Dungeon
{
    internal class ObjNoFollower : GameObject
    {
        public ObjNoFollower() : base("editor no follower") { }

        public ObjNoFollower(Map.Map map, int posX, int posY) : base(map)
        {
            Game1.GameManager.SetNoFollowersMap();
        }
    }
}
