using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Dungeon;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyAntiFairy : GameObject
    {
        private readonly Animator _animator;
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _aiDamageState;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private int _lives = EnemyLives.AntiFairy;
        private int _dropIndex = 7;
        private bool _lastEpSafe;

        // Values configurable via lahdmod.
        private bool  light_source = true;
        private int   light_red    = 255;
        private int   light_grn    = 255;
        private int   light_blu    = 255;
        private float light_bright = 1.0f;
        private int   light_size   = 120;

        public EnemyAntiFairy() : base("antiFairy") { }

        public EnemyAntiFairy(Map.Map map, int posX, int posY) : base(map)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "EnemyAntiFairy.lahdmod");
            ModFile.Parse(modFile, this);

            // not used for the enemy trigger
            Tags = Values.GameObjectTag.Damage;

            EntityPosition = new CPosition(posX + 8, posY + 8, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 8, 0);
            EntitySize = new Rectangle(-32, -32, 64, 64);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/anti-fairy");
            _animator.Play("idle");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -8));

            _body = new BodyComponent(EntityPosition, -6, -6, 12, 12, 8)
            {
                IgnoreHeight = true,
                IgnoreHoles = true,
                FieldRectangle = map.GetField(posX, posY),
                MoveCollision = OnCollision,
                CollisionTypes =
                    Values.CollisionTypes.Normal |
                    Values.CollisionTypes.Hole |
                    Values.CollisionTypes.NPCWall
            };
            _body.VelocityTarget = new Vector2(-1, 1) * (3 / 4.0f);

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", new AiState());
            _aiComponent.ChangeState("idle");
            _aiDamageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex, false) { IgnoreZeroDamage = true, FlameOffset = new Point(0, 2), OnDeath = OnDeath, OnBurn = OnBurn, SpawnPowerups = false };

            var damageBox   = new CBox(EntityPosition, -2, -2, 0,  4,  4, 4);
            var hittableBox = new CBox(EntityPosition, -8, -8, 0, 16, 16, 8);

            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { BoomerangMultiplier = true });
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer) { WaterOutline = false });
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(sprite));
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(hittableBox, OnPush));
            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight));
        }

        public override void Reset()
        {
            _animator.Continue();
            _aiComponent.ChangeState("idle");
            _aiComponent.ChangeState("idle");
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _body.VelocityTarget = new Vector2(-1, 1) * (3 / 4.0f);
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            OnCollision(MapManager.ObjLink.Direction % 2 == 0
                ? Values.BodyCollision.Horizontal
                : Values.BodyCollision.Vertical);
            return true;
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (hitType == HitType.Boomerang || hitType == HitType.MagicPowder)
                return _aiDamageState.OnHit(originObject, direction, hitType, damage, pieceOfPower);

            return Values.HitCollision.Blocking;
        }

        private void OnCollision(Values.BodyCollision collider)
        {
            if ((collider & Values.BodyCollision.Horizontal) != 0)
                _body.VelocityTarget.X = -_body.VelocityTarget.X;
            if ((collider & Values.BodyCollision.Vertical) != 0)
                _body.VelocityTarget.Y = -_body.VelocityTarget.Y;
        }

        private void DrawLight(SpriteBatch spriteBatch)
        {
            // No sense in constantly updating the value.
            if (_lastEpSafe != GameSettings.EpilepsySafe)
            {
                if (GameSettings.EpilepsySafe)
                    _animator.SpeedMultiplier = 0.25f;
                else
                    _animator.SpeedMultiplier = 1f;
                _lastEpSafe = GameSettings.EpilepsySafe;
            }

            if (light_source && GameSettings.ObjectLights)
            {
                Rectangle _lightRectangle = new Rectangle((int)EntityPosition.X - light_size / 2, (int)EntityPosition.Y - light_size / 2, light_size, light_size);
                DrawHelper.DrawLight(spriteBatch, _lightRectangle, new Color(light_red, light_grn, light_blu) * light_bright);
            }
        }

        private void OnDeath(bool pieceOfPower)
        {
            // spawn fairy? ~50% cance
            // not sure how this is calculated in the original game
            if (Game1.RandomNumber.Next(0, 100) < 50)
                Map.Objects.SpawnObject(new ObjDungeonFairy(Map, (int)EntityPosition.X, (int)EntityPosition.Y + 4, 0));

            _aiDamageState.BaseOnDeath(pieceOfPower);
        }
    }
}
