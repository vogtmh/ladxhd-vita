using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Base.Systems;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.NPCs
{
    internal class ObjDog : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiTriggerSwitch _changeDirectionSwitch;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly InteractComponent _interactComponent;
        private readonly PushableComponent _pushComponent;

        private Vector2 _marinPosition;

        private int _direction;
        private int _dropIndex = 2;

        public ObjDog() : base("dog") { }

        public ObjDog(Map.Map map, int posX, int posY) : base(map)
        {
            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);

            CanReset = true;
            OnReset = Reset;

            _body = new BodyComponent(EntityPosition, -6, -8, 12, 8, 8)
            {
                MoveCollision = OnCollision,
                Gravity = -0.15f,
                Drag = 0.75f,
                DragAir = 0.95f,
                FieldRectangle = map.GetField(posX, posY),
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.NPCWall |
                                 Values.CollisionTypes.Player,
                AvoidTypes =     Values.CollisionTypes.Hole |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.NPCWall |
                                 Values.CollisionTypes.Player,
            };

            _animator = AnimatorSaveLoad.LoadAnimator("NPCs/dog");
            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, Vector2.Zero);

            var stateIdle = new AiState() { Init = InitIdle };
            stateIdle.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("walking"), 500, 1500));
            var stateWalking = new AiState(UpdateWalking) { Init = InitWalking };
            stateWalking.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("idle"), 750, 1500));
            stateWalking.Trigger.Add(_changeDirectionSwitch = new AiTriggerSwitch(250));
            var stateListening = new AiState(UpdateListening);
            var statePreAttack = new AiState();
            statePreAttack.Trigger.Add(new AiTriggerCountdown(500, null, () => _aiComponent.ChangeState("attack")));
            var stateAttack = new AiState(UpdateAttack) { Init = InitAttack };

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("walking", stateWalking);
            _aiComponent.States.Add("listening", stateListening);
            _aiComponent.States.Add("preAttack", statePreAttack);
            _aiComponent.States.Add("attack", stateAttack);
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, 2, _dropIndex) { OnBurn = OnBurn, OnDeath = TrackKills };
            _aiComponent.ChangeState(Game1.RandomNumber.Next(0, 10) < 5 ? "idle" : "walking");

            var box = new CBox(EntityPosition, -7, -14, 14, 14, 8);

            AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(OnKeyChange));
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(box, HitType.Enemy, 2) { IsActive = false });
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(box, OnHit));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush) { RepelMultiplier = 2.25f });
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, sprite) { ShadowWidth = 10 });
            AddComponent(InteractComponent.Index, _interactComponent = new InteractComponent(_body.BodyBox, Interact));

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
            _damageField.IsActive = false;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _interactComponent.IsActive = true;
            _aiComponent.ChangeState("idle");
        }

        private void OnKeyChange()
        {
            if (_aiComponent.CurrentStateId == "burning")
                return;

            var marinPosition = Game1.GameManager.SaveManager.GetString("marin_sing_position");
            if (!string.IsNullOrEmpty(marinPosition))
            {
                var splitString = marinPosition.Split(',');
                if (splitString.Length == 2)
                {
                    int.TryParse(splitString[0], out int posX);
                    int.TryParse(splitString[1], out int posY);

                    _marinPosition = new Vector2(posX, posY);
                }
            }
            else
                _marinPosition = Vector2.Zero;
            
        }

        private void InitIdle()
        {
            // stop and wait
            _body.VelocityTarget.X = 0;
            _body.VelocityTarget.Y = 0;
            _animator.Play("idle_" + _direction);
        }

        private void InitWalking()
        {
            // change the direction
            var rotation = Game1.RandomNumber.Next(0, 628) / 100f;
            var speed = Game1.RandomNumber.Next(40, 55) / 100f;

            _body.VelocityTarget = new Vector2((float)Math.Sin(rotation), (float)Math.Cos(rotation)) * speed;

            UpdateAnimation();
        }

        private void UpdateWalking()
        {
            // jump up and down while walking
            if (_body.IsGrounded)
            {
                _body.Velocity.Z = 0.85f;

                if (_marinPosition != Vector2.Zero)
                {
                    var direction = _marinPosition - EntityPosition.Position;
                    var distance = direction.Length();

                    if (distance < 24)
                    {
                        _body.Velocity.Z = 0;
                        _body.VelocityTarget = Vector2.Zero;

                        _direction = direction.X < 0 ? 0 : 1;
                        _animator.Play("idle_" + _direction);

                        _aiComponent.ChangeState("listening");
                    }
                    else if (distance < 64)
                    {
                        if (direction != Vector2.Zero)
                            direction.Normalize();

                        _body.VelocityTarget = direction * 0.5f;

                        UpdateAnimation();
                    }
                }
            }
        }

        private void OnBurn()
        {
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
            _interactComponent.IsActive = false;
            _animator.Pause();
        }

        private void UpdateListening()
        {
            if (_marinPosition == Vector2.Zero)
                _aiComponent.ChangeState("walking");
        }

        private void InitAttack()
        {
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            if (playerDirection != Vector2.Zero)
                playerDirection.Normalize();

            _body.VelocityTarget = playerDirection * 3;
            _body.IsGrounded = false;
            _body.Velocity.Z = 1.45f;
            _damageField.IsActive = true;

            UpdateAnimation();
        }

        private void UpdateAttack()
        {
            // finished attacking?
            if (_body.IsGrounded)
            {
                _damageField.IsActive = false;
                _aiComponent.ChangeState("idle");
            }
        }

        private void UpdateAnimation()
        {
            _direction = _body.VelocityTarget.X < 0 ? 0 : 1;
            _animator.Play("idle_" + _direction);
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (_aiComponent.CurrentStateId == "preAttack" ||
                _aiComponent.CurrentStateId == "attack")
                return false;

            if (type == PushableComponent.PushType.Continues)
            {
                // push the dog away
                SystemBody.MoveBody(_body, new Vector2(direction.X, direction.Y) * 0.33f * Game1.TimeMultiplier, _body.CollisionTypes, false, false, false);
                _body.Position.NotifyListeners();

                if (_aiComponent.CurrentStateId == "walking")
                    return true;

                // start moving away from the pusher
                _aiComponent.ChangeState("walking");

                var offsetAngle = MathHelper.ToRadians(Game1.RandomNumber.Next(55, 85) * (Game1.RandomNumber.Next(0, 2) * 2 - 1));
                var newDirection = new Vector2(
                                       direction.X * (float)Math.Cos(offsetAngle) - direction.Y * (float)Math.Sin(offsetAngle),
                                       direction.X * (float)Math.Sin(offsetAngle) + direction.Y * (float)Math.Cos(offsetAngle)) * 0.5f;
                _body.VelocityTarget = newDirection;
                UpdateAnimation();
            }
            return true;
        }

        private void OnCollision(Values.BodyCollision moveCollision)
        {
            if (_aiComponent.CurrentStateId == "attack")
                return;

            // rotate after wall collision
            // horizontal collision
            if ((moveCollision & Values.BodyCollision.Horizontal) != 0)
            {
                if (!_changeDirectionSwitch.State)
                    return;
                _changeDirectionSwitch.Reset();

                _body.VelocityTarget.X = -_body.VelocityTarget.X;

                UpdateAnimation();
            }
            // vertical collision
            else if ((moveCollision & Values.BodyCollision.Vertical) != 0)
                _body.VelocityTarget.Y = -_body.VelocityTarget.Y;
            
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (GameSettings.NoAnimalDamage)
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
            if (_aiComponent.CurrentStateId == "idle" ||
                _aiComponent.CurrentStateId == "walking")
            {
                Game1.AudioManager.PlaySoundEffect("D360-03-03");

                _aiComponent.ChangeState("preAttack");
                _damageState.SetDamageState();
                _body.Velocity = new Vector3(direction * 1.5f, 0);

                return Values.HitCollision.Enemy;
            }
            return Values.HitCollision.None;
        }

        private bool Interact()
        {
            if (_aiComponent.CurrentStateId == "listening")
                Game1.GameManager.StartDialogPath("animals_absorbed");
            else
                Game1.GameManager.StartDialogPath("dog");

            return true;
        }
    }
}
