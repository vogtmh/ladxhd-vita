using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Base.Components.AI
{
    class AiDeepWaterState
    {
        private readonly BodyComponent _body;
        
        public delegate void OnDrownedFunction();
        public OnDrownedFunction OnDrown;

        public int FallTime = 100;

        private float _deepWaterCounter;
        private double _lastDeepWaterCollision;

        public AiDeepWaterState(BodyComponent body)
        {
            _body = body;
            _body.OnDeepWaterFunction = OnDeepWaterCollision;
        }

        public void OnDeepWaterCollision()
        {
            if (_lastDeepWaterCollision != Game1.TotalGameTimeLast)
                _deepWaterCounter = FallTime;

            _deepWaterCounter -= Game1.DeltaTime;

            _lastDeepWaterCollision = Game1.TotalGameTime;

            if (_deepWaterCounter < 0)
            {
                FallDeath();
            }
        }

        private void FallDeath()
        {
            // Play the splash sound effect.
            Game1.AudioManager.PlaySoundEffect("D360-14-0E");

            // Spawn the splash graphics effect.
            var fallAnimation = new ObjAnimator(_body.Owner.Map,
                (int)(_body.Position.X + _body.OffsetX + _body.Width / 2.0f),
                (int)(_body.Position.Y + _body.OffsetY + _body.Height / 2.0f),
                Values.LayerPlayer, "Particles/fishingSplash", "idle", true);
            _body.Owner.Map.Objects.SpawnObject(fallAnimation);

            _body.Owner.Map.Objects.DeleteObjects.Add(_body.Owner);

            // Fire the delegate when the drown happens.
            OnDrown?.Invoke();
        }
    }
}
