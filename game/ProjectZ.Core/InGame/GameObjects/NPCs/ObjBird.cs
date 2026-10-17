using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.NPCs
{
    internal class ObjBird : GameObject, IHasSpriteVisibility
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiTriggerSwitch _changeDirectionSwitch;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CarriableComponent _carriableComponent;
        private readonly CSprite _sprite;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;
        private readonly DamageFieldComponent _damageField;

        private float _attackCounter;
        private float _attackingCounter;
        private float _attackTransparency;
        private bool _attackMode;
        private bool _swarmSpawn;
        private bool _isThrown;
        private int _hitCounter;
        private int _direction;
        private int _dropIndex = 2;

        private Rectangle _objectField;

        private List<ObjBird> _chickenList = new List<ObjBird>();

        public CSprite Sprite => _sprite;

        public ObjBird() : base("bird") { }

        public ObjBird(Map.Map map, int posX, int posY, bool swarmSpawn) : base(map)
        {
            var rectangle = new Rectangle(0, 0, 14, 8);
            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -32, 16, 32);

            CanReset = true;
            OnReset = Reset;

            _body = new BodyComponent(EntityPosition, -6, -8, 12, 8, 8)
            {
                FieldRectangle = map.GetField(posX, posY),
                MoveCollision = OnCollision,
                Bounciness = 0.25f,
                Drag = 0.15f,
                DragAir = 1.0f,
                Gravity = -0.1f,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.NPCWall |
                                 Values.CollisionTypes.Player,
                AvoidTypes =     Values.CollisionTypes.Hole |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.NPCWall |
                                 Values.CollisionTypes.Player,
            };
            _objectField = map.GetField(posX, posY);
            _swarmSpawn = swarmSpawn;
            _animator = AnimatorSaveLoad.LoadAnimator("NPCs/bird");
            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-8, -15));

            var stateIdle = new AiState() { Init = InitIdle };
            stateIdle.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("walking"), 500, 1500));
            var stateWalking = new AiState(UpdateWalking) { Init = InitWalk };
            stateWalking.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("idle"), 750, 1500));
            stateWalking.Trigger.Add(_changeDirectionSwitch = new AiTriggerSwitch(250));
            var stateFleeIdle = new AiState(UpdateFleeState) { Init = InitIdle };
            stateFleeIdle.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("fleeingWalking"), 500, 1500));
            var stateFleeWalking = new AiState(UpdateFleeState) { Init = InitWalk };
            stateFleeWalking.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("fleeingIdle"), 750, 1500));
            var stateFleeing = new AiState(UpdateFleeing);
            var stateAttack = new AiState(UpdateAttack);
            var stateThrown = new AiState(UpdateThrown);
            var statePickedUp = new AiState(UpdatePickedUp);

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("walking", stateWalking);
            _aiComponent.States.Add("fleeingIdle", stateFleeIdle);
            _aiComponent.States.Add("fleeingWalking", stateFleeWalking);
            _aiComponent.States.Add("fleeing", stateFleeing);
            _aiComponent.States.Add("attack", stateAttack);
            _aiComponent.States.Add("thrown", stateThrown);
            _aiComponent.States.Add("pickedUp", statePickedUp);
            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, 2, _dropIndex) { OnBurn = OnBurn, OnDeath = TrackKills };
            _aiComponent.ChangeState(Game1.RandomNumber.Next(0, 10) < 5 ? "idle" : "walking");

            var box = new CBox(EntityPosition, -6, -12, 0, 12, 12, 8, true);

            // Do not allow picking up chickens that are swarming the player.
            if (!swarmSpawn)
                AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-6, -14, 12, 14)), CarryInit, CarryUpdate, CarryThrow) { IsInstant = true, StartGrabbing = StartGrabbing });
            
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(box, HitType.Enemy, 2) { IsActive = false });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, _sprite));
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush) { RepelMultiplier = 2.25f });
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(_body.BodyBox, OnHit));

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
        }

        private void TrackKills(bool pieceOfPower)
        {
            // If all animals in the village are killed then give the achievement.
            int animalKills = Game1.GameManager.SaveManager.GetInt("animal_kills", 0) + 1;
            if (animalKills >= 8)
            {
                AchievementManager.Earn(101);
                Game1.GameManager.SaveManager.RemoveInt("animal_kills");
            }
            else
                Game1.GameManager.SaveManager.SetInt("animal_kills", animalKills);
            _damageState.BaseOnDeath(pieceOfPower);
        }

        public override void Reset()
        {
            _attackMode = false;
            _damageField.IsActive = false;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            if (_carriableComponent != null)
                _carriableComponent.IsActive = true;
            _aiComponent.ChangeState("idle");
            _hitCounter = 0;
            DeleteBirds();
        }

        public void InitAttackMode()
        {
            _aiComponent.ChangeState("attack");

            // spawn around the player
            var radiant = Game1.RandomNumber.Next(0, 628) / 100f;
            var offset = new Vector2(MathF.Sin(radiant), MathF.Cos(radiant));

            var spawnPosition = MapManager.ObjLink.Position + new Vector2(0, 24) + offset * 80;
            EntityPosition.Set(spawnPosition);
            EntityPosition.Z = 16;

            _damageField.IsActive = true;

            var direction = MapManager.ObjLink.Position - new Vector2(EntityPosition.Position.X, EntityPosition.Position.Y - 16);
            if (direction != Vector2.Zero)
                direction.Normalize();
            _body.VelocityTarget = direction * 1.5f;

            _body.IgnoresZ = true;
            _body.CollisionTypes = Values.CollisionTypes.None;
            _body.AvoidTypes = Values.CollisionTypes.None;

            _direction = _body.VelocityTarget.X < 0 ? 0 : 1;
            _animator.Play("walk_" + _direction);
            _animator.SpeedMultiplier = 2;
        }

        private void UpdateAttack()
        {
            _attackingCounter += Game1.DeltaTime;

            var target = _attackingCounter < 2000 ? 1 : 0;
            _attackTransparency = AnimationHelper.MoveToTarget(_attackTransparency, target, 0.1f * Game1.TimeMultiplier);
            _sprite.Color = Color.White * _attackTransparency;

            Game1.AudioManager.PlaySoundEffect("D378-45-2D", false);

            if (_attackTransparency == 0)
            {
                _chickenList.Remove(this);
                Map.Objects.DeleteObjects.Add(this);
            }
        }

        private void DeleteBirds()
        {
            foreach (ObjBird chicken in _chickenList.ToList())
            {
                _chickenList.Remove(chicken);
                Map.Objects.DeleteObjects.Add(chicken);
            }
        }

        private void SpawnBirds()
        {
            if (!_attackMode)
                return;

            var playerDir = MapManager.ObjLink.Position - EntityPosition.Position;

            if ((Camera.ClassicMode && MapManager.ObjLink.FieldTransition(_objectField)) || (!Camera.ClassicMode && playerDir.Length() > 140))
            {
                _attackMode = false;
                _hitCounter = 0;
                _aiComponent.ChangeState("idle");
                return;
            }
            Game1.AudioManager.PlaySoundEffect("D370-19-13", false);

            _attackCounter -= Game1.DeltaTime;
            if (_attackCounter < 0)
            {
                _attackCounter += Game1.RandomNumber.Next(300, 550);

                var objBird = new ObjBird(Map, (int)EntityPosition.X, (int)EntityPosition.Y, true);
                objBird.InitAttackMode();

                // Achievement: Trigger a chicken storm.
                AchievementManager.Earn(17);

                Map.Objects.SpawnObject(objBird);
                Map.Objects.RegisterAlwaysAnimateObject(objBird);

                _chickenList.Add(objBird);
            }
        }

        private void OnBurn()
        {
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
            _carriableComponent.IsActive = false;
            _animator.Pause();
        }

        private void InitIdle()
        {
            // stop and wait
            _body.VelocityTarget = Vector2.Zero;
            _animator.Play("idle_" + _direction);
        }

        private void InitWalk()
        {
            // change the direction
            var rotation = Game1.RandomNumber.Next(0, 628) / 100f;
            _body.VelocityTarget = new Vector2(
                                       (float)Math.Sin(rotation),
                                       (float)Math.Cos(rotation)) * Game1.RandomNumber.Next(25, 40) / 100f;

            UpdateAnimation();
        }

        private void UpdateWalking()
        {
            // jump while walking
            if (_body.IsGrounded)
                _body.Velocity.Z = 0.65f;
        }

        private void UpdateFleeState()
        {
            SpawnBirds();

            // start fleeing from the player
            var distance = EntityPosition.Position - MapManager.ObjLink.Position;
            if (distance.Length() < 32)
                _aiComponent.ChangeState("fleeing");
        }

        private void UpdateFleeing()
        {
            SpawnBirds();

            // flee from the player
            var playerDir = EntityPosition.Position - MapManager.ObjLink.Position;

            // stop fleeing
            if (playerDir.Length() > 48)
            {
                _aiComponent.ChangeState("fleeingIdle");
                return;
            }

            if (playerDir != Vector2.Zero)
                playerDir.Normalize();
            _body.VelocityTarget = playerDir;

            UpdateAnimation();
        }

        private void UpdateAnimation()
        {
            _direction = _body.VelocityTarget.X < 0 ? 0 : 1;
            _animator.Play("walk_" + _direction);
        }

        private void StartGrabbing()
        {
            if (_isThrown)
                MapManager.ObjLink.CurrentState = ObjLink.State.Idle;
        }

        private Vector3 CarryInit()
        {
            _body.IgnoresZ = true;
            _body.Velocity = Vector3.Zero;
            _body.VelocityTarget = Vector2.Zero;
            _animator.SpeedMultiplier = 2.0f;
            _aiComponent.ChangeState("pickedUp");

            return new Vector3(EntityPosition.X, EntityPosition.Y, EntityPosition.Z);
        }

        private bool CarryUpdate(Vector3 position)
        {
            EntityPosition.Set(new Vector3(position.X, position.Y, position.Z));
            return true;
        }

        private void CarryThrow(Vector2 velocity)
        {
            _isThrown = true;
            _carriableComponent.Thrown = true;
            _body.IgnoresZ = false;
            _body.IsGrounded = false;
            _body.DragAir = 0.94f;
            _body.Velocity = new Vector3(velocity.X, velocity.Y, 0) * 0.95f;
            _aiComponent.ChangeState("thrown");
        }

        private void UpdatePickedUp()
        {
            Game1.AudioManager.PlaySoundEffect("D370-19-13", false);
            UpdateAnimation();
        }

        private void UpdateThrown()
        {
            if (_body.IsGrounded)
            {
                _body.DragAir = 1.0f;
                _aiComponent.ChangeState("walking");
            }
            UpdateAnimation();
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // If it's a swarm spawn or animal damage is disabled don't register hits.
            if (GameSettings.NoAnimalDamage || _swarmSpawn)
                return Values.HitCollision.None;

            if (hitType == HitType.MagicPowder || hitType == HitType.MagicRod)
            {
                if (_aiComponent.CurrentStateId != "burning")
                {
                    _aiComponent.ChangeState("burning");
                    var speedMultiply = (hitType == HitType.MagicPowder ? 0.125f : 0.5f);

                    Game1.AudioManager.PlaySoundEffect("D378-18-12");

                    return Values.HitCollision.Enemy;
                }
            }
            if (_damageState.IsInDamageState())
                return Values.HitCollision.None;

            _damageState.SetDamageState();
            
            // start spawning other birds
            _hitCounter++;
            if (_hitCounter > 35)
                _attackMode = true;

            Game1.AudioManager.PlaySoundEffect("D360-03-03");
            Game1.AudioManager.PlaySoundEffect("D370-19-13");
            _aiComponent.ChangeState("fleeing");

            Game1.GameManager.StartDialogPath("bird_hit");

            _body.Velocity.X = direction.X * 3.5f;
            _body.Velocity.Y = direction.Y * 3.5f;

            return Values.HitCollision.Blocking;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Continues)
            {
                // push the bird away
                _body.Velocity = new Vector3(direction.X, direction.Y, 0) * 0.65f;

                // try to walk away from the pusher
                if (_aiComponent.CurrentStateId != "idle")
                    return true;

                _aiComponent.ChangeState("walking");

                var offsetAngle = MathHelper.ToRadians(Game1.RandomNumber.Next(45, 85) * (_direction * 2 - 1));
                var newDirection = new Vector2(
                                       direction.X * (float)Math.Cos(offsetAngle) -
                                       direction.Y * (float)Math.Sin(offsetAngle),
                                       direction.X * (float)Math.Sin(offsetAngle) +
                                       direction.Y * (float)Math.Cos(offsetAngle)) * 0.5f;
                _body.VelocityTarget = newDirection;

                UpdateAnimation();
            }
            return true;
        }

        private void OnCollision(Values.BodyCollision moveCollision)
        {
            if ((moveCollision & Values.BodyCollision.Floor) != 0)
            {
                _body.Velocity.X = _body.Velocity.X * 0.80f;
                _isThrown = false;
                _carriableComponent.Thrown = false;
                return;
            }
            // can only change the direction every so often
            if (!_changeDirectionSwitch.State)
                return;
            _changeDirectionSwitch.Reset();

            // rotate after wall collision
            if ((moveCollision & Values.BodyCollision.Horizontal) != 0)
                _body.VelocityTarget.X = -_body.VelocityTarget.X * 0.5f;
            else if ((moveCollision & Values.BodyCollision.Vertical) != 0)
                _body.VelocityTarget.Y = -_body.VelocityTarget.Y * 0.5f;

            UpdateAnimation();
        }
    }
}
