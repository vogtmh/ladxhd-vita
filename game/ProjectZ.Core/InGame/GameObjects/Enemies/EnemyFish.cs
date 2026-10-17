using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyFish : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AnimationComponent _animationComponent;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private float _speed = 0.5f;
        private int _direction;
        private int _lives = EnemyLives.Fish;
        private int _dropIndex = 2;

        // blinking
        public EnemyFish() : base("fish") { }

        public EnemyFish(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 11, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 11, 0);
            EntitySize = new Rectangle(-8, -11 - 16, 16, 32);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/fish");
            _animator.Play("swim");

            _sprite = new CSprite(EntityPosition);
            _animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(0, 4));

            _body = new BodyComponent(EntityPosition, -5, -7, 10, 8, 8)
            {
                FieldRectangle = map.GetField(posX, posY),
                FieldLocked = false,
                MoveCollision = OnCollision,
                Gravity = -0.075f,
                DragAir = 1.0f,
                IgnoreHeight = true,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.NonWater |
                                 Values.CollisionTypes.NPCWall
            };

            // start swimming randomly left or right
            _direction = Game1.RandomNumber.Next(0, 2) * 2 - 1;
            _body.VelocityTarget.X = _direction * _speed;

            // states
            var stateSwim = new AiState(UpdateSwim) { Init = StartSwimming };
            stateSwim.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("jump"), 1500, 3000));
            var stateJump = new AiState(UpdateJump) { Init = StartJump };

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("swim", stateSwim);
            _aiComponent.States.Add("jump", stateJump);
            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex) { HitMultiplierX = 0, HitMultiplierY = 0, FlameOffset = new Point(0, 2), IsActive = false, OnBurn = OnBurn };

            _aiComponent.ChangeState("swim");

            var damageBox   = new CBox(EntityPosition, -3,  -8, 0,  6,  6, 4, true);
            var hittableBox = new CBox(EntityPosition, -8, -11, 0, 16, 14, 8, true);
            var pushableBox = new CBox(EntityPosition, -7, -11, 0, 14, 14, 8, true);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, _damageState.OnHit) { BoomerangMultiplier = true });
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(pushableBox, OnPush));
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, _animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer) { DeepWaterOutline = true, WaterOutlineOffsetY = 1 });
        }

        public override void Reset()
        {
            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _aiComponent.ChangeState("swim");
            _aiComponent.ChangeState("swim");
            _body.IgnoresZ = false;
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
            _body.IgnoresZ = true;
        }

        private void StartSwimming()
        {
            _damageState.IsActive = false;
            _animator.Play("swim");
            _body.VelocityTarget.X = _direction * _speed;
        }

        private void UpdateSwim()
        {
            _animationComponent.MirroredH = _direction > 0;
        }

        private void Splash()
        {
            Game1.AudioManager.PlaySoundEffect("D360-14-0E", false, EntityPosition.Position);

            // spawn splash effect
            var fallAnimation = new ObjAnimator(_body.Owner.Map,
                (int)(_body.Position.X + _body.OffsetX + _body.Width / 2.0f),
                (int)(_body.Position.Y + _body.OffsetY + 9),
                Values.LayerPlayer, "Particles/fishingSplash", "idle", true);
            _body.Owner.Map.Objects.SpawnObject(fallAnimation);
        }

        private void StartJump()
        {
            _body.VelocityTarget = Vector2.Zero;
            _body.Velocity = new Vector3(_direction * 0.85f, 0, 1.5f);
            _body.DragAir = 1.0f;

            Splash();
        }

        private void UpdateJump()
        {
            _animator.Play(_body.Velocity.Z > 0 ? "jump_up" : "jump_down");
            _damageState.IsActive = true;
            _sprite.SpriteEffect = _direction > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType pushType)
        {
            if (pushType == PushableComponent.PushType.Impact && _aiComponent.CurrentStateId == "jump")
            {
                _body.Velocity.X += direction.X;
                _body.Velocity.Y += direction.Y;
                _body.DragAir = 0.95f;
            }
            return true;
        }

        public void MakeVulerable()
        {
            _damageState.IsActive = true;
        }

        private void OnCollision(Values.BodyCollision direction)
        {
            if (_aiComponent.CurrentStateId == "damage" || _damageState.IsInDamageState())
                return;

            _body.Velocity.X = 0;
            _body.Velocity.Y = 0;

            // change direction
            if (_aiComponent.CurrentStateId == "swim")
            {
                _direction = -_direction;
                _body.VelocityTarget.X = _direction * _speed;
            }
            else if ((direction & Values.BodyCollision.Floor) != 0)
            {
                Splash();
                _aiComponent.ChangeState("swim");
            }
        }
    }
}
