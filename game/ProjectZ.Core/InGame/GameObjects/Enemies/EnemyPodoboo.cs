using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyPodoboo : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AnimationComponent _animationComponent;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;

        private Vector2 _startPosition;

        // Values configurable via lahdmod.
        private bool  light_source = true;
        private int   light_red    = 255;
        private int   light_grn    = 200;
        private int   light_blu    = 200;
        private float light_bright = 0.75f;
        private int   light_size   = 64;

        public EnemyPodoboo() : base("podoboo") { }

        public EnemyPodoboo(Map.Map map, int posX, int posY, int timeOffset) : base(map)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "EnemyPodoboo.lahdmod");
            ModFile.Parse(modFile, this);

            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-32, -8 - 32, 64, 64);
            CanReset = true;
            OnReset = Reset;

            _startPosition = EntityPosition.Position;

            var animator = AnimatorSaveLoad.LoadAnimator("Enemies/podoboo");
            animator.Play("idle");

            _sprite = new CSprite(EntityPosition);
            _animationComponent = new AnimationComponent(animator, _sprite, new Vector2(0, -8));

            _body = new BodyComponent(EntityPosition, -5, -8 - 5, 10, 10, 8)
            {
                FieldRectangle = map.GetField(posX, posY),
                Gravity2D = 0.05f,
                CollisionTypes = Values.CollisionTypes.None,
                SplashEffect = false
            };

            _aiComponent = new AiComponent();

            var stateFlying = new AiState(UpdateFlying) { Init = InitFlying };
            stateFlying.Trigger.Add(new AiTriggerCountdown(250, null, SpawnParticle) { ResetAfterEnd = true });
            var stateHidden = new AiState() { Init = InitHidden };
            var hiddenCountdown = new AiTriggerCountdown(2000, null, () => _aiComponent.ChangeState("flying"));
            stateHidden.Trigger.Add(hiddenCountdown);

            _aiComponent.States.Add("flying", stateFlying);
            _aiComponent.States.Add("hidden", stateHidden);

            var damageBox = new CBox(EntityPosition, -3, -11, 0, 6, 6, 8, true);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, _animationComponent);
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerBottom));
            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight));

            _aiComponent.ChangeState("hidden");
            hiddenCountdown.CurrentTime = timeOffset;
        }

        public override void Reset()
        {
            _aiComponent.ChangeState("hidden");
            _aiComponent.ChangeState("hidden");
        }

        private void InitFlying()
        {
            _sprite.IsVisible = true;
            _damageField.IsActive = true;
            _body.IsActive = true;
            _body.Velocity.Y = -2.7f;

            SpawnSplash();
        }

        private void UpdateFlying()
        {
            _sprite.SpriteShader = Game1.TotalGameTime % (8000 / 60f) >= (4000 / 60f) ? Resources.DamageSpriteShader0 : null;

            if (_body.Velocity.Y > 0 && !_animationComponent.MirroredV)
            {
                _animationComponent.MirroredV = true;
            }

            if (EntityPosition.Y > _startPosition.Y)
            {
                SpawnSplash();
                _aiComponent.ChangeState("hidden");
            }
        }

        private void InitHidden()
        {
            EntityPosition.Set(_startPosition);
            _body.IsActive = false;
            _sprite.IsVisible = false;
            _damageField.IsActive = false;
            _animationComponent.MirroredV = false;
        }

        private void SpawnParticle()
        {
            var particle = new EnemyPodobooParticle(Map, new Vector2(EntityPosition.X, EntityPosition.Y), _animationComponent.MirroredV);
            Map.Objects.SpawnObject(particle);
        }

        private void SpawnSplash()
        {
            Map.Objects.SpawnObject(new EnemyPodobooSplash(Map, new Vector2(_startPosition.X, _startPosition.Y), new Vector2(-0.5f, -0.85f)));
            Map.Objects.SpawnObject(new EnemyPodobooSplash(Map, new Vector2(_startPosition.X, _startPosition.Y), new Vector2(0.5f, -0.85f)));
        }

        private void DrawLight(SpriteBatch spriteBatch)
        {
            if (light_source && GameSettings.ObjectLights)
                DrawHelper.DrawLight(spriteBatch, new Rectangle((int)EntityPosition.X - light_size / 2, (int)EntityPosition.Y - light_size / 2, light_size, light_size), new Color(light_red, light_grn, light_blu) * light_bright);
        }
    }
}
