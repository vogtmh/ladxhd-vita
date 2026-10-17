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
    class GameMenuPage : InterfacePage
    {
        public GameMenuPage(int width, int height)
        {
            // main layout
            var mainLayout = new InterfaceListLayout() { Size = new Point(width, height), Selectable = true };
            mainLayout.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "game_menu_header", 
                new Point(150, (int)(height * Values.MenuHeaderSize)), new Point(0, 0)) 
                { TextColor = InterfaceElement.MainTextColor });

            // Size = new Point(width, (int)(height * Values.MenuContentSize))
            var contentLayout = new InterfaceListLayout { AutoSize = true, Selectable = true };
            contentLayout.AddElement(new InterfaceButton(new Point(150, 25), Point.Zero, "game_menu_back_to_game", e => ClosePage()) { Margin = new Point(0, 2) });
            contentLayout.AddElement(new InterfaceButton(new Point(150, 25), Point.Zero, "game_menu_save_continue", OnClickSaveContinue) { Margin = new Point(0, 2) });
            contentLayout.AddElement(new InterfaceButton(new Point(150, 25), Point.Zero, "game_menu_settings", OnClickSettings) { Margin = new Point(0, 2) });
            contentLayout.AddElement(new InterfaceButton(new Point(150, 25), Point.Zero, "game_menu_exit_to_the_menu", OnClickBackToMenu) { Margin = new Point(0, 2) });
            contentLayout.AddElement(new InterfaceButton(new Point(150, 25), Point.Zero, "game_menu_exit_the_game", OnClickExitGame) { Margin = new Point(0, 2) });

            mainLayout.AddElement(contentLayout);
            mainLayout.AddElement(new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuFooterSize)) });

            PageLayout = mainLayout;
            PageLayout.Select(InterfaceElement.Directions.Top, false);
        }

        public override void OnLoad(Dictionary<string, object> intent)
        {
            Game1.AudioManager.PauseMusic();

            // select the "Back to Game" button
            PageLayout.Deselect(false);
            PageLayout.Select(InterfaceElement.Directions.Top, false);
        }

        public override void OnPop(Dictionary<string, object> intent)
        {
            Game1.AudioManager.ResumeMusic();
        }

        public override void Update(CButtons pressedButtons, GameTime gameTime)
        {
            base.Update(pressedButtons, gameTime);
            if (ControlHandler.ButtonPressed(CButtons.Start) || ControlHandler.ButtonPressed(ControlHandler.CancelButton))
                ClosePage();
        }

        private void ClosePage()
        {
            MapManager.ObjLink.DisableItems = true;
            MapManager.ObjLink.DisableItemCounter = 350;
            Game1.GameManager.InGameOverlay.CloseOverlay();
        }

        public void OnClickSaveContinue(InterfaceElement element)
        {
            MapManager.ObjLink.DisableItems = true;
            MapManager.ObjLink.DisableItemCounter = 350;
            SettingsSaveLoad.SaveSettings();
            SaveGameSaveLoad.SaveGame(Game1.GameManager, true);
            AchievementManager.Save();
            Game1.GameManager.InGameOverlay.CloseOverlay();
        }

        public void OnClickSettings(InterfaceElement element)
        {
            Game1.UiPageManager.ChangePage(typeof(SettingsPage));
        }

        public void OnClickBackToMenu(InterfaceElement element)
        {
            Game1.UiPageManager.ChangePage(typeof(QuitGamePage));
        }

        public void OnClickExitGame(InterfaceElement element)
        {
            Game1.SaveAndExitGame = true;
            Game1.UiPageManager.ChangePage(typeof(ExitGamePage));
        }
    }
}
