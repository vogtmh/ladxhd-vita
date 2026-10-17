using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Dungeon;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.MidBoss
{
    class MBossDodongoSnake : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly AiTriggerRandomTime _directionTrigger;
        private readonly BodyComponent _body;
        private readonly BodyDrawComponent _bodyDrawComponent;
        private readonly CSprite _sprite;

        private readonly DictAtlasEntry _spriteHead;
        private readonly DictAtlasEntry _spriteBody0;
        private readonly DictAtlasEntry _spriteBody1;
        private readonly DictAtlasEntry _spriteBody2;

        private readonly List<GameObject> _collidingObjects = new List<GameObject>();

        private readonly string _saveKey;
        private readonly int _color;

        private const float TailDistance = 12;
        private CBox _eatBox;

        private Rectangle _fieldRectangle;
        private Vector2 _bodyPosition;
        private Vector2 _bodyExplosionPosition;
        private Vector2 _turningPosition;
        private Vector2 _bodyOffset;
        private Vector2 _lastHeadPosition;

        private int _direction;
        private float _explosionCounter;
        private float _bodyDistance;
        private float _movementSpeed = 0.375f;
        private float _resetTimer;
        private bool _initialized;
        private bool _playedExplosion;
        private bool _isDying;
        private bool _drawBody = true;
        private bool _wallCollision = true;
        private bool _stopDraggin = true;
        private int _bossCount;
        private int _lives = EnemyLives.DodongoSnake;

        private bool _playerInField => _fieldRectangle.Contains(MapManager.ObjLink.CenterPosition);

        public MBossDodongoSnake() : base("snake blue") { }

        public MBossDodongoSnake(Map.Map map, int posX, int posY, string saveKey, int color, bool resetKey) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-22, -8 - 22, 44, 44);
            CanReset = false;

            // Get the field the object is in.
            if (map != null)
                _fieldRectangle = map.GetField(posX, posY);

            _bodyPosition = EntityPosition.Position;
            _lastHeadPosition = EntityPosition.Position;

            _saveKey = saveKey;
            _color = color;

            var strColor = _color == 0 ? "blue" : "green";

            // was the boss already defeated?
            if (!string.IsNullOrEmpty(_saveKey) && Game1.GameManager.SaveManager.GetString(_saveKey) == "1")
            {
                if (resetKey)
                {
                    Game1.GameManager.SaveManager.SetString(_saveKey, "0");
                }
                else
                {
                    IsDead = true;
                    return;
                }
            }
            _spriteHead = Resources.GetSprite("snake " + strColor);
            _spriteBody0 = Resources.GetSprite("snake body " + strColor);
            _spriteBody1 = Resources.GetSprite("snake body");
            _spriteBody2 = Resources.GetSprite("snake big " + strColor);

            _eatBox = new CBox(EntityPosition, -1, -8, 2, 4, 8);

            _sprite = new CSprite("snake " + strColor, EntityPosition, new Vector2(-8, -16));

            _body = new BodyComponent(EntityPosition, -7, -13, 14, 12, 8)
            {
                MoveCollision = OnCollision,
                Drag = 0.65f,
                DragAir = 0.95f,
                Gravity = -0.15f,
                FieldRectangle = map.GetField(posX, posY),
                AvoidTypes = Values.CollisionTypes.Hole | Values.CollisionTypes.NPCWall
            };

            var stateMoving = new AiState(UpdateMoving);
            stateMoving.Trigger.Add(_directionTrigger = new AiTriggerRandomTime(ChangeDirection, 1000, 1500));
            var stateExplosion = new AiState(UpdateExplosion);

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("moving", stateMoving);
            _aiComponent.States.Add("explosion", stateExplosion);

            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, 8, 0, false)
            {
                OnDeath = OnDeath
            };

            _bodyDrawComponent = new BodyDrawComponent(_body, _sprite, Values.LayerPlayer);

            var damageCollider = new CBox(EntityPosition, -7, -11, 0, 14, 11, 8, true);
            AddComponent(DamageFieldComponent.Index, new DamageFieldComponent(damageCollider, HitType.Enemy, 4));

            var hittableBox = new CBox(EntityPosition, -7, -15, 0, 14, 14, 8, true);
            AddComponent(PushableComponent.Index, new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(HittableComponent.Index, new HittableComponent(hittableBox, OnHit));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerPlayer, EntityPosition));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, _sprite) { ShadowWidth = 16, ShadowHeight = 6 });

            Map.Objects.RegisterAlwaysAnimateObject(this);

            ChangeDirection();
            _aiComponent.ChangeState("moving");
        }

        private int GetDodongoSnakeCount()
        {
            // Gets the number of remaining Dodongo Snakes. Used to properly start/stop the music.
            List<GameObject> dodongoSnakes = new List<GameObject>();
            Map.Objects.GetComponentList(dodongoSnakes, (int)EntityPosition.Position.X - 80, (int)EntityPosition.Position.Y - 64, 160, 128, BodyComponent.Mask);

            for (int i = dodongoSnakes.Count - 1; i >= 0; i--)
            {
                if (dodongoSnakes[i] is not MBossDodongoSnake)
                    dodongoSnakes.RemoveAt(i);
            }
            return dodongoSnakes.Count;
        }

        private void ResetSnake()
        {
            // There's enough stuff to do that it made sense to branch it out.
            _bodyPosition = ResetPosition.Position;
            _lastHeadPosition = ResetPosition.Position;
            _bodyDistance = 0;
            _turningPosition = Vector2.Zero;
            _stopDraggin = true;
            EntityPosition.Set(ResetPosition);
        }

        private void UpdateMoving()
        {
            // Stop updating if the boss is currently dying.
            if (_isDying)
                return;

            // Update the box that swallows bombs.
            UpdateEatBox();

            // Find how many bosses still remain.
            _bossCount = GetDodongoSnakeCount();

            // Check if player is in the field rect.
            if (!_initialized && _playerInField)
            {
                if (Camera.ClassicMode)
                    ResetSnake();

                if (Game1.AudioManager.GetCurrentMusic() != 79)
                    Game1.AudioManager.SetMusicFadeTransition(79, 2, 350);

                _initialized = true;
            }
            // Check if the player left the room.
            else if (_initialized && !_playerInField)
            {
                Game1.AudioManager.SetMusicFadeTransition(-1, 2, 350);

                _initialized = false;
                _lives = EnemyLives.DodongoSnake;
                ChangeDirection();

                // Create an effect at the monster's position if Modern Camera.
                if (!Camera.ClassicMode)
                {
                    var anim = new ObjAnimator(Map, (int)EntityPosition.X, (int)EntityPosition.Y - 8, Values.LayerTop, "Particles/pieceOfPowerExplosion", "run", true);
                    Map.Objects.SpawnObject(anim);
                    anim.Animator.SpeedMultiplier = 1.75f;
                    Game1.AudioManager.PlaySoundEffect("D360-47-2F");
                    _resetTimer = 200;
                    _sprite.IsVisible = false;
                    _drawBody = false;
                }
                return;
            }
            // If the player leaves the field, reset the monster's position.
            if (_resetTimer > 0)
            {
                _resetTimer -= Game1.DeltaTime;
                if (_resetTimer <= 0)
                {
                    _resetTimer = 0;
                    _sprite.IsVisible = true;
                    ResetSnake();
                    _drawBody = true;
                }
            }

            // Try to eat any bombs found within range.
            EatBombs();

            var offset = 0.5f;
            var speed = 55;

            _sprite.DrawOffset.X = -8 + ((_direction == 0 || _direction == 2) ? MathF.Sin((float)(Game1.TotalGameTime / speed)) * offset : 0);
            _sprite.DrawOffset.Y = -16 + ((_direction == 1 || _direction == 3) ? MathF.Sin((float)(Game1.TotalGameTime / speed)) * offset : 0);

            _bodyOffset.X = (_direction == 0 || _direction == 2) ? MathF.Sin((float)(Game1.TotalGameTime / speed) + MathF.PI * 0.9f) * offset : 0;
            _bodyOffset.Y = (_direction == 1 || _direction == 3) ? MathF.Sin((float)(Game1.TotalGameTime / speed) + MathF.PI * 0.9f) * offset : 0;

            // updated body distance
            var distance = (_lastHeadPosition - EntityPosition.Position).Length();
            _bodyDistance += distance;

            if (distance < 0.001f)
            {
                _sprite.DrawOffset.X = -8;
                _sprite.DrawOffset.Y = -16;
            }

            if (_bodyDistance > TailDistance)
            {
                _bodyDistance = TailDistance;
                _stopDraggin = false;
            }

            if (!_stopDraggin || _wallCollision)
            {
                _bodyDistance -= _movementSpeed * Game1.TimeMultiplier;
                if (_bodyDistance < 0)
                    _bodyDistance = 0;
            }

            // drag the body behind the head
            if (_turningPosition != Vector2.Zero)
            {
                // update position
                var directionTurningPoint = _turningPosition - EntityPosition.Position;
                var turningPointDistance = directionTurningPoint.Length();
                if (turningPointDistance > _bodyDistance)
                {
                    directionTurningPoint.Normalize();
                    _bodyPosition = EntityPosition.Position + directionTurningPoint * _bodyDistance;
                    _turningPosition = Vector2.Zero;
                }
                else
                {
                    // update position
                    var direction = _bodyPosition - _turningPosition;
                    if (direction != Vector2.Zero)
                    {
                        direction.Normalize();
                        _bodyPosition = _turningPosition + direction * (_bodyDistance - turningPointDistance);
                    }
                }
            }
            else
            {
                // update position
                var direction = _bodyPosition - EntityPosition.Position;

                if (direction != Vector2.Zero)
                {
                    direction.Normalize();
                    _bodyPosition = EntityPosition.Position + direction * _bodyDistance;
                }
            }

            _lastHeadPosition = EntityPosition.Position;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType pushType)
        {
            return true;
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            return Values.HitCollision.RepellingParticle;
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            _wallCollision = true;
            _directionTrigger.CurrentTime = Math.Min(_directionTrigger.CurrentTime, 250);
        }

        private void ChangeDirection()
        {
            _direction = Game1.RandomNumber.Next(0, 4);

            _body.VelocityTarget = AnimationHelper.DirectionOffset[_direction] * _movementSpeed;

            _turningPosition = EntityPosition.Position;

            if (_wallCollision)
            {
                _stopDraggin = true;
                _wallCollision = false;
            }
        }

        private void ToExploding()
        {
            _aiComponent.ChangeState("explosion");
            _bodyExplosionPosition = _bodyPosition;
            _damageState.SetDamageState();
            _playedExplosion = false;
        }

        private void UpdateExplosion()
        {
            _body.VelocityTarget = Vector2.Zero;
            _explosionCounter += Game1.DeltaTime;

            if (_explosionCounter > 94 / 0.06 && _explosionCounter - Game1.DeltaTime < 94 / 0.06)
            {
                var particlePosition = EntityPosition.Position + AnimationHelper.DirectionOffset[_direction] * 13;
                Map.Objects.SpawnObject(new ObjAnimator(Map,
                    (int)particlePosition.X, (int)particlePosition.Y, -8, -16, Values.LayerPlayer, "Particles/spawn", "run", true));
            }

            if (_explosionCounter > 76 / 0.06 && _explosionCounter - Game1.DeltaTime < 76 / 0.06)
            {
                _lives--;

                // enemy is dead?
                if (_lives <= 0)
                {
                    // Prevent from resetting once the boss is dying.
                    _isDying = true;
                    OnDeath();
                    return;
                }
            }

            if (_explosionCounter > 112 / 0.06)
            {
                _explosionCounter = 0;
                ChangeDirection();
                _aiComponent.ChangeState("moving");
                _bodyPosition = _bodyExplosionPosition;
            }
        }

        private void UpdateEatBox()
        {
            _eatBox = _direction switch
            {
                0 => _eatBox = new CBox(EntityPosition, -4, -12, 3, 10, 8),
                1 => _eatBox = new CBox(EntityPosition, -3, -12, 6, 3, 8),
                2 => _eatBox = new CBox(EntityPosition,  1, -12, 3, 10, 8),
                3 => _eatBox = new CBox(EntityPosition, -3, -7, 6, 3, 8),
                _ => _eatBox = new CBox(EntityPosition, -4, -12, 3, 10, 8)
            };
        }

        private void EatBombs()
        {
            _collidingObjects.Clear();
            Map.Objects.GetComponentList(_collidingObjects, (int)EntityPosition.Position.X - 8, (int)EntityPosition.Position.Y - 16, 16, 16, BodyComponent.Mask);

            foreach (var collidingObject in _collidingObjects)
            {
                var body = (BodyComponent)collidingObject.Components[BodyComponent.Index];

                if (collidingObject.GetType() == typeof(ObjBomb) && _eatBox.Box.Intersects(body.BodyBox.Box))
                {
                    var bomb = (ObjBomb)collidingObject;
                    if (bomb.Body.IsActive)
                    {
                        MapManager.ObjLink.BombList.Remove(bomb);
                        bomb.IsActive = false;
                        bomb.Map.Objects.DeleteObjects.Add(bomb);

                        // Play the bomb eating sound effect.
                        Game1.AudioManager.PlaySoundEffect("D360-42-2A");
                        ToExploding();
                    }
                }
            }
        }
        private void OnDeath()
        {
            if (!string.IsNullOrEmpty(_saveKey))
                Game1.GameManager.SaveManager.SetString(_saveKey, "1");

            // When it's the last snake remaining, stop the music on death.
            if (_bossCount <= 1)
                Game1.AudioManager.SetMusicFadeTransition(-1, 2, 350);

            // Play explosion sound effect & spawn fairy.
            Game1.AudioManager.PlaySoundEffect("D378-26-1A");
            Game1.AudioManager.PlaySoundEffect("D360-27-1B");
            Map.Objects.SpawnObject(new ObjDungeonFairy(Map, (int)_bodyExplosionPosition.X, (int)_bodyExplosionPosition.Y + 8, 0));

            // Shake the screen.
            if (GameSettings.ExScreenShake)
                Game1.GameManager.ShakeScreen(200, 2.00f, 1.00f, 50.00f, 25.50f);

            // Spawn the explosion effect.
            Map.Objects.SpawnObject(new ObjAnimator(Map,
                (int)_bodyExplosionPosition.X, (int)_bodyExplosionPosition.Y - 8, Values.LayerPlayer, "Particles/explosionBomb", "run2", true));

            // Remove from the map.
            Map.Objects.DeleteObjects.Add(this);
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            _sprite.SourceRectangle.X = _spriteHead.ScaledRectangle.X;
            _sprite.SourceRectangle.Y = _spriteHead.ScaledRectangle.Y;

            if (_direction == 1)
                _sprite.SourceRectangle.X += 18;
            else if (_direction == 3)
                _sprite.SourceRectangle.X += 36;

            _sprite.SpriteEffect = _direction == 2 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            var bodyDrawPosition = _bodyPosition + new Vector2(-8, -16) + _bodyOffset;
            var bodyRectangle = _spriteBody0.ScaledRectangle;

            // explosion going on?
            if (_explosionCounter > 0)
            {
                _sprite.DrawOffset = new Vector2(-8, -16);

                // change the color to green
                if (_explosionCounter < 102 / 0.06)
                {
                    var dir = _color == 0 ? 1 : -1;
                    _sprite.SourceRectangle.Y += 18 * dir;
                    bodyRectangle.Y += 18 * dir;
                }

                var targetPosition = EntityPosition.Position - new Vector2(
                    AnimationHelper.DirectionOffset[_direction].X * 13,
                    AnimationHelper.DirectionOffset[_direction].Y * 12);
                var distance = (_bodyExplosionPosition - targetPosition).Length();

                if (distance > 0)
                {
                    var amount = Math.Min(1, (1 * Game1.TimeMultiplier) / distance);
                    _bodyExplosionPosition = Vector2.Lerp(_bodyExplosionPosition, targetPosition, amount);
                }

                if (_explosionCounter < 60 / 0.06)
                {

                }
                else if (_explosionCounter < 66 / 0.06)
                {
                    bodyRectangle = _spriteBody1.ScaledRectangle;
                    _sprite.DrawOffset += AnimationHelper.DirectionOffset[_direction] * 2;
                }
                else if (_explosionCounter < 86 / 0.06)
                {
                    bodyRectangle = _spriteBody2.ScaledRectangle;
                    _sprite.DrawOffset += AnimationHelper.DirectionOffset[_direction] * 4;

                    if (!_playedExplosion)
                    {
                        Game1.AudioManager.PlaySoundEffect("D378-12-0C");
                        _playedExplosion = true;
                    }
                }
                else if (_explosionCounter < 92 / 0.06)
                {
                    bodyRectangle = _spriteBody1.ScaledRectangle;
                    _sprite.DrawOffset += AnimationHelper.DirectionOffset[_direction] * 2;
                }
                else if (_explosionCounter < 98 / 0.06)
                {

                }
                bodyDrawPosition = _bodyExplosionPosition + new Vector2(-bodyRectangle.Width / 2, -8 - bodyRectangle.Height / 2);
            }

            var drawBodyFirst = bodyDrawPosition.Y + bodyRectangle.Height <= EntityPosition.Y || (_explosionCounter > 0 && _direction != 1);

            // draw the body
            if (drawBodyFirst && _drawBody)
                spriteBatch.Draw(_spriteHead.Texture, bodyDrawPosition, bodyRectangle, Color.White);

            // draw the head
            _bodyDrawComponent.Draw(spriteBatch);

            // draw the body
            if (!drawBodyFirst && _drawBody)
                spriteBatch.Draw(_spriteHead.Texture, bodyDrawPosition, bodyRectangle, Color.White);
        }

        private void OnDeath(bool pieceOfPower)
        {
            _aiComponent.ChangeState("death");

            Game1.AudioManager.PlaySoundEffect("D370-16-10");
        }
    }
}
