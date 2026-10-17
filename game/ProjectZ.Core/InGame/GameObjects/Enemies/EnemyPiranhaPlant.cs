using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyPiranhaPlant : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _aiDamageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CBox _headBox;
        private readonly CPosition _headPosition;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;

        private int _lives = EnemyLives.PiranhaPlant;
        private int _dropIndex = 2;

        public EnemyPiranhaPlant() : base("piranha plant") { }

        public EnemyPiranhaPlant(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -32, 16, 32);
            CanReset = false;
            OnReset = Reset;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/piranha plant");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-8, -16));

            _body = new BodyComponent(EntityPosition, -6, -11, 12, 11, 8);

            var stateHidden = new AiState();
            stateHidden.Trigger.Add(new AiTriggerCountdown(2500, null, ToIdle));
            var stateIdle = new AiState(UpdateIdle);

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("hidden", stateHidden);
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.ChangeState("hidden");

            _headPosition = new CPosition(posX + 8, posY, 0);
            _headBox = new CBox(_headPosition, -6, 0, 12, 14, 8);

            _aiDamageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex) { MoveBody = false, OnBurn = OnBurn };

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(_headBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(_headBox, _aiDamageState.OnHit) { BoomerangMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerBottom));

            Deactivate();
        }

        public override void Reset()
        {
            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _aiComponent.ChangeState("hidden");
            _aiComponent.ChangeState("hidden");
        }

        private void OnBurn()
        {
            _animator.Pause();
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
        }

        private void ToIdle()
        {
            // check if the player is standing next to the plant -> stay hidden
            var distance = EntityPosition.Position.X - MapManager.ObjLink.Position.X;
            if (Math.Abs(distance) < 16)
            {
                _aiComponent.ChangeState("hidden");
                return;
            }

            _animator.Play("spawn");
            _aiComponent.ChangeState("idle");
            Activate();
        }

        private void UpdateIdle()
        {
            // update the head position
            _headPosition.Set(new Vector2(_headPosition.X, _sprite.Position.Y + _sprite.DrawOffset.Y));
            _aiDamageState.ExplosionOffsetY = (int)(_headPosition.Y - EntityPosition.Y) + 16;
            _aiDamageState.FlameOffset.Y = _aiDamageState.ExplosionOffsetY;

            if (!_animator.IsPlaying)
            {
                _aiComponent.ChangeState("hidden");
                Deactivate();
            }
        }

        private void Activate()
        {
            _sprite.IsVisible = true;
            _damageField.IsActive = true;
            _aiDamageState.IsActive = true;
        }

        private void Deactivate()
        {
            _sprite.IsVisible = false;
            _damageField.IsActive = false;
            _aiDamageState.IsActive = false;
        }
    }
}
