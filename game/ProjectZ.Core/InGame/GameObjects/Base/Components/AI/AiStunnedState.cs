using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Base.Components.AI
{
    class AiStunnedState
    {
        private readonly AiComponent _aiComponent;
        private readonly AnimationComponent _animationComponent;
        
        public delegate void OnStunFunction();
        public OnStunFunction OnStun;
        
        public delegate void OnStunReleaseFunction();
        public OnStunReleaseFunction OnStunRelease;

        private readonly int _shakeTime;

        private readonly AiTriggerCountdown _stunCountdown;
        private readonly AiTriggerCountdown _shakeCountdown;

        private BodyComponent _body;
        private CBox _defaultBodyBox;
        private bool _hasBodyState;
        private string _oldState;
        private float _spriteOffsetX;

        public string ReturnState;
        public float ShakeOffset = 2;
        public bool SilentStateChange = true;

        public float StunKnockbackSpeed = 4.0f;

        public bool Active = false;

        public AiStunnedState(BodyComponent bodyComponent, AiComponent aiComponent, AnimationComponent animationComponent, int stunTime, int shakeTime)
        {
            _aiComponent = aiComponent;
            _animationComponent = animationComponent;
            _aiComponent.StunnedState = this;
            _shakeTime = shakeTime;

            _body = bodyComponent;
            _spriteOffsetX = animationComponent.SpriteOffset.X;

            var stateStunned = new AiState();
            stateStunned.Trigger.Add(_stunCountdown = new AiTriggerCountdown(stunTime, null, () => _aiComponent.ChangeState("shake")));
            var stateShake = new AiState();
            stateShake.Trigger.Add(_shakeCountdown = new AiTriggerCountdown(_shakeTime, ShakeTick, ShakeEnd));

            aiComponent.States.Add("stunned", stateStunned);
            aiComponent.States.Add("shake", stateShake);
        }

        public void StartStun()
        {
            Active = true;

            // Make sure to not save the stunned state to not create an endless stunned loop.
            if (_aiComponent.CurrentStateId != "stunned" &&
                _aiComponent.CurrentStateId != "shake")
            {
                // Store the old state so it can be restored later.
                _oldState = _aiComponent.CurrentStateId;
                _spriteOffsetX = _animationComponent.SpriteOffset.X;

                // The body box and height needs to be computed after the stun ends or it
                // may be able to clip through some walls if starting on a different height.
                if (_body != null)
                {
                    _defaultBodyBox = _body.BodyBox;
                    _hasBodyState = true;
                }
            }
            // Play the stunning sound effect.
            Game1.AudioManager.PlaySoundEffect("D360-03-03");

            // Change the AiState to stunned.
            _aiComponent.ChangeState("stunned");

            // Fire the delegate when the stun happens.
            OnStun?.Invoke();
        }

        public bool IsStunned()
        {
            return _aiComponent.CurrentStateId == "stunned" || _aiComponent.CurrentStateId == "shake";
        }

        public void ResetStun()
        {
            if (!IsStunned())
                return;

            if (_aiComponent.CurrentStateId == "shake")
            {
                _shakeCountdown.Stop();
                _animationComponent.SpriteOffset.X = _spriteOffsetX;
                _animationComponent.UpdateSprite();
                _aiComponent.ChangeState("stunned", true);
            }
            _stunCountdown.Restart();
            Active = true;
        }

        public void PauseStun()
        {
            if (_aiComponent.CurrentStateId == "stunned")
                _stunCountdown.Stop();
            else if (_aiComponent.CurrentStateId == "shake")
                _shakeCountdown.Stop();
        }

        public void ResumeStun()
        {
            if (_aiComponent.CurrentStateId == "stunned")
                _stunCountdown.Start();
            else if (_aiComponent.CurrentStateId == "shake")
                _shakeCountdown.Start();
        }

        private void ShakeTick(double counter)
        {
            // 4 frames to go left/right
            _animationComponent.SpriteOffset.X = _spriteOffsetX + ShakeOffset * MathF.Sin(MathF.PI * ((_shakeTime - (float)counter) / 1000 * (60 / 4f)));
            _animationComponent.UpdateSprite();
        }

        private void ShakeEnd()
        {
            Active = false;

            _animationComponent.SpriteOffset.X = _spriteOffsetX;

            // change back to the state before the entity got stunned
            _aiComponent.ChangeState(ReturnState != null ? ReturnState : _oldState, SilentStateChange);

            // Fire the delegate when the stun ends.
            OnStunRelease?.Invoke();
        }

        public void EndStun()
        {
            Active = false;
            _stunCountdown.Stop();
            _shakeCountdown.Stop();
            _animationComponent.SpriteOffset.X = _spriteOffsetX;
            _animationComponent.UpdateSprite();
            OnStunRelease?.Invoke();
            ResetBodyState();
        }

        public void ResetBodyState()
        {
            if (_body == null || !_hasBodyState)
                return;

            // Restore the body's original box and height.
            _body.BodyBox = _defaultBodyBox;
            _body.Level = MapStates.GetLevel(_body.CurrentFieldState);
            _hasBodyState = false;
        }

        public Values.HitCollision HitStun(BodyComponent body, DamageFieldComponent damageField, Vector2 direction)
        {
            if (body != null)
            {
                body.VelocityTarget = Vector2.Zero;
                body.Velocity.X += direction.X * StunKnockbackSpeed;
                body.Velocity.Y += direction.Y * StunKnockbackSpeed;
            }
            if (damageField != null)
                damageField.IsActive = false;

            _animationComponent.Animator.Pause();
            StartStun();

            return Values.HitCollision.Enemy;
        }
    }
}
