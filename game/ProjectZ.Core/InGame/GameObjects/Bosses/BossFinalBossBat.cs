using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Bosses
{
    class BossFinalBossBat : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;

        private BossFinalBoss _owner;

        // Values configurable via lahdmod.
        private bool  light_source = true;
        private int   light_red    = 255;
        private int   light_grn    = 200;
        private int   light_blu    = 100;
        private float light_bright = 0.95f;
        private int   light_size   = 70;

        public BossFinalBossBat(Map.Map map, BossFinalBoss owner, int posX, int posY) : base(map)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "BossFinalBossBat.lahdmod");
            ModFile.Parse(modFile, this);

            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(-8, -8, 16, 16);

            _owner = owner;
            _animator = AnimatorSaveLoad.LoadAnimator("Nightmares/nightmare bat");

            _sprite = new CSprite(EntityPosition);
            var animatorComponent = new AnimationComponent(_animator, _sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -5, -4, 10, 8, 8)
            {
                IgnoresZ = true,
                IgnoreHoles = true,
                CollisionTypes = Values.CollisionTypes.None
            };

            _aiComponent = new AiComponent();

            var stateIdle = new AiState() { Init = InitIdle };
            stateIdle.Trigger.Add(new AiTriggerCountdown(400, null, () => _aiComponent.ChangeState("fire")));
            var stateFire = new AiState() { Init = InitFire };
            stateFire.Trigger.Add(new AiTriggerCountdown(400, null, () => _aiComponent.ChangeState("bat")));
            var stateBat = new AiState() { Init = InitBat };
            stateBat.Trigger.Add(new AiTriggerCountdown(550, null, () => _aiComponent.ChangeState("flying")));
            var stateFlying = new AiState() { Init = InitFlying };
            stateFlying.Trigger.Add(new AiTriggerCountdown(2000, FadeOut, Despawn));

            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("fire", stateFire);
            _aiComponent.States.Add("bat", stateBat);
            _aiComponent.States.Add("flying", stateFlying);
            _aiComponent.ChangeState("idle");

            var damageCollider = new CBox(EntityPosition, -5, -4, 0, 10, 8, 8);
            AddComponent(DamageFieldComponent.Index, new DamageFieldComponent(damageCollider, HitType.Enemy, 2) { OnDamagedPlayer = OnDamagedPlayer });
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(BaseAnimationComponent.Index, animatorComponent);
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerTop));
            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void OnDamagedPlayer()
        {
            // If it deals damage transfer to the main boss.
            _owner.DealtPlayerDamage = true;
        }

        private void Update()
        {
            _sprite.SpriteShader =
                Game1.TotalGameTime % (AiDamageState.BlinkTime * 2) < AiDamageState.BlinkTime ? Resources.DamageSpriteShader0 : null;
        }

        private void InitIdle()
        {
            _animator.Play("idle");
        }

        private void InitFire()
        {
            _animator.Play("fire");
        }

        private void InitBat()
        {
            _animator.Play("bat");
        }

        private void InitFlying()
        {
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            if (playerDirection != Vector2.Zero)
                playerDirection.Normalize();
            _body.VelocityTarget = playerDirection * 1.75f;

            Game1.AudioManager.PlaySoundEffect("D378-40-28");
        }

        private void FadeOut(double time)
        {
            var percentage = MathHelper.Clamp((float)time / 75, 0, 1);
            _sprite.Color = Color.White * percentage;
        }

        private void Despawn()
        {
            Map.Objects.DeleteObjects.Add(this);
        }

        private void DrawLight(SpriteBatch spriteBatch)
        {
            if (light_source && GameSettings.ObjectLights)
            {
                var _lightColor = new Color(light_red, light_grn, light_blu);

                var _lightRectangle = new Rectangle(
                    (int)EntityPosition.X - light_size / 2, 
                    (int)EntityPosition.Y - light_size / 2, 
                    light_size, light_size);

                spriteBatch.Draw(Resources.SprLight, _lightRectangle, _lightColor * light_bright);
            }
        }
    }
}
