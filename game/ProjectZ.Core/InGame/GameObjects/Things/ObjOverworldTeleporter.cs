using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    internal class ObjOverworldTeleporter : GameObject
    {
        public static Dictionary<int, ObjOverworldTeleporter> TeleporterDictionary = new Dictionary<int, ObjOverworldTeleporter>();
        private readonly Rectangle _field;
        private readonly int _teleporterId;
        private bool _registered;
        public CBox CollisionBox;

        public ObjOverworldTeleporter(Map.Map map, int posX, int posY, int teleporterId) : base(map)
        {
            SprEditorImage = Resources.SprWhite;
            EditorIconSource = new Rectangle(0, 0, 16, 16);
            EditorColor = Color.HotPink * 0.75f;

            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _teleporterId = teleporterId;
            _field = Map.GetField(posX, posY);

            var animator = AnimatorSaveLoad.LoadAnimator("Objects/holeTeleporter");
            animator.Play("idle");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(animator, sprite, new Vector2(8, 8));
            var collisionRectangle = new Rectangle(posX, posY, 16, 16);

            // Store the collision box in a public field so it can be referenced from other places. Currently used to detect if landing on a teleporter when jumping.
            CollisionBox = new CBox(posX + 1, posY + 1, 0, 14, 14, 16);
            AddComponent(ObjectCollisionComponent.Index, new ObjectCollisionComponent(collisionRectangle, OnCollision));
            AddComponent(CollisionComponent.Index, new BoxCollisionComponent(CollisionBox, Values.CollisionTypes.Hole));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(sprite, Values.LayerBottom));

            // Clear the teleporter dictionary.
            TeleporterDictionary.Clear();
        }

        public override void Init()
        {
            // Register the teleporter if it was already unlocked.
            if (Game1.GameManager.SaveManager.GetString("unlocked_teleporter_" + _teleporterId) == "1")
                RegisterTeleporter();
        }

        public void SetNextTeleporterPosition()
        {
            // find the next teleporter
            var minId = int.MaxValue;
            var minBiggerId = int.MaxValue;

            foreach (var teleporter in TeleporterDictionary)
            {
                // find the next bigger teleporter
                if (teleporter.Key > _teleporterId && teleporter.Key < minBiggerId)
                    minBiggerId = teleporter.Key;
                // find the teleporter with the smallest id
                if (teleporter.Key < minId && teleporter.Key >= 0)
                    minId = teleporter.Key;
            }
            if (minBiggerId != int.MaxValue)
                TeleporterDictionary[minBiggerId].SetPosition();
            else if (minId != int.MaxValue)
                TeleporterDictionary[minId].SetPosition();
            else
                SetPosition();
        }

        public void SetPosition()
        {
            MapManager.ObjLink.StartWorldTelportation(new Vector2(EntityPosition.X + 8, EntityPosition.Y + 38));
        }

        private void Update()
        {
            if (!_registered && _field.Contains(MapManager.ObjLink.CenterPosition))
            {
                RegisterTeleporter();
                UnlockTeleporter();
            }
        }

        private void RegisterTeleporter()
        {
            _registered = true;

            if (!TeleporterDictionary.ContainsKey(_teleporterId))
                TeleporterDictionary.Add(_teleporterId, this);

            if (TeleporterDictionary.Keys.Count == 4)
                AchievementManager.Earn(96);
        }

        private void OnCollision(GameObject gameObject)
        {
            // unlock the teleporter
            if (!_registered)
            {
                RegisterTeleporter();
                UnlockTeleporter();
            }
            MapManager.ObjLink.HoleTeleporterId = _teleporterId;
        }

        private void UnlockTeleporter()
        {
            Game1.GameManager.SaveManager.SetString("unlocked_teleporter_" + _teleporterId, "1");
        }
    }
}
