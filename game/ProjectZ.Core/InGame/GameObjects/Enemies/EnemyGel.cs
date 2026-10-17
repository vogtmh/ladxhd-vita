using System;
using System.Collections.Generic;
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
    internal class EnemyGel : GameObject, IHasVisibility
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiTriggerSwitch _grabCooldown;
        private readonly AnimationComponent _animatorComponent;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly BodyDrawComponent _bodyDrawComponent;
        private readonly CSprite _sprite;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private int _grabX;
        private int _grabY;
        private int _dir;
        private int _timerOffset;
        private int _lives = EnemyLives.Gel;
        private int _dropIndex = 2;
        private bool _invulnerable;

        // Used to respawn Red Zol if both Gels are alive.
        private Vector2 ZolRespawnPos;
        private EnemyGel OtherGel;
        private bool IsMainGel;
        private bool WasSpawned;

        public bool IsVisible { get; private set; }

        public EnemyGel() : base("gel") { }

        public EnemyGel(Map.Map map, int posX, int posY) : base(map)
        {
            IsVisible = false;
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-4, -12, 7, 17);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/gel");
            _animator.Play("0");

            _sprite = new CSprite(EntityPosition);
            _animatorComponent = new AnimationComponent(_animator, _sprite, new Vector2(-4, -7));
            _invulnerable = true;

            _timerOffset = Game1.RandomNumber.Next(0, 1000);

            _body = new BodyComponent(EntityPosition, -3, -7, 6, 8, 8)
            {
                FieldRectangle = map.GetField(posX, posY),
                Gravity = -0.2f,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                AvoidTypes =     Values.CollisionTypes.Hole |
                                 Values.CollisionTypes.NPCWall,
            };

            _grabX = Game1.RandomNumber.Next(90, 110);
            _grabY = Game1.RandomNumber.Next(25, 40);

            _aiComponent = new AiComponent();
            var stateInit = new AiState();
            stateInit.Trigger.Add(new AiTriggerCountdown(100, null, EndIdle));
            var stateIdle = new AiState(UpdateIdle);
            stateIdle.Trigger.Add(new AiTriggerCountdown(200, null, EndIdle));
            var stateWalking = new AiState(UpdateWalking);
            stateWalking.Trigger.Add(new AiTriggerRandomTime(EndWalking, 100, 150));
            var stateShaking = new AiState(UpdateShaking);
            stateShaking.Trigger.Add(new AiTriggerCountdown(1000, null, EndShaking));
            var stateJumping = new AiState(UpdateJumping);
            var stateGrabbing = new AiState(UpdateGrabbing);
            stateGrabbing.Trigger.Add(new AiTriggerRandomTime(EndGrabbing, 1500, 2500));
            var stateGrabbingRelease = new AiState(UpdateJumping);

            _aiComponent.States.Add("init", stateInit);
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("walking", stateWalking);
            _aiComponent.States.Add("shaking", stateShaking);
            _aiComponent.States.Add("jumping", stateJumping);
            _aiComponent.States.Add("grabbing", stateGrabbing);
            _aiComponent.States.Add("grabbingRelease", stateGrabbingRelease);
            new AiFallState(_aiComponent, _body, null, null, 100);
            new AiDeepWaterState(_body);
            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex, true, true) { OnBurn = OnBurn };

            _aiComponent.Trigger.Add(_grabCooldown = new AiTriggerSwitch(2000));
            _aiComponent.ChangeState("init");

            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(_body.BodyBox, OnHit) { BoomerangMultiplier = true });
            AddComponent(ObjectCollisionComponent.Index, new ObjectCollisionComponent(new CRectangle(EntityPosition, new Rectangle(-3, -7, 6, 8)), OnPlayerCollision));
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, _animatorComponent);
            AddComponent(DrawComponent.Index, _bodyDrawComponent = new BodyDrawComponent(_body, _sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, _sprite));
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush) { RepelMultiplier = 2.25f });

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadows");
        }

        public override void Reset()
        {
            // It needs to be active or Gels attached to unsplit Zols will trigger this. This
            // also only applies to Gels that were spawned from splitting a Red Zol.
            if (IsActive && WasSpawned)
            {
                List<GameObject> enemyTriggers = new List<GameObject>();

                // Make sure both Gels are alive and check the index to prevent double respawn.
                if (OtherGel != null && !IsDead && !OtherGel.IsDead && IsMainGel)
                {
                    // Spawn the Red Zol if both Gels are alive. 
                    var newZol = new EnemyRedZol(Map, (int)ZolRespawnPos.X, (int)ZolRespawnPos.Y);
                    Map.Objects.SpawnObject(newZol);

                    // If there is utility objects in the room find them.
                    Map.Objects.GetGameObjectsWithTag(enemyTriggers, Values.GameObjectTag.Utility,
                        (int)_body.FieldRectangle.X, (int)_body.FieldRectangle.Y, (int)_body.FieldRectangle.Width, (int)_body.FieldRectangle.Height);

                    // Loop through the list of utility objects.
                    foreach (var trigger in enemyTriggers) 
                    {
                        // If it's an enemy trigger add the Red Zol.
                        if (trigger is ObjEnemyTrigger etrig)
                        {
                            etrig.EnemyTriggerList.Add(newZol);
                            newZol.AddToEnemyTriggerGroup(etrig);
                        }
                    }
                }
                // Always remove the Gel.
                IsActive = false;
                _sprite.IsVisible = false;
                _damageState.IsActive = false;
                Map.Objects.DeleteObjects.Add(this);
            }
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _aiComponent.ChangeState("idle");
        }

        private void OnBurn()
        {
            _animator.Pause();
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type != PushableComponent.PushType.Impact)
                return false;
            return true;
        }

        public void InitSpawn()
        {
            _body.Velocity.Z = 2;
        }

        private void UpdateIdle()
        {
            IsVisible = IsActive;

            _body.VelocityTarget = Vector2.Zero;
            _animator.Play(_dir.ToString());
        }

        private void EndIdle()
        {
            // Remove invincibility if it survives to idle state.
            _invulnerable = false;

            // If Link is not on the field return it to idle state. 
            if (!_body.FieldRectangle.Contains(MapManager.ObjLink.CenterPosition))
            {
                _aiComponent.ChangeState("idle");
                return;
            }
            // If Link is on the same field, walk towards him. 
            _aiComponent.ChangeState("walking");
        }

        private void UpdateWalking()
        {
            var vecDirection = new Vector2(
                MapManager.ObjLink.PosX - EntityPosition.X,
                MapManager.ObjLink.PosY - EntityPosition.Y);
            if (vecDirection != Vector2.Zero)
                vecDirection.Normalize();
            _dir = vecDirection.X < 0 ? -1 : 1;

            _animator.Play((-_dir).ToString());

            _body.VelocityTarget = vecDirection * 0.5f;
        }

        private void EndWalking()
        {
            // start shaking
            if (Game1.RandomNumber.Next(0, 10) == 0)
                _aiComponent.ChangeState("shaking");
            else
                _aiComponent.ChangeState("idle");
        }

        private void UpdateShaking()
        {
            _body.VelocityTarget = Vector2.Zero;
            _animatorComponent.SpriteOffset.X = -4 + (float)Math.Sin((Game1.TotalGameTime + _timerOffset) / 25f);
            _animatorComponent.UpdateSprite();
        }

        private void EndShaking()
        {
            // start jumping
            _aiComponent.ChangeState("jumping");

            _animatorComponent.SpriteOffset.X = -4;

            var vecDirection = new Vector2(
                MapManager.ObjLink.PosX - EntityPosition.X,
                MapManager.ObjLink.PosY - EntityPosition.Y);
            if (vecDirection != Vector2.Zero)
                vecDirection.Normalize();

            _body.VelocityTarget = vecDirection * 1.25f;
            _body.Velocity.Z = 1.25f;
        }

        private void UpdateJumping()
        {
            // If it jumps straight to the player after spawn, "init" and "idle is skipped.
            _invulnerable = false;
            if (_body.IsGrounded)
            {
                _body.VelocityTarget = Vector2.Zero;
                _aiComponent.ChangeState("idle");
            }
        }

        private void UpdateGrabbing()
        {
            EntityPosition.Set(MapManager.ObjLink.EntityPosition);
            MapManager.ObjLink.SlowDown(0.5f);
            MapManager.ObjLink.DisableItems = true;

            _bodyDrawComponent.Layer = Values.LayerTop;
            _animator.Play(_dir.ToString());

            _animatorComponent.SpriteOffset.X = -4 + (float)Math.Sin((Game1.TotalGameTime + _timerOffset) / _grabX) * 3.5f;
            _animatorComponent.SpriteOffset.Y = -7 - 2 + (float)Math.Sin((Game1.TotalGameTime + _timerOffset) / _grabY) * 1.5f;
            _animatorComponent.UpdateSprite();
        }

        private void EndGrabbing()
        {
            var angle = Game1.RandomNumber.Next(-100, 100) / 200f * (float)Math.PI;
            var vecDirection = new Vector2((float)Math.Sin(angle), -(float)Math.Cos(angle));
            _body.VelocityTarget = vecDirection;
            _body.Velocity.Z = 1.5f;

            _animatorComponent.SpriteOffset.X = -4;
            _animatorComponent.SpriteOffset.Y = -7;

            _bodyDrawComponent.Layer = Values.LayerPlayer;
            _aiComponent.ChangeState("grabbingRelease");
            _grabCooldown.Reset();
        }

        private void OnPlayerCollision(GameObject gameObject)
        {
            if (_grabCooldown.State &&
                _aiComponent.CurrentStateId != "grabbing" &&
                _aiComponent.CurrentStateId != "grabbingRelease" &&
                _aiComponent.CurrentStateId != "burning")
                _aiComponent.ChangeState("grabbing");
        }

        public void SetOtherGel(EnemyGel otherGel, bool isMainGel, Vector2 zolPos)
        {
            // If this ran, it's a Gel spawned from a Red Zol.
            WasSpawned = true;

            // Store some properties of the other Gel that split off of the Zol. 
            OtherGel = otherGel;
            IsMainGel = isMainGel;
            ZolRespawnPos = zolPos;
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // When it is first spawning, give it some invulnerability time.
            if (_invulnerable)
                return Values.HitCollision.None;

            // If currently grabbed onto the player it can't be killed.
            if (_aiComponent.CurrentStateId == "grabbing")
                return Values.HitCollision.None;

            // When hit with the sword, they don't get knocked back.
            if ((hitType & HitType.AnySword) != 0)
            {
                _damageState.CooldownTime = 0;
                _damageState.MoveBody = false;
            }
            // Track that this Gel is now dead.
            IsDead = true;

            // In the original game, it does not get knocked back at all.
            _body.VelocityTarget = Vector2.Zero;

            // And then just return normal damage state.
            return _damageState.OnHit(originObject, direction, hitType, damage, pieceOfPower);
        }
    }
}
