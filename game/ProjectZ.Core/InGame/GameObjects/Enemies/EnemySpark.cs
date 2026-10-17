using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemySpark : GameObject
    {
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        
        private Vector2 _lastPosition;
        private string _destructionKey;

        private double _directionChangeTime;
        private float _lightState;
        private float _lightCount;
        private int _moveDir;
        private bool _goingClockwise;
        private bool _wasTouchingWall;
        private bool _gettingDestroyed;
        private bool _init;

        private int _resetDir;
        private bool _resetClockwise;
        private int _dropIndex = 9;

        private bool _lastEpSafe;

        // Values configurable via lahdmod.
        private bool  light_source = true;
        private int   light_red = 255;
        private int   light_grn = 255;
        private int   light_blu = 255;
        private float light_bright = 0.25f;
        private int   light_size = 64;

        public EnemySpark() : base("spark") { }

        public EnemySpark(Map.Map map, int posX, int posY, int direction, bool clockwise, string destructionKey) : base(map)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "EnemySpark.lahdmod");
            ModFile.Parse(modFile, this);

            // maybe create a new tag for enemies that should be ignored by the enemy trigger
            Tags = Values.GameObjectTag.Damage;

            EntityPosition = new CPosition(posX + 8, posY + 8, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 8, 0);
            EntitySize = new Rectangle(-32, -32, 64, 64);
            CanReset = true;
            OnReset = Reset;

            _lastPosition = EntityPosition.Position;

            _moveDir = direction;
            _resetDir = direction;

            _goingClockwise = clockwise;
            _resetClockwise = clockwise;

            _destructionKey = destructionKey;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/spark");
            _animator.Play("idle");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -8));

            _body = new BodyComponent(EntityPosition, -4, -4, 8, 8, 8)
            {
                FieldRectangle = map.GetField(posX, posY),
                MoveCollision = OnCollision,
                IgnoreHeight = true,
                IgnoresZ = true,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Hole |
                                 Values.CollisionTypes.NPCWall
            };
            var aiComponent = new AiComponent();
            _damageState = new AiDamageState(this, _body, aiComponent, sprite, 1, _dropIndex, false, false) { HitMultiplierY = 1.0f };

            var damageBox = new CBox(EntityPosition, -2, -2, 0, 4, 4, 4);
            var hittableBox = new CBox(EntityPosition, -6, -6, 12, 12, 8);

            if (!string.IsNullOrEmpty(destructionKey))
                AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(OnKeyChanged));

            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { BoomerangMultiplier = true });
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer) { WaterOutline = false });
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(sprite));
            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight));
        }

        public override void Reset()
        {
            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _lastPosition = ResetPosition.Position;
            _moveDir = _resetDir;
            _goingClockwise = _resetClockwise;
            _directionChangeTime = 0;
        }

        public override void Init()
        {
            _init = true;
            base.Init();
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // The Boomerang is the only weapon that can kill them.
            if (hitType == HitType.Boomerang)
                Destroy();

            return Values.HitCollision.Blocking;
        }

        private void OnKeyChanged()
        {
            if (_gettingDestroyed || !_init)
                return;

            if (Game1.GameManager.SaveManager.GetString(_destructionKey) == "1")
                Destroy();
        }

        private void Destroy()
        {
            _gettingDestroyed = true;

            // Cancel out the normal death effects and run BaseOnDeath for item drops.
            _damageState.NullifyDeathEffects();
            _damageState.DropTableIndex = 9;
            _damageState.BaseOnDeath(false);

            // Play the crunch sound and show the smoke effect.
            Game1.AudioManager.PlaySoundEffect("D360-03-03");
            var explosionAnimation = new ObjAnimator(Map, (int)EntityPosition.X-8, (int)EntityPosition.Y-8, Values.LayerTop, "Particles/spawn", "run", true);
            Map.Objects.SpawnObject(explosionAnimation);
        }

        private void OnCollision(Values.BodyCollision collider)
        {
            if ((collider & Values.BodyCollision.Vertical) != 0 && AnimationHelper.DirectionOffset[_moveDir].Y != 0 ||
               (collider & Values.BodyCollision.Horizontal) != 0 && AnimationHelper.DirectionOffset[_moveDir].X != 0)
                _moveDir = (_moveDir + (_goingClockwise ? 3 : 1)) % 4;
        }

        private void Update()
        {
            _lightCount += Game1.DeltaTime;
            _lightState = GameSettings.EpilepsySafe ? 2 : (int)(Math.Sin(_lightCount / 10f) + 1.5);

            // if the body is not sliding on the wall anymore change the direction
            var positionChange = (EntityPosition.Position - _lastPosition) * AnimationHelper.DirectionOffset[(_moveDir + 1) % 4];
            if (positionChange.Length() > 0.0f && (_directionChangeTime + 250 <= Game1.TotalGameTime || _wasTouchingWall))
            {
                _directionChangeTime = Game1.TotalGameTime;
                _moveDir = (_moveDir + (_goingClockwise ? 1 : 3)) % 4;
            }
            _wasTouchingWall = positionChange.Length() == 0.0f;

            var moveVelocity = new Vector2(
                AnimationHelper.DirectionOffset[_moveDir].X + AnimationHelper.DirectionOffset[(_moveDir + (_goingClockwise ? 1 : 3)) % 4].X * 0.25f,
                AnimationHelper.DirectionOffset[_moveDir].Y + AnimationHelper.DirectionOffset[(_moveDir + (_goingClockwise ? 1 : 3)) % 4].Y * 0.25f);
            _body.VelocityTarget = moveVelocity;

            _lastPosition = EntityPosition.Position;
        }

        private void DrawLight(SpriteBatch spriteBatch)
        {
            // Slow down animations for epilepsy mode.
            if (_lastEpSafe != GameSettings.EpilepsySafe)
            {
                _animator.SpeedMultiplier = GameSettings.EpilepsySafe ? 0.10f : 1f;
                _lastEpSafe = GameSettings.EpilepsySafe;
            }

            if (light_source && GameSettings.ObjectLights)
            {
                Rectangle _lightRectangle = new Rectangle((int)EntityPosition.X - light_size / 2, (int)EntityPosition.Y - light_size / 2, light_size, light_size);
                DrawHelper.DrawLight(spriteBatch, _lightRectangle, new Color(light_red, light_grn, light_blu) * (0.125f + _lightState * light_bright));
            }
        }
    }
}
