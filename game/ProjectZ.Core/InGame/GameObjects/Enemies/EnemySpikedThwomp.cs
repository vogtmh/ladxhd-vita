using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Base.Systems;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    class EnemySpikedThwomp : GameObject
    {
        private readonly List<GameObject> _collidingObjects = new List<GameObject>();

        private readonly Animator _animator;
        private readonly AiComponent _aiComponent;
        private readonly BodyComponent _body;
        private readonly CBox _collisionBox;

        private readonly Vector2 _startPosition;

        private Box _lastCollisionBox;

        public EnemySpikedThwomp() : base("spiked thwomp") { }

        public EnemySpikedThwomp(Map.Map map, int posX, int posY) : base(map)
        {
            EntityPosition = new CPosition(posX + 16, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 16, posY + 16, 0);
            EntitySize = new Rectangle(-16, -16, 32, 32);
            CanReset = true;
            OnReset = Reset;

            _startPosition = EntityPosition.Position;

            _body = new BodyComponent(EntityPosition, -16, -16, 32, 31, 8)
            {
                Gravity2D = 0.175f,
                IgnoresZ = true,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.NPCWall,
                MoveCollision = OnCollision
            };

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/spiked thwomp");
            _animator.Play("attack");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-16, -16));

            var stateIdle = new AiState(UpdateIdle);
            var stateAttack = new AiState();
            var stateAttackCooldown = new AiState();
            stateAttackCooldown.Trigger.Add(new AiTriggerCountdown(1000, null, ToGoingUp)); // 800 closer to the actual value
            var stateGoingUp = new AiState(UpdateGoingUp);
            var stateUpWaiting = new AiState();
            stateUpWaiting.Trigger.Add(new AiTriggerCountdown(600, null, ToIdle));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("attack", stateAttack);
            _aiComponent.States.Add("attackCooldown", stateAttackCooldown);
            _aiComponent.States.Add("goingUp", stateGoingUp);
            _aiComponent.States.Add("upWaiting", stateUpWaiting);

            _aiComponent.ChangeState("idle");

            var damageBox = new CBox(EntityPosition, -14, -16, 28, 32, 4);
            var hittableBox = new CBox(EntityPosition, -14, -16, 28, 32, 8);
            _collisionBox = new CBox(EntityPosition, -16, -16, 32, 4, 8);

            AddComponent(HittableComponent.Index, new HittableComponent(hittableBox, OnHit));
            AddComponent(DamageFieldComponent.Index, new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(CollisionComponent.Index, new BoxCollisionComponent(_collisionBox, Values.CollisionTypes.Enemy));
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(sprite, Values.LayerBottom));

            Map.Objects.RegisterAlwaysAnimateObject(this);
        }
        public override void Reset()
        {
            _aiComponent.ChangeState("idle");
            _body.Velocity = Vector3.Zero;
            _body.VelocityTarget = Vector2.Zero;
            _body.IgnoresZ = true;
            _animator.Play("idle");
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            return Values.HitCollision.RepellingParticle;
        }

        private void ToIdle()
        {
            _aiComponent.ChangeState("idle");
        }

        private void UpdateIdle()
        {
            // look at the player
            var lookDirection = MapManager.ObjLink.Position - EntityPosition.Position;

            var radiant = MathF.Atan2(lookDirection.X, -lookDirection.Y);

            var clampedValue = Math.Clamp((Math.Abs(radiant) - 0.75f) / (MathF.PI - 1.25f), 0, 2);
            var animationDir = (int)(clampedValue * 3) + 1;

            if (animationDir <= 3)
                _animator.Play((radiant < 0 ? "l" : "r") + animationDir);
            else
                _animator.Play("down");

            // start attacking?
            if (Math.Abs(lookDirection.X) < 22)
            {
                ToAttacking();
            }
        }

        private void ToAttacking()
        {
            _aiComponent.ChangeState("attack");
            _animator.Play("attack");
            _body.IgnoresZ = false;
            Game1.AudioManager.PlaySoundEffect("D360-08-08");
        }

        private void ToGoingUp()
        {
            _aiComponent.ChangeState("goingUp");
            _body.IgnoresZ = true;
            _body.Velocity.Y = 0;
        }

        private void UpdateGoingUp()
        {
            _lastCollisionBox = _collisionBox.Box;

            EntityPosition.Move(new Vector2(0, -0.5f));
            if (EntityPosition.Y < _startPosition.Y)
            {
                EntityPosition.Set(new Vector2(EntityPosition.X, _startPosition.Y));
                _aiComponent.ChangeState("upWaiting");
            }

            MoveBodies();
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            if ((collision & Values.BodyCollision.Bottom) != 0 &&
                _aiComponent.CurrentStateId == "attack")
            {
                HitGround();
            }
        }

        private void HitGround()
        {
            // shake the screen
            if (GameSettings.ScreenShake)
                Game1.GameManager.ShakeScreen(350, 0.00f, 1.00f, 10.0f, 25.0f);

            Game1.AudioManager.PlaySoundEffect("D378-12-0C");

            _aiComponent.ChangeState("attackCooldown");
        }

        private void MoveBodies()
        {
            // Check for colliding bodies and push them forward.
            _collidingObjects.Clear();
            Map.Objects.GetComponentList(_collidingObjects,
                (int)_lastCollisionBox.Left, (int)_lastCollisionBox.Back - 8,
                (int)_lastCollisionBox.Width, (int)_lastCollisionBox.Height, BodyComponent.Mask);

            foreach (var collidingObject in _collidingObjects)
            {
                var body = (BodyComponent)collidingObject.Components[BodyComponent.Index];

                // The enemy moves into the body that is standing on top of it.
                if (body.BodyBox.Box.Front <= _lastCollisionBox.Back &&
                    body.BodyBox.Box.Front >= _collisionBox.Box.Back &&
                    body.BodyBox.Box.Intersects(_collisionBox.Box))
                {
                    // Snap the body into the correct offset if there has been change.
                    float correction = _collisionBox.Box.Back - body.BodyBox.Box.Front;
                    if (correction != 0f)
                    {
                        var offset = new Vector2(0, correction);
                        SystemBody.MoveBody(body, offset, body.CollisionTypes, false, false, false);
                        body.Position.NotifyListeners();
                    }
                }
            }
        }
    }
}
