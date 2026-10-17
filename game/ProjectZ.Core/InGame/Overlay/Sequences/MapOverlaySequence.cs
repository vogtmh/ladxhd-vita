using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Map;

namespace ProjectZ.InGame.Overlay.Sequences
{
    class MapOverlaySequence : GameSequence
    {
        private MapOverlay _mapOverlay;
        private new float _scale => Game1.GameManager.InGameOverlay.Scale;

        public MapOverlaySequence()
        {
            _sequenceWidth = 144;
            _sequenceHeight = 144;
            _usePixelGrid = true;

            _mapOverlay = new MapOverlay(_sequenceWidth, _sequenceHeight, 0, true);
            _mapOverlay.Load();
            _mapOverlay.IsSelected = true;
        }

        public override void OnStart()
        {
            base.OnStart();

            _mapOverlay.OnFocus();
        }

        public override void Update()
        {
            base.Update();

            _mapOverlay.UpdateRenderTarget();
            _mapOverlay.Update();

            // Overlay can be closed if dialog box is not visible.
            if (ControlHandler.ButtonPressed(ControlHandler.CancelButton) && !Game1.GameManager.InGameOverlay.TextboxOverlay.IsOpen)
            {
                Game1.GameManager.InGameOverlay.CloseOverlay();
                MapManager.ObjLink.ManboTeleport = false;
            }
        }

        public override void DrawRT(SpriteBatch spriteBatch)
        {
            _mapOverlay.DrawRenderTarget(spriteBatch);
            Game1.Graphics.GraphicsDevice.SetRenderTarget(null);
        }

        public override void Draw(SpriteBatch spriteBatch, float transparency)
        {
            spriteBatch.End();

            // Use unscaled logical dimensions - the overlay itself handles UiScale.
            var width = _sequenceWidth;
            var height = _sequenceHeight;

            _mapOverlay.Draw(spriteBatch, new Rectangle(
                (int)(Game1.WindowWidth / 2 - width * _scale / 2),
                (int)(Game1.WindowHeight / 2 - height * _scale / 2),
                width, height),
                Color.White * transparency, Game1.GetMatrix);

            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null, null, null);

            // Draw the close + button text.
            var selectStr = "";
            if (ControlHandler.LastKeyboardDown && ControlHandler.ButtonDictionary[ControlHandler.CancelButton].Keys.Length > 0)
                selectStr = ControlHandler.ButtonDictionary[ControlHandler.CancelButton].Keys[0].ToString();

            if (!ControlHandler.LastKeyboardDown && ControlHandler.ButtonDictionary[ControlHandler.CancelButton].Buttons.Length > 0)
                selectStr = ControlHandler.GetButtonName(ControlHandler.ButtonDictionary[ControlHandler.CancelButton].Buttons[0]);

            var inputHelper = selectStr + ": " + Game1.LanguageManager.GetString("map_overlay_close", "error");

            DrawHelper.DrawString(spriteBatch, inputHelper, new Vector2(8 * _scale, Game1.WindowHeight - 16 * _scale), InterfaceElement.MainTextColor * transparency, 0, Vector2.Zero, _scale, SpriteEffects.None, 0);

            // When navigating the map, get the currently selected map position.
            var nodeSelected = _mapOverlay.SelectionPosition;

            // If we're in map mode and one of the dungeons are selected.
            if (Game1.GameManager.InGameOverlay.TryGetMapTeleportTarget(nodeSelected, out _))
            {
                // Get the selected dungeon and check if the instrument has been collected.
                int dungeonLevel = Game1.GameManager.InGameOverlay.TeleportMap[nodeSelected].Level - 1;
                var instrument = Game1.GameManager.GetItem("instrument" + dungeonLevel);
                var hasInstrument = instrument != null && instrument.Count > 0;
                var isManboPond = dungeonLevel < 0 && MapManager.ObjLink.ManboTeleport;

                // If instrument has not been collected don't draw the text.
                if (dungeonLevel < 99 && !hasInstrument && !isManboPond)
                    return;

                // Reverse the label "X" with label "Y" if the user has a need for this.
                var button = ControlHandler.ReverseTeleportLabels ? CButtons.Y : CButtons.X;

                // Get the correct button to display next to the text.
                var teleStart = "";
                if (ControlHandler.LastKeyboardDown && ControlHandler.ButtonDictionary[CButtons.X].Keys.Length > 0)
                    teleStart = ControlHandler.ButtonDictionary[button].Keys[0].ToString();
                if (!ControlHandler.LastKeyboardDown && ControlHandler.ButtonDictionary[CButtons.X].Buttons.Length > 0)
                    teleStart = ControlHandler.GetButtonName(ControlHandler.ButtonDictionary[button].Buttons[0]);

                // Set up the string to display.
                var teleString = teleStart + ": " + Game1.LanguageManager.GetString("overlay_teleport", "error");
                var teleTextSize = DrawHelper.MeasureString(teleString);
                var teleDrawPos = new Vector2(Game1.WindowWidth - (teleTextSize.X + 6) * _scale, Game1.WindowHeight - 16 * _scale);

                // Draw the teleport button and label.
                DrawHelper.DrawString(spriteBatch, teleString, teleDrawPos, InterfaceElement.MainTextColor, 0, Vector2.Zero, _scale, SpriteEffects.None, 0);
            }
        }
    }
}
