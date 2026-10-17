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
    internal class EnemyGhiniGiant : GameObject, IHasVisibility
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private Rectangle _fieldRectangle;
        private Vector2 _velocity;
        private Vector2 _vecDirection;

        private double _direction;
        private string _spawnKey;
        private float _rotationDirection;
        private float _dirChangeCount;
        private float _transparency;
        private bool _spawnAnimation;

        private int _flyHeight = 7;
        private int _lives = EnemyLives.GhiniGiant;
        private int _dropIndex = 9;

        public bool IsVisible { get; private set; }
        public AiComponent AIComponent => _aiComponent;
        public AiDamageState AIDamageState => _damageState;

        public EnemyGhiniGiant() : base("giant ghini") { }

        public EnemyGhiniGiant(Map.Map map, int posX, int posY, bool spawnAnimation, string spawnKey) : base(map)
        {
            IsVisible = false;
            Tags = Values.GameObjectTag.Enemy;
            BowWowImmune = true;

            EntityPosition = new CPosition(posX + 8, posY + 16 + 7, spawnAnimation ? 0 : _flyHeight);
            ResetPosition  = new CPosition(posX + 8, posY + 16 + 7, spawnAnimation ? 0 : _flyHeight);
            EntitySize = new Rectangle(-16, -(30 + _flyHeight), 32, 30 + _flyHeight);
            CanReset = true;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/ghiniGiant");
            _animator.Play("fly_1");
            _spawnKey = spawnKey;
            _spawnAnimation = spawnAnimation;

            _sprite = new CSprite(EntityPosition) { Color = spawnAnimation ? Color.Transparent : Color.White };
            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-16, -30));

            _fieldRectangle = map.GetField(posX, posY, 16);

            _body = new BodyComponent(EntityPosition, -12, -30, 24, 30, 8)
            {
                FieldRectangle = map.GetField(posX, posY),
                FieldLocked = false,
                CollisionTypes = Values.CollisionTypes.Field,
                AvoidTypes     = Values.CollisionTypes.NPCWall,
                IgnoreHoles = true,
                IgnoresZ = true,
            };

            var stateSpawning = new AiState(UpdateSpawning);
            var stateFlying = new AiState(UpdateFlying);

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("spawning", stateSpawning);
            _aiComponent.States.Add("flying", stateFlying);

            _aiComponent.ChangeState(spawnAnimation ? "spawning" : "flying");

            var damageBox = new CBox(EntityPosition, -9, -22, 0, 18, 18, 8, true);
            var hittableBox = new CBox(EntityPosition, -13, -29, 0, 26, 28, 8, true);
            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex, true, false) { OnDeath = OnDeath, IsActive = !spawnAnimation };

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 4) { IsActive = !spawnAnimation });
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { ArrowMultiplier = true, BombMultiplier = true, BoomerangMultiplier = true, MagicRodMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new ShadowBodyDrawComponent(EntityPosition) { ShadowWidth = 24, ShadowHeight = 6 });
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(hittableBox, OnPush));

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowl");
        }

        public override void Reset()
        {
            _damageState.CurrentLives = EnemyLives.GhiniGiant;

            if (_spawnAnimation && !string.IsNullOrEmpty(_spawnKey))
            {
                Game1.GameManager.SaveManager.SetString(_spawnKey, "0");
                _aiComponent.ChangeState("spawning");
                IsVisible = false;
                EntityPosition.Z = 0;
                _sprite.Color = Color.Transparent;
                _damageField.IsActive = false;
                _damageState.IsActive = false;
                _transparency = 0;
                _body.VelocityTarget = Vector2.Zero;
            }
        }

        private void UpdateSpawning()
        {
            _transparency = AnimationHelper.MoveToTarget(_transparency, 1, Game1.TimeMultiplier * 0.15f);
            _sprite.Color = Color.White * _transparency;

            EntityPosition.Z += Game1.TimeMultiplier * 0.25f;

            if (EntityPosition.Z >= _flyHeight)
            {
                EntityPosition.Z = _flyHeight;
                _aiComponent.ChangeState("flying");
                _aiComponent.ChangeState("flying");
                _damageState.IsActive = true;
                _damageField.IsActive = true;
            }
            if (_transparency > 0.5f)
                IsVisible = true;
        }

        private void UpdateFlying()
        {
            _dirChangeCount -= Game1.DeltaTime;

            // change the direction
            if (_dirChangeCount <= 0)
            {
                var newDirection = Game1.RandomNumber.Next(0, 628) / 100f;
                _vecDirection = new Vector2((float)Math.Cos(newDirection), (float)Math.Sin(newDirection));
                _direction = newDirection;

                // new direction + new rotation speed
                _dirChangeCount = Game1.RandomNumber.Next(600, 1200);
                _rotationDirection = Game1.RandomNumber.Next(-100, 100) / 1000f;
            }

            _velocity *= (float)Math.Pow(0.95f, Game1.TimeMultiplier);

            _velocity += new Vector2((float)Math.Cos(_direction), (float)Math.Sin(_direction)) * 0.025f * Game1.TimeMultiplier;
            _direction += _rotationDirection * Game1.TimeMultiplier;

            _velocity += _vecDirection * 0.025f * Game1.TimeMultiplier;

            if ((EntityPosition.X < _fieldRectangle.X && _vecDirection.X < 0) ||
                (EntityPosition.X > _fieldRectangle.X + _fieldRectangle.Width && _vecDirection.X > 0))
            {
                _vecDirection.X = -Math.Sign(_vecDirection.X);
                _vecDirection.Y = 0;
                _dirChangeCount += 500;
                _direction = 1;
            }

            if ((EntityPosition.Y < _fieldRectangle.Y && _vecDirection.Y < 0) ||
                (EntityPosition.Y > _fieldRectangle.Y + _fieldRectangle.Height && _vecDirection.Y > 0))
            {
                _vecDirection.X = 0;
                _vecDirection.Y = -Math.Sign(_vecDirection.Y);
                _dirChangeCount += 500;
            }

            _body.VelocityTarget = _velocity;

            _animator.Play("fly_" + (_body.VelocityTarget.X < 0 ? -1 : 1));
        }

        private void OnDeath(bool pieceOfPower)
        {
            _damageState.BaseOnDeath(pieceOfPower);
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (hitType == HitType.MagicPowder)
                return Values.HitCollision.None;

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
