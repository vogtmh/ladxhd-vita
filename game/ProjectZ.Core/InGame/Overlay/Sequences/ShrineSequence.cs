using Microsoft.Xna.Framework;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.SaveLoad;

namespace ProjectZ.InGame.Overlay.Sequences
{
    class ShrineSequence : GameSequence
    {
        public ShrineSequence()
        {
            _sequenceWidth = 160;
            _sequenceHeight = 144;
            _usePixelGrid = true;
            Sprites.Add(new SeqSprite("shrine", new Vector2(0, 0), 0));
        }

        public override void Update()
        {
            base.Update();
            if (!Game1.GameManager.DialogIsRunning() && 
                (ControlHandler.ButtonReleased(CButtons.Start) || 
                ControlHandler.ButtonPressed(ControlHandler.CancelButton) || 
                ControlHandler.ButtonPressed(ControlHandler.ConfirmButton)))
            {
                Game1.GameManager.InGameOverlay.CloseOverlay();
                AchievementManager.Earn(76);
            }
        }
    }
}
