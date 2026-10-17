using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyMiniMoldorm : GameObject
    {
        private readonly AiComponent _aiComp;
        private readonly AiDamageState _damageState;
        private readonly BodyComponent _bodyComp;
        private readonly BodyDrawComponent _bodyDrawComp;
        private readonly CSprite _sprite;
        private readonly DictAtlasEntry _spriteHead0;
        private readonly DictAtlasEntry _spriteHead1;
        private readonly DictAtlasEntry _spritePart0;
        private readonly DictAtlasEntry _spritePart1;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private Vector2 _tailOnePosition;
        private Vector2 _tailTwoPosition;

        private const int SpriteOffsetY = 7;
        private float _directionChangeMultiplier;
        private float _direction;
        private float _changeDirCount;
        private int _dir = 1;
        private int _lives = EnemyLives.MiniMoldorm;
        private int _dropIndex = 4;

        // In the original game, the tail does not use a "follow the leader" style update but rather a
        // position history array. This should make the movements appear identical to the original game.
        private const int HistorySize = 32;
        private const float HistoryFrameMs = 1000f / 60f;   // one GB frame

        private Vector2[] _positionHistory = new Vector2[HistorySize];
        private int _historyIndex;
        private float _historyAccumulator;
        private bool _hideTail;

        public EnemyMiniMoldorm(Map.Map map, int posX, int posY) : base(map, "miniMoldormHead0")
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 8 + SpriteOffsetY, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 8 + SpriteOffsetY, 0);
            EntitySize = new Rectangle(-20, -20 - SpriteOffsetY, 40, 40);
            CanReset = true;
            OnReset = Reset;

            ResetTailHistory();

            _spriteHead0 = Resources.GetSprite("miniMoldormHead0");
            _spriteHead1 = Resources.GetSprite("miniMoldormHead1");
            _spritePart0 = Resources.GetSprite("miniMoldormPart0");
            _spritePart1 = Resources.GetSprite("miniMoldormPart1");

            _sprite = new CSprite("miniMoldormHead0", EntityPosition, new Vector2(0, -SpriteOffsetY)) { Center = new Vector2(8, 8) };

            _bodyComp = new BodyComponent(EntityPosition, -5, -5 - SpriteOffsetY, 10, 10, 8)
            {
                MoveCollision = OnCollision,
                HoleAbsorb = OnHoleAbsorb,
                AbsorbPercentage = 1f,
                Gravity = -0.1f,
                DragAir = 1.0f,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field,
                AvoidTypes =     Values.CollisionTypes.Hole | 
                                 Values.CollisionTypes.NPCWall,
                FieldRectangle = map.GetField(posX, posY)
            };

            _aiComp = new AiComponent();

            var stateWalking = new AiState(Update);
            _aiComp.States.Add("walking", stateWalking);
            _damageState = new AiDamageState(this, _bodyComp, _aiComp, _sprite, _lives, _dropIndex, false)
            {
                FlameOffset = new Point(0, 10 - SpriteOffsetY),
                UpdateLastStateFire = true
            };

            _aiComp.ChangeState("walking");

            var damageBox = new CBox(EntityPosition, -2, -2 - SpriteOffsetY, 4, 4, 4);
            var hittableBox = new CBox(EntityPosition, -6, -6 - SpriteOffsetY, 12, 12, 8);

            AddComponent(AiComponent.Index, _aiComp);
            AddComponent(BodyComponent.Index, _bodyComp);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_bodyComp.BodyBox, OnPush));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { ArrowMultiplier = true, BombMultiplier = true, BoomerangMultiplier = true });
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            _bodyDrawComp = new BodyDrawComponent(_bodyComp, _sprite, Values.LayerPlayer);
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerPlayer, EntityPosition));
        }

        public override void Reset()
        {
            _aiComp.ChangeState("walking");
            _damageState.CurrentLives = EnemyLives.MiniMoldorm;

            // Hide the tail and collapse the history onto the reset position so the
            // tail doesn't draw at stale coordinates during the screen transition.
            ResetTailHistory();
        }

        private void UpdateHeadSprite(Vector2 direction)
        {
            var modRotation = (MathF.Abs(_direction)) % (MathF.PI / 2);
            var sprite = MathF.PI / 8 < modRotation && modRotation < MathF.PI / 2 - MathF.PI / 8;
            _sprite.SourceRectangle = sprite ? _spriteHead1.ScaledRectangle : _spriteHead0.ScaledRectangle;

            // rotation of the sprite
            var dir = AnimationHelper.GetDirection(direction, MathF.PI * (9 / 8f));
            _sprite.Rotation = dir * (float)Math.PI / 2;
        }

        private void Update()
        {
            if (_hideTail && Math.Abs(_bodyComp.Velocity.X) < 0.075f && Math.Abs(_bodyComp.Velocity.Y) < 0.075f)
            {
                var headPos = new Vector2(EntityPosition.X, EntityPosition.Y - SpriteOffsetY);
                for (var i = 0; i < HistorySize; i++)
                    _positionHistory[i] = headPos;
                _historyAccumulator = 0;
                _hideTail = false;
            }
            _changeDirCount -= Game1.DeltaTime;

            if (_changeDirCount < 0)
                ChangeDirection();

            _direction += _dir * (MathF.PI / 32f) * Game1.TimeMultiplier;

            if (_direction < 0)
                _direction += (float)(Math.PI * 2);

            // move
            var vecDirection = new Vector2((float)Math.Sin(_direction), (float)Math.Cos(_direction));
            _bodyComp.VelocityTarget = vecDirection;

            if (_aiComp.CurrentStateId == "burning")
            {
                _damageField.IsActive = false;
                _bodyComp.VelocityTarget = Vector2.Zero;
            }
            _directionChangeMultiplier = AnimationHelper.MoveToTarget(_directionChangeMultiplier, 1, 0.025f * Game1.TimeMultiplier);

            UpdateHeadSprite(vecDirection);
            UpdateTailPositions();
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            // Change the draw effect.
            if (_sprite.SpriteShader != null)
            {
                spriteBatch.End();
                ObjectManager.SpriteBatchBegin(spriteBatch, _sprite.SpriteShader);
            }
            // Draw the tail.
            if (!_hideTail)
            {
                var partTwoRectangle = _spritePart1.ScaledRectangle;
                var posTwo = _tailTwoPosition - new Vector2(partTwoRectangle.Width / 2f, partTwoRectangle.Height / 2f);
                spriteBatch.Draw(Resources.SprEnemies, posTwo, partTwoRectangle, Color.White);

                var partOneRectangle = _spritePart0.ScaledRectangle;
                var posOne = _tailOnePosition - new Vector2(partOneRectangle.Width / 2f, partOneRectangle.Height / 2f);
                spriteBatch.Draw(Resources.SprEnemies, posOne, partOneRectangle, Color.White);
            }
            // Draw the head.
            _bodyDrawComp.Draw(spriteBatch);

            // Change the draw effect.
            if (_sprite.SpriteShader != null)
            {
                spriteBatch.End();
                ObjectManager.SpriteBatchBegin(spriteBatch, null);
            }
        }

        private void ResetTailHistory()
        {
            var headPos = new Vector2(EntityPosition.X, EntityPosition.Y - SpriteOffsetY);
            for (var i = 0; i < HistorySize; i++)
                _positionHistory[i] = headPos;

            _historyIndex = 0;
            _historyAccumulator = 0;
            _tailOnePosition = headPos;
            _tailTwoPosition = headPos;
            _hideTail = true;
        }

        private void UpdateTailPositions()
        {
            // Record head positions at a rate of "60 FPS" regardless of current framerate.
            _historyAccumulator += Game1.DeltaTime;
            while (_historyAccumulator >= HistoryFrameMs)
            {
                _historyAccumulator -= HistoryFrameMs;
                _historyIndex = (_historyIndex + 1) & 0x1F;
                _positionHistory[_historyIndex] = new Vector2(EntityPosition.X, EntityPosition.Y - SpriteOffsetY);
            }
            // Because we are updating in absolute positions, the tail could appear "stuttery" compared to
            // the head which is moving extremely smooth. So interpolate the position between each frame.
            float t = _historyAccumulator / HistoryFrameMs;
            _tailOnePosition = Vector2.Lerp(_positionHistory[(_historyIndex - 9)  & 0x1F], _positionHistory[(_historyIndex - 8)  & 0x1F], t);
            _tailTwoPosition = Vector2.Lerp(_positionHistory[(_historyIndex - 16) & 0x1F], _positionHistory[(_historyIndex - 15) & 0x1F], t);
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            if (Game1.RandomNumber.Next(0, 2) == 0)
                _dir = -_dir;

            if ((collision & Values.BodyCollision.Horizontal) != 0)
                _direction = (float)Math.Atan2(-_bodyComp.VelocityTarget.X * _directionChangeMultiplier, _bodyComp.VelocityTarget.Y);
            else if ((collision & Values.BodyCollision.Vertical) != 0)
                _direction = (float)Math.Atan2(_bodyComp.VelocityTarget.X, -_bodyComp.VelocityTarget.Y * _directionChangeMultiplier);

            _directionChangeMultiplier *= 0.5f;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
            {
                _bodyComp.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _bodyComp.Velocity.Z);
                _hideTail = true;
            }
            return true;
        }

        private void ChangeDirection()
        {
            _changeDirCount = Game1.RandomNumber.Next(267, 783);
            _dir = Game1.RandomNumber.Next(0, 2) == 0 ? 1 : -1;
        }

        private void OnHoleAbsorb()
        {
            // absorb the tail
            _tailOnePosition = Vector2.Lerp(_tailOnePosition, new Vector2(EntityPosition.X, EntityPosition.Y - SpriteOffsetY), 0.15f * Game1.TimeMultiplier);
            _tailTwoPosition = Vector2.Lerp(_tailTwoPosition, _tailOnePosition, 0.15f * Game1.TimeMultiplier);

            if ((new Vector2(EntityPosition.X, EntityPosition.Y - SpriteOffsetY) - _tailTwoPosition).Length() > 2)
                return;

            Map.Objects.DeleteObjects.Add(this);

            var fallAnimation = new ObjAnimator(Map, (int)EntityPosition.X - 5, (int)EntityPosition.Y - 5 - SpriteOffsetY, Values.LayerBottom, "Particles/fall", "idle", true);
            Map.Objects.SpawnObject(fallAnimation);

            Game1.AudioManager.PlaySoundEffect("D360-24-18");
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Register the hit.
            var hit = _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            // Hide the tail when knocked back.
            if (hit != Values.HitCollision.None)
                _hideTail = true;

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
