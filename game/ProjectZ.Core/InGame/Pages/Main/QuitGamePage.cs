using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    class QuitGamePage : InterfacePage
    {
        public QuitGamePage(int width, int height)
        {
            var margin = 6;

            var hLayout = new InterfaceListLayout() { Size = new Point(150, 25), Margin = new Point(0, 2), Selectable = true, HorizontalMode = true };
            hLayout.AddElement(new InterfaceButton(new Point(74, 25), Point.Zero, "game_menu_exit_yes", OnClickYes) { Margin = new Point(2, 0) });
            hLayout.AddElement(new InterfaceButton(new Point(74, 25), Point.Zero, "game_menu_exit_no", OnClickNo) { Margin = new Point(2, 0) });

            var yesNoLayoutInside = new InterfaceListLayout() { Size = new Point(150 + margin * 2, 55), Selectable = true };
            yesNoLayoutInside.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "game_menu_exit_header", new Point(150, 30), new Point(1, 2)) { TextColor = InterfaceElement.MainTextColor });
            yesNoLayoutInside.AddElement(hLayout);

            var yesNoLayout = new InterfaceListLayout() { Size = new Point(width, height), Selectable = true };
            yesNoLayout.AddElement(yesNoLayoutInside);

            PageLayout = yesNoLayout;
        }

        public override void Update(CButtons pressedButtons, GameTime gameTime)
        {
            if (ControlHandler.ButtonPressed(CButtons.Up)  | ControlHandler.ButtonDown(CButtons.Up) || 
                ControlHandler.ButtonPressed(CButtons.Down) || ControlHandler.ButtonDown(CButtons.Down))
                return;

            base.Update(pressedButtons, gameTime);

            if (ControlHandler.ButtonPressed(ControlHandler.CancelButton))
                Game1.UiPageManager.PopPage();
        }

        public override void OnLoad(Dictionary<string, object> intent)
        {
            PageLayout.Deselect(false);
            PageLayout.Select(InterfaceElement.Directions.Right, false);
        }

        public void OnClickNo(InterfaceElement element)
        {
            Game1.UiPageManager.PopPage();
        }

        public void OnClickYes(InterfaceElement element)
        {
            Game1.InProgress = false;

            if (Game1.GameManager.SaveManager.HistoryEnabled)
            {
                Game1.GameManager.SaveManager.RevertHistory();
                Game1.GameManager.SaveManager.DisableHistory();
            }
            if (GameSettings.Autosave)
            {
                SettingsSaveLoad.SaveSettings();
                SaveGameSaveLoad.SaveGame(Game1.GameManager, false);
                AchievementManager.Save();
            }
            MapManager.CameraOffset = Vector2.Zero;

            Game1.ScreenManager.ChangeScreen(Values.ScreenNameMenu);
        }
    }
}
