using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Systems;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.GameSystems;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.NPCs
{
    public class ObjMarin : GameObjectFollower, IHasVisibility
    {
        private enum States 
        { 
            Idle, Sequence, Fade, Singing, SingingFinal, 
            AnimalSinging, SingingDuo, PostDuo, SingingWalrus, 
            FollowPlayer, Jumping, Saved, DungeonReturn 
        };
        private States _currentState = States.Idle;

        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly BodyDrawComponent _bodyDrawComponent;
        private readonly BodyDrawShadowComponent _shadowComponent;
        private readonly CSprite _sprite;
        private readonly DictAtlasEntry _spriteNote;

        private ObjSpriteShadow _spriteShadow;
        private Queue<MoveStep> _nextMoveStep = new Queue<MoveStep>();

        struct MoveStep
        {
            public bool OffsetMode;
            public float MoveSpeed;
            public Vector2 Offset;
            public Vector2 Position;
        }
        private Rectangle _fieldRectangle;

        private bool _playerInField => _fieldRectangle.Contains(MapManager.ObjLink.CenterPosition);
        private bool _wasHidden;
        private bool _enterDungeonMessage;
        private bool _dungeonLeaveSequence;

        private Vector2 _returnStart;
        private Vector2 _returnEnd;

        private Vector2 _railJumpStartPosition;
        private Vector2 _railJumpTargetPosition;
        private float _railJumpSpeed;
        private float _railJumpHeight;
        private float _railJumpPercentage;
        private bool _isRailJumping;
        private float _holeAbsorbCounter;
        private bool _holeAbsorb;
        private bool _wasInDeepWater;

        private float _fountainFallDelay = 250;
        private float _fountainDelayCounter;
        private float _fountainStartZ;
        private bool _fountainSeqInit;
        private bool _fountainSequence;
        private bool _fountainTransition;

        private bool _worldTeleportRise;
        private float _worldTeleportRiseTime;
        private float _worldTeleportRiseDuration;
        private bool _worldTeleportFallHold;
        private float _worldTeleportFallDelay;

        private float _returnCounter;
        private bool _returnInit;
        private bool _returnFinished;

        private double _dungeonEnterTime;
        private int _dungeonEnterLives;

        private Vector2 _followVelocity;
        private Vector2 _targetPosition;
        private float _moveSpeed;

        private float _fadeTime;
        private float _fadeCounter;

        private float _noteCount;
        private float _noteEndTime;
        private int _lastDirection = -1;
        private int _cycleTime = 1000;

        private float _duoCounter;
        private int _duoIndex;
        private int _walkDirection;

        private bool _isSinging;
        private bool _isSingingWithSound;

        private bool _helpDialogShown;
        private bool _isPulled;
        private int _pullOffsetY;

        private bool _isMoving;
        private Map.Map _map;

        private bool _beachMusic;

        // Prevent player from jumping past the bridge sequence forcing hookshot usage.
        private RectangleF _blockingField;
        private Vector2 _blockingVector;
        private bool _blockingStart;
        private bool _blockingFinish;

        private bool[] _reactDialogLines = new bool[5];

        // Used for photo event achievement near the weathervane.
        private Rectangle _weatherVaneField = new Rectangle(336, 1152, 160, 128);
        private bool _weatherVaneHole = false;
        private bool _playedOcarina = false;

        private bool _isVisible;
        private bool _hideRequested;

        public bool EnterDungeonMessage;
        public bool DungeonLeaveSequence => _dungeonLeaveSequence;
        public bool IsVisible
        {
            get => _isVisible;
            internal set
            {
                _isVisible = value;
                if (!value)
                    _hideRequested = true;
            }
        }

        public override bool IsActive
        {
            get => base.IsActive;
            set
            {
                base.IsActive = value;
                if (value)
                    Activate();
            }
        }

        public ObjMarin() : base("marin") { }

        public ObjMarin(Map.Map map, int posX, int posY) : base(map)
        {
            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);

            _map = map;
            _spriteNote = Resources.GetSprite("note");
            _animator = AnimatorSaveLoad.LoadAnimator("NPCs/marin");
            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, Vector2.Zero);

            // Get the field the object is in.
            if (map != null)
                _fieldRectangle = map.GetField(posX, posY);

            _body = new BodyComponent(EntityPosition, -4, -10, 8, 10, 8)
            {
                MoveCollision = OnCollision,
                IgnoreHoles = true,
                Gravity = -0.15f
            };
            _bodyDrawComponent = new BodyDrawComponent(_body, _sprite, 1);

            AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(KeyChanged));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(CollisionComponent.Index, new BodyCollisionComponent(_body, Values.CollisionTypes.Normal | Values.CollisionTypes.PushIgnore | Values.CollisionTypes.NPC));
            AddComponent(InteractComponent.Index, new InteractComponent(_body.BodyBox, Interact));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerPlayer, EntityPosition));
            AddComponent(DrawShadowComponent.Index, _shadowComponent = new BodyDrawShadowComponent(_body, _sprite));

            // Marin will crash the game when playing the intro without this since it's not a map.
            if (map != null)
                map.Objects.RegisterAlwaysAnimateObject(this);
        }

        public override void Init()
        {
            Game1.GameManager.SaveManager.RemoveString("marin_sing_position");

            _enterDungeonMessage = false;
            EnterDungeonMessage = false;

            _sprite.IsVisible = true;

            // Fall with the player from the ceiling?
            if (MapManager.ObjLink.EntityPosition.Z > 0)
            {
                EntityPosition.Z = MapManager.ObjLink.EntityPosition.Z;
                _body.Velocity.Z = 0.5f;
                _body.IsGrounded = false;
                _fountainSequence = true;
                _fountainSeqInit = false;

                // Hold her at the ceiling for a moment so the player has time to move.
                _fountainStartZ = EntityPosition.Z;
                _fountainDelayCounter = _fountainFallDelay;
                _fountainTransition = false;
                _sprite.IsVisible = false;

                _followVelocity = Vector2.Zero;
                _walkDirection = 3;
                _animator.Play("jump_down_" + _walkDirection);
            }

            // Is disabled for the current room?
            var wasHidden = _hideRequested;
            if (_hideRequested)
            {
                _hideRequested = false;
                base.IsActive = _isVisible = false;
            }
            else if (_wasHidden)
            {
                base.IsActive = _isVisible = true;
            }
            _wasHidden = wasHidden;

            if (_dungeonLeaveSequence)
                base.IsActive = _isVisible = true;

            string active = "0";

            if (IsActive)
            {
                Activate();
                active = "1";
            }
            Game1.GameManager.SaveManager.SetString("marin_active", active);
        }

        public override void SetPosition(Vector2 position)
        {
            if (_currentState == States.DungeonReturn)
                return;

            EntityPosition.Set(position);
        }

        public void StartWorldTeleportRise(float durationMs)
        {
            _worldTeleportRise = true;
            _worldTeleportRiseTime = 0;
            _worldTeleportRiseDuration = durationMs;
            _body.IgnoresZ = true;
            _body.Velocity.Z = 0;
            EntityPosition.Z = 0;
        }

        public void HoldForWorldTeleportFall(float delayMs)
        {
            _worldTeleportFallHold = true;
            _worldTeleportFallDelay = delayMs;
            _body.IgnoresZ = true;
            _body.Velocity.Z = 0;

            _walkDirection = 3;
            _animator.Play("jump_down_" + _walkDirection);
        }

        public override void SetFacingDirection(int direction)
        {
            _lastDirection = direction;
            _walkDirection = direction;
            _animator.Play("stand_" + direction);
        }

        public void LeaveDungeonSequence(Vector2 position, int offsetX = 0, int offsetY = 0)
        {
            _returnInit = true;
            _returnFinished = false;
            _returnCounter = 0;

            _returnStart = new Vector2(position.X + 40 + offsetX, position.Y + 24 + offsetY);
            _returnEnd = new Vector2(position.X + 8, position.Y + 28);

            _dungeonLeaveSequence = true;
            Game1.GameManager.SaveManager.SetString("maria_dungeon", "0");

            _animator.Play("wait");
        }

        public void OnAppendMapChange()
        {
            if (_currentState == States.FollowPlayer && (_enterDungeonMessage || EnterDungeonMessage))
            {
                _body.VelocityTarget = Vector2.Zero;
                _currentState = States.Idle;
                Game1.GameManager.SaveManager.SetString("maria_dungeon", "1");
                Game1.GameManager.StartDialogPath("marin_dungeon");
                _dungeonEnterTime = Game1.TotalGameTime;
                _dungeonEnterLives = Game1.GameManager.CurrentHealth;

                // stop walking
                _animator.Stop();
            }
        }

        private void Activate()
        {
            // Get Marin's current internal state.
            var marinState = Game1.GameManager.SaveManager.GetString("maria_state");

            // Marin is following the player.
            if (marinState == "3" || marinState == "8")
            {
                if (Components[CollisionComponent.Index] != null)
                    RemoveComponent(CollisionComponent.Index);

                var marinDungeonState = Game1.GameManager.SaveManager.GetString("maria_dungeon");
                if (!string.IsNullOrEmpty(marinDungeonState) && marinDungeonState == "1")
                {
                    IsActive = _isVisible = false;
                    return;
                }
                else
                    _isVisible = true;

                if (_dungeonLeaveSequence)
                {
                    _dungeonLeaveSequence = false;
                    _currentState = States.DungeonReturn;
                    return;
                }

                _currentState = States.FollowPlayer;
                _followVelocity = Vector2.Zero;

                if (MapManager.ObjLink.NextMapPositionStart.HasValue &&
                    MapManager.ObjLink.NextMapPositionEnd.HasValue)
                {
                    var direction = MapManager.ObjLink.NextMapPositionEnd.Value - MapManager.ObjLink.NextMapPositionStart.Value;
                    if (direction != Vector2.Zero)
                        _walkDirection = AnimationHelper.GetDirection(direction);
                    _animator.Play("walk_" + _walkDirection);
                }
            }
            // Marin is singing in Animal Village.
            else if (marinState == "4")
            {
                var animal0 = new ObjPersonNew(Map, (int)EntityPosition.X + 8, (int)EntityPosition.Y - 32, null, "animal_rabbit", "animals_absorbed", "dance_3", new Rectangle(0, 0, 12, 12));
                var animal1 = new ObjPersonNew(Map, (int)EntityPosition.X - 8, (int)EntityPosition.Y + 32, null, "animal_rabbit", "animals_absorbed", "dance_1", new Rectangle(0, 0, 12, 12));
                var animal2 = new ObjPersonNew(Map, (int)EntityPosition.X - 40, (int)EntityPosition.Y + 16, null, "animal_rabbit", "animals_absorbed", "dance_1", new Rectangle(0, 0, 12, 12));
                var animal3 = new ObjPersonNew(Map, (int)EntityPosition.X - 40, (int)EntityPosition.Y - 32, null, "animal 02", "animals_absorbed", "dance", new Rectangle(0, 0, 12, 12));
                var animal4 = new ObjPersonNew(Map, (int)EntityPosition.X + 24, (int)EntityPosition.Y + 16, null, "animal 03", "animals_absorbed", "dance", new Rectangle(0, 0, 12, 12));

                Map.Objects.SpawnObject(animal0);
                Map.Objects.SpawnObject(animal1);
                Map.Objects.SpawnObject(animal2);
                Map.Objects.SpawnObject(animal3);
                Map.Objects.SpawnObject(animal4);

                _currentState = States.AnimalSinging;

                StartSinging();
            }
            // Marin is jumping on the bridge waiting to be saved.
            else if (marinState == "5")
            {
                 base.IsActive = _isVisible = true;
                _currentState = States.Jumping;
                _animator.Play("land");
                ((BodyCollisionComponent)Components[BodyCollisionComponent.Index]).IsActive = false;
            }
        }

        private void Update()
        {
            // Rotate towards the player.
            if (_currentState == States.Idle)
            {
                var playerDistance = new Vector2(
                    MapManager.ObjLink.EntityPosition.X - (EntityPosition.X),
                    MapManager.ObjLink.EntityPosition.Y - (EntityPosition.Y - 4));

                var dir = 3;

                // Get Marin's current internal state.
                var marinState = Game1.GameManager.SaveManager.GetString("maria_state", "0");

                // During the intro, her turn detection range is much larger than when she is outside.
                var distance = (marinState == "0") ? 128 : 20;

                // Rotate in the direction of the player.
                if (playerDistance.Length() < distance)
                    dir = AnimationHelper.GetDirection(playerDistance);

                // Catch when the direction changes.
                if (_lastDirection != dir)
                {
                    // Play idle animation when facing forward except when at the beach.
                    if (dir == 3 && marinState != "3")
                    {
                        _animator.Play("idle");
                        _animator.SpeedMultiplier = 1.0f;
                    }
                    // Other facing directions play different animations.
                    else
                    {
                        // During the intro a slower version of Marin's walking animation is used.
                        if (marinState == "0")
                        {
                            _animator.Play("walk_" + dir);
                            _animator.SpeedMultiplier = 0.50f;
                        }
                        // Most of the time the stand animation is used.
                        else
                        {
                            _animator.Play("stand_" + dir);
                            _animator.SpeedMultiplier = 1.0f;
                        }
                    }
                    _lastDirection = dir;
                }
                // Marin is standing on the beach looking at the ocean.
                if (marinState == "2")
                {
                    // When entering the field, start the beach music.
                    if (!_beachMusic && _playerInField)
                    {
                        base.IsActive = _isVisible = true;
                        Game1.AudioManager.SetMusic(76, 2);
                        _beachMusic = true;

                    }
                    // When leaving, return to overworld music.
                    else if (_beachMusic && !_playerInField)
                    {
                        Game1.AudioManager.SetMusic(-1, 2);
                        _beachMusic = false;
                    }
                }
            }
            else if (_currentState == States.AnimalSinging)
            {
                // Start/stop depending on the distance to the player.
                var nearPlayer = _playerInField;

                if (!_isSingingWithSound && nearPlayer)
                {
                    _isSingingWithSound = true;
                    Game1.AudioManager.SetMusicFadeTransition(46, 2, 350);
                }
                else if (_isSingingWithSound && !nearPlayer)
                {
                    _isSingingWithSound = false;
                    Game1.AudioManager.SetMusicFadeTransition(-1, 2, 350);
                }
            }
            else if (_currentState == States.Singing)
            {
                if (!_playerInField)
                {
                    StopSinging();
                    _currentState = States.Idle;
                }
            }
            else if (_currentState == States.SingingDuo)
                UpdateSingingDuo();
            
            else if (_currentState == States.FollowPlayer)
                UpdateFollowPlayer();
            
            else if (_currentState == States.Jumping)
                UpdateMountainSequence();
            
            else if (_currentState == States.DungeonReturn)
                UpdateReturn();
            
            UpdateMoving();

            UpdateFade();

            if (_isSinging || _noteCount < _noteEndTime)
                _noteCount += Game1.DeltaTime;
            else
                _noteCount = _noteEndTime;

            // If sprite shadow is null, the map has changed, and Marin is active.
            if ((_spriteShadow == null || Map != _map) && IsActive)
            {
                // Update the map to the new map.
                _map = Map;

                // If a sprite shadow already exists remove it.
                if (_spriteShadow != null)
                    _spriteShadow.Destroy();

                // Spawn a new sprite shadow on this map and always animate it.
                _spriteShadow = new ObjSpriteShadow(Map, this, Values.LayerPlayer, "sprshadowm");
                Map.Objects.RegisterAlwaysAnimateObject(_spriteShadow);
            }
        }

        private void UpdateMountainSequence()
        {
            // Only allow the player through when using hookshot.
            if (!_blockingFinish && MapManager.ObjLink.CurrentState != ObjLink.State.Hookshot)
            {
                // If Link is falling down the hole, reset the blocking states.
                if (MapManager.ObjLink.CurrentState == ObjLink.State.Falling)
                {
                    _blockingField = RectangleF.Empty;
                    _blockingStart = false;
                    _blockingVector = Vector2.Zero;
                }
                // If Link is currently trying to jump over the bridge.
                else
                {
                    // Set up the blocking field rectangle to the right of Marin.
                    _blockingField = new RectangleF(EntityPosition.X + 6, EntityPosition.Y - 15, 18, 50);

                    // Check to see if Link entered the blocking field.
                    if (_blockingField.Contains(MapManager.ObjLink.CenterPosition))
                    {
                        // Throw the rooster if using it.
                        if (MapManager.ObjLink.IsFlying())
                            MapManager.ObjLink.ReleaseCarriedObject();

                        // Set up the blocking position.
                        if (!_blockingStart)
                            _blockingVector = new Vector2(MapManager.ObjLink.EntityPosition.X, MapManager.ObjLink.EntityPosition.Y);

                        // Start blocking at that position.
                        _blockingStart = true;

                        // Don't trap Link in the air. Stop blocking when Z depth hits zero.
                        if (MapManager.ObjLink.EntityPosition.Z > 0)
                            MapManager.ObjLink.SetPosition(_blockingVector);
                    }
                }
            }
            // Get player position, direction, and distance from Marin.
            var playerPosition = MapManager.ObjLink.Position;
            var playerDirection = EntityPosition.Position - playerPosition;
            var distance = playerDirection.Length();

            // If distance is less than 72 and dialog isn't visible, show it.
            if (distance < 72 && !_helpDialogShown)
            {
                _helpDialogShown = true;
                Game1.GameManager.StartDialogPath("marin_help");
            }
            // If distance is greater than 128 track that the dialog is not shown.
            else if (distance > 128)
            {
                _helpDialogShown = false;
            }
            // When the distance is less than 16 assume the player is mid-flight with the
            // hookshot and encountered Marin. Start the pull and set the pull offset.
            if (!_isPulled && distance < 16 && playerDirection.X > 8)
            {
                _blockingFinish = true;
                _isPulled = true;
                _pullOffsetY = (int)playerDirection.Y;
                _animator.Play("stand_0");
            }
            // The hookshot is currently being used and pulling Marin.
            if (_isPulled)
            {
                // Update the position of Marin as she is being pulled.
                EntityPosition.Set(new Vector2(playerPosition.X + 14, playerPosition.Y + _pullOffsetY));

                // When the hookshot is finished assume we're on the other side of the gap.
                if (!MapManager.ObjLink.IsUsingHookshot())
                {
                    _blockingFinish = false;
                    _isPulled = false;
                    _currentState = States.Saved;
                    Game1.GameManager.StartDialogPath("marin_saved");
                }
            }
            // When Marin jumped and is falling, play the "landing" animation.
            if (_animator.CurrentAnimation.Id == "jump" && _body.IsGrounded)
            {
                _animator.Play("land");
            }
            // When Marin touches ground and jumps, play the "jumping" animation.
            if (_animator.CurrentAnimation.Id == "land" && !_animator.IsPlaying)
            {
                _animator.Play("jump");
                _body.Velocity.Z = 1.25f;
            }
        }

        private void UpdateReturn()
        {
            var fieldState = SystemBody.GetFieldState(_body);
            var inDeepWater = fieldState.HasFlag(MapStates.FieldStates.DeepWater);
            
            // freeze the player (need to make sure to play the transition animation)
            var transitionSystem = (MapTransitionSystem)Game1.GameManager.GameSystems[typeof(MapTransitionSystem)];
            if (!transitionSystem.IsTransitioningIn())
                MapManager.ObjLink.FreezePlayer();

            _returnCounter += Game1.DeltaTime;

            if (_returnInit)
            {
                _returnInit = false;
                EntityPosition.Set(_returnStart);
            }

            if (_returnFinished)
            {
                _walkDirection = 1;
                if (inDeepWater)
                    _animator.Play("swim_1");
                else
                    _animator.Play("stand_1");

                if (_returnCounter > 2500)
                {
                    // Determines if one of the two "are you hurt" dialogs are shown.
                    var lowHPValue   = MapManager.ObjLink.GetLowHPValue();
                    var heartsDialog = MapManager.ObjLink.IsLowHealth && (_dungeonEnterLives >= lowHPValue || _dungeonEnterLives == 0);
                    var randomDialog = Game1.RandomNumber.Next(0, 64);
                    var dialogUsed   = heartsDialog ? (randomDialog < 63 ? "1" : "2") : "0";
                    Game1.GameManager.SaveManager.SetString("marin_dungeon_hearts", dialogUsed);

                    // Determines if one of the two "time based" dialogs are shown.
                    var longTimeDialog = _dungeonEnterTime + 137000 < Game1.TotalGameTime;
                    Game1.GameManager.SaveManager.SetString("marin_dungeon_time", longTimeDialog ? "1" : "0");

                    // Play the line that was selected.
                    Game1.GameManager.StartDialogPath("marin_dungeon_leave");

                    // Marin resumes following the player.
                    _currentState = States.FollowPlayer;

                    // If the achievement has not been earned.
                    if (!AchievementManager.IsEarned(49))
                    {
                        // Resolve which line actually plays.
                        int shownLine;
                        if (dialogUsed == "1")      { shownLine = 0; } // The common HP dialog "...EEEK! You're hurt!..."
                        else if (dialogUsed == "2") { shownLine = 1; } // The rare HP dialog "...You idiot! I told you..."
                        else if (longTimeDialog)    { shownLine = 2; } // The rare timed dialog "...You're late!...".
                        else                        { shownLine = 3; } // The common timed dialog "You're back!...".

                        // Each of the four dialogs occupies one bit of this session-only mask.
                        int dialogMask = Game1.GameManager.SaveManager.GetInt("marin_dungeon_dialog_mask", 0);

                        // If this dialog's bit isn't set yet, record it.
                        if ((dialogMask & (1 << shownLine)) == 0)
                        {
                            // Flip the bit for the dialog that just played, leaving the other bits untouched.
                            dialogMask |= (1 << shownLine);

                            // Every unique line has been seen this session so grant the achievement.
                            if (dialogMask == 0b1111)
                            {
                                AchievementManager.Earn(49);
                                Game1.GameManager.SaveManager.RemoveInt("marin_dungeon_dialog_mask");
                            }
                            // Otherwise store the bit since there is still lines to be seen.
                            else
                            {
                                Game1.GameManager.SaveManager.SetInt("marin_dungeon_dialog_mask", dialogMask);
                            }
                        }
                    }
                }
                return;
            }

            // start walking
            if (_returnCounter > 1500)
            {
                if (inDeepWater)
                    _animator.Play("swim_0");
                else
                    _animator.Play("walk_0");

                var newPosition = AnimationHelper.MoveToTarget(EntityPosition.Position, _returnEnd, 0.75f * Game1.TimeMultiplier);
                EntityPosition.Set(newPosition);

                if (newPosition == _returnEnd)
                    _returnFinished = true;
            }
        }

        private void UpdateFade()
        {
            if (_fadeTime <= 0)
                return;

            _fadeCounter -= Game1.DeltaTime;
            
            if (_fadeCounter <= 0)
                _map.Objects.DeleteObjects.Add(this);
            else
            {
                var percentage = _fadeCounter / _fadeTime;
                _sprite.Color = Color.White * percentage;
                _shadowComponent.Transparency = percentage;
            }
        }

        private void UpdateMoving()
        {
            if (!_isMoving)
                return;

            // move towards the target position
            var targetDistance = _targetPosition - EntityPosition.Position;
            if (targetDistance.Length() > _moveSpeed * Game1.TimeMultiplier)
            {
                targetDistance.Normalize();
                _body.VelocityTarget = targetDistance * _moveSpeed;

                var dir = AnimationHelper.GetDirection(targetDistance);
                _animator.Play("walk_" + dir);
            }
            // finished walking
            else
            {
                _body.VelocityTarget = Vector2.Zero;
                EntityPosition.Set(_targetPosition);

                if (_nextMoveStep.Count > 0)
                    DequeueMove();
                else
                {
                    _isMoving = false;
                    SetMovingString(false);
                }
            }
        }

        private void DequeueMove()
        {
            var move = _nextMoveStep.Dequeue();
            _moveSpeed = move.MoveSpeed;

            if (!move.OffsetMode)
                _targetPosition = move.Position;
            else
                _targetPosition = EntityPosition.Position + move.Offset;
        }

        private void SetMovingString(bool state)
        {
            Game1.GameManager.SaveManager.SetString("marinMoving", state ? "1" : "0");
        }

        private void UpdateSingingDuo()
        {
            _duoCounter += Game1.DeltaTime;
            if (_duoIndex == 0 && _duoCounter > 7500)
            {
                _duoIndex++;
                _isSinging = false;
                _animator.Play("idle");
                MapManager.ObjLink.StartOcarinaDuo();
            }
            else if (_duoIndex == 1 && _duoCounter > 16000)
            {
                _duoIndex++;
                _isSinging = true;
                _animator.Play("sing");
            }
            else if (_duoIndex == 2 && _duoCounter > 32000)
            {
                _duoIndex++;
                _isSinging = false;
                _animator.Play("idle");
            }
            else if (_duoIndex == 3 && _duoCounter > 33000)
            {
                _currentState = States.PostDuo;
                MapManager.ObjLink.StopOcarinaDuo();
                Game1.GameManager.StartDialogPath("marin_singing_end");
                Game1.AudioManager.SetMusic(-1, 2);
                Game1.GameManager.SaveManager.RemoveString("marin_sing_position");
            }

            if (_duoIndex == 0)
                MapManager.ObjLink.FreezePlayer();

            Game1.GameManager.InGameOverlay.DisableInventoryToggle = true;
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            if ((collision & Values.BodyCollision.Vertical) != 0)
            {
                _followVelocity.X += Math.Abs(_followVelocity.Y) * MathF.Sign(_followVelocity.X);
                _followVelocity.Y = 0;
            }
            if ((collision & Values.BodyCollision.Horizontal) != 0)
            {
                _followVelocity.Y += Math.Abs(_followVelocity.X) * MathF.Sign(_followVelocity.Y);
                _followVelocity.X = 0;
            }
        }

        private void UpdateFollowPlayer()
        {
            // ---------------------------------------------------------------------
            // WORLD TELEPORT: Marin rises as Link rises during a world teleport.
            if (_worldTeleportRise)
            {
                _worldTeleportRiseTime += Game1.DeltaTime;
                _body.VelocityTarget = Vector2.Zero;
                EntityPosition.Z = MathHelper.Clamp(_worldTeleportRiseTime / _worldTeleportRiseDuration, 0, 1) * 128;

                if (_worldTeleportRiseTime >= _worldTeleportRiseDuration)
                    _worldTeleportRise = false;

                return;
            }

            // Holds Marin in the air until Link falls first.
            else if (_worldTeleportFallHold)
            {
                _worldTeleportFallDelay -= Game1.DeltaTime;
                _body.VelocityTarget = Vector2.Zero;
                if (_worldTeleportFallDelay <= 0)
                {
                    _worldTeleportFallHold = false;
                    _body.IgnoresZ = false;
                }
                return;
            }
            // ---------------------------------------------------------------------

            var Link = MapManager.ObjLink;
            var fieldState = SystemBody.GetFieldState(_body);
            var inDeepWater = fieldState.HasFlag(MapStates.FieldStates.DeepWater);
            var transitioningIn = ((MapTransitionSystem)Game1.GameManager.GameSystems[typeof(MapTransitionSystem)]).IsTransitioningIn();

            // ---------------------------------------------------------------------
            // PHOTO SEQUENCE: Everything below here is part of the photo sequence where Link and Marin jump down the well.
            if (!_fountainSeqInit && _fountainSequence)
            {
                // Set Marin to the proper height.
                _fountainSeqInit = true;
                EntityPosition.Set(new Vector2(EntityPosition.X, EntityPosition.Y - 8));

                // Build a list of the map objects.
                var objects = new List<GameObject>();
                Map.Objects.GetComponentList(objects, 0, 0, 160, 128, BodyComponent.Mask);

                // Loop through that list.
                for (int i = 0; i < objects.Count; i++)
                {
                    // We are looking for the Photo Mouse.
                    if (objects[i] is ObjPhotoMouse photoMouse)
                    {
                        // Disable interactions and make it face north.
                        photoMouse.DisableInteractions();
                        photoMouse.PlayAnimation("stand_1");
                        break;
                    }
                }
            }

            // When fountain sequence is happening, delay Marin's fall. Must happen before the transition
            // check which returns early allowing "SystemBody" to do it's thing and pull her down.
            if (_fountainSequence && _fountainDelayCounter > 0)
            {
                // Lock Marin in position.
                EntityPosition.Z = _fountainStartZ;
                _body.Velocity.Z = 0;
                _body.VelocityTarget = Vector2.Zero;

                // Start counting down once the room begins fading in.
                if (transitioningIn || _fountainTransition)
                {
                    _fountainTransition = true;
                    _fountainDelayCounter -= Game1.DeltaTime;
                }

                // When the timer is done counting down make Marin's sprite visible and set
                // her Z-Velocity to a negative so it drops her straight down immediately.
                if (_fountainDelayCounter <= 0)
                {
                    _body.Velocity.Z = -1.5f;
                    _sprite.IsVisible = true;
                }
                return;
            }

            // While transition in prevent following the player. 
            if (transitioningIn)
            {
                _body.VelocityTarget = Vector2.Zero;
                return;
            }

            // Marin has fallen and hit the ground.
            if (_fountainSequence && _body.IsGrounded)
            {
                _fountainSequence = false;

                // Check the distance between Link and Marin when she lands to determine a collision.
                var playerDist = Link.EntityPosition.Position - new Vector2(EntityPosition.Position.X, EntityPosition.Position.Y);
                var fallenOnLink = playerDist.Length() < 5;

                // Store whether Marin fell on Link or he dodged her.
                Game1.GameManager.SaveManager.SetString("fallen_on_link", (fallenOnLink ? "1" : "0"));
                Game1.GameManager.StartDialogPath("seq_fountain");

                // Play a screen shake when falling on Link.
                if (fallenOnLink)
                {
                    if (GameSettings.ScreenShake)
                        Game1.GameManager.ShakeScreen(450, 0, 2, 0, 5);
                    Game1.AudioManager.PlaySoundEffect("D360-11-0B");
                }
            }

            // Until she lands, drop straight down instead of following Link.
            if (_fountainSequence)
            {
                _followVelocity = Vector2.Zero;
                _body.VelocityTarget = Vector2.Zero;
                _animator.Play("jump_down_" + _walkDirection);
                return;
            }

            // ---------------------------------------------------------------------
            // Jump when Link jumps or when rail jumping.
            if (Link.IsJumpingState() && _body.IsGrounded && !inDeepWater &&
                (Link.RailJumpAmount() > 0.45f || (!Link.IsRailJumping() && Link.Body.Velocity.Z < 0)))
            {
                _body.Velocity.Z = 2.35f;

                if (Link.IsRailJumping())
                {
                    Game1.AudioManager.PlaySoundEffect("D360-08-08");

                    _isRailJumping = true;
                    _holeAbsorb = false;

                    _body.IgnoreHeight = true;
                    _body.IgnoresZ = true;
                    _body.IsGrounded = false;
                    _body.VelocityTarget = Vector2.Zero;

                    _railJumpPercentage = 0;
                    _railJumpStartPosition = EntityPosition.Position;
                    _railJumpTargetPosition = Link.RailJumpTarget();
                    _railJumpSpeed = Link.RailJumpSpeed();
                    _railJumpHeight = Link.RailJumpHeight();

                    _walkDirection = Link.Direction;
                    _animator.Play("stand_" + _walkDirection);
                }
                else 
                    Game1.AudioManager.PlaySoundEffect("D360-36-24");
            }
            // Marin is currently in a rail jump.
            if (_isRailJumping)
            {
                _railJumpPercentage += Game1.TimeMultiplier * _railJumpSpeed;
                var amount = MathF.Sin(_railJumpPercentage * (MathF.PI * 0.3f)) / MathF.Sin(MathF.PI * 0.3f);
                var newPosition = Vector2.Lerp(_railJumpStartPosition, _railJumpTargetPosition, amount);
                EntityPosition.Set(newPosition);
                EntityPosition.Z = MathF.Sin(_railJumpPercentage * MathF.PI) * _railJumpHeight;

                // Finished rail jump?
                if (_railJumpPercentage >= 1)
                {
                    _isRailJumping = false;
                    _body.IgnoreHeight = false;
                    _body.IgnoresZ = false;
                    _body.Velocity.Z = -1f;
                    EntityPosition.Set(_railJumpTargetPosition);
                }
                // If Link jumps into a hole Marin should follow.
                if (Link.IsHoleAbsorb())
                {
                    _holeAbsorb = true;
                    _holeAbsorbCounter = 175;
                }
                return;
            }

            // Fall into a hole with Link?
            if (_holeAbsorb)
            {
                _holeAbsorbCounter -= Game1.DeltaTime;
                if (_holeAbsorbCounter >= 0)
                    return;

                var fallAnimation = new ObjAnimator(Map, 0, 0, Values.LayerBottom, "Particles/fall", "idle", true);
                fallAnimation.EntityPosition.Set(new Vector2(
                    _body.Position.X + _body.OffsetX + _body.Width / 2.0f - 5,
                    _body.Position.Y + _body.OffsetY + _body.Height / 2.0f - 5));
                Map.Objects.SpawnObject(fallAnimation);

                _sprite.IsVisible = false;
                _holeAbsorb = false;

                return;
            }
            // If rail jumping then exit early.
            if (Link.IsRailJumping())
                return;

            // Check if landing on ground and if in deep water.
            bool landedGround = _body.IsGrounded && !_body.WasGrounded;
            bool enteredWater = inDeepWater && !_wasInDeepWater && _body.IsGrounded;

            // Play a splash effect and sound.
            if ((landedGround && inDeepWater) || enteredWater)
            {
                var splashAnimator = new ObjAnimator(
                    Map, 0, 0, 0, 3, Values.LayerPlayer,
                    "Particles/splash", "idle", true);

                splashAnimator.EntityPosition.Set(new Vector2(
                    _body.Position.X + _body.OffsetX + _body.Width / 2f,
                    _body.Position.Y + _body.OffsetY + _body.Height - _body.Position.Z - 6));

                Map.Objects.SpawnObject(splashAnimator);

                Game1.AudioManager.PlaySoundEffect("D360-14-0E");
            }
            // Landed on solid ground.
            else if (landedGround)
                Game1.AudioManager.PlaySoundEffect("D378-07-07");

            var playerDirection = Link.EntityPosition.Position - EntityPosition.Position;
            var playerDistance = Math.Abs(playerDirection.X) + Math.Abs(playerDirection.Y);
            if (playerDirection != Vector2.Zero)
                playerDirection.Normalize();
            var walkSpeedMult = playerDistance / 16;
            var targetVelocity = Vector2.Zero;

            var collisionCheckDist = 8;
            var collidingBox = Box.Empty;

            // Check for future collisions.
            var collisionH = SystemBody.Collision(_body,
                EntityPosition.X + playerDirection.X * collisionCheckDist,
                EntityPosition.Y, 0, _body.CollisionTypes, false, ref collidingBox);
            var collisionV = SystemBody.Collision(_body,
                EntityPosition.X,
                EntityPosition.Y + playerDirection.Y * collisionCheckDist, 0, _body.CollisionTypes, false, ref collidingBox);

            // Disable the collision if we are too far away from the player; this will prevent situations where we are stuck.
            var ignoreCollisions = Link.IsRailJumping() || playerDistance > 24;
            _body.CollisionTypes = ignoreCollisions ? Values.CollisionTypes.None : Values.CollisionTypes.Normal;

            if (playerDistance > 16)
            {
                targetVelocity = playerDirection * walkSpeedMult;

                // Try to avoid future collisions by walking around the colliding object.
                if (!ignoreCollisions && collisionH)
                {
                    targetVelocity.Y += (Math.Abs(targetVelocity.X) * MathF.Sign(targetVelocity.Y)) * 0.5f;
                    targetVelocity.X *= 0.5f;
                }
                else if (!ignoreCollisions && collisionV)
                {
                    targetVelocity.X += (Math.Abs(targetVelocity.Y) * MathF.Sign(targetVelocity.X)) * 0.5f;
                    targetVelocity.Y *= 0.5f;
                }
            }
            _followVelocity = Vector2.Lerp(_followVelocity, targetVelocity, 0.45f * Game1.TimeMultiplier);
            _body.VelocityTarget = _followVelocity;

            if (_followVelocity.Length() > 0.1f)
                _walkDirection = AnimationHelper.GetDirection(_followVelocity);

            // Play jumping animation if Link jumped.
            if (Link.IsJumpingState() && !inDeepWater && _followVelocity.Length() < 0.1f)
            {
                _animator.Play("jump_up_" + _walkDirection);
            }
            else if (!_body.IsGrounded)
            {
                if (_body.Velocity.Z > 0)
                    _animator.Play("jump_up_" + _walkDirection);
                else
                    _animator.Play("jump_down_" + _walkDirection);
            }
            // Play walk or swim animation when moving.
            else if (_followVelocity.Length() > 0.1f)
            {
                if (inDeepWater)
                    _animator.Play("swim_" + _walkDirection);
                else
                    _animator.Play("walk_" + _walkDirection);

                _animator.SpeedMultiplier = walkSpeedMult;
            }
            // Play stand or swim animation when not moving.
            else
            {
                // Marin's swim velocity is not monitored so use Link's to make things easier.
                if (inDeepWater)
                {
                    _animator.Play("swim_" + _walkDirection);

                    // The velocity is used to determine if the animation cycles frames or sits on frame 1.
                    if (Link.GetSwimVelocity().Length() < 0.1)
                        _animator.IsPlaying = false;
                }
                else
                    _animator.Play("stand_" + _walkDirection);
            }
            // If the dungeon message is set Link entered a dungeon, so show the dialog now.
            _enterDungeonMessage = EnterDungeonMessage;
            EnterDungeonMessage = false;

            // Store the previous field state for the next frame.
            _wasInDeepWater = inDeepWater;

            // If the player has not yet earned the achievement for weather vane picture.
            if (!AchievementManager.IsEarned(48))
            {
                // We are on the field with the weather vane.
                if (MapManager.ObjLink.CurrentField == _weatherVaneField)
                {
                    // Track if a hole was dug on the field.
                    if (!_weatherVaneHole && Map.FieldHasDugHole(_weatherVaneField))
                        _weatherVaneHole = true;

                    // Track if Link played the Ocarina.
                    if (MapManager.ObjLink.CurrentState == ObjLink.State.Ocarina)
                        _playedOcarina = true;

                    // Store that the achievement can be earned when the photo is taken.
                    if (_weatherVaneHole && _playedOcarina)
                        Game1.GameManager.SaveManager.SetString("marin_weathervane_achieve", "1");
                }
                // We don't want to constantly be calling "FieldHasDugHole" since
                // it is a demanding function. So stop calling it if a hole is dug.
                else
                {
                    // The actual achievement is earned in "scripts.zScript" in path "seq_weathervane_achievement".
                    _weatherVaneHole = false;
                    _playedOcarina = false;
                    Game1.GameManager.SaveManager.RemoveString("marin_weathervane_achieve");
                }
            }

            // If the achievement for dialogs has not yet been earned.
            if (!AchievementManager.IsEarned(50))
            {
                // The number of reaction lines currently said.
                int reactionTotal = 0;

                // Get the state of all reaction dialog lines.
                for (int i = 0; i < 5 ; i++) 
                {
                    // When a line is said, it will be stored in the strings "marin_react_achieve_1-5".
                    _reactDialogLines[i] = Game1.GameManager.SaveManager.GetString("marin_react_achieve_" + (i + 1).ToString(), "0") == "1";

                    // If the current line has been said store it.
                    if (_reactDialogLines[i])
                        reactionTotal++;
                }
                // If all five lines have been said.
                if (reactionTotal == 5)
                    AchievementManager.Earn(50);
            }
        }

        private void StartSinging()
        {
            _isSinging = true;
            _noteCount = 0;
            _noteEndTime = 0;
            _body.VelocityTarget = Vector2.Zero;
            _animator.Play("sing");

            // set the position for the dogs listening
            Game1.GameManager.SaveManager.SetString("marin_sing_position", (int)EntityPosition.X + "," + (int)EntityPosition.Y);
        }

        private void StopSinging()
        {
            Game1.AudioManager.SetMusicFadeTransition(-1, 2, 350);
            _isSinging = false;
            _isSingingWithSound = false;
            _lastDirection = -1;
            _noteEndTime = (int)(_noteCount / (_cycleTime * 0.5f) + 2) * (_cycleTime * 0.5f);

            Game1.GameManager.SaveManager.RemoveString("marin_sing_position");
        }

        private void KeyChanged()
        {
            if (!IsActive)
                return;

            // Start fading away?
            var fadeValue = Game1.GameManager.SaveManager.GetString("maria_fade");
            if (!string.IsNullOrEmpty(fadeValue))
            {
                _fadeTime = int.Parse(fadeValue);
                _fadeCounter = _fadeTime;
                Game1.GameManager.SaveManager.RemoveString("maria_fade");
            }

            // Start moving?
            var moveValue = Game1.GameManager.SaveManager.GetString("maria_walk");
            if (!string.IsNullOrEmpty(moveValue))
            {
                var split = moveValue.Split(',');

                if (split.Length == 3)
                {
                    var offsetX = int.Parse(split[0]);
                    var offsetY = int.Parse(split[1]);
                    _moveSpeed = float.Parse(split[2], CultureInfo.InvariantCulture);
                    _nextMoveStep.Enqueue(new MoveStep() { MoveSpeed = _moveSpeed, Offset = new Vector2(offsetX, offsetY), OffsetMode = true });
                }
                if (split.Length == 4)
                {
                    var positionX = int.Parse(split[0]);
                    var positionY = int.Parse(split[1]);
                    _moveSpeed = float.Parse(split[2], CultureInfo.InvariantCulture);
                    _nextMoveStep.Enqueue(new MoveStep() { MoveSpeed = _moveSpeed, Position = new Vector2(positionX, positionY) });
                }

                if (!_isMoving)
                {
                    _isMoving = true;
                    DequeueMove();
                    SetMovingString(true);
                    _body.CollisionTypes = Values.CollisionTypes.None;
                }

                _isMoving = true;
                _currentState = States.Idle;
                Game1.GameManager.SaveManager.RemoveString("maria_walk");
            }

            // Start singing?
            var value = Game1.GameManager.SaveManager.GetString("maria_sing");
            if (value != null && value == "1")
            {
                StartSinging();
                Game1.AudioManager.SetMusicFadeTransition(46, 2, 350);
                _currentState = States.Singing;
                Game1.GameManager.SaveManager.RemoveString("maria_sing");
            }

            // Start singing for the final scene?
            var singFinal = Game1.GameManager.SaveManager.GetString("maria_sing_final", "0");
            if (singFinal == "1")
            {
                StartSinging();
                _currentState = States.SingingFinal;
                Game1.GameManager.SaveManager.RemoveString("maria_sing_final");
            }

            var animalKey = Game1.GameManager.SaveManager.GetString("maria_sing_animals");
            if (animalKey != null && animalKey == "1")
            {
                StartSinging();
                Game1.AudioManager.SetMusicFadeTransition(46, 2, 350);
                _currentState = States.AnimalSinging;
                Game1.GameManager.SaveManager.RemoveString("maria_sing_animals");
            }

            var walrusKey = Game1.GameManager.SaveManager.GetString("maria_stop_singing");
            if (walrusKey != null && walrusKey == "1")
            {
                StopSinging();
                _currentState = States.Sequence;
                Game1.GameManager.SaveManager.RemoveString("maria_stop_singing");
            }

            var animationKey = Game1.GameManager.SaveManager.GetString("maria_play_animation");
            if (!string.IsNullOrEmpty(animationKey))
            {
                _animator.Play(animationKey);
                _currentState = States.Sequence;
                Game1.GameManager.SaveManager.RemoveString("maria_play_animation");
            }

            var stopSingingKey = Game1.GameManager.SaveManager.GetString("maria_sing_walrus");
            if (stopSingingKey != null && stopSingingKey == "1")
            {
                StartSinging();
                Game1.AudioManager.SetMusic(46, 2);
                _currentState = States.SingingWalrus;
                Game1.GameManager.SaveManager.RemoveString("maria_sing_walrus");
            }

            var duoKey = Game1.GameManager.SaveManager.GetString("maria_start_duo");
            if (duoKey != null && duoKey == "1")
            {
                _duoIndex = 0;
                _duoCounter = 0;
                StartSinging();
                Game1.AudioManager.SetMusic(73, 2);
                _currentState = States.SingingDuo;
                MapManager.ObjLink.FreezePlayer();
                Game1.GameManager.SaveManager.RemoveString("maria_start_duo");
            }
        }

        private bool Interact()
        {
            if (_currentState != States.Idle &&
                _currentState != States.AnimalSinging &&
                _currentState != States.PostDuo)
                return false;

            if (_currentState == States.AnimalSinging)
            {
                _isSinging = false;
                _animator.Play("idle");
                Game1.AudioManager.SetMusicFadeTransition(-1, 2, 350);
            }
            Game1.GameManager.StartDialogPath("maria");
            return true;
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            _bodyDrawComponent.Draw(spriteBatch);

            var leftNotePosition = new Vector2(EntityPosition.X - 8 - _spriteNote.SourceRectangle.Width / 2f, EntityPosition.Y - 16 - _spriteNote.SourceRectangle.Height / 2f);
            var leftNoteDirection = new Vector2(-0.4f, -1.0f);
            DrawNote(spriteBatch, leftNotePosition, leftNoteDirection, 0);

            var rightNotePosition = new Vector2(EntityPosition.X + 8 - _spriteNote.SourceRectangle.Width / 2f, EntityPosition.Y - 16 - _spriteNote.SourceRectangle.Height / 2f);
            var rightNoteDirection = new Vector2(0.4f, -1.0f);
            DrawNote(spriteBatch, rightNotePosition, rightNoteDirection, _cycleTime / 2);
        }

        private void DrawNote(SpriteBatch spriteBatch, Vector2 position, Vector2 direction, int timeOffset)
        {
            if (_noteCount < timeOffset ||
                !_isSinging && (int)((_noteCount - timeOffset) / _cycleTime + 1) * _cycleTime + timeOffset > _noteEndTime)
                return;

            var time = (_noteCount + timeOffset) % _cycleTime;
            position += direction * time * 0.02f + new Vector2(-direction.X, direction.Y) * (float)Math.Sin(time * 0.015) * 1.25f;

            var transparency = 1.0f;
            if (time > _cycleTime - 100)
                transparency = (_cycleTime - time) / 100f;
            else if (time < 100)
                transparency = time / 100;

            DrawHelper.DrawNormalized(spriteBatch, _spriteNote, position, Color.White * transparency);
        }
    }
}
