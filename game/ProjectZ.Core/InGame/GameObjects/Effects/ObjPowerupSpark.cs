using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;

namespace ProjectZ.InGame.GameObjects.Effects
{
    internal class ObjPowerupSparks : GameObject
    {
        // The center point where the sparks converge relative to Link's EntityPosition. 
        private static readonly Vector2 _convergeOffset = new Vector2(-4, -28);

        // Distance each spark starts from the convergence point.
        private int _spread = 24;

        // The speed the sparks move towards the convergence point.
        private float _moveSpeed = 1.25f;

        private readonly ObjSwordShotSpark[] _sparks = new ObjSwordShotSpark[4];
        private readonly bool[] _arrived = new bool[4];

        private readonly Vector2 _endPosition;
        private bool _playedRun;

        public ObjPowerupSparks(Map.Map map, Vector2 linkPosition) : base(map)
        {
            // The postion of where the sparks converge.
            _endPosition = linkPosition + _convergeOffset;

            // The position of where each spark starts.
            var corners = new[]
            {
                new Vector2(_endPosition.X - _spread, _endPosition.Y - _spread),
                new Vector2(_endPosition.X + _spread, _endPosition.Y - _spread),
                new Vector2(_endPosition.X - _spread, _endPosition.Y + _spread),
                new Vector2(_endPosition.X + _spread, _endPosition.Y + _spread),
            };
            // Create the four sparks.
            for (var i = 0; i < corners.Length; i++)
            {
                _sparks[i] = new ObjSwordShotSpark(map, (int)corners[i].X, (int)corners[i].Y, 0, 0);
                map.Objects.SpawnObject(_sparks[i]);
                _sparks[i].Animator.Pause();
            }
            // Add an update component to move the sparks.
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            map.Objects.RegisterFreezePersistObject(this);
        }

        private void Update()
        {
            // Assume all sparks arrived already.
            var allArrived = true;

            // Check if each individual spark actually arrived yet.
            for (var i = 0; i < _sparks.Length; i++)
            {
                if (!_arrived[i])
                    _arrived[i] = MoveTowardsTarget(_sparks[i]);
                allArrived &= _arrived[i];
            }
            // Exit early if they are still traveling.
            if (!allArrived)
                return;

            // Start up their animation so they explode.
            if (!_playedRun)
            {
                _playedRun = true;
                for (var i = 0; i < _sparks.Length; i++)
                    _sparks[i].Animator.Play("run");
            }
            // The animator system is frozen with the world, so tick these manually.
            var stillPlaying = false;
            for (var i = 0; i < _sparks.Length; i++)
            {
                _sparks[i].Animator.Update();
                stillPlaying |= _sparks[i].Animator.IsPlaying;
            }
            if (stillPlaying)
                return;

            // Remove the sparks and this object governing them.
            for (var i = 0; i < _sparks.Length; i++)
                Map.Objects.DeleteObjects.Add(_sparks[i]);
            Map.Objects.DeleteObjects.Add(this);
        }

        private bool MoveTowardsTarget(ObjSwordShotSpark spark)
        {
            // Calculate the spark's position.
            var direction = _endPosition - spark.EntityPosition.Position;
            var distance = direction.Length();
            var step = _moveSpeed * Game1.TimeMultiplier;

            // Close enough to snap into place this frame.
            if (distance <= step)
            {
                // Return that the spark made it to its destination.
                spark.EntityPosition.Set(_endPosition);
                return true;
            }
            // Update the position of the spark.
            direction.Normalize();
            spark.EntityPosition.Set(spark.EntityPosition.Position + direction * step);
            return false;
        }
    }
}
