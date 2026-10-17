using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.NPCs
{
    internal class ObjPersonNew : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyCollisionComponent _collisionComponent;
        private readonly BodyComponent _body;
        private readonly BodyDrawComponent _drawComponent;
        private readonly BodyDrawShadowComponent _shadowComponent;
        private readonly CSprite _sprite;
        private readonly HittableComponent _hittableComponent;
        private readonly InteractComponent _interactionComponent;

        private readonly string _animationId;
        private readonly string _animationName;
        private readonly string _dialogId;
        private string _currentAnimation;
        private string _spawnCondition;
        private float _lookCounter;
        private int _lookRange;
        private bool _directionMode = true;

        private bool _isMoving;
        private bool _binaryFacing;
        private Vector2 _targetPosition;
        private float _moveSpeed;

        private float _fadeTime;
        private float _fadeCounter;

        private float _jumpTime;
        private float _jumpCounter;

        private int _lastFieldTime;
        private bool _textboxSimulated;

        // Track specific NPCs created from this object.
        private bool _isNpcRichard;
        private bool _isNpcTarin;
        private bool _isNpcZora;

        private bool _despawnTarin;

        private int _zoraLives = EnemyLives.RiverZora;
        private int _zoraDropIndex = 2;

        private Rectangle _objectField;

        struct MoveStep
        {
            public float MoveSpeed;
            public Vector2 Offset;
        }
        private Queue<MoveStep> _nextMoveStep = new Queue<MoveStep>();

        public BodyComponent Body => _body;
        public Animator Animator => _animator;

        public ObjPersonNew() : base("person") { }

        public ObjPersonNew(Map.Map map, int posX, int posY, string spawnCondition, string animationId, string dialogId, string animationName, Rectangle bodyRectangle, bool binaryFacing = false, int lookrange = 32) : base(map)
        {
            if (string.IsNullOrEmpty(animationId))
            {
                IsDead = true;
                return;
            }
            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(bodyRectangle.X - bodyRectangle.Width / 2, bodyRectangle.Y - bodyRectangle.Height, bodyRectangle.Width, bodyRectangle.Height);

            _lookRange = lookrange;
            _spawnCondition = spawnCondition;
            _animationId = animationId;
            _dialogId = dialogId;
            _animationName = animationName;
            _animator = AnimatorSaveLoad.LoadAnimator("NPCs/" + _animationId);
            _binaryFacing = binaryFacing;

            // Track if it's certain NPCs.
            _isNpcRichard = _animationId == "npc_frog_boy";
            _isNpcTarin   = _animationId == "tarin";
            _isNpcZora    = _animationId == "npc_zora";

            if (_animator == null)
            {
                IsDead = true;
                return;
            }
            _sprite = new CSprite(EntityPosition);

            var bodyOffsetX = bodyRectangle.X - bodyRectangle.Width / 2;
            var bodyOffsetY = bodyRectangle.Y - bodyRectangle.Height;

            _body = new BodyComponent(EntityPosition, bodyOffsetX, bodyOffsetY, bodyRectangle.Width, bodyRectangle.Height, bodyRectangle.Height) { Gravity = -0.15f };

            AddComponent(BaseAnimationComponent.Index, new AnimationComponent(_animator, _sprite, Vector2.Zero));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(CollisionComponent.Index, _collisionComponent = new BodyCollisionComponent(_body, Values.CollisionTypes.Normal | Values.CollisionTypes.PushIgnore | Values.CollisionTypes.NPC));
            AddComponent(DrawComponent.Index, _drawComponent = new BodyDrawComponent(_body, _sprite, Values.LayerPlayer) { WaterOutline = false });
            AddComponent(DrawShadowComponent.Index, _shadowComponent = new BodyDrawShadowComponent(_body, _sprite));
            AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(OnKeyChange));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));

            // Don't add an interact component unless the NPC has dialog.
            if (!string.IsNullOrEmpty(_dialogId))
                AddComponent(InteractComponent.Index, _interactionComponent = new InteractComponent(_body.BodyBox, Interact));

            // If spawning Tarin in bed, do not cast shadows or it looks funky.
            if (_isNpcTarin && _animationName == "sleep")
                _shadowComponent.IsActive = false;

            // If it's the hidden Zora in Animal Village, it's able to be killed.
            if (_isNpcZora)
            {
                Tags = Values.GameObjectTag.Enemy;

                _aiComponent = new AiComponent();
                _aiComponent.States.Add("npc", new AiState());
                _aiComponent.ChangeState("npc");

                _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _zoraLives, _zoraDropIndex)
                {
                    HitMultiplierX = 1.5f,
                    HitMultiplierY = 1.5f,
                    FlameOffset = new Point(0, 2)
                };
                _damageState.IsActive = true;

                var hittableBox = new CBox(EntityPosition, -8, -16, 16, 16, 4);

                AddComponent(AiComponent.Index, _aiComponent);
                AddComponent(HittableComponent.Index, _hittableComponent = new HittableComponent(hittableBox, OnHit) { BoomerangMultiplier = true });
            }
            // Hides the stick Tarin uses to knock down the honeycomb.
            if (_animationName == "pHidden")
            {
                SetVisibility(false);
            }
            // If an animation was passed in then play that animation.
            else if (!string.IsNullOrEmpty(_animationName))
            {
                _directionMode = false;
                _animator.Play(_animationName);
            }
            // Otherwise just make the NPC face forward.
            else
            {
                _animator.Play("stand_3");
            }
            // If the NPC has a spawn condition.
            if (!string.IsNullOrEmpty(_spawnCondition))
            {
                // See if the condition is met and if not hide the NPC by setting it to inactive.
                var spawnValue = Game1.GameManager.SaveManager.GetString(_spawnCondition);
                if (spawnValue != "1")
                    SetActive(false);
            }
            // Sample inside the sprite rather than on its bottom edge, which can land on the next field.
            var fieldPoint = new Vector2(EntityPosition.X, EntityPosition.Y - 8);
            _lastFieldTime = Map.GetUpdateState(fieldPoint);
            _objectField   = Map.GetField(fieldPoint);
        }

        private void SetActive(bool isActive)
        {
            _collisionComponent.IsActive = isActive;
            _interactionComponent.IsActive = isActive;
            _drawComponent.IsActive = isActive;
            _shadowComponent.IsActive = isActive;

            if (_isNpcZora)
            {
                _hittableComponent.IsActive = isActive;
                _damageState.IsActive = isActive;
            }
        }

        private bool TrySimulateTextBoxOpen()
        {
            // Textboxes can't pause everything in Richard's Villa or the achievement timer
            // will fall out of sync. So we can simulate pausing with an animation freeze.
            if (Game1.GameManager.InGameOverlay.TextboxOverlay.IsOpen)
            {
                if (!_textboxSimulated)
                {
                    _animator.Pause();
                    _textboxSimulated = true;
                }
                return true;
            }
            // No sense in running this stuff every frame.
            if (_textboxSimulated)
            {
                _animator.Continue();
                _textboxSimulated = false;
            }
            return false;
        }

        private void Update()
        {
            // When it enters death state after being attacked, stop updating the object.
            if (_isNpcZora && _aiComponent.CurrentStateId != "npc")
                return;

            // Manually pause Richard since his dialog does not freeze everything.
            if (_isNpcRichard && TrySimulateTextBoxOpen())
                return;

            // Move the NPC if it's been told to move.
            UpdateMoving();

            // Fade out the NPC if "_fadeTime" was set. 
            UpdateFade();

            // Make the NPC jump in place if "_jumpTime" was set.
            UpdateJumpMode();

            // Update the NPC's facing direction.
            UpdateLookAnimation();

            // When an animation finishes playing, store a key-value pair as (animation name + "Finished") with value of "1".
            if (_currentAnimation != null && !_animator.IsPlaying)
            {
                _currentAnimation = null;
                Game1.GameManager.SaveManager.SetString(_dialogId + "Finished", "1");
            }

            // Despawn Tarin in the forest after sprinkling powder on the raccoon.
            if (_isNpcTarin && _dialogId == "tarin_healed")
            {
                // Classic Camera: Despawn on field change.
                if (Camera.ClassicMode)
                {
                    // Despawn once a field scroll settles on a field other than this one.
                    if (MapManager.ObjLink.FieldTransition(_objectField))
                        Map.Objects.DeleteObjects.Add(this);
                }
                // Modern Camera: Despawn when 3 fields away.
                else
                {
                    // Get the map state to determine the number of fields away we are.
                    var updateState = Map.GetUpdateState(EntityPosition.Position);

                    // Despawn when moving 3 fields away in Modern Camera.
                    if (!_despawnTarin && _lastFieldTime < updateState)
                    {
                        _despawnTarin = true;
                        _fadeCounter = _fadeTime = 750;
                    } 
                }
            }
        }

        private void UpdateLookAnimation()
        {
            // Only run the timer while the NPC is actually free to turn.
            if (_isMoving || !_directionMode)
                return;

            _lookCounter -= Game1.DeltaTime;

            // Delay turning towards a new facing direction for a quarter of a second.
            if (_lookCounter < 0)
            {
                _lookCounter += 250;
                ApplyLookDirection();
            }
        }

        private void ApplyLookDirection()
        {
            // The NPC can only face in two directions and not four.
            if (_binaryFacing)
            {
                // Get the distance between Link and the NPC as a vector2.
                var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
                var playerDistance = playerDirection.Length();

                // Default facing left.
                var direction = 0;

                // If the player's X value is greater than the NPC's X value face right.
                if (playerDistance < _lookRange)
                    if (MapManager.ObjLink.EntityPosition.X > EntityPosition.X)
                        direction = 2;

                // Update the facing direction.
                _animator.Play("stand_" + direction);
            }
            // If the NPC has four facing directions, use the vector2 method instead.
            else
            {
                // Get the distance between Link and the NPC as a vector2.
                var playerDistance = new Vector2(
                    MapManager.ObjLink.EntityPosition.X - (EntityPosition.X),
                    MapManager.ObjLink.EntityPosition.Y - (EntityPosition.Y - 4));

                // Default facing down.
                var dir = 3;

                // Rotate in the direction of the player.
                if (playerDistance.Length() < _lookRange)
                    dir = AnimationHelper.GetDirection(playerDistance);

                // Look at the player.
                if (_currentAnimation == null)
                {
                    var animationIndex = _animator.GetAnimationIndex("stand_" + dir);
                    if (animationIndex >= 0)
                        _animator.Play(animationIndex);
                    else
                        _animator.Play("stand_" + (playerDistance.Y < 0 ? "1" : "3"));
                }
            }
        }

        private bool Interact()
        {
            // Always turn to face Link before the dialog opens.
            if (!_isMoving && _directionMode)
            {
                // Reset the timer and apply the facing direction.
                _lookCounter = 250;
                ApplyLookDirection();
            }
            // Start the dialog.
            Game1.GameManager.StartDialogPath(_dialogId);
            return true;
        }

        private void SetMovingString(bool state)
        {
            Game1.GameManager.SaveManager.SetString(_dialogId + "Moving", state ? "1" : "0");
        }

        private void UpdateMoving()
        {
            if (!_isMoving)
                return;

            // Move towards the target position.
            var targetDistance = _targetPosition - EntityPosition.Position;
            if (targetDistance.Length() > _moveSpeed * Game1.TimeMultiplier)
            {
                targetDistance.Normalize();
                _body.VelocityTarget = targetDistance * _moveSpeed;

                if (_currentAnimation == null)
                {
                    var dir = AnimationHelper.GetDirection(targetDistance);
                    _animator.Play("walk_" + dir);
                }
            }
            // Movement has finished.
            else
            {
                _lookCounter = 0;
                _body.VelocityTarget = Vector2.Zero;
                EntityPosition.Set(_targetPosition);

                if (_nextMoveStep.Count > 0)
                    DequeueMove();
                else
                {
                    _isMoving = false;
                    SetMovingString(false);
                    _interactionComponent.IsActive = true;
                }
            }
        }

        private void DequeueMove()
        {
            var move = _nextMoveStep.Dequeue();
            _moveSpeed = move.MoveSpeed;
            _targetPosition = EntityPosition.Position + move.Offset;
        }

        private void UpdateFade()
        {
            if (_fadeTime < 0)
            {
                _fadeCounter += Game1.DeltaTime;
                if (_fadeCounter >= -_fadeTime)
                    _fadeCounter = -_fadeTime;

                var percentage = _fadeCounter / -_fadeTime;
                _sprite.Color = Color.White * percentage;
                _shadowComponent.Transparency = percentage;

                if (_fadeCounter >= -_fadeTime)
                    _fadeTime = 0;
            }
            else if (_fadeTime > 0)
            {
                _fadeCounter -= Game1.DeltaTime;

                if (_fadeCounter <= 0)
                    Map.Objects.DeleteObjects.Add(this);
                else
                {
                    var percentage = _fadeCounter / _fadeTime;
                    _sprite.Color = Color.White * percentage;
                    _shadowComponent.Transparency = percentage;
                }
            }
        }

        private void UpdateJumpMode()
        {
            if (_jumpTime <= 0)
                return;

            _jumpCounter -= Game1.DeltaTime;
            if (_jumpCounter < 0)
            {
                _jumpCounter += _jumpTime;
                _body.Velocity.Z = 1.125f;
            }
        }

        public void DisableRotating()
        {
            _directionMode = false;
        }

        private void SetVisibility(bool visible)
        {
            _sprite.IsVisible = visible;
            _shadowComponent.IsActive = visible;
        }

        private void OnKeyChange()
        {
            if (!string.IsNullOrEmpty(_spawnCondition))
            {
                var spawnValue = Game1.GameManager.SaveManager.GetString(_spawnCondition);
                if (spawnValue == "1")
                    SetActive(true);
            }

            // start new animation?
            var animationString = _dialogId + "Animation";
            var animationValues = Game1.GameManager.SaveManager.GetString(animationString);
            if (animationValues != null)
            {
                if (animationValues == "-")
                {
                    _currentAnimation = null;
                }
                else if (animationValues != "")
                {
                    SetVisibility(true);
                    _currentAnimation = animationValues;
                    _animator.Play(_currentAnimation);
                }
                else
                {
                    SetVisibility(false);
                    _currentAnimation = null;
                }

                Game1.GameManager.SaveManager.RemoveString(animationString);
            }

            // start moving?
            var moveString = _dialogId + "Move";
            var moveValue = Game1.GameManager.SaveManager.GetString(moveString);
            if (moveValue != null)
            {
                // offsetX; offsetY; movementSpeed
                var split = moveValue.Split(',');
                if (split.Length == 3)
                {
                    var offsetX = int.Parse(split[0]);
                    var offsetY = int.Parse(split[1]);
                    var moveSpeed = float.Parse(split[2], CultureInfo.InvariantCulture);

                    _nextMoveStep.Enqueue(new MoveStep() { MoveSpeed = moveSpeed, Offset = new Vector2(offsetX, offsetY) });

                    if (!_isMoving)
                    {
                        _isMoving = true;
                        DequeueMove();
                        SetMovingString(true);
                        _body.CollisionTypes = Values.CollisionTypes.None;
                        _interactionComponent.IsActive = false;
                    }
                }

                Game1.GameManager.SaveManager.RemoveString(moveString);
            }

            // start jumping?
            var jumpString = _dialogId + "Jump";
            var jumpValue = Game1.GameManager.SaveManager.GetString(jumpString);
            if (!string.IsNullOrEmpty(jumpValue))
            {
                var split = jumpValue.Split(',');
                if (split.Length == 1)
                {
                    _jumpTime = int.Parse(jumpValue);
                }
                else
                {
                    // jump one time
                    _body.Velocity.Z = float.Parse(split[0], CultureInfo.InvariantCulture);
                    _body.Gravity = float.Parse(split[1], CultureInfo.InvariantCulture);
                }
                Game1.GameManager.SaveManager.RemoveString(jumpString);
            }

            // change look range?
            var rangeString = _dialogId + "Range";
            var rangeValue = Game1.GameManager.SaveManager.GetString(rangeString);
            if (!string.IsNullOrEmpty(rangeValue))
            {
                _lookRange = int.Parse(rangeValue);
                Game1.GameManager.SaveManager.RemoveString(rangeString);
            }

            // start fading away?
            var fadeString = _dialogId + "Fade";
            var fadeValue = Game1.GameManager.SaveManager.GetString(fadeString);
            if (!string.IsNullOrEmpty(fadeValue))
            {
                // negative value -> fade in
                // positive value -> fade out
                _fadeTime = int.Parse(fadeValue);
                _fadeCounter = _fadeTime;
                UpdateFade();

                Game1.GameManager.SaveManager.RemoveString(fadeString);
            }
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // The only thing incapable of killing the NPC is the sword.
            if ((hitType & (HitType.AnySword)) != 0)
                return Values.HitCollision.None;

            // Prevent the boots from pushing it out of it's position.
            if ((hitType & (HitType.PegasusBootsPush)) != 0)
                return Values.HitCollision.None;

            // While this is currently unused since only the Zora in Animal village
            // is killable, this will prevent moving the NPC once it's taken a hit.
            if (_isMoving)
            {
                _isMoving = false;
                SetMovingString(false);
                _body.VelocityTarget = Vector2.Zero;
            }
            // Return the hit. Just about everything kills the Zora except the sword.
            return _damageState.OnHit(originObject, direction, hitType, damage, pieceOfPower);
        }
    }
}
