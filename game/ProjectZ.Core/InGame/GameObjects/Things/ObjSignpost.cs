using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjSignpost : GameObject
    {
        private readonly string _signText;
        private readonly int _direction;

        public ObjSignpost() : base("signpost_0") { }

        public ObjSignpost(Map.Map map, int posX, int posY, string signText, string spriteId, Rectangle interactionRectangle, int direction) : base(map)
        {
            _signText = signText;
            _direction = direction;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);

            var interactBox = new CBox(
                posX + interactionRectangle.X, posY + interactionRectangle.Y, 0,
                interactionRectangle.Width, interactionRectangle.Height, 16);
            AddComponent(InteractComponent.Index, new InteractComponent(interactBox, OnInteract));

            if (string.IsNullOrEmpty(spriteId))
                return;

            AddComponent(CollisionComponent.Index, new BoxCollisionComponent(interactBox, Values.CollisionTypes.Normal | Values.CollisionTypes.Hookshot));
            AddComponent(DrawComponent.Index, new DrawSpriteComponent(spriteId, EntityPosition, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowSpriteComponent(null, spriteId, EntityPosition));
            AddComponent(CarriableComponent.Index, new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-7, -14, 14, 14)), null, null, null) { IsCollision = true });
        }

        private void TryEarnOwlStatueAchievement()
        {
            // Get the owl statue index.
            var index = _signText.Substring(_signText.Length - 1);

            // Store that this owl statue was talked to.
            Game1.GameManager.SaveManager.SetString("owl_statue_" + index, "1");

            // There are 9 owl statues to talk to, verify if the player talked to them all.
            for (int i = 1; i <= 9; i++)
            {
                // Check if the player interacted with this statue.
                bool interacted = Game1.GameManager.SaveManager.GetString("owl_statue_" + i.ToString(), "0") == "1";

                // If the result is "0" then this statue has not been interacted with.
                if (!interacted)
                    return;
            }
            // If the player doesn't have the achievement yet then grant it.
            if (!AchievementManager.IsEarned(87))
            {
                // Remove the strings as we no longer need them.
                for (int i = 1; i <= 9; i++)
                    Game1.GameManager.SaveManager.RemoveString("owl_statue_" + i.ToString());

                // Earn the achievement.
                AchievementManager.Earn(87);
            }
        }

        private bool OnInteract()
        {
            // Must be facing the statue to interact with it.
            if (_direction >= 0 && MapManager.ObjLink.Direction != _direction)
                return false;

            // If talking to an owl statue in the overworld.
            if (_signText.Contains("signHead"))
                TryEarnOwlStatueAchievement();

            // Start the dialog path.
            Game1.GameManager.StartDialogPath(_signText);

            // Return that the interaction happened.
            return true;
        }
    }
}
