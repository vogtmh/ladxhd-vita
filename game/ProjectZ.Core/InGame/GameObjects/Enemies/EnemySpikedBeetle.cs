using System;
using Microsoft.Xna.Framework;
using ProjectZ.Base;
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
    internal class EnemySpikedBeetle : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AnimationComponent _animationComponent;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private Vector2 _velocityTarget;

        private float _walkSpeed = 0.5f;
        private float _runSpeed = 1.5f;
        private bool _playerInsideField;
        private int _lives = EnemyLives.SpikedBeetle;
        private int _dropIndex = 4;

        public EnemySpikedBeetle() : base("spiked beetle") { }

        public EnemySpikedBeetle(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/spiked beetle");
            _animator.Play("walk");

            var sprite = new CSprite(EntityPosition);
            _animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -16));

            _body = new BodyComponent(EntityPosition, -6, -10, 12, 9, 8)
            {
                MoveCollision = OnCollision,
                Drag = 0.8f,
                Gravity = -0.1f,
                Bounciness = 0.5f,
                FieldRectangle = map.GetField(posX, posY),
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                AvoidTypes =     Values.CollisionTypes.Hole |
                                 Values.CollisionTypes.NPCWall |
                                 Values.CollisionTypes.DeepWater
            };

            var stateWalking = new AiState(UpdateAttack);
            stateWalking.Trigger.Add(new AiTriggerRandomTime(ToWaiting, 750, 1000));
            var stateWaiting = new AiState(UpdateAttack);
            stateWaiting.Trigger.Add(new AiTriggerRandomTime(ToWalking, 750, 1000));
            var stateBack = new AiState();
            stateBack.Trigger.Add(new AiTriggerCountdown(5000, BackTick, BackEnd));
            var stateRushing = new AiState();
            var stateStunned = new AiState();
            stateStunned.Trigger.Add(new AiTriggerCountdown(1000, null, ToWalking));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("walking", stateWalking);
            _aiComponent.States.Add("rushing", stateRushing);
            _aiComponent.States.Add("waiting", stateWaiting);
            _aiComponent.States.Add("stunned", stateStunned);
            _aiComponent.States.Add("back", stateBack);
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { OnBurn = OnBurn };

            new AiFallState(_aiComponent, _body, OnAbsorption, null, 250);
            new AiDeepWaterState(_body);

            // random start position/state
            ToWalking();

            var damageBox   = new CBox(EntityPosition, -3, -8, 0,  6,  4, 4);
            var hittableBox = new CBox(EntityPosition, -8, -14, 0, 16, 14, 8);
            var pushableBox = new CBox(EntityPosition, -7, -13, 0, 14, 13, 8);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { ArrowMultiplier = true, BombMultiplier = true, BoomerangMultiplier = true });
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(pushableBox, OnPush));
            AddComponent(BaseAnimationComponent.Index, _animationComponent);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, sprite) { ShadowWidth = 10, ShadowHeight = 5 });

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
        }

        public override void Reset()
        {
            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _animator.SpeedMultiplier = 1f;
            ToWalking();
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void Update()
        {
            if (_body.FieldRectangle.Contains(MapManager.ObjLink.CenterPosition))
                _playerInsideField = true;
        }

        private void ToWaiting()
        {
            _animator.Play("walk");
            _animator.IsPlaying = false;

            _aiComponent.ChangeState("waiting");
            _velocityTarget = Vector2.Zero;
            _body.VelocityTarget = Vector2.Zero;
        }

        private void ToWalking()
        {
            _animator.Play("walk");
            _aiComponent.ChangeState("walking");

            var dir = Game1.RandomNumber.Next(0, 4);
            _velocityTarget = AnimationHelper.DirectionOffset[dir] * _walkSpeed;
        }

        private void ToBack()
        {
            _animator.Play("back");
            _aiComponent.ChangeState("back");
            _body.VelocityTarget = Vector2.Zero;
            _velocityTarget = Vector2.Zero;
        }

        private void BackTick(double time)
        {
            // start shaking
            if (time <= 2000)
            {
                _animationComponent.SpriteOffset.X = -8 + (float)Math.Sin(time / 25f);
                _animationComponent.UpdateSprite();
            }
        }

        private void BackEnd()
        {
            _animationComponent.SpriteOffset.X = -8;
            _animationComponent.UpdateSprite();

            _body.Velocity.Z = 1.5f;
            ToWalking();
        }

        private void ToRushing(int direction)
        {
            _body.VelocityTarget = AnimationHelper.DirectionOffset[direction] * _runSpeed;
            _aiComponent.ChangeState("rushing");
            _animator.Play("walk");
        }

        private void UpdateAttack()
        {
            var oldPercentage = (float)Math.Pow(0.9f, Game1.TimeMultiplier);
            _body.VelocityTarget = _body.VelocityTarget * oldPercentage +
                                   _velocityTarget * (1 - oldPercentage);

            if (!_playerInsideField)
                return;

            var collisionRectangles = new RectangleF[4];
            collisionRectangles[0] = new RectangleF(EntityPosition.X - 128, EntityPosition.Y - 5, 128, 2);
            collisionRectangles[1] = new RectangleF(EntityPosition.X, EntityPosition.Y - 128, 2, 128);
            collisionRectangles[2] = new RectangleF(EntityPosition.X, EntityPosition.Y - 5, 128, 2);
            collisionRectangles[3] = new RectangleF(EntityPosition.X, EntityPosition.Y, 2, 128);

            for (var i = 0; i < collisionRectangles.Length; i++)
                if (collisionRectangles[i].Intersects(MapManager.ObjLink.BodyRectangle))
                {
                    ToRushing(i);
                    break;
                }

            _playerInsideField = false;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (_damageState.IsInDamageState())
                return Values.HitCollision.None;

            if (_damageState.CurrentLives <= 0)
            {
                _damageField.IsActive = false;
                _hitComponent.IsActive = false;
                _pushComponent.IsActive = false;
            }

            _body.DragAir = 0.9f;

            if (_aiComponent.CurrentStateId == "back")
                return _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            if (hitType == HitType.MagicRod)
                return Values.HitCollision.SpawnFire;

            if (hitType == HitType.ThrownObject)
            {
                _body.Velocity = new Vector3(direction.X * 0.5f, direction.Y * 0.5f, 1.0f);
                ToBack();

                return Values.HitCollision.Enemy;
            }

            if (_aiComponent.CurrentStateId == "stunned")
            {
                _damageState.SetDamageState(false);
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, 0);
                return Values.HitCollision.RepellingParticle;
            }

            _aiComponent.ChangeState("stunned");

            _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, 0);
            _body.VelocityTarget = Vector2.Zero;
            _velocityTarget = Vector2.Zero;

            _animator.IsPlaying = false;

            return Values.HitCollision.RepellingParticle;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
            {
                _body.DragAir = 0.975f;
                if (_aiComponent.CurrentStateId != "back")
                {
                    _body.Velocity = new Vector3(direction.X * 0.75f, direction.Y * 0.75f, 1.5f);
                    ToBack();
                }
                else
                {
                    _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);
                }
            }

            return true;
        }

        private void OnCollision(Values.BodyCollision direction)
        {
            if (_aiComponent.CurrentStateId == "stunned")
                return;

            // stop rushing
            if (_aiComponent.CurrentStateId == "rushing")
            {
                _aiComponent.ChangeState("waiting");
                return;
            }

            // collide with a wall
            if ((direction & Values.BodyCollision.Horizontal) != 0 && Math.Sign(_body.VelocityTarget.X) == Math.Sign(_velocityTarget.X))
                _velocityTarget.X = -_velocityTarget.X;
            else if ((direction & Values.BodyCollision.Vertical) != 0 && Math.Sign(_body.VelocityTarget.Y) == Math.Sign(_velocityTarget.Y))
                _velocityTarget.Y = -_velocityTarget.Y;
        }

        private void OnAbsorption()
        {
            _animator.SpeedMultiplier = 1.5f;
        }
    }
}
