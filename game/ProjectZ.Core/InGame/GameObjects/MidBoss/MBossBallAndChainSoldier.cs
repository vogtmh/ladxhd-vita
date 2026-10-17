using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.MidBoss
{
    internal class MBossBallAndChainSoldier : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiTriggerTimer _walkTimer;
        private readonly AnimationComponent _animatorComponent;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly MBossBallAndChain _ballAndChain;

        private readonly Rectangle _fieldRectangle;

        private const string _leafSaveKey = "ow_goldLeafBalls";
        private const float _ballDistance = 10;
        private const float _ballDistanceThrow = 56;

        private float _currentBallSpeed = 300;
        private int _lives = EnemyLives.BaCSoldier;

        private string _strKey;
        private float _ballState;
        private float _throwDirection;
        private int _ballCounter;
        private bool _startThrowing;
        private bool _isThrowing;
        private float _ballRadiant;
        private float _distance;
        private bool _wasBlocked;

        public MBossBallAndChainSoldier() : base("ballAndChain") { }

        public MBossBallAndChainSoldier(Map.Map map, int posX, int posY, string strKey) : base(map)
        {
            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);

            _strKey = strKey;

            _fieldRectangle = map.GetField(posX, posY, 16);
            _fieldRectangle.X += 16;

            // was already defeated?
            if (!string.IsNullOrEmpty(_strKey) && Game1.GameManager.SaveManager.GetString(_strKey) == "1")
            {
                IsDead = true;

                // spawn the leaf if is was not already collected
                var objLeaf = new ObjItem(Map, posX, posY, null, _leafSaveKey, "goldLeaf", null);
                if (!objLeaf.IsDead)
                    Map.Objects.SpawnObject(objLeaf);

                return;
            }

            _animator = AnimatorSaveLoad.LoadAnimator("MidBoss/ball and chain soldier");
            _animator.Play("swing1");

            _sprite = new CSprite(EntityPosition);
            _animatorComponent = new AnimationComponent(_animator, _sprite, new Vector2(-8, -16));

            _body = new BodyComponent(EntityPosition, -7, -12, 14, 12, 8);

            var stateWalk = new AiState(UpdateWalk);
            stateWalk.Trigger.Add(_walkTimer = new AiTriggerTimer(500));
            var stateSwing = new AiState(UpdateSwing);
            var stateThrow = new AiState(UpdateThrow);

            _aiComponent = new AiComponent();
            _aiComponent.Trigger.Add(new AiTriggerUpdate(UpdateBall));
            new AiFallState(_aiComponent, _body, null, KillBoss, 500);

            _aiComponent.States.Add("walk", stateWalk);
            _aiComponent.States.Add("swing", stateSwing);
            _aiComponent.States.Add("throw", stateThrow);
            _aiComponent.ChangeState("walk");

            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, 0, false) { OnDeath = OnDeath };

            var damageCollider = new CBox(EntityPosition, -8, -16, 0, 16, 16, 8);
            AddComponent(AnimationComponent.Index, _animatorComponent);
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageCollider, HitType.Enemy, 4));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(_body.BodyBox, OnHit) { ArrowMultiplier = true, BombMultiplier = true, BoomerangMultiplier = true, MagicRodMultiplier = true });
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, _sprite));

            _ballAndChain = new MBossBallAndChain(map, this);
            Map.Objects.SpawnObject(_ballAndChain);
        }

        private void OnDeath(bool pieceOfPower)
        {
            KillBoss();
            _damageState.BaseOnDeath(pieceOfPower);
        }

        private void KillBoss()
        {
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            if (playerDirection != Vector2.Zero)
                playerDirection.Normalize();
            playerDirection *= 1.75f;

            // spawn the golden leaf jumping towards the player
            var objLeaf = new ObjItem(Map, 0, 0, null, _leafSaveKey, "goldLeaf", null);
            if (!objLeaf.IsDead)
            {
                objLeaf.EntityPosition.Set(new Vector3(EntityPosition.X, EntityPosition.Y, EntityPosition.Z));
                objLeaf.SetVelocity(new Vector3(playerDirection.X, playerDirection.Y, 1.0f));
                objLeaf.Collectable = false;
                Map.Objects.SpawnObject(objLeaf);
            }

            // save
            if (!string.IsNullOrEmpty(_strKey))
                Game1.GameManager.SaveManager.SetString(_strKey, "1");

            Map.Objects.DeleteObjects.Add(_ballAndChain);
        }

        private void ToWalk()
        {
            _aiComponent.ChangeState("walk");
            _startThrowing = false;
            _isThrowing = false;
            _ballAndChain.Deactivate();
        }

        private void UpdateWalk()
        {
            if (!_walkTimer.State)
                return;

            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            if (playerDirection.Length() < 48)
            {
                ToSwing();
                return;
            }

            // do not walk towards the player if he is not in the field
            if (!_fieldRectangle.Contains(MapManager.ObjLink.CenterPosition))
                return;

            // walk towards the player
            playerDirection.Normalize();
            _body.VelocityTarget = playerDirection / 4;
        }

        private void ToSwing()
        {
            _aiComponent.ChangeState("swing");
            _body.VelocityTarget = Vector2.Zero;
            _ballCounter = 0;
        }

        private void UpdateSwing()
        {

        }

        private void ToThrow()
        {
            _aiComponent.ChangeState("throw");
            _isThrowing = true;
            _wasBlocked = false;
            _ballAndChain.Activate();
        }

        private void UpdateThrow()
        {

        }

        private void UpdateBall()
        {
            Vector2 ballOffset;

            if (_isThrowing)
            {
                if (_wasBlocked)
                    _ballState += (Game1.DeltaTime / 500.0f) * MathF.PI;
                else
                    _ballState += (Game1.DeltaTime / 1000.0f) * MathF.PI;

                // finished animation?
                if (_ballState > _throwDirection + MathF.PI)
                {
                    ToWalk();
                }
            }
            else
            {
                var isSwinging = _aiComponent.CurrentStateId == "swing";

                var target = isSwinging ? 300.0f : 500.0f;
                _currentBallSpeed = AnimationHelper.MoveToTarget(_currentBallSpeed, target, target * 0.1f * Game1.TimeMultiplier);

                // 2 times per second or 4 if he is swinging fast
                _ballState += Game1.DeltaTime / _currentBallSpeed * MathF.PI * 2;

                if (_startThrowing && _ballState >= _throwDirection)
                {
                    _currentBallSpeed = 500.0f;
                    ToThrow();
                    _ballState = _throwDirection + ((_ballState - _throwDirection) / 1000.0f) * MathF.PI;
                }
                else if (_ballState >= MathF.PI * 2)
                {
                    _ballState -= MathF.PI * 2;

                    if (isSwinging)
                    {
                        _ballCounter++;
                        if (_ballCounter >= 2)
                        {
                            _startThrowing = true;
                            var playerDirection = MapManager.ObjLink.CenterPosition -
                                                  new Vector2(EntityPosition.X - 5, EntityPosition.Y - 15);
                            var playerAngle = MathF.Atan2(playerDirection.Y, playerDirection.X) + MathF.PI * 5 / 2;
                            if (playerAngle >= MathF.PI * 2)
                                playerAngle -= MathF.PI * 2;
                            _throwDirection = playerAngle;
                        }
                    }
                }
            }

            _animator.Play("swing" + (_ballState < MathF.PI ? "0" : "1"));

            // calculate the ball offset
            if (!_isThrowing)
            {
                ballOffset = new Vector2(-MathF.Cos(_ballState), -MathF.Sin(_ballState)) * _ballDistance;
            }
            else
            {
                if (_wasBlocked)
                {
                    if (_ballDistance < _distance - 2.5f * Game1.TimeMultiplier)
                        _distance -= 2.5f * Game1.TimeMultiplier;
                    else
                        _distance = _ballDistance;

                    ballOffset = new Vector2(-MathF.Cos(_ballState), -MathF.Sin(_ballState)) * _distance;
                }
                else
                {
                    var throwState = MathF.Sin(_ballState - _throwDirection);
                    _distance = MathHelper.Lerp(_ballDistance, _ballDistanceThrow, throwState);

                    var tan = MathF.Tan((((_ballState - _throwDirection) / MathF.PI) * 2 - 1) * MathF.Atan(10)) / 10;
                    _ballRadiant = _throwDirection + (tan * 0.5f + 0.5f) * MathF.PI;

                    ballOffset = new Vector2(-MathF.Cos(_ballRadiant), -MathF.Sin(_ballRadiant)) * _distance;
                }
            }
            _ballAndChain.EntityPosition.Set(new Vector2(EntityPosition.X - 5, EntityPosition.Y - 7) + ballOffset);
        }

        public void BlockBall()
        {
            _wasBlocked = true;
            _ballState = _ballRadiant;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Register the hit.
            var hit = _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            // When a hit removes all lives disable components.
            if (_damageState.CurrentLives <= 0)
            {
                _body.IsActive = false;
                _damageField.IsActive = false;
                _hitComponent.IsActive = false;
            }
            // Return the hit.
            return hit;
        }
    }
}
