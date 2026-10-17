using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyKarakoro : GameObject, IHasSpriteVisibility
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly BoxCollisionComponent _boxCollision;
        private readonly CarriableComponent _carriableComponent;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly DrawComponent _drawComponent;
        private readonly DrawShadowComponent _shadowComponent;

        private readonly List<GameObject> _holeList = new List<GameObject>();

        private ObjHole? _hole;
        private Vector2 _holeStartPosition;
        private Vector2 _holeTargetPosition;
        private const int HoleTime = 350;
        private bool _claimedHole;
        private bool _inHole;

        private readonly int _colorIndex;
        private readonly string _strKey;
        private readonly string _strAllSetKey;
        private const float WalkSpeed = 0.25f;
        private const float RotateSpeed = 0.85f;
        private const int ShakeTime = 900;

        private float _initShakeSpriteOffsetX;
        private bool _smallBody;
        private bool _throwDamage;
        private bool _isThrown;
        private int _direction;
        private int _lives = EnemyLives.Karakoro;
        private int _dropIndex = 0;

        private Color[] _colors;

        // Values configurable via lahdmod.
        private int red_karakoro_red = 255;
        private int red_karakoro_grn = 8;
        private int red_karakoro_blu = 42;
        private int grn_karakoro_red = 17;
        private int grn_karakoro_grn = 172;
        private int grn_karakoro_blu = 66;
        private int blu_karakoro_red = 25;
        private int blu_karakoro_grn = 132;
        private int blu_karakoro_blu = 255;

        public CSprite Sprite => _sprite;

        public EnemyKarakoro() : base("karakoro") { }

        public EnemyKarakoro(Map.Map map, int posX, int posY, int colorIndex, string strKey, string strAllSetKey) : base(map)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "EnemyKarakoro.lahdmod");
            ModFile.Parse(modFile, this);

            _colors = new Color[]
            {
                new Color(grn_karakoro_red, grn_karakoro_grn, grn_karakoro_blu), 
                new Color(red_karakoro_red, red_karakoro_grn, red_karakoro_blu), 
                new Color(blu_karakoro_red, blu_karakoro_grn, blu_karakoro_blu)
            };

            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 12, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 12, 0);
            EntitySize = new Rectangle(-12, -15, 24, 16);
            CanReset = true;
            OnReset = Reset;

            _colorIndex = MathHelper.Clamp(colorIndex, 0, 2);
            _strKey = strKey;
            _strAllSetKey = strAllSetKey;

            // Always reset their key to 0 on initialization so they can respawn after
            // the puzzle has been solved. Completing it again yields no extra reward.
            if (!string.IsNullOrEmpty(_strKey))
                Game1.GameManager.SaveManager.SetString(_strKey, "0");

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/karakoro");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -7, -12, 14, 12, 8)
            {
                MoveCollision = OnMoveCollision,
                HoleAbsorb = () => OnHoleAbsorb(Vector2.Zero, 100),
                IgnoreHoles = true,
                AbsorbPercentage = 0.9f,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.NPCWall,
                AvoidTypes =     Values.CollisionTypes.Hole,
                FieldRectangle = map.GetField(posX, posY),
                Bounciness = 0.55f,
                Drag = 0.9f,
                DragAir = 1.0f
            };

            var stateWalk = new AiState { Init = InitWalk };
            stateWalk.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("idle"), 750, 1000));
            var stateRotate = new AiState { Init = InitRotate };
            stateRotate.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("idle"), 500, 750));
            var stateIdle = new AiState { Init = InitIdle };
            stateIdle.Trigger.Add(new AiTriggerRandomTime(EndIdle, 250, 500));
            var stateBall = new AiState(UpdateBall) { Init = InitBall };
            stateBall.Trigger.Add(new AiTriggerCountdown(3300, null, () => _aiComponent.ChangeState("shake")));
            var stateCarried = new AiState() { Init = InitCarried };
            var stateShake = new AiState { Init = InitShake };
            stateShake.Trigger.Add(new AiTriggerCountdown(ShakeTime, ShakeTick, ShakeEnd));
            var stateHoleJump = new AiState { Init = InitHoleJump };
            stateHoleJump.Trigger.Add(new AiTriggerCountdown(HoleTime, HoleJumpTick, HoleJumpEnd));
            var stateHole = new AiState();
            var stateWrongHole = new AiState();
            stateWrongHole.Trigger.Add(new AiTriggerCountdown(400, null, EndWrongHole));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("walk", stateWalk);
            _aiComponent.States.Add("rotate", stateRotate);
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("ball", stateBall);
            _aiComponent.States.Add("carried", stateCarried);
            _aiComponent.States.Add("shake", stateShake);
            _aiComponent.States.Add("holeJump", stateHoleJump);
            _aiComponent.States.Add("hole", stateHole);
            _aiComponent.States.Add("wrongHole", stateWrongHole);

            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex, true, false) { SpawnPowerups = false, HitMultiplierX = 2.5f, HitMultiplierY = 2.5f };

            _aiComponent.ChangeState(Game1.RandomNumber.Next(0, 2) == 0 ? "idle" : "walk");
            _aiComponent.ChangeState("walk");

            var damageBox   = new CBox(EntityPosition, -3,  -8, 0,  6,  6, 4);
            var hittableBox = new CBox(EntityPosition, -8, -14, 0, 16, 14, 8);
            var pushableBox = new CBox(EntityPosition, -8, -14, 0, 16, 14, 8);

            if (!string.IsNullOrEmpty(_strAllSetKey))
                AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(OnKeyChange));
            AddComponent(CarriableComponent.Index, _carriableComponent = new CarriableComponent(new CRectangle(EntityPosition, new Rectangle(-8, -15, 16, 16)), CarryInit, CarryUpdate, CarryThrow) { IsInstant = true, StartGrabbing = StartGrabbing, IsActive = false });
            AddComponent(CollisionComponent.Index, _boxCollision = new BoxCollisionComponent(new CBox(EntityPosition, -8, -14, 16, 14, 8), Values.CollisionTypes.Enemy) { IsActive = false });
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, new HittableComponent(hittableBox, OnHit));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(PushableComponent.Index, new PushableComponent(pushableBox, OnPush));
            AddComponent(DrawComponent.Index, _drawComponent = new BodyDrawComponent(_body, DrawSprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, _shadowComponent = new BodyDrawShadowComponent(_body, _sprite) { Height = 1.0f, Rotation = 0.1f, ShadowWidth = 10, ShadowHeight = 5 });

            new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
        }

        public override void Reset()
        {
            // Reset the hole that the karakoro is in.
            if (_hole != null)
                _hole.IsActive = true;
            _inHole = false;

            _body.IsActive = true;
            _boxCollision.IsActive = false;
            _carriableComponent.IsActive = false;
            _shadowComponent.IsActive = true;
            _aiComponent.ChangeState("walk");
            _aiComponent.ChangeState("walk");
            _drawComponent.Layer = Values.LayerPlayer;
        }

        private void OnKeyChange()
        {
            // Once all holes are filled despawn the enemy.
            if (Game1.GameManager.SaveManager.GetString(_strAllSetKey, "0") == "1")
                Despawn();
        }

        private void Despawn()
        {
            // Restore the hole it occupies, show some cool effects, and delete them.
            if (_hole != null)
                _hole.IsActive = true;
            Map.Objects.SpawnObject(new ObjAnimator(Map, (int)EntityPosition.X - 8, (int)EntityPosition.Y - 16, Values.LayerPlayer, "Particles/spawn", "run", true));
            Map.Objects.DeleteObjects.Add(this);
            Game1.GameManager.SaveManager.SetString(_strKey, "0");
        }

        private void UpdateBall()
        {
            // The ball form is allowed to hit other objects when thrown.
            if (_throwDamage)
            {
                // Deal a hit to whatever it comes in contact with.
                var box = _body.BodyBox.Box;
                var hitCollision = Map.Objects.Hit(this, box.Center, box, HitType.ThrownObject, 2, false);
                if (hitCollision != 0)
                {
                    _body.Velocity.X = -_body.Velocity.X * 0.5f;
                    _body.Velocity.Y = -_body.Velocity.Y * 0.5f;
                }
            }
        }

        private void EndIdle()
        {
            var playerDistance = MapManager.ObjLink.Position - EntityPosition.Position;

            if (playerDistance.Length() < 38)
                _aiComponent.ChangeState("rotate");
            else
                _aiComponent.ChangeState("walk");
        }

        private void InitRotate()
        {
            var lastFrame = _animator.CurrentFrameIndex;

            if (_animator.CurrentAnimation.Id == "rotate")
            {
                _animator.Continue();
            }
            else
            {
                _animator.Play("rotate");

                // Make sure to start the animation at the same frame as the current walk animation.
                var directionFrame = _direction;
                if (directionFrame == 1)
                    directionFrame = 2;
                if (directionFrame == 2)
                    directionFrame = 1;

                _animator.SetFrame(directionFrame * 2 + lastFrame);
            }

            var direction = MapManager.ObjLink.Position - EntityPosition.Position;
            if (direction != Vector2.Zero)
                direction.Normalize();
            _body.VelocityTarget = direction * RotateSpeed;
        }

        private void InitCarried()
        {
            if (_aiComponent.LastStateId == "shake")
                _sprite.DrawOffset.X = _initShakeSpriteOffsetX;
        }

        private void InitBall()
        {
            _carriableComponent.IsActive = true;
            _damageField.IsActive = false;
            _body.IgnoreHoles = false;
            _body.VelocityTarget = Vector2.Zero;
            _animator.Play("ball");
        }

        private void InitWalk()
        {
            // walk into a random direction
            _direction = Game1.RandomNumber.Next(0, 4);
            _body.VelocityTarget = AnimationHelper.DirectionOffset[_direction] * WalkSpeed;

            _animator.Play("walk_" + _direction);
        }

        private void InitIdle()
        {
            _animator.Pause();
            _body.VelocityTarget = Vector2.Zero;

            // @HACK: the gets smaller when thrown;
            // if this would not be the case the enemy could be moved into a wall because he has a smaller collision box
            // so after the body was thrown we try to restore the original size
            if (_smallBody)
            {
                var box = new Box(EntityPosition.Position.X - 7, EntityPosition.Position.Y - 12, 0, 14, 12, 8);
                var cBox = Box.Empty;
                if (!Map.Objects.Collision(
                    box, Box.Empty, _body.CollisionTypes, _body.CollisionTypesIgnore, 0, _body.Level, ref cBox))
                {
                    _smallBody = false;
                    _body.OffsetX = -7;
                    _body.OffsetY = -12;
                    _body.Width = 14;
                    _body.Height = 12;
                }
            }
        }

        private void InitShake()
        {
            _initShakeSpriteOffsetX = _sprite.DrawOffset.X;
        }

        private void ShakeTick(double counter)
        {
            _sprite.DrawOffset.X = _initShakeSpriteOffsetX + (float)Math.Sin((ShakeTime - counter) / 1000 * (60 / 4f) * Math.PI) * 2;
        }

        private void ShakeEnd()
        {
            _isThrown = false;
            _carriableComponent.Thrown = false;
            _carriableComponent.IsActive = false;
            _damageField.IsActive = true;
            _body.IgnoreHoles = true;
            _sprite.DrawOffset.X = _initShakeSpriteOffsetX;
            _aiComponent.ChangeState("walk");
        }

        private void StartGrabbing()
        {
            if (_isThrown)
                MapManager.ObjLink.CurrentState = ObjLink.State.Idle;
        }

        private Vector3 CarryInit()
        {
            // If lifting just as the hole is about to absorb it  clear the 
            // hole and set it back to active or it will be stuck indefinitely.
            if (_claimedHole && _hole != null)
            {
                _hole.IsActive = true;
                _claimedHole = false;
            }
            _inHole = false;
            _smallBody = true;
            _body.OffsetX = -4;
            _body.OffsetY = -10;
            _body.Width = 8;
            _body.Height = 10;
            _body.IsActive = false;
            _aiComponent.ChangeState("carried");
            return EntityPosition.ToVector3();
        }

        private bool CarryUpdate(Vector3 newPosition)
        {
            if (!_body.FieldRectangle.Contains(new RectangleF(
                newPosition.X + _body.OffsetX, newPosition.Y + _body.OffsetY, _body.Width, _body.Height)))
                return false;

            EntityPosition.X = newPosition.X;
            EntityPosition.Y = newPosition.Y;
            EntityPosition.Z = newPosition.Z;
            EntityPosition.NotifyListeners();

            return true;
        }

        private void CarryThrow(Vector2 velocity)
        {
            _aiComponent.ChangeState("ball");

            _throwDamage = true;
            _isThrown = true;
            _carriableComponent.Thrown = true;

            _body.IsActive = true;
            _body.IsGrounded = false;
            _body.JumpStartHeight = 0;

            var throwMultiplier = 0.75f;
            _body.Velocity.X = velocity.X * throwMultiplier;
            _body.Velocity.Y = velocity.Y * throwMultiplier;
            _body.Velocity.Z = 1.0f;
        }

        private void InitHoleJump()
        {
            _boxCollision.IsActive = false;
            _inHole = true;
            _carriableComponent.IsActive = false;
            _body.IsActive = false;
        }

        private void HoleJumpTick(double counter)
        {
            var lerpAmount = 1 - (float)(counter / HoleTime);
            var newPosition = Vector2.Lerp(_holeStartPosition, _holeTargetPosition, lerpAmount);
            EntityPosition.Set(newPosition);
            EntityPosition.Z = MathF.Sin(lerpAmount * MathF.PI) * 8;
        }

        private void HoleJumpEnd()
        {
            HoleJumpTick(0);

            if (_hole.Color == _colorIndex)
            {
                _claimedHole = false;

                if (!string.IsNullOrEmpty(_strKey))
                    Game1.GameManager.SaveManager.SetString(_strKey, "1");

                Game1.AudioManager.PlaySoundEffect("D378-04-04");
                _aiComponent.ChangeState("hole");
            }
            else
            {
                Game1.AudioManager.PlaySoundEffect("D360-29-1D");
                _aiComponent.ChangeState("wrongHole");
            }

            EntityPosition.Set(_holeTargetPosition);
        }

        private void EndWrongHole()
        {
            _claimedHole = false;
            _drawComponent.Layer = Values.LayerPlayer;
            _shadowComponent.IsActive = true;
            _boxCollision.IsActive = false;
            _hole.IsActive = true;
            _inHole = false;
            _carriableComponent.IsActive = true;
            _body.IsActive = true;
            _body.Velocity.X = _body.FieldRectangle.Center.X < EntityPosition.X ? -1.25f : 1.25f;
            _body.Velocity.Z = 1.75f;
            _aiComponent.ChangeState("ball");
        }

        private void OnHoleAbsorb(Vector2 direction, float percentage)
        {
            if (!_inHole && percentage > 0.50f && _aiComponent.CurrentStateId == "ball")
            {
                var bodyBox = _body.BodyBox.Box;

                // Search for the hole using a list of nearby holes.
                _holeList.Clear();
                Map.Objects.GetComponentList(_holeList, (int)bodyBox.X, (int)bodyBox.Y, (int)bodyBox.Width, (int)bodyBox.Height, CollisionComponent.Mask);

                // Find the hole that the enemy has fallen into.
                foreach (var gameObjectHole in _holeList)
                {
                    var collisionComponent = gameObjectHole.Components[CollisionComponent.Index] as CollisionComponent;
                    var collidingBox = Box.Empty;

                    if (collisionComponent == null || (collisionComponent.CollisionType & Values.CollisionTypes.Hole) == 0 || !collisionComponent.Collision(bodyBox, 0, 0, ref collidingBox))
                        continue;

                    if (gameObjectHole is ObjHole holeObject)
                    {
                        _hole = holeObject;
                        _hole.IsActive = false;
                        _claimedHole = true;
                        _carriableComponent.IsActive = false;

                        _holeStartPosition = EntityPosition.Position;
                        _holeTargetPosition = new Vector2(holeObject.Center.X, holeObject.Center.Y + 8);

                        _shadowComponent.IsActive = false;
                        _drawComponent.Layer = Values.LayerBottom;
                        _aiComponent.ChangeState("holeJump");

                        return;
                    }
                }
            }
        }

        private void DrawSprite(SpriteBatch spriteBatch)
        {
            _sprite.Draw(spriteBatch);

            // draw the colored part of the sprite
            var sourceX = _sprite.SourceRectangle.X;
            _sprite.SourceRectangle.X += (int)(28 / _sprite.Scale);

            _sprite.Color = _colors[_colorIndex];
            _sprite.Draw(spriteBatch);

            _sprite.SourceRectangle.X = sourceX;
            _sprite.Color = Color.White;
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (_damageState.IsInDamageState() || originObject == this)
                return Values.HitCollision.None;

            // If it's not in a hole yet knock it into a ball state.
            if (!_inHole)
            {
                Game1.AudioManager.PlaySoundEffect("D360-03-03");

                _aiComponent.ChangeState("ball");
                _damageState.HitKnockBack(originObject, direction, hitType, pieceOfPower, false);

                return Values.HitCollision.Blocking;
            }
            // Prevent hits from throwing other karakoro once in the hole.
            if (hitType == HitType.ThrownObject)
                return Values.HitCollision.None;

            if (hitType == HitType.Bow)
                return Values.HitCollision.Repelling;

            return Values.HitCollision.None;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (!_inHole && type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

            return true;
        }

        private void OnMoveCollision(Values.BodyCollision direction)
        {
            if ((direction & Values.BodyCollision.Horizontal) != 0)
                _body.Velocity.X = -_body.Velocity.X * 0.5f;
            if ((direction & Values.BodyCollision.Vertical) != 0)
                _body.Velocity.Y = -_body.Velocity.Y * 0.5f;
            if ((direction & Values.BodyCollision.Floor) != 0)
            {
                _throwDamage = false;
                _isThrown = false;
                _carriableComponent.Thrown = false;

                if (_body.Velocity.Z == 0)
                    _body.Velocity *= 0.5f;
                else
                {
                    _body.Velocity.X *= 0.8f;
                    _body.Velocity.Y *= 0.8f;
                }
            }
        }
    }
}
