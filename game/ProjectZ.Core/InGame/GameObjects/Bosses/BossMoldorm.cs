using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Bosses
{
    class BossMoldorm : GameObject
    {
        private BossMoldormTail _tail;

        private AiComponent _aiComponent;
        private BodyComponent _body;
        private BodyDrawComponent _bodyDrawComponent;
        private DamageFieldComponent _damageField;
        private CSprite _sprite;
        private HittableComponent _hitComponent;

        private Rectangle _headSourceRectangle = new Rectangle(2, 4, 28, 24);
        private Rectangle _headSourceRectangleDamage = new Rectangle(34, 4, 28, 24);

        private Rectangle[] _tailRectangles = 
        {
            new Rectangle(8, 40, 16, 16), 
            new Rectangle(8, 40, 16, 16), 
            new Rectangle(41, 41, 14, 14)
        };
        private Rectangle[] _tailRectanglesDamage = 
        {
            new Rectangle(8, 72, 16, 16), 
            new Rectangle(8, 72, 16, 16), 
            new Rectangle(41, 73, 14, 14)
        };
        private Vector2[] _tailPositions = new Vector2[4];
        private float[] _tailDistance = { 7f, 6f, 6f, 5f };
        private Vector2[] _savedPosition = new Vector2[28];

        private string _saveKey;
        private string _triggerKey;

        private float _saveInterval = 33;
        private float _saveCounter;
        private int _saveIndex;

        private float _direction;
        private float _runCounter;
        private int _lives = EnemyLives.Moldorm;

        private int _dyingState = 4;
        private int _tailState = 2;
        private float _dyingCounter = 250;
        private float _soundTimer = 200;
        private bool _blinking;

        private bool DealtPlayerDamage;

        private const float GBFrameMs = 1000f / 60f;
        private float _stepAccumulator;

        private int _headingIndex;
        private int _turnDirection = 1;
        private int _angleTimer = 6;
        private int _holdTimer = 16;
        private float _visualDirection;

        public BossMoldorm() : base("moldorm") { }

        public BossMoldorm(Map.Map map, int posX, int posY, string saveKey, string triggerKey) : base(map)
        {
            if (!string.IsNullOrEmpty(saveKey) &&
                Game1.GameManager.SaveManager.GetString(saveKey) == "1")
            {
                IsDead = true;
                return;
            }

            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 16, posY + 16, 0);
            EntitySize = new Rectangle(-24, -24, 48, 48);

            for (var i = 0; i < 4; i++)
                _tailPositions[i] = EntityPosition.Position;
            for (var i = 0; i < _savedPosition.Length; i++)
                _savedPosition[i] = EntityPosition.Position;

            _saveKey = saveKey;
            _triggerKey = triggerKey;

            _sprite = new CSprite(EntityPosition);
            _sprite.SprTexture = Resources.SprNightmares;
            _sprite.SourceRectangle = _headSourceRectangle;
            _sprite.Center = new Vector2(16, 12);

            _direction = (float)Math.PI * 2;
            _sprite.Rotation = -_direction - (float)Math.PI / 2;

            _body = new BodyComponent(EntityPosition, -7, -7, 14, 14, 8)
            {
                MoveCollision = OnCollision,
                AbsorbPercentage = 1f,
                Gravity = -0.1f,
                DragAir = 1.0f,
                Drag = 0.925f,
                CollisionTypes =
                    Values.CollisionTypes.Normal |
                    Values.CollisionTypes.Hole,
                FieldRectangle = map.GetField(posX, posY)
            };

            _aiComponent = new AiComponent();

            var stateWaiting = new AiState();
            var stateMoving = new AiState(UpdateMoving);
            var stateRunning = new AiState(UpdateRunning);
            var stateDamage = new AiState(UpdateDamage);
            stateDamage.Trigger.Add(new AiTriggerCountdown(500, UpdateDamageTick, ToRunning));
            var stateDying = new AiState(UpdateDying);

            _aiComponent.States.Add("waiting", stateWaiting);
            _aiComponent.States.Add("moving", stateMoving);
            _aiComponent.States.Add("running", stateRunning);
            _aiComponent.States.Add("damage", stateDamage);
            _aiComponent.States.Add("dying", stateDying);

            _aiComponent.ChangeState("waiting");

            _bodyDrawComponent = new BodyDrawComponent(_body, _sprite, 1);
            var damageCollider = new CBox(EntityPosition, -8, -8, 0, 16, 16, 8);

            AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(OnKeyChange));
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(PushableComponent.Index, new PushableComponent(_body.BodyBox, OnPush) { RepelMultiplier = 2.35f });
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(damageCollider, OnHit));
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerPlayer, EntityPosition));
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageCollider, HitType.Enemy, 4) { PushMultiplier = 2.5f, OnDamagedPlayer = OnDamagedPlayer });
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));

            // add the tail to the map
            _tail = new BossMoldormTail(map, this);
            map.Objects.SpawnObject(_tail);
        }

        public void OnDamagedPlayer()
        {
            DealtPlayerDamage = true;
        }

        private void Update()
        {
            if (_aiComponent.CurrentStateId == "dying")
                return;

            if (_aiComponent.CurrentStateId == "moving")
                Game1.AudioManager.PlaySoundEffect("D378-27-1B", false);

            else if (_aiComponent.CurrentStateId == "running" || _aiComponent.CurrentStateId == "damage")
            {
                Game1.AudioManager.StopSoundEffect("D378-27-1B");

                _soundTimer -= Game1.DeltaTime;

                if (_soundTimer <= 0)
                {
                    Game1.AudioManager.PlaySoundEffect("D378-01-01");
                    _soundTimer = 200;
                }
            }
        }

        private void UpdateMoving()
        {
            Move(false);
            UpdateTailPositions(1f);
        }

        private void ToRunning()
        {
            _aiComponent.ChangeState("running");
            _sprite.SourceRectangle = _headSourceRectangle;
            _blinking = false;
        }

        private void UpdateRunning()
        {
            // Dont stop running with one live left.
            if (_lives > 1)
            {
                _runCounter -= Game1.DeltaTime;
                if (_runCounter <= 0)
                    _aiComponent.ChangeState("moving");
            }
            Move(true);
            UpdateTailPositions(2f);
        }

        private static float LerpAngle(float from, float to, float amount)
        {
            var diff = MathF.IEEERemainder(to - from, MathF.PI * 2);
            return from + diff * MathF.Min(amount, 1f);
        }

        private void Move(bool enraged)
        {
            _stepAccumulator += Game1.DeltaTime;
            while (_stepAccumulator >= GBFrameMs)
            {
                _stepAccumulator -= GBFrameMs;
                MovementStep();
            }
            _direction = _headingIndex * MathF.PI / 8f;
            var vecDirection = new Vector2(MathF.Sin(_direction), MathF.Cos(_direction));
            _body.VelocityTarget = vecDirection * (enraged ? 2f : 1f);

            _visualDirection = LerpAngle(_visualDirection, _direction, 0.25f * Game1.TimeMultiplier);
            _sprite.Rotation = -_visualDirection - MathF.PI / 2f;
        }

        private void MovementStep()
        {
            // Rotate the heading one 22.5 degrees step every 6 frames.
            if (--_angleTimer <= 0)
            {
                _angleTimer = 6;
                _headingIndex = (_headingIndex + _turnDirection) & 0x0F;
            }
            // Every 16-47 frames, re-roll the turn direction.
            if (--_holdTimer <= 0)
            {
                _holdTimer = Game1.RandomNumber.Next(16, 48);
                _turnDirection = Game1.RandomNumber.Next(0, 2) == 0 ? 1 : -1;
            }
        }

        private void UpdateDamage()
        {
            _body.VelocityTarget = Vector2.Zero;
            UpdateTailPositions(1f);
        }

        private void UpdateDamageTick(double time)
        {
            _blinking = (time % 150) >= 75;
            _visualDirection += _turnDirection * (float)Math.Sin(((time + 150) / 800f) * Math.PI) * 0.1f * Game1.TimeMultiplier;
            _sprite.Rotation = -_visualDirection - (float)Math.PI / 2;
            _sprite.SourceRectangle = _blinking ? _headSourceRectangleDamage : _headSourceRectangle;
        }

        private void ToDying()
        {
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;

            _body.Velocity = new Vector3(_body.VelocityTarget.X, _body.VelocityTarget.Y, _body.Velocity.Z);
            _body.VelocityTarget = Vector2.Zero;

            _aiComponent.ChangeState("dying");
            Game1.AudioManager.PlaySoundEffect("D370-16-10");
            Game1.AudioManager.StopSoundEffect("D378-27-1B");
        }

        private void UpdateDying()
        {
            UpdateTailPositions(_body.Velocity.Length());

            _dyingCounter -= Game1.DeltaTime;

            if (_dyingCounter <= 0)
            {
                _dyingCounter = 250;
                _dyingState--;

                if (_tail != null)
                {
                    Map.Objects.DeleteObjects.Add(_tail);
                    _tail = null;
                }
                else
                    _tailState--;

                if (_dyingState < -10)
                {
                    Map.Objects.DeleteObjects.Add(this);

                    // stop music
                    Game1.AudioManager.StopAllMusic(20);
                    Game1.AudioManager.PlaySoundEffect("D378-26-1A");

                    // set the save key
                    Game1.GameManager.SaveManager.SetString(_saveKey, "1");

                    // spawn big heart
                    Map.Objects.SpawnObject(new ObjItem(Map, (int)EntityPosition.X - 8, (int)EntityPosition.Y - 8, "j", "d1_nHeart", "heartMeterFull", null));

                    // Achievement: If the player didn't take damage, unlock the achievement.
                    if (!DealtPlayerDamage)
                        AchievementManager.Earn(10);
                }
                else if (_dyingState < 0)
                {
                    _dyingCounter = 50;
                    Game1.AudioManager.PlaySoundEffect("D378-19-13");

                    // spawn explosion effect arount the head
                    var position = new Point((int)(Math.Sin(-_dyingState / 1.5f) * 8), (int)(Math.Cos(-_dyingState / 1.5f) * 8));
                    Map.Objects.SpawnObject(new ObjAnimator(Map,
                        (int)EntityPosition.X - 8 + position.X,
                        (int)EntityPosition.Y - 8 + position.Y, Values.LayerTop, "Particles/spawn", "run", true));
                }
                else
                {
                    Game1.AudioManager.PlaySoundEffect("D378-19-13");

                    // spawn explosion at the tail
                    Map.Objects.SpawnObject(new ObjAnimator(Map,
                        (int)_tailPositions[_dyingState].X - 8,
                        (int)_tailPositions[_dyingState].Y - 8, Values.LayerTop, "Particles/spawn", "run", true));
                }
            }
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            // draw the tail
            var tailRectangles = _blinking ? _tailRectanglesDamage : _tailRectangles;
            for (var i = _tailState; i >= 0; i--)
            {
                var rawPosition = _tailPositions[i] - new Vector2(tailRectangles[i].Width / 2f, tailRectangles[i].Height / 2f);
                var position = GameSettings.PixelSnapping
                    ? new Vector2(MathF.Round(rawPosition.X), MathF.Round(rawPosition.Y))
                    : rawPosition;
                spriteBatch.Draw(Resources.SprNightmares, position, tailRectangles[i], Color.White);
            }
            // draw the head
            _bodyDrawComponent.Draw(spriteBatch);
        }

        public Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            return Values.HitCollision.RepellingParticle;
        }

        public Values.HitCollision OnHitTail(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (_aiComponent.CurrentStateId == "damage" || _aiComponent.CurrentStateId == "dying")
                return Values.HitCollision.None;

            // Can only be damaged with the sword.
            if ((hitType & HitType.Sword) == 0)
                return Values.HitCollision.None;

            _lives -= damage;
            _runCounter = 2000;

            Game1.AudioManager.PlaySoundEffect("D370-07-07");

            if (_lives > 0)
            {
                _soundTimer = 0;
                _aiComponent.ChangeState("damage");
            }
            else
                ToDying();

            return Values.HitCollision.Enemy;
        }

        private void UpdateTailPositions(float speedMultiplier)
        {
            SavePosition(speedMultiplier);

            var indexCount = _saveIndex + (_saveCounter / _saveInterval);
            var timeDiff = _saveCounter + _saveInterval * 1000;

            for (var i = 0; i < _tailPositions.Length; i++)
            {
                indexCount -= _tailDistance[i];
                if (indexCount < 0)
                    indexCount += _savedPosition.Length;

                timeDiff -= _tailDistance[i] * _saveInterval;
                var index = (int)indexCount;

                _tailPositions[i] = Vector2.Lerp(
                    _savedPosition[index], _savedPosition[(index + 1) % _savedPosition.Length],
                    (timeDiff % _saveInterval) / _saveInterval);
            }

            // set the position of the tail
            _tail?.EntityPosition.Set(_tailPositions[_tailPositions.Length - 1]);
        }

        private void SavePosition(float speedMultiplier)
        {
            var position = EntityPosition.Position + _body.VelocityTarget * (Game1.DeltaTime / 16.6667f);
            _saveCounter += Game1.DeltaTime * speedMultiplier;
            var diff = _saveCounter % _saveInterval;

            var updateSteps = (int)(_saveCounter / _saveInterval);
            _saveIndex = (_saveIndex + updateSteps) % _savedPosition.Length;
            var index = _saveIndex;

            var currentDirection = _direction;

            while (_saveCounter >= _saveInterval)
            {
                _saveCounter -= _saveInterval;

                index--;
                if (index < 0)
                    index = _savedPosition.Length - 1;

                var vecDir = new Vector2((float)Math.Sin(currentDirection), (float)Math.Cos(currentDirection));
                _savedPosition[index] = position - vecDir * (diff / 16.6667f);

                position = _savedPosition[index];
                diff = _saveInterval;
                currentDirection -= _turnDirection * (MathF.PI / 48f) * (_saveInterval / 16.6667f);
            }
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            // Original snaps the heading to point directly away from the wall.
            if ((collision & Values.BodyCollision.Horizontal) != 0)
                _headingIndex = _body.VelocityTarget.X > 0 ? 12 : 4;
            else if ((collision & Values.BodyCollision.Vertical) != 0)
                _headingIndex = _body.VelocityTarget.Y > 0 ? 8 : 0;

            // 50% chance to flip the turning direction.
            if (Game1.RandomNumber.Next(0, 2) == 0)
                _turnDirection = -_turnDirection;

            // Original resets the re-roll countdown to 16 on wall contact.
            _holdTimer = 16;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
                _body.Velocity = new Vector3(direction.X * 0.25f, direction.Y * 0.25f, _body.Velocity.Z);

            return true;
        }

        private void OnKeyChange()
        {
            // activate the boss if the trigger key was set
            if (_aiComponent.CurrentStateId == "waiting" &&
                !string.IsNullOrEmpty(_triggerKey) && Game1.GameManager.SaveManager.GetString(_triggerKey) == "1")
            {
                _aiComponent.ChangeState("moving");
            }
        }
    }
}
