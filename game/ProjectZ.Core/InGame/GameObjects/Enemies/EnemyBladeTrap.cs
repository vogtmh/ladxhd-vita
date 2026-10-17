using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyBladeTrap : GameObject
    {
        private readonly AiComponent _aiComponent;

        private readonly RectangleF[] _collisionRectangles = new RectangleF[4];
        private readonly Vector2[] _directions = { new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1), new Vector2(0, 1) };
        private readonly int[] _maxPosition = new int[4];
        private readonly int[] _travelDistance = new int[4];

        private const int MovementStep = 2;

        private Rectangle _field;
        private float _cTimer;
        private float _cReset = 225;

        // In Level 2 dungeon, the room left from the dark room has traps that stop just before the doorway. After a condition
        // is met, they then travel the full distance. I'm not sure what that condition is, but in this port it's when they 
        // manage to return to their original position at least once. This only affects classic camera.
        private int _top;
        private int _bottom;
        private bool _useWarning;
        private bool _warningShot;

        private Vector2 _startPosition;
        private float _movePosition;
        private int _moveDir;

        // How far the trap detects the player from it's center. The original game detected the player
        // from 5 pixels away on each side. So we want the range to be 26 pixels (5 + 8 + 8 + 5 = 26 pixels).
        const int TriggerHalfWidth = 13;

        public EnemyBladeTrap() : base("bladeTrap") { }

        public EnemyBladeTrap(Map.Map map, int posX, int posY, int left, int right, int top, int bottom, bool warning) : base(map)
        {
            Tags = Values.GameObjectTag.Damage;

            EntityPosition = new CPosition(posX, posY, 0);
            ResetPosition  = new CPosition(posX, posY, 0);
            _startPosition = new Vector2(posX, posY);
            CanReset = true;
            OnReset += Reset;

            // Used for warning shot: trap doesn't travel full distance up/down for first movement.
            _top = top;
            _bottom = bottom;
            _useWarning = warning;

            _cTimer = _cReset;

            var animator = AnimatorSaveLoad.LoadAnimator("Enemies/bladetrap");
            animator.Play("idle");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(animator, sprite, new Vector2(0, 0));

            var padding = 2;
            var width = 16 + 2 * padding;
            var height = 16 + 2 * padding;

            _maxPosition[0] = left * 16;
            _maxPosition[1] = right * 16;
            _travelDistance[0] = left * 16;
            _travelDistance[1] = right * 16;

            // Used to limit movement for warning shot.
            int extra = 0;
            if (Camera.ClassicMode && warning) 
                extra = 8;

            _maxPosition[2] = top * 16 - extra;
            _maxPosition[3] = bottom * 16 - extra;
            _travelDistance[2] = top * 16 - extra;
            _travelDistance[3] = bottom * 16 - extra;

            _collisionRectangles[0] = new RectangleF(posX - left * 16 - 16, posY + 8 - TriggerHalfWidth, left * 16 + 16, TriggerHalfWidth * 2);
            _collisionRectangles[1] = new RectangleF(posX + 16, posY + 8 - TriggerHalfWidth, right * 16 + 16, TriggerHalfWidth * 2);
            _collisionRectangles[2] = new RectangleF(posX + 8 - TriggerHalfWidth, posY - top * 16 - 16, TriggerHalfWidth * 2, top * 16 + 16);
            _collisionRectangles[3] = new RectangleF(posX + 8 - TriggerHalfWidth, posY + 16, TriggerHalfWidth * 2, bottom * 16 + 16);

            _field = Map.GetField(posX, posY);

            var stateWait = new AiState();
            stateWait.Trigger.Add(new AiTriggerCountdown(350, null, () => _aiComponent.ChangeState("back")));
            var stateCooldown = new AiState();
            stateCooldown.Trigger.Add(new AiTriggerCountdown(500, null, () => _aiComponent.ChangeState("idle")));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", new AiState(UpdateIdle));
            _aiComponent.States.Add("snap", new AiState(UpdateSnap));
            _aiComponent.States.Add("wait", stateWait);
            _aiComponent.States.Add("back", new AiState(UpdateMoveBack));
            _aiComponent.States.Add("cooldown", stateCooldown);
            _aiComponent.ChangeState("idle");

            var bodyBox   = new CBox(EntityPosition, 0, 0, 0, 16, 16, 4);
            var damageBox = new CBox(EntityPosition, 5, 5, 0,  6,  6, 4);

            AddComponent(PushableComponent.Index, new PushableComponent(bodyBox, OnPush));
            AddComponent(DamageFieldComponent.Index, new DamageFieldComponent(damageBox, HitType.Enemy, 4));
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(HittableComponent.Index, new HittableComponent(bodyBox, OnHit));
            AddComponent(DrawComponent.Index, new DrawCSpriteComponent(sprite, Values.LayerPlayer));
        }

        public override void Reset()
        {
            _cTimer = _cReset;
            _movePosition = 0;
            UpdatePosition();
            _aiComponent.ChangeState("idle");
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            return true;
        }

        private Values.HitCollision OnHit(GameObject originObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            return Values.HitCollision.RepellingParticle | Values.HitCollision.SpawnFire;
        }

        private int PredictMaxMove(int dirIndex)
        {
            int step = 1;
            int max = _travelDistance[dirIndex];
            Vector2 direction = _directions[dirIndex];

            // Sweep forward until max distance
            for (int dist = 0; dist <= max; dist += step)
            {
                float x = _startPosition.X + direction.X * dist;
                float y = _startPosition.Y + direction.Y * dist;

                // Check collision at this position
                Box block = Box.Empty;
                Box box = new Box(x, y, 0, 16, 16, 4);
                if (Map.Objects.Collision(box, Box.Empty, Values.CollisionTypes.Normal, 0, 0, ref block))
                    return dist;
            }
            return max;
        }

        private void UpdateIdle()
        {
            // Do not trigger unless on the same field as the trap.
            if (Camera.ClassicMode)
            {
                if (!_field.Contains(MapManager.ObjLink.CenterPosition))
                    return;
                else
                    _cTimer -= Game1.DeltaTime;

                if (_cTimer > 0)
                    return;
            }
            // Loop through the directions the trap can move.
            for (int i = 0; i < _directions.Length; i++)
            {
                // Check if a direction intercepts Link.
                if (_collisionRectangles[i].Intersects(MapManager.ObjLink.BodyRectangle))
                {
                    // Predict the amount of pixels it can move.
                    int predicted = PredictMaxMove(i);

                    // The predicted value must exceed it's movement step.
                    if (predicted < MovementStep)
                        return;

                    // Set the maximum position it can move.
                    _maxPosition[i] = predicted;

                    // Play the movement sound effect and start moving.
                    Game1.AudioManager.PlaySoundEffect("D378-10-0A");
                    _aiComponent.ChangeState("snap");
                    _moveDir = i;
                }
            }
        }

        private void UpdateSnap()
        {
            // Move the trap towards Link's position.
            _movePosition += MovementStep * Game1.TimeMultiplier;

            // Reached the end of its destination.
            if (_movePosition >= _maxPosition[_moveDir])
            {
                // Play the collision sound effect and snap the trap into place.
                _movePosition = _maxPosition[_moveDir];
                _aiComponent.ChangeState("wait");
                Game1.AudioManager.PlaySoundEffect("D360-07-07");
            }
            // Update the trap's position until it reaches it's max movement value.
            UpdatePosition();
        }

        private void UpdateMoveBack()
        {
            // Move the trap back into it's original position.
            if (_movePosition > 0)
                _movePosition -= 0.5f * Game1.TimeMultiplier;

            // When it reaches it's destination, snap it into place.
            else
            {
                _movePosition = 0;
                _aiComponent.ChangeState("cooldown");

                // If it fired it's warning shot, fix the travel distances.
                if (_useWarning && !_warningShot)
                {
                    _warningShot = true;
                    _travelDistance[2] = _top * 16;
                    _travelDistance[3] = _bottom * 16;
                }
            }
            // Update the trap's position until it reaches it's original position.
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            // Update the position of the trap.
            EntityPosition.Set(_startPosition + _directions[_moveDir] * _movePosition);
        }
    }
}
