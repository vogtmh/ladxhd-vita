using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyRiverZora : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;

        private readonly Rectangle _fieldPosition;

        private Vector2 _initialPosition;
        private Vector2 _alternativePosition;
        private float _floatCount;
        private bool _stationary;
        private bool _playSplash;
        private int _lives = EnemyLives.RiverZora;
        private int _dropIndex = 2;

        public EnemyRiverZora() : base("river zora") { }

        public EnemyRiverZora(Map.Map map, int posX, int posY, bool stationary = false, int altPosX = 0, int altPosY = 0) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY - 2 + 8, 0);
            ResetPosition  = new CPosition(posX + 8, posY - 2 + 8, 0);
            EntitySize = new Rectangle(-8, -8, 16, 16);
            CanReset = true;
            OnReset = Reset;

            // If it's a stationary or dual-position Zora, store it's initial position.
            _initialPosition = new Vector2(EntityPosition.X, EntityPosition.Y);

            // Some Zoras spawn in two positions. Store the second position only if it's been defined.
            _alternativePosition = (altPosX != 0 && altPosY != 0) 
                ? new Vector2(altPosX + 8, altPosY - 2 + 8) 
                : Vector2.Zero;

            _fieldPosition = map.GetField(posX + 8, posY - 2 + 8);
            _stationary = stationary;
            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/river zora");
            _animator.Play("idle");

            _playSplash = false;

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-8, -8));

            _body = new BodyComponent(EntityPosition, -6, -5, 12, 10, 8) 
            { 
                FieldRectangle = map.GetField(posX, posY),
                DragWater = 0.9f 
            };
            var stateWaiting = new AiState();
            stateWaiting.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("positioning"), 3500, 4500));
            var statePositioning = new AiState(UpdatePositioning);
            var stateSpawning = new AiState() { Init = InitSpawning };
            stateSpawning.Trigger.Add(new AiTriggerCountdown(2000, null, ToIdle));
            var stateIdle = new AiState(UpdateIdle);
            stateIdle.Trigger.Add(new AiTriggerRandomTime(ToAttacking, 500, 1000));
            var stateAttacking = new AiState(UpdateAttacking);
            stateAttacking.Trigger.Add(new AiTriggerCountdown(600, null, ToDespawning));
            var stateDespawning = new AiState(UpdateDespawning);
            stateDespawning.Trigger.Add(new AiTriggerCountdown(500, null, ToWait));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("waiting", stateWaiting);
            _aiComponent.States.Add("positioning", statePositioning);
            _aiComponent.States.Add("spawning", stateSpawning);
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("attacking", stateAttacking);
            _aiComponent.States.Add("despawning", stateDespawning);
            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex) { HitMultiplierX = 1.5f, HitMultiplierY = 1.5f, FlameOffset = new Point(0, 2) };

            ToWait();

            AddComponent(BodyComponent.Index, _body);
            AddComponent(HittableComponent.Index, new HittableComponent(_body.BodyBox, _damageState.OnHit) { BoomerangMultiplier = true });
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer) { WaterOutline = false });
        }

        public override void Reset()
        {
            _playSplash = false;
            ToWait();
            _playSplash = true;
        }

        private void ToWait()
        {
            _aiComponent.ChangeState("waiting");

            _floatCount = 0;
            _sprite.DrawOffset.Y = -8;

            _sprite.IsVisible = false;
            _body.IsGrounded = false;
            _damageState.IsActive = false;

            // splash effect
            if (_playSplash)
            {
                var splashAnimator = new ObjAnimator(Map, 0, 0, 0, 3, Values.LayerPlayer, "Particles/splash", "idle", true);
                splashAnimator.EntityPosition.Set(new Vector2(
                    _body.Position.X + _body.OffsetX + _body.Width / 2f,
                    _body.Position.Y + _body.OffsetY + _body.Height - _body.Position.Z - 3));
                Map.Objects.SpawnObject(splashAnimator);
            }
        }

        private void UpdatePositioning()
        {
            // If the Zora is not supposed to move.
            if (_stationary)
            {
                // Start with it's initial position.
                Vector2 newPosition = _initialPosition;

                // If it's a dual position Zora.
                if (_alternativePosition != Vector2.Zero)
                {
                    // The chance to spawn at either location is 50/50.
                    newPosition = (Game1.RandomNumber.Next(0, 2) == 0)
                        ? _initialPosition
                        : _alternativePosition;
                }
                // Spawn the Zora at the new location.
                EntityPosition.Set(newPosition);
                _aiComponent.ChangeState("spawning");
                return;
            }
            // Try to find a new position.
            for (var i = 0; i < 25; i++)
            {
                // Search a random position in the current field.
                var newPosition = new Vector2(
                    _fieldPosition.X + Game1.RandomNumber.Next(0, 10) * 16 + 8,
                    _fieldPosition.Y + Game1.RandomNumber.Next(0, 8) * 16 + 8 - 2);

                // If it's a deep water tile spawn the Zora there.
                var fieldState = Map.GetFieldState(newPosition);
                if ((fieldState & MapStates.FieldStates.DeepWater) != 0)
                {
                    EntityPosition.Set(newPosition);
                    _aiComponent.ChangeState("spawning");
                    return;
                }
            }
        }

        private void InitSpawning()
        {
            _playSplash = true;
            _sprite.IsVisible = true;
            _animator.Play("spawn");
        }

        private void ToIdle()
        {
            _aiComponent.ChangeState("idle");

            _animator.Play("idle");
            _damageState.IsActive = true;
        }

        private void UpdateIdle()
        {
            UpdateOffset();
        }

        private void ToAttacking()
        {
            // Zoras have unlimited range in Classic Camera. 
            if (!Camera.ClassicMode)
            {
                // If Modern Camera is active, limit the shot to 144 pixels of distance.
                var distance = EntityPosition.Position - MapManager.ObjLink.Position;
                if (distance.Length() > 144)
                {
                    ToDespawning();
                    return;
                }
            }
            _aiComponent.ChangeState("attacking");
            _animator.Play("attack");

            // Spawn a blinking fireball.
            Map.Objects.SpawnObject(new EnemyFireball(Map, (int)EntityPosition.X, (int)EntityPosition.Y, 1.5f, true));
        }

        private void UpdateAttacking()
        {
            UpdateOffset();
        }

        private void ToDespawning()
        {
            _aiComponent.ChangeState("despawning");

            _animator.Play("idle");
        }

        private void UpdateDespawning()
        {
            UpdateOffset();
        }

        private void UpdateOffset()
        {
            _floatCount += Game1.DeltaTime;
            _sprite.DrawOffset.Y = -8 - (float)Math.Sin(_floatCount / 200f);
        }
    }
}
