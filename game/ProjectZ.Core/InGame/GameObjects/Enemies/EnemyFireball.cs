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
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyFireball : GameObject
    {
        private readonly BodyComponent _body;
        private readonly CBox _damageBox;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private double _liveTime = 2500;
        private double _hitDelay;
        private bool _reflected;
        private bool _hitBlink;
        private bool _blink;

        private ObjAnimator _deathAnimation;
        public Action OnHitPlayer;

        // Values configurable via lahdmod.
        private bool  light_source = true;
        private int   light_red    = 255;
        private int   light_grn    = 240;
        private int   light_blu    = 235;
        private float light_bright = 0.75f;
        private int   light_size   = 30;

        public EnemyFireball(Map.Map map, int posX, int posY, float speed, bool blink, double hitDelay = 0) : base(map)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "EnemyFireball.lahdmod");
            ModFile.Parse(modFile, this);

            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(-5, -5, 10, 10);
            CanReset = true;
            OnReset = Reset;

            var animator = AnimatorSaveLoad.LoadAnimator("Enemies/fireball");
            animator.Play("idle");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(animator, _sprite, new Vector2(-5, -5));

            _body = new BodyComponent(EntityPosition, -5, -5, 10, 10, 8)
            {
                FieldRectangle = map.GetField(posX, posY),
                FieldLocked = false,
                IgnoresZ = true,
                IgnoreHoles = true,
                CollisionTypes = Values.CollisionTypes.None
            };

            _blink = blink;
            _hitDelay = hitDelay;

            var playerDirection = new Vector2(MapManager.ObjLink.EntityPosition.X, MapManager.ObjLink.EntityPosition.Y - 4) - EntityPosition.Position;
            if (playerDirection != Vector2.Zero)
                playerDirection.Normalize();
            _body.VelocityTarget = playerDirection * speed;

            _damageBox = new CBox(EntityPosition, -3, -3, 0, 6, 6, 4);
            var hittableBox = new CBox(EntityPosition, -4, -4, 0, 8, 8, 8);

            AddComponent(BodyComponent.Index, _body);
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(_damageBox, HitType.Enemy, 2) { OnDamagedPlayer = OnDamagedPlayer });
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit));
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_damageBox, OnPush));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(_sprite, Values.LayerTop));
            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight));
            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void OnDamagedPlayer()
        {
            OnHitPlayer?.Invoke();
        }

        public override void Reset()
        {
            _sprite.IsVisible = false;
            _damageField.IsActive = false;
            Map.Objects.DeleteObjects.Add(this);
        }

        public void SetVelocity(Vector2 velocity)
        {
            _body.VelocityTarget = velocity;
        }

        private void Update()
        {
            if (_hitDelay > 0)
                _hitDelay -= Game1.DeltaTime;

            _liveTime -= Game1.DeltaTime;

            // Some fireballs rapidly cycle colors. Use the damage shader for this.
            if (_blink && !GameSettings.EpilepsySafe)
                _sprite.SpriteShader = (Game1.TotalGameTime % (AiDamageState.BlinkTime * 2) < AiDamageState.BlinkTime) ? Resources.DamageSpriteShader0 : null;
            
            // Fade the fireball out when it's timer is about to expire.
            if (_liveTime <= 125)
                _sprite.Color = Color.White * ((float)_liveTime / 125f);

            // Delete the fireball after it's timer reaches 0 or lower.
            if (_liveTime <= 0)
            {
                Delete();
                return;
            }
            // If the shot was reflected, try to hit an enemy.
            if (_reflected)
            {
                // Probably the closest parallel to player damage types is the Bow.
                var collision = Map.Objects.Hit(MapManager.ObjLink, EntityPosition.Position, _damageBox.Box, HitType.Bow, 2, false, false);
                if ((collision & Values.HitCollision.Enemy) != 0)
                    Map.Objects.DeleteObjects.Add(this);
            }
        }

        private bool OnPush(Vector2 vecDirection, PushableComponent.PushType type)
        {
            // Check if the incoming push type is from the shield.
            if (type == PushableComponent.PushType.Impact)
            {
                // We only want a single interaction so check if it's been reflected.
                if (!_reflected)
                {
                    // The direction must be an inversion of incoming damage.
                    int direction = AnimationHelper.GetDirection(vecDirection) + 2 % 4;

                    // If the shield is able to reflect the shot.
                    if (MapManager.ObjLink.Reflected(direction))
                    {
                        Reflect(vecDirection);
                        return false;
                    }
                    // Otherwise kill it.
                    else if (_hitDelay <= 0)
                        OnDeath(false);
                }
            }
            // The shot was not reflected so perform the knockback.
            return true;
        }

        private void Reflect(Vector2 shieldDirection)
        {
            // Play the deflection sound.
            Game1.AudioManager.PlaySoundEffect("D360-22-16");

            // Don't let the spear reflect more than once.
            _reflected = true;

            // It should not damage Link from this point on.
            _hitComponent.IsActive = false;
            _damageField.IsActive = false;
            _pushComponent.IsActive = false;
            _liveTime = 4000;

            // Use the incoming direction and the shield reflect direction to determine new direction.
            shieldDirection.Normalize();
            var incoming = _body.VelocityTarget;
            var reflected = (incoming - 2 * Vector2.Dot(incoming, shieldDirection) * shieldDirection) * 1.75f;

            // Reverse the movement of the projectile.
            _body.VelocityTarget = reflected;
        }

        private void OnDeath(bool playSound)
        {
            if (playSound)
                Game1.AudioManager.PlaySoundEffect("D360-03-03");

            bool blink = _blink || _hitBlink;

            _deathAnimation = new ObjFireballDeath(Map, (int)EntityPosition.Position.X,  (int)EntityPosition.Position.Y, -8, -8, blink);
            Map.Objects.SpawnObject(_deathAnimation);

            Delete();
        }

        private void Delete()
        {
            Map.Objects.DeleteObjects.Add(this);
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if ((hitType & HitType.Sword) == 0)
                return Values.HitCollision.None;

            // If it can't be killed immediately.
            if (_hitDelay > 0)
                return Values.HitCollision.None;

            _hitBlink = true;
            Game1.AudioManager.PlaySoundEffect("D360-03-03");
            OnDeath(true);
            return Values.HitCollision.Enemy;
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
