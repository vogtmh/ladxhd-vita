using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyPeahat : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiTriggerCountdown _flyCounter;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private const int StartTime = 2500;
        private const int FlyTime = 7500;
        private const int LandTime = 1500;

        private float _flyState;
        private float _turnSpeed;
        private int _dir = 1;
        private int _lives = EnemyLives.Peahat;
        private int _dropIndex = 2;

        public EnemyPeahat() : base("peahat") { }

        public EnemyPeahat(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -32, 16, 32);
            CanReset = true;
            OnReset = Reset;

            _turnSpeed = 0.02f;
            _flyState = (float)Math.PI / 2 * Game1.RandomNumber.Next(0, 4);
            _dir = Game1.RandomNumber.Next(0, 2) * 2 - 1;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/peahat");
            _animator.SpeedMultiplier = 0;
            _animator.Play("idle");

            var sprite = new CSprite(EntityPosition);
            var animatorComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -16));

            _body = new BodyComponent(EntityPosition, -6, -12, 12, 10, 8)
            {
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                MoveCollision = OnCollision,
                IgnoresZ = true,
                FieldRectangle = map.GetField(posX, posY),
                MaxJumpHeight = 4
            };

            _aiComponent = new AiComponent();

            var stateStart = new AiState();
            stateStart.Trigger.Add(new AiTriggerCountdown(StartTime, StartTick, ToFlying));
            var stateFly = new AiState(UpdateFlying);
            stateFly.Trigger.Add(new AiTriggerRandomTime(ChangeFlyingDirection, 500, 1500));
            stateFly.Trigger.Add(_flyCounter = new AiTriggerCountdown(FlyTime, null, ToLand));
            var stateLand = new AiState();
            stateLand.Trigger.Add(new AiTriggerCountdown(LandTime, LandTick, ToIdle));
            var stateIdle = new AiState();
            stateIdle.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("start"), 3000, 6000));
            var stateStunned = new AiState();
            stateStunned.Trigger.Add(new AiTriggerCountdown(200, null, EndStunned));

            _aiComponent.States.Add("start", stateStart);
            _aiComponent.States.Add("fly", stateFly);
            _aiComponent.States.Add("land", stateLand);
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("stunned", stateStunned);
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex, false) { OnBurn = OnBurn };

            _aiComponent.ChangeState("start");

            var damageBox   = new CBox(EntityPosition, -3, -10, 0,  6,  6, 4, true);
            var hittableBox = new CBox(EntityPosition, -6, -14, 0, 12, 14, 8, true);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { BoomerangMultiplier = true });
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(damageBox, OnPush));
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, animatorComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer) { DeepWaterOutline = true });
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, sprite));

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
        }

        public override void Reset()
        {
            EntityPosition.Z = 0;

            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;

            _body.Velocity = Vector3.Zero;
            _body.VelocityTarget = Vector2.Zero;
            _animator.SpeedMultiplier = 0;
            _aiComponent.ChangeState("start");
            _aiComponent.ChangeState("start");
            _damageState.CurrentLives = EnemyLives.Peahat;
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            // speed up the collision time
            _flyState += (_dir == -1 ? -1 : 1) * MathF.PI * 0.025f * Game1.TimeMultiplier;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }

        private void EndStunned()
        {
            _aiComponent.ChangeState(_aiComponent.LastStateId, true);
        }

        private void StartTick(double count)
        {
            // speed up animation
            var mult = (StartTime - (float)count + 250f) / 1500f;
            mult = Math.Clamp(mult, 0, 1);
            _animator.SpeedMultiplier = mult;

            // start moving up
            if (count < StartTime - 1250)
                EntityPosition.Z = (float)(1 - count / (StartTime - 1250)) * 16;
        }

        private void ToFlying()
        {
            _aiComponent.ChangeState("fly");
            EntityPosition.Z = 16;
        }

        private void ChangeFlyingDirection()
        {
            _dir = Game1.RandomNumber.Next(0, 3) - 1;
        }

        private void UpdateFlying()
        {
            _animator.SpeedMultiplier = 1;

            _flyState += _dir * _turnSpeed * Game1.TimeMultiplier;
            var vecDirection = new Vector2((float)Math.Sin(_flyState), (float)Math.Cos(_flyState));

            // speeds up / slows down
            float speedMult;
            if (_flyCounter.CurrentTime >= FlyTime - 250)
                speedMult = Math.Clamp((float)(FlyTime - _flyCounter.CurrentTime) / 250, 0, 1);
            else
                speedMult = Math.Min(1, (float)_flyCounter.CurrentTime / 250);

            // fly
            _body.VelocityTarget = vecDirection * 0.5f * speedMult;
        }

        private void ToLand()
        {
            _aiComponent.ChangeState("land");
            _body.VelocityTarget = Vector2.Zero;
        }

        private void LandTick(double count)
        {
            // speed up animation
            var mult = (float)count / LandTime;
            mult = Math.Clamp(mult, 0, 1);
            _animator.SpeedMultiplier = mult;

            // move down
            var posZ = (float)((count - 500) / (LandTime - 500));
            posZ = Math.Clamp(posZ, 0, 1);
            EntityPosition.Z = posZ * 16;
        }

        private void ToIdle()
        {
            _aiComponent.ChangeState("idle");
            EntityPosition.Z = 0;
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (_aiComponent.CurrentStateId == "stunned")
                return Values.HitCollision.None;

            if (_animator.SpeedMultiplier > 0.25f)
            {
                if (hitType == HitType.Boomerang)
                    return Values.HitCollision.Blocking;

                if ((hitType & HitType.AnySword) != 0)
                {
                    _aiComponent.ChangeState("stunned");
                    _animator.SpeedMultiplier = 0;
                    _body.VelocityTarget = Vector2.Zero;
                }
                return Values.HitCollision.RepellingParticle | Values.HitCollision.SpawnFire;
            }
            // Register the hit.
            var hit = _damageState.OnHit(originObject, direction, hitType, damage, pieceOfPower);

            // When a hit removes all lives disable components.
            if (_damageState.CurrentLives <= 0)
            {
                _damageField.IsActive = false;
                _hitComponent.IsActive = false;
                _pushComponent.IsActive = false;
            }
            // Return the hit.
            return hit;
        }
    }
}
