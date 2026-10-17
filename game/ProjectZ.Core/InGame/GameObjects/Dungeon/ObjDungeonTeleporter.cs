using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Dungeon
{
    public class ObjDungeonTeleporter : GameObject
    {
        public Vector2 TeleportPosition;

        private readonly Animator _centerAnimator;
        private readonly AnimationComponent _animationComponent;
        private readonly CSprite _sprite;
        private readonly Rectangle _pointRectangle;
        private readonly Vector2[] _pointPositions = new Vector2[4];
        private readonly Point _origin;

        private readonly string _teleportMap;
        private readonly string _teleporterId;

        private float _rotateCount;
        private bool _lockTeleporter;
        private bool _isColliding;

        // Values configurable via lahdmod.
        private bool  enabled        = true;
        private float teleport_range = 4.25f;
        private bool  light_source   = true;
        private int   light_red      = 255;
        private int   light_grn      = 175;
        private int   light_blu      = 175;
        private float light_bright   = 1.00f;
        private int   light_size     = 64;

        public ObjDungeonTeleporter() : base("teleporter_middle") { }

        public ObjDungeonTeleporter(Map.Map map, int posX, int posY, string teleportMap, string teleporterId) : base(map)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "ObjDungeonTeleporter.lahdmod");
            ModFile.Parse(modFile, this);

            if (!enabled) return;

            _pointRectangle = Resources.SourceRectangle("teleporter_outer");
            var sourceRectangle = Resources.SourceRectangle("teleporter_middle");

            EntityPosition = new CPosition(posX + 8, posY + 8, 0);
            EntitySize = new Rectangle(-8, -8, 16, 16);

            TeleportPosition = new Vector2(EntityPosition.X, EntityPosition.Y + MapManager.ObjLink.CollisionBoxSize.Y / 2f);

            _teleportMap = teleportMap;
            _teleporterId = teleporterId;

            _origin = new Point(posX + 8, posY + 8);

            _centerAnimator = AnimatorSaveLoad.LoadAnimator("Objects/dTeleporter");
            _centerAnimator.Play("idle");

            _sprite = new CSprite(EntityPosition);
            _animationComponent = new AnimationComponent(_centerAnimator, _sprite, Vector2.Zero);
            _lockTeleporter = true;

            // Tracks the dungeon teleporters in a static list.
            Map.DungeonTeleporters.Add(this);

            // Has the player just teleported to this teleporter?
            if (teleporterId != null && MapManager.ObjLink.NextMapPositionId == teleporterId)
            {
                PlacePlayer();
                Lock();
            }
            AddComponent(BaseAnimationComponent.Index, _animationComponent);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerBottom, EntityPosition));
            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight) { Layer = Values.LightLayer1 });

            // Update the positions of the rotating orbs.
            UpdatePositions();
        }

        public void Lock()
        {
            _isColliding = true;
            _lockTeleporter = true;
        }

        private void Update()
        {
            // Check if the player is close enough to the teleporter.
            var distance = TeleportPosition - MapManager.ObjLink.Position;
            if (distance.Length() < teleport_range)
                OnCollision();

            // Update the positions of the rotating orbs.
            _rotateCount -= Game1.DeltaTime;
            UpdatePositions();

            // Unlock the teleporter when not colliding.
            if (!_isColliding)
                _lockTeleporter = false;

            // Set collision to false.
            _isColliding = false;
        }

        private void OnCollision()
        {
            _isColliding = true;

            // If the teleporter is locked or Link is jumping then return.
            if (_lockTeleporter || MapManager.ObjLink.EntityPosition.Z > 1)
                return;

            // Play the teleport sound effect.
            Game1.AudioManager.PlaySoundEffect("D360-28-1C");

            // The teleporter is not on the current map. This only applies for Level 7 dungeon.
            if (!string.IsNullOrEmpty(_teleportMap) && Map.MapName != _teleportMap)
            {
                // Warp Link to the other teleporter.
                MapManager.ObjLink.SetPosition(TeleportPosition);
                MapManager.ObjLink.StartTeleportation(_teleportMap, _teleporterId);
                _lockTeleporter = true;
                return;
            }
            // Loop through the dungeon teleporters.
            foreach (var teleporter in Map.DungeonTeleporters)
            {
                // Find the teleporter that is not this one.
                if (teleporter != this && teleporter._teleporterId == _teleporterId)
                {
                    // Warp Link to the other teleporter.
                    MapManager.ObjLink.SetPosition(TeleportPosition);
                    MapManager.ObjLink.StartTeleportation(teleporter);
                    _lockTeleporter = true;
                    break;
                }
            }
        }

        private void UpdatePositions()
        {
            var radiants = _rotateCount / 150f;

            // Rotate around the field.
            for (var i = 0; i < 4; i++)
            {
                _pointPositions[i] = new Vector2(
                    _origin.X + (float)(7 * Math.Sin(radiants + Math.PI / 2 * i) - 2),
                    _origin.Y + (float)(7 * Math.Cos(radiants + Math.PI / 2 * i)) - 2);
            }
        }

        private void PlacePlayer()
        {
            MapManager.ObjLink.NextMapPositionStart = TeleportPosition;
            MapManager.ObjLink.NextMapPositionEnd = TeleportPosition;
            MapManager.ObjLink.TransitionInWalking = false;
            MapManager.ObjLink.DirectionEntry = 3;
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            _sprite.Draw(spriteBatch);

            // Draw the rotating circles.
            for (var i = 0; i < _pointPositions.Length; i++)
            {
                // Snap pixels to grid if enabled.
                var position = GameSettings.PixelSnapping
                    ? new Vector2(MathF.Round(_pointPositions[i].X), MathF.Round(_pointPositions[i].Y))
                    : _pointPositions[i];
                spriteBatch.Draw(Resources.SprObjects, position, _pointRectangle, Color.White);
            }
        }

        private void DrawLight(SpriteBatch spriteBatch)
        {
            if (light_source && GameSettings.ObjectLights)
            {
                var _lightColor = new Color(light_red, light_grn, light_blu);
                var _lightRectangle = new Rectangle((int)EntityPosition.X - light_size / 2, (int)EntityPosition.Y - light_size / 2, light_size, light_size);
                DrawHelper.DrawLight(spriteBatch, _lightRectangle, _lightColor * light_bright);
            }
        }
    }
}
