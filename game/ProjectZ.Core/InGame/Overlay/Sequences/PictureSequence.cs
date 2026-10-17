using Microsoft.Xna.Framework;
using ProjectZ.InGame.Controls;

namespace ProjectZ.InGame.Overlay.Sequences
{
    class PictureSequence : GameSequence
    {
        private float closeTimer = 1000;

        public PictureSequence()
        {
            _sequenceWidth = 103;
            _sequenceHeight = 128;
            _usePixelGrid = true;

            // background
            Sprites.Add(new SeqSprite("trade_picture", new Vector2(0, 0), 0));
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
                Game1.GameManager.StartDialogPath("close_picture");
            }
        }
    }
}
