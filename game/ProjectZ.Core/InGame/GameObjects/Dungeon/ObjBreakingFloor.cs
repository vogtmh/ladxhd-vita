using System.IO;
using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Dungeon
{
    // TODO: this needs some kind of effect while standing on
    internal class ObjBreakingFloor : GameObject
    {
        private readonly DrawSpriteComponent _drawComponent;
        private readonly ObjHole _objHole;

        private readonly Box _collisionBox;

        private float _respawnCounter;
        private float _breakCounter;

        private bool _isActive;

        // Values configurable via lahdmod.
        private float break_time = 670f;
        private float respawn_time = 15000f;
        private bool  no_sword_interrupt = true;

        private Rectangle _objectField;

        public ObjBreakingFloor(Map.Map map, int posX, int posY, string spriteId) : base(map, spriteId)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "ObjBreakingFloor.lahdmod");
            ModFile.Parse(modFile, this);

            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);
            CanReset = true;
            OnReset = Reset;

            var margin = 4;
            _collisionBox = new Box(posX, posY + margin, 0, 16, 16 - margin * 2, 1);
            _objectField = Map.GetField(new Vector2(posX + 8, posY + 8));

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, _drawComponent = new DrawSpriteComponent(spriteId, EntityPosition, Vector2.Zero, Values.LayerBottom));

            _objHole = new ObjHole(Map, (int)EntityPosition.X, (int)EntityPosition.Y, 16, 14, Rectangle.Empty, 0, 1, 0);
            _objHole.IsActive = false;
            Map.Objects.SpawnObject(_objHole);
            Activate();
        }

        private new void Reset()
        {
            _objHole.IsActive = false;
        }

        private void Update()
        {
            // It's more efficent to reference Link once in a local. 
            var Link = MapManager.ObjLink;

            if (!_isActive)
            {
                // Force the floor to respawn once a scroll settles on another field.
                if (Camera.ClassicMode && Link.FieldTransition(_objectField))
                    Activate();

                // Respawn the floor after 15 seconds.
                if (!Camera.ClassicMode)
                {
                    _respawnCounter -= Game1.DeltaTime;
                    if (_respawnCounter <= 0)
                        Activate();
                }
                return;
            }
            // is the player standing on the floor tile?
            if (MapManager.ObjLink.Body.BodyBox.Box.Intersects(_collisionBox))
            {
                bool idleState = Link.CurrentState == ObjLink.State.Idle;
                bool pushState = Link.IsPushingState();
                bool stunState = Link.IsStunned();

                // If Link is idle, pushing, or stunned the floor should break. Otherwise
                // pause the timer until the next loop iteration.
                if (!idleState && !pushState && !stunState)
                    return;

                _breakCounter += Game1.DeltaTime;

                // Reset the timer when swinging the sword.
                if (Link.IsAttackingState() && !no_sword_interrupt)
                    _breakCounter = 0;

                // Spawn the hole and delete the floor tile.
                if (_breakCounter >= break_time)
                {
                    Game1.AudioManager.PlaySoundEffect("D378-43-2B");
                    Deactivate();
                }
            }
            else
            {
                // Reset the timer.
                _breakCounter -= Game1.DeltaTime * 1.5f;
                if (_breakCounter < 0)
                    _breakCounter = 0;
            }
        }

        private void Activate()
        {
            _isActive = true;
            _breakCounter = 0;
            _objHole.IsActive = false;
            _drawComponent.IsActive = true;
        }

        private void Deactivate()
        {
            _isActive = false;
            _respawnCounter = respawn_time;
            _objHole.IsActive = true;
            _drawComponent.IsActive = false;
        }
    }
}
