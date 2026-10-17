using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.Controls;
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
    internal class EnemyStalfosOrange : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiTriggerTimer _jumpSwitch;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private readonly bool _isBoneThrower;

        private float _acceleration = 1.75f;
        private float _walkSpeed = 0.5f;
        private float _changeDirCount;
        private float _throwCounter;
        private bool _throwBone;
        private int _dir;
        private int _lives = EnemyLives.StalfosOrange;
        private int _dropIndex = 4;

        public bool WasSpawned;

        public EnemyStalfosOrange() : base("stalfos orange") { }

        public EnemyStalfosOrange(Map.Map map, int posX, int posY, bool isBoneThrower) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            _isBoneThrower = isBoneThrower;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -32, 16, 32);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/stalfos orange");
            _animator.Play("walk");

            var sprite = new CSprite(EntityPosition);
            var animatorComponent = new AnimationComponent(_animator, sprite, new Vector2(-8, -16));

            _body = new BodyComponent(EntityPosition, -6, -10, 11, 10, 8)
            {
                MoveCollision = OnCollision,
                Gravity = -0.1f,
                FieldRectangle = map.GetField(posX, posY),
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.NPCWall,
                AvoidTypes =     Values.CollisionTypes.Hole | 
                                 Values.CollisionTypes.NPCWall
            };

            _aiComponent = new AiComponent();

            var stateWalking = new AiState(UpdateWalking);
            stateWalking.Trigger.Add(_jumpSwitch = new AiTriggerTimer(75));
            var stateJumping = new AiState(UpdateJumping);

            _aiComponent.States.Add("walking", stateWalking);
            _aiComponent.States.Add("jumping", stateJumping);
            new AiFallState(_aiComponent, _body, null, null, 200);
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { OnBurn = OnBurn };
            _aiComponent.ChangeState("walking");

            var damageBox   = new CBox(EntityPosition, -3,  -8, 0,  6,  6, 4);
            var hittableBox = new CBox(EntityPosition, -7, -15, 2, 13, 15, 8, true);
            var pushableBox = new CBox(EntityPosition, -6, -14, 2, 12, 14, 4);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { ArrowMultiplier = true, BombMultiplier = true, BoomerangMultiplier = true });
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, animatorComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(pushableBox, OnPush));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, sprite) { ShadowWidth = 10 });

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
        }

        public override void Reset()
        {
            // Check if it spawned from a Gibdo.
            if (IsActive && WasSpawned && !IsDead)
            {
                List<GameObject> enemyTriggers = new List<GameObject>();

                // Respawn the Gibdo if the Stalfos is still alive. 
                var newGibdo = new EnemyGibdo(Map, (int)ResetPosition.X - 8, (int)ResetPosition.Y - 16);
                Map.Objects.SpawnObject(newGibdo);

                // If there is utility objects in the room find them.
                Map.Objects.GetGameObjectsWithTag(enemyTriggers, Values.GameObjectTag.Utility,
                    (int)_body.FieldRectangle.X, (int)_body.FieldRectangle.Y, (int)_body.FieldRectangle.Width, (int)_body.FieldRectangle.Height);

                // Loop through the list of utility objects.
                foreach (var trigger in enemyTriggers) 
                {
                    // If it's an enemy trigger add the Gibdo.
                    if (trigger is ObjEnemyTrigger etrig)
                    {
                        etrig.EnemyTriggerList.Add(newGibdo);
                        newGibdo.AddToEnemyTriggerGroup(etrig);
                    }
                }
                // Remove the stalfos.
                Map.Objects.DeleteObjects.Add(this);
                return;
            }
            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _aiComponent.ChangeState("walking");
            _aiComponent.ChangeState("walking");
            _damageState.CurrentLives = EnemyLives.StalfosOrange;
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void UpdateWalking()
        {
            _animator.Play("walk");

            if (_throwBone)
            {
                _throwCounter -= Game1.DeltaTime;
                if (_throwCounter < 0)
                {
                    _throwBone = false;

                    // throw a bone towards the player
                    Map.Objects.SpawnObject(new EnemyBone(Map, (int)EntityPosition.X, (int)EntityPosition.Y - 8, 1.5f));
                }
            }

            // jump away when the player is pressing the use key
            var distance = EntityPosition.Position - MapManager.ObjLink.Position;
            if (_jumpSwitch.State && distance.Length() < 48)
                for (var i = 0; i < 4; i++)
                    if (ControlHandler.ButtonPressed((CButtons)((int)CButtons.A * Math.Pow(2, i))))
                    {
                        ToJumping();
                        break;
                    }

            _changeDirCount -= Game1.DeltaTime;

            // change direction
            if (_changeDirCount <= 0)
                ChangeDirection();
        }

        private void ToJumping()
        {
            Game1.AudioManager.PlaySoundEffect("D360-36-24");

            _aiComponent.ChangeState("jumping");

            // jump away from the player
            var vecDirection = EntityPosition.Position - MapManager.ObjLink.Position;
            vecDirection.Normalize();
            _body.Velocity = new Vector3(0, 0, _acceleration);
            _body.VelocityTarget = new Vector2(vecDirection.X, vecDirection.Y);

            _throwBone = _isBoneThrower;
            _throwCounter = 300;
        }

        private void UpdateJumping()
        {
            _animator.Play("jump");

            if (_body.IsGrounded)
                _aiComponent.ChangeState("walking");
        }

        private void ChangeDirection()
        {
            _changeDirCount = Game1.RandomNumber.Next(200, 600);
            _dir = Game1.RandomNumber.Next(0, 4);
            _body.VelocityTarget = AnimationHelper.DirectionOffset[_dir] * _walkSpeed;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            if (_aiComponent.CurrentStateId == "walking")
                ChangeDirection();
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (hitType == HitType.MagicRod || hitType == HitType.MagicPowder)
            {
                return Values.HitCollision.Blocking;
            }
            // Register the hit.
            var hit = _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

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
