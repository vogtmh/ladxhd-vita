using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyMonkey : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;

        private ObjBomb _objBomb;

        private const int FadeTime = 125;
        private float _fleeCounter = 1500;
        private int _throwState;
        private int _bombCountdown = 5;
        private bool _fleeing;
        private int _dropIndex = 2;

        public EnemyMonkey() : base("monkey enemy") { }

        public EnemyMonkey(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 33, 17);
            EntitySize = new Rectangle(-16, -38, 32, 38);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/monkey");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -8, -16, 16, 16, 8)
            {
                FieldRectangle = map.GetField(posX, posY),
                FieldLocked = false,
                IgnoresZ = true,
                DragAir = 1,
                Gravity = -0.125f,
                CollisionTypes = Values.CollisionTypes.None
            };

            var stateIdle = new AiState { Init = InitIdle };
            stateIdle.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("throwL"), 1000, 1500));
            var stateThrowL = new AiState { Init = InitThrow };
            stateThrowL.Trigger.Add(new AiTriggerCountdown(666, null, () => _aiComponent.ChangeState("throwR")));
            var stateThrowR = new AiState { Init = InitThrow };
            stateThrowR.Trigger.Add(new AiTriggerCountdown(666, null, () => _aiComponent.ChangeState("idle")));
            var stateFall = new AiState(UpdateFall) { Init = InitFall };
            var stateJump = new AiState(UpdateJump) { Init = InitJump };
            var stateFlee = new AiState(UpdateFlee);

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("throwL", stateThrowL);
            _aiComponent.States.Add("throwR", stateThrowR);
            _aiComponent.States.Add("fall", stateFall);
            _aiComponent.States.Add("jump", stateJump);
            _aiComponent.States.Add("flee", stateFlee);
            _aiComponent.ChangeState("idle");

            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, 1, _dropIndex, true, false);

            var hitBox = new CBox(posX - 8, posY, 0, 32, 32, 8);

            AddComponent(HittableComponent.Index, new HittableComponent(hitBox, OnHit));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, _sprite));

            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        public override void Reset()
        {
            if (_objBomb != null)
                Map.Objects.DeleteObjects.Add(_objBomb);
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if ((hitType & HitType.BowWow) != 0)
                return _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            if (!_fleeing && (hitType & HitType.PegasusBootsPush) != 0)
            {
                _fleeing = true;
                _body.IgnoresZ = false;
                _body.IsGrounded = false;
                _body.Velocity = new Vector3(direction.X * 0.35f, 0.75f, 1.75f);
                _aiComponent.ChangeState("fall");
            }

            return Values.HitCollision.None;
        }

        private void InitIdle()
        {
            _animator.Play("idle");
        }

        private void InitThrow()
        {
            // is the player close enough?
            var direction = MapManager.ObjLink.Position - EntityPosition.Position;
            if (direction.Y < -32 || Math.Abs(direction.X) > 64 || direction.Length() > 80)
            {
                _aiComponent.ChangeState("idle");
                return;
            }

            Game1.AudioManager.PlaySoundEffect("D360-08-08");

            _body.VelocityTarget = new Vector2(0, 0);

            _animator.Play("throw" + (_throwState == 0 ? "l" : "r"));
            _throwState = (_throwState + 1) % 2;

            var throwDirection = new Vector3(_throwState == 0 ? 0.5f : -0.5f, 0.75f, 1.75f);
            _bombCountdown--;

            if (_bombCountdown >= 0)
            {
                // spawn a nut
                Map.Objects.SpawnObject(new EnemyNut(Map, new Vector3(EntityPosition.X, EntityPosition.Y, 20), throwDirection));
            }
            else
            {
                // spawn a bomb
                _objBomb = new ObjBomb(Map, 0, 0, false, true);
                _objBomb.EntityPosition.Set(new Vector3(EntityPosition.X, EntityPosition.Y, 20));
                _objBomb.Body.Velocity = throwDirection;
                _objBomb.Body.Gravity = -0.1f;
                _objBomb.Body.DragAir = 1.0f;
                _objBomb.Body.Bounciness = 0.25f;
                Map.Objects.SpawnObject(_objBomb);
                new ObjSpriteShadow(Map, _objBomb, Values.LayerPlayer, "sprshadowm");
                _bombCountdown = Game1.RandomNumber.Next(10, 16);
            }
        }

        private void InitFall()
        {
            _animator.Play("fall");
        }

        private void UpdateFall()
        {
            if (_body.IsGrounded)
                _aiComponent.ChangeState("jump");
        }

        private void InitJump()
        {
            _animator.Play("idle");

            _body.IsGrounded = false;
            _body.Velocity = new Vector3(0, 0, 2.0f);

            Game1.AudioManager.PlaySoundEffect("D370-20-14");
        }

        private void UpdateJump()
        {
            if (_body.IsGrounded)
                _aiComponent.ChangeState("flee");
        }

        private void UpdateFlee()
        {
            _fleeCounter -= Game1.DeltaTime;
            _sprite.Color = Color.White * Math.Clamp(_fleeCounter / FadeTime, 0, 1);
            if (_fleeCounter <= 0)
            {
                Map.Objects.DeleteObjects.Add(this);
                return;
            }

            if (_body.IsGrounded)
            {
                var direction = EntityPosition.Position - MapManager.ObjLink.Position;
                if (direction != Vector2.Zero)
                    direction.Normalize();

                // jump away from the player
                _body.Velocity = new Vector3(direction.X * 1.25f, direction.Y * 1.25f, 1.25f);

                Game1.AudioManager.PlaySoundEffect("D370-20-14");
            }
        }
    }
}
