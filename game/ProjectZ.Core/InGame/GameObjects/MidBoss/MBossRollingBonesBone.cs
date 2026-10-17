using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.MidBoss
{
    internal class MBossRollingBonesBone : GameObject
    {
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly PushableComponent _pushComponent;
        private readonly HittableComponent _hitComponent;

        private const float MoveSpeed = 1f;

        private float _deathCount;
        private bool _hasCollided;
        private bool _isDying;
        private int _deathState;

        public MBossRollingBonesBone(Map.Map map, int posX, int posY, int offset) : base(map)
        {
            var fieldRectangle = map.GetField(posX, posY, 16);

            EntityPosition = new CPosition(fieldRectangle.X + offset, fieldRectangle.Y, 0);
            EntitySize = new Rectangle(0, 0, 16, 96);

            _animator = AnimatorSaveLoad.LoadAnimator("MidBoss/mbossOne");
            _animator.Play("idle");

            _body = new BodyComponent(EntityPosition, 2, 0, 12, 96, 8)
            {
                MoveCollision = OnCollision,
                AbsorbPercentage = 0.75f,
                Drag = 1.0f,
                DragAir = 1.0f,
            };
            var hittableCollider = new CBox(EntityPosition, 0, 0, 0, 16, 96, 8);
            var damageCollider = new CBox(EntityPosition, 2, 0, 0, 12, 96, 4);
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableCollider, OnHit));
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageCollider, HitType.Enemy, 2));
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush) { RepelMultiplier = 2.0f });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerBottom, EntityPosition));
        }

        public void Push(int direction)
        {
            _hasCollided = false;
            _body.Velocity.X = direction == 0 ? -MoveSpeed : MoveSpeed;
            _animator.Play("move");
        }

        public void bossDeath()
        {
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        public void Delete()
        {
            _isDying = true;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (!MapManager.ObjLink.IsGrounded())
                return Values.HitCollision.None;

            if ((hitType & HitType.Sword) == 0)
                return Values.HitCollision.None;

            return Values.HitCollision.RepellingParticle;
        }

        private void Update()
        {
            _animator.Update();

            if (_isDying)
            {
                _deathCount += Game1.DeltaTime;
                if (_deathCount > _deathState * 75)
                {
                    _deathState++;
                    Game1.AudioManager.PlaySoundEffect("D378-04-04");

                    // spawn explosion effect
                    Map.Objects.SpawnObject(new ObjAnimator(Map, (int)EntityPosition.X, (int)EntityPosition.Y + (6 - _deathState) * 16, Values.LayerBottom, "Particles/spawn", "run", true));

                    if (_deathState >= 6)
                        Map.Objects.DeleteObjects.Add(this);
                }
            }

            // move sound effect
            if (Math.Abs(_body.Velocity.X) > 0.05f && !_hasCollided)
                Game1.AudioManager.PlaySoundEffect("D370-26-1A", false);

            // collided with the wall? => slow down and stop
            if (_hasCollided)
            {
                _body.Velocity.X *= (float)Math.Pow(0.95f, Game1.TimeMultiplier);

                // stop moving
                if (Math.Abs(_body.Velocity.X) < 0.15f * Game1.TimeMultiplier)
                {
                    _body.Velocity.X = 0;
                    _animator.Play("idle");
                }
            }
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            // draw the bar
            for (var i = 0; i < 6; i++)
                if (i < 6 - _deathState)
                    _animator.Draw(spriteBatch, new Vector2(EntityPosition.X, EntityPosition.Y + 16 * i), Color.White);
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            return true;
        }

        private void OnCollision(Values.BodyCollision direction)
        {
            Game1.AudioManager.PlaySoundEffect("D360-11-0B");
            if (GameSettings.ScreenShake)
                Game1.GameManager.ShakeScreen(650, 1.75f, 0.00f, 6.50f, 0.00f);

            _body.Velocity.X = -_body.Velocity.X;
            _hasCollided = true;
        }
    }
}
