using System;
using System.Collections.Generic;
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
    internal class EnemyGhini : GameObject, IHasVisibility
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private readonly Rectangle _triggerField;
        private readonly Vector2 _centerPosition;

        private Vector2 _velocity;

        private string _spawnKey;
        private double _direction;
        private float _flyHeight = 14;
        private float _rotationDirection;
        private float _dirChangeCount;
        private float _transparency;
        private bool _mainGhini;
        private bool _spawnAnimation;
        private int _lives = EnemyLives.Ghini;
        private int _dropIndex = 9;

        // Used by ObjMoveStone when simultaneously spawning a Ghini and pushing the gravestone.
        private bool _isBeingPushed;
        public bool IsSpawning => _aiComponent.CurrentStateId == "spawning";

        public bool IsVisible { get; private set; }
        public AiComponent AIComponent => _aiComponent;
        public AiDamageState AIDamageState => _damageState;

        public EnemyGhini() : base("ghini") { }

        public EnemyGhini(Map.Map map, int posX, int posY, bool mainGhini, bool spawnAnimation, string spawnKey) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;
            BowWowImmune = true;

            EntityPosition = new CPosition(posX + 8, posY + 16, spawnAnimation ? 0 : _flyHeight);
            ResetPosition  = new CPosition(posX + 8, posY + 16, spawnAnimation ? 0 : _flyHeight);
            EntitySize = new Rectangle(-8, -32, 16, 32);
            CanReset = true;
            OnReset = Reset;

            _spawnKey = spawnKey;
            _mainGhini = mainGhini;
            _spawnAnimation = spawnAnimation;
            IsVisible = mainGhini;

            _triggerField = map.GetField(posX, posY);
            _centerPosition = new Vector2(_triggerField.Center.X, _triggerField.Center.Y + 16);

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/ghini");
            _animator.Play("fly_1");

            _sprite = new CSprite(EntityPosition) { Color = spawnAnimation ? Color.Transparent : Color.White };
            var animationComponent = new AnimationComponent(_animator, _sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -6, -12, 12, 12, 8)
            {
                FieldRectangle = map.GetField(posX, posY),
                FieldLocked = false,
                CollisionTypes = Values.CollisionTypes.Field,
                AvoidTypes     = Values.CollisionTypes.NPCWall,
                IgnoreHoles = true,
                IgnoresZ = true,
            };

            var stateInit = new AiState();
            stateInit.Trigger.Add(new AiTriggerCountdown(64, null, () => _aiComponent.ChangeState("spawning")));
            var stateSpawning = new AiState(UpdateSpawning);
            var stateFlying = new AiState(UpdateFlying);

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("init", stateInit);
            _aiComponent.States.Add("spawning", stateSpawning);
            _aiComponent.States.Add("flying", stateFlying);
            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex, true, false) { IsActive = !spawnAnimation };
            _damageState.OnDeath = OnDeath;
            _aiComponent.ChangeState(spawnAnimation ? "init" : "flying");

            var damageBox = new CBox(EntityPosition, -3, -10, 0, 6, 6, 4, true);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 4) { IsActive = !spawnAnimation });
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(damageBox, OnHit) { ArrowMultiplier = true, BombMultiplier = true, BoomerangMultiplier = true, MagicRodMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new ShadowBodyDrawComponent(EntityPosition));
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(damageBox, OnPush));

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
        }

        public override void Reset()
        {
            _damageState.CurrentLives = EnemyLives.Ghini;

            if (_mainGhini)
                _aiComponent.ChangeState("flying");

            else if (_spawnAnimation && !string.IsNullOrEmpty(_spawnKey))
            {
                Game1.GameManager.SaveManager.SetString(_spawnKey, "0");
                _aiComponent.ChangeState("init");
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
            // Fade the Ghini into existence.
            _transparency = AnimationHelper.MoveToTarget(_transparency, 1, Game1.TimeMultiplier * 0.15f);
            _sprite.Color = Color.White * _transparency;

            // Do not update it's spawn if it's being pushed by a gravestone.
            if (_isBeingPushed)
                return;

            // Slowly increase it's height from the ground.
            EntityPosition.Z += Game1.TimeMultiplier * 0.25f;

            // When the height reaches a certain threshold start flying around.
            if (EntityPosition.Z >= _flyHeight)
            {
                EntityPosition.Z = _flyHeight;
                _aiComponent.ChangeState("flying");
                _damageState.IsActive = true;
                _damageField.IsActive = true;
            }
            // Track once it's at least halfway visible.
            if (_transparency > 0.5f)
                IsVisible = true;
        }

        private void UpdateFlying()
        {
            _dirChangeCount -= Game1.DeltaTime;

            // change the direction
            if (_dirChangeCount <= 0)
            {
                // the farther away the enemy is from the origin the more likely it becomes that he will move towards the center position
                var directionToStart = _centerPosition - EntityPosition.Position;
                var radiusToCenter = Math.Atan2(directionToStart.Y, directionToStart.X);

                var maxDistanceX = 85.0f;
                var maxDistanceY = 55.0f;
                var distanceMultiplier = Math.Clamp(
                    Math.Min(
                        (maxDistanceX - Math.Abs(directionToStart.X)) / maxDistanceX,
                        (maxDistanceY - Math.Abs(directionToStart.Y)) / maxDistanceY), 0, 1);

                _direction = radiusToCenter + (Math.PI - Game1.RandomNumber.Next(0, 628) / 100f) * distanceMultiplier;

                // new direction + new rotation speed
                _dirChangeCount = Game1.RandomNumber.Next(750, 1500) * (distanceMultiplier * 0.5f + 0.5f);
                _rotationDirection = Game1.RandomNumber.Next(-100, 100) / 1000f * distanceMultiplier;
            }

            _velocity *= (float)Math.Pow(0.95f, Game1.TimeMultiplier);
            _velocity += new Vector2((float)Math.Cos(_direction), (float)Math.Sin(_direction)) * 0.035f * Game1.TimeMultiplier;
            _direction += _rotationDirection * Game1.TimeMultiplier;

            // clamp the speed
            if (_velocity.Length() > 1.75f)
            {
                _velocity.Normalize();
                _velocity *= 1.75f;
            }
            _body.VelocityTarget = _velocity;
            _animator.Play("fly_" + (_body.VelocityTarget.X < 0 ? -1 : 1));
        }

        private void OnDeath(bool pieceOfPower)
        {
            // If this is the main Ghini kill the others that were awakened.
            if (_mainGhini)
                KillOtherGhinies();

            _damageState.BaseOnDeath(pieceOfPower);
        }

        private void KillOtherGhinies()
        {
            // Find the other Ghinis on the screen.
            var findGhiniList = new List<GameObject>();
            Map.Objects.GetGameObjectsWithTag(findGhiniList, Values.GameObjectTag.Enemy, _triggerField.X, _triggerField.Y, _triggerField.Width, _triggerField.Height);

            // We want only the active Ghinis that are not this one.
            var realGhiniList = new List<GameObject>();
            foreach (var ghini in findGhiniList)
            {
                if (ghini != this && ghini.IsActive)
                    realGhiniList.Add(ghini);
            }
            // If there is no Ghinis to affect we're done.
            if (realGhiniList.Count == 0)
                return;

            // Also change this Ghini's loot table and kill it.
            _damageState.DropTableIndex = 15;

            // Loop through the Ghini list.
            foreach (var ghini in realGhiniList)
            {
                // Make sure it's not this Ghini and it's been awakened.
                if (ghini != this && ghini.IsActive)
                {
                    // Change the loot table to random items and kill them.
                    if (ghini is EnemyGhini ghiniSmall)
                    {
                        ghiniSmall.AIDamageState.DropTableIndex = 15;
                        ghiniSmall.AIComponent.ChangeState("damageDeath");
                    }
                    else if (ghini is EnemyGhiniGiant ghiniGiant)
                    {
                        ghiniGiant.AIDamageState.DropTableIndex = 15;
                        ghiniGiant.AIComponent.ChangeState("damageDeath");
                    }
                }
            }
        }

        public void PushedByGrave(bool isPushed)
        {
            // Prevents the spawn loop from updating.
            _isBeingPushed = isPushed;

            // Pause the Ghini while it's being pushed.
            if (isPushed)
            {
                _velocity = Vector2.Zero;
                _body.VelocityTarget = Vector2.Zero;
            }
        }

        public void MoveWithGrave(Vector2 offset)
        {
            // Move the Ghini with the gravestone.
            EntityPosition.Set(EntityPosition.Position + offset);
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

            // If we got here, it was probably a hit from the back or another weapon than sword.
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
