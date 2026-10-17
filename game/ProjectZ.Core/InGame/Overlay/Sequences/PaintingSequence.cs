using Microsoft.Xna.Framework;
using ProjectZ.InGame.Controls;

namespace ProjectZ.InGame.Overlay.Sequences
{
    class PaintingSequence : GameSequence
    {
        private float closeTimer = 1000;

        public PaintingSequence()
        {
            _sequenceWidth = 160;
            _sequenceHeight = 144;
            _usePixelGrid = true;
            _textBoxOffset = false;

            // background
            Sprites.Add(new SeqSprite("alligator_painting", new Vector2(0, 0), 0));
        }

        public override void Update()
        {
            base.Update();

            closeTimer -= Game1.DeltaTime;

            if (ControlHandler.ButtonPressed(CButtons.Start) || 
                !Game1.GameManager.DialogIsRunning() && closeTimer <= 0 && 
                (ControlHandler.ButtonPressed(ControlHandler.CancelButton) || 
                ControlHandler.ButtonPressed(ControlHandler.ConfirmButton)))
            {
                closeTimer = 1000;
                Game1.GameManager.InGameOverlay.CloseOverlay();
            }
        }
    }
}
