using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectZ.Base;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;

namespace ProjectZ.InGame.Pages
{
    class NewGamePage : InterfacePage
    {
        private InterfaceButton _capsLockButton;
        private InterfaceButton[,] _keyboardButtons;
        private InterfaceListLayout[] _keyboardRows;

        private InterfaceListLayout _newGameLayout;
        private InterfaceListLayout _keyboardLayout;
        private InterfaceListLayout _gameTypeLayout;
        private InterfaceListLayout _bottomLayout;

        private readonly InterfaceButton _nameEntryButton;
        private readonly InterfaceButton _startGameButton;
        private readonly InterfaceLabel _labelNameInput;
        private readonly InterfaceSlider _gameTypeSlider;

        private const int MaxNameLength = 12;
        private string _strNameInput;
        private int _selectedSaveSlot;
        private int _gameType;

        private const char CapsLockCharacter = '³';
        private const char BackCharacter = '°';

        private bool _upperMode;
        private bool _showTooltip;
        private bool _startError;
        private bool _oneFrameBlock;

        private float _keyRepeatTimer;
        private float _keyRepeatLimit = 500;

        private char[,] _charactersUpper = new char[,]
        {
            { 'Q', 'W', 'E', 'R', 'T', 'Y', 'U', 'I', 'O', 'P' },
            { 'A', 'S', 'D', 'F', 'G', 'H', 'J', 'K', 'L', '-' },
            { CapsLockCharacter, 'Z', 'X', 'C', 'V', 'B', 'N', 'M', ' ', BackCharacter }
        };

        private char[,] _charactersLower = new char[,]
        {
            { 'q', 'w', 'e', 'r', 't', 'y', 'u', 'i', 'o', 'p' },
            { 'a', 's', 'd', 'f', 'g', 'h', 'j', 'k', 'l', '-' },
            { CapsLockCharacter, 'z', 'x', 'c', 'v', 'b', 'n', 'm', ' ', BackCharacter }
        };

        // The dummy button is created just to extract the color from it.
        private InterfaceButton _dummyButton = new InterfaceButton();
        private Color _capsLockButtonColor;
        private Color _capsLockSelectedColor;

        public NewGamePage(int width, int height)
        {
            // Get the colors from the dummy button.
            _capsLockButtonColor = _dummyButton.Color;
            _capsLockSelectedColor = _dummyButton.SelectionColor;

            // Enable that this page has tooltips.
            EnableTooltips = true;

            // Create the main layout that holds everything.
            _newGameLayout = new InterfaceListLayout { Size = new Point(width, height), Selectable = true };

            // Create the name entry window.
            _newGameLayout.AddElement(new InterfaceLabel("new_game_menu_save_name") { Margin = new Point(0, 2) });
            _labelNameInput = new InterfaceLabel(null) { Selectable = true, Size = new Point(200, 20) };
            var layerButton = new InterfaceListLayout { Size = new Point(200, 20) };
            layerButton.AddElement(_labelNameInput);
            _nameEntryButton = new InterfaceButton { Size = new Point(200, 20), InsideElement = layerButton };
            _newGameLayout.AddElement(_nameEntryButton);

            // Create the keyboard layout.
            {
                _keyboardLayout = new InterfaceListLayout { AutoSize = true, Margin = new Point(0, 5), Selectable = true };

                var keyWidth = 20;
                var keyHeight = 20;

                _keyboardButtons = new InterfaceButton[_charactersUpper.GetLength(0), _charactersUpper.GetLength(1)];
                _keyboardRows = new InterfaceListLayout[_charactersUpper.GetLength(0)];

                for (var y = 0; y < _charactersUpper.GetLength(0); y++)
                {
                    _keyboardRows[y] = new InterfaceListLayout { AutoSize = true, HorizontalMode = true, Selectable = true };

                    for (int x = 0; x < _charactersUpper.GetLength(1); x++)
                    {
                        if (_charactersUpper[y, x] == '-')
                            continue;

                        var letterX = x;
                        var letterY = y;

                        _keyboardButtons[y, x] = new InterfaceButton(new Point(keyWidth, keyHeight), new Point(1, 1), "", element => KeyPressed(letterX, letterY)) { CornerRadius = 0 };
                        ((InterfaceLabel)_keyboardButtons[y, x].InsideElement).SetText(_charactersUpper[y, x].ToString());

                        if (_charactersUpper[y, x] == CapsLockCharacter)
                            _capsLockButton = _keyboardButtons[y, x];

                        _keyboardRows[y].AddElement(_keyboardButtons[y, x]);
                    }
                    _keyboardRows[y].SetSelectionIndex(4);
                    _keyboardLayout.AddElement(_keyboardRows[y]);
                }
                _newGameLayout.AddElement(_keyboardLayout);
            }

            // Create a slider to select the game type.
            _gameTypeLayout = new InterfaceListLayout { Size = new Point(260, 22), HorizontalMode = true, Selectable = true };
            _gameTypeSlider = new InterfaceSlider("new_game_menu_game_type", 
                260, 11, new Point(1, 2), 0, 4, 1, _gameType, 
                number => { _gameType = number; }) 
                { SetString = number => GameTypeScaleSliderAdjustment(number) };
            _gameTypeLayout.AddElement(_gameTypeSlider);
            _newGameLayout.AddElement(_gameTypeLayout);

            // Create the "Back" and "Start" buttons.
            _bottomLayout = new InterfaceListLayout { Size = new Point(260, 34), HorizontalMode = true, Selectable = true };
            _bottomLayout.AddElement(new InterfaceButton(new Point(110, 20), new Point(1, 0), "new_game_menu_back", OnClickBackButton));
            _bottomLayout.AddElement(_startGameButton = new InterfaceButton(new Point(110, 20), new Point(1, 0), "new_game_menu_create_file", OnClickNewGameButton));
            _bottomLayout.Select(InterfaceElement.Directions.Right, false);
            _bottomLayout.Deselect(false);
            _newGameLayout.AddElement(_bottomLayout);

            // Select the name entry.
            _newGameLayout.Select(InterfaceElement.Directions.Top, false);

            PageLayout = _newGameLayout;
        }

        public override void OnLoad(Dictionary<string, object> intent)
        {
            // PageManager.Reload rebuilds retained pages without navigation data.
            if (intent == null)
                return;

            // Capture input text.
            InputHandler.EnableTextInput();

            // Get the selected save slot number from the intent.
            _selectedSaveSlot = (int)intent["SelectedSaveSlot"];

            // Reset the name of the save slot.
            if (Game1.LanguageManager.CurrentLanguageCode == "chn")
                _strNameInput = "林克";
            else
                _strNameInput = "Link";

            _labelNameInput.SetText(_strNameInput + " ");
            _gameTypeSlider.CurrentStep = 0;

            _upperMode = true;
            UpdateKeyboard();

            PageLayout.Deselect(false);
            PageLayout.Select(InterfaceElement.Directions.Top, false);

            base.OnLoad(intent);
        }

        public override void OnPop(Dictionary<string, object> intent)
        {
            // Stop capturing input text.
            InputHandler.DisableTextInput();
        }

        private string GameTypeScaleSliderAdjustment(int number)
        {
            string gameType = number switch
            {
                0 => Game1.LanguageManager.GetString("new_game_menu_gtype_settings", "error"),
                1 => Game1.LanguageManager.GetString("new_game_menu_gtype_modern", "error"),
                2 => Game1.LanguageManager.GetString("new_game_menu_gtype_classic", "error"),
                3 => Game1.LanguageManager.GetString("new_game_menu_gtype_hybrid", "error"),
                4 => Game1.LanguageManager.GetString("new_game_menu_gtype_purist", "error"),
                _ => Game1.LanguageManager.GetString("new_game_menu_gtype_settings", "error")
            };
            return " " + gameType;
        }

        private void UpdateKeyboard()
        {
            // Toggle the color based on whether or not caps lock is enabled.
            _capsLockButton.Color = _upperMode ? _capsLockSelectedColor : _capsLockButtonColor;

            // Update the rest of the buttons labels which changes case if caps lock is toggled.
            for (var y = 0; y < _charactersUpper.GetLength(0); y++)
                for (int x = 0; x < _charactersUpper.GetLength(1); x++)
                    if (_keyboardButtons[y, x] != null)
                        ((InterfaceLabel)_keyboardButtons[y, x].InsideElement).SetText((_upperMode ? _charactersUpper[y, x] : _charactersLower[y, x]).ToString());
        }

        public override void Update(CButtons pressedButtons, GameTime gameTime)
        {
            base.Update(pressedButtons, gameTime);

            // Keep the current column when moving around on the keyboard.
            for (var y = 0; y < _charactersUpper.GetLength(0); y++)
            {
                for (int x = 0; x < _charactersUpper.GetLength(1); x++)
                {
                    if (_keyboardButtons[y, x] != null && _keyboardButtons[y, x].Selected)
                    {
                        for (var y1 = 0; y1 < _charactersUpper.GetLength(0); y1++)
                            _keyboardRows[y1].SetSelectionIndex(x);
                    }
                }
            }

            // When the name entry button is selected, allow keyboard input and
            // deleting characters with the controller "Cancel" button.
            if (_nameEntryButton.Selected)
            {
                var strInput = InputHandler.ReturnCharacter();
                AddCharacters(strInput);

                // The "Back" button was pressed on controller only.
                if (ControlHandler.ButtonPressed(ControlHandler.CancelButton, true))
                    Game1.UiPageManager.PopPage();
                
                // When pressing/holding the "Delete" key.
                else if (InputHandler.KeyDown(Keys.Back))
                {
                    // On first press delete a character.
                    if (_keyRepeatTimer == 0)
                        RemoveCharacter();

                    // Start a timer for 500ms before repeating deleting.
                    _keyRepeatTimer += Game1.DeltaTime;

                    // When timer expires restart it at a faster rate.
                    if (_keyRepeatTimer > _keyRepeatLimit)
                    {
                        _keyRepeatLimit = 50;
                        _keyRepeatTimer = 0;
                    }
                }
                // When releasing the "Delete" key.
                else if (!InputHandler.KeyDown(Keys.Back))
                {
                    // Reset the timer and the repeat rate.
                    _keyRepeatTimer = 0;
                    _keyRepeatLimit = 500;
                }
            }
            // When the keyboard layout is selected, only allow input via the
            // on-screen keys and allow deleting with controller "Cancel" button.
            else if (_keyboardLayout.Selected)
            {
                if (ControlHandler.ButtonPressed(ControlHandler.CancelButton))
                    RemoveCharacter();
            }
            // When any other button is selected.
            else
            {
                // The "Back" button was pressed.
                if (ControlHandler.ButtonPressed(ControlHandler.CancelButton))
                {
                    _startError = false;
                    _showTooltip = false;
                    Game1.UiPageManager.PopPage();
                }
            }
            // The "Tooltip" button was pressed.
            bool keyboardSelected = _newGameLayout.SelectionIndex == 1;
            if (ControlHandler.ButtonPressed(CButtons.Y, keyboardSelected))
            {
                _showTooltip = !_showTooltip;
                if (_showTooltip)
                    Game1.AudioManager.PlaySoundEffect("D360-21-15");
                else
                    _startError = false;
            }
            // Allow toggling caps lock with the LT or RT buttons.
            else if (ControlHandler.ButtonPressed(CButtons.LT, true) || ControlHandler.ButtonPressed(CButtons.RT, true) || 
                ControlHandler.ButtonPressed(CButtons.LB, true) || ControlHandler.ButtonPressed(CButtons.RB, true))
            {
                _upperMode = !_upperMode;
                UpdateKeyboard();
            }
            // Hide the tooltip when pressing anything.
            else if (ControlHandler.AnyButtonPressed())
            {
                if (_oneFrameBlock)
                    _oneFrameBlock = false;
                else
                {
                    _startError = false;
                    _showTooltip = false;
                }
            }
            // Update the entered name shown to the player.
            _labelNameInput.SetText(_strNameInput + ((gameTime.TotalGameTime.Milliseconds % 500) < 250 ? "_" : " "));

            // Allow forcefully starting a new game.
            if (ControlHandler.ButtonPressed(CButtons.Start))
            {
                Game1.AudioManager.PlaySoundEffect("D360-19-13");
                OnClickNewGameButton(_startGameButton);
            }
        }

        private void RemoveCharacter()
        {
            if (string.IsNullOrEmpty(_strNameInput))
                return;

            var textEnum = StringInfo.GetTextElementEnumerator(_strNameInput);
            int lastIndex = 0;

            while (textEnum.MoveNext())
                lastIndex = textEnum.ElementIndex;

            _strNameInput = _strNameInput.Substring(0, lastIndex);
        }

        private void AddCharacters(string letter)
        {
            _strNameInput += letter;

            if (_strNameInput.Length > MaxNameLength)
                _strNameInput = _strNameInput.Remove(MaxNameLength);
        }

        private void KeyPressed(int x, int y)
        {
            var characters = _upperMode ? _charactersUpper : _charactersLower;

            if (characters[y, x] == CapsLockCharacter)
            {
                _upperMode = !_upperMode;
                UpdateKeyboard();
            }
            else if (characters[y, x] == BackCharacter)
                RemoveCharacter();
            else
            {
                AddCharacters(characters[y, x].ToString());
            }
        }

        private void OnClickNewGameButton(InterfaceElement element)
        {
            // A name must be entered.
            if (_strNameInput.Length <= 0)
            {
                _showTooltip = true;
                _startError = true;
                _oneFrameBlock = true;
                return;
            }
            // Get the entered name as all lowercase to compare.
            string name = _strNameInput.ToLower();

            // Achievement: If entering a secret name play music.
            if (name == "totaka" || name == "totakeke" || name == "moyse")
            {
                Game1.AudioManager.SetMusic(59, 2);
                AchievementManager.Earn(0);
            }
            else if (name == "zelda" || name == "塞尔达")
            {
                Game1.AudioManager.SetMusic(95, 2);
                AchievementManager.Earn(0);
            }
            // Initialize a new save file: SaveSlot, SaveName, Equipment, SaveManager, etc.
            Game1.GameManager.CreateNewSaveFile(_selectedSaveSlot, _strNameInput);

            // Get the "Presets" page from the stack.
            if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(PresetOptionsPage), out var presetsPage))
            {
                // Cast it into the proper type.
                var PresetSettingsPage = presetsPage as PresetOptionsPage;

                // Apply the settings based on the user's selection.
                switch (_gameType)
                {
                    case 1: { PresetSettingsPage.SetModernValues(); break; }
                    case 2: { PresetSettingsPage.SetClassicValues(); break; }
                    case 3: { PresetSettingsPage.SetHybridValues(); break; }
                    case 4: { PresetSettingsPage.SetPuristValues(); break; }
                }
            }
            // The map is not written during "StartNewGame" so write it now. Normally this is done
            // when a new game is started and saved. But since we are not starting a new game...
            var link = MapManager.ObjLink;
            link.SaveMap = "house1.map";
            link.SavePosition = new Vector2(link.PosX, link.PosY);

            // Write the save files and return to file select.
            SaveGameSaveLoad.SaveGame(Game1.GameManager, false);
            AchievementManager.Save(); 
            Game1.UiPageManager.PopPage(SkipSound: true);
        }

        private void OnClickBackButton(InterfaceElement element)
        {
            Game1.UiPageManager.PopPage();
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, float height, float alpha)
        {
            // Always draw the menu even when not showing tooltips.
            base.Draw(spriteBatch, position, height, alpha);

            // If the user pressed the top most face button, show the tooltip window.
            if (_showTooltip)
            {
                string tooltipText = GetOptionToolip();
                PageTooltip.Draw(spriteBatch, tooltipText);
            }
        }

        private string GetOptionToolip()
        {
            // Get the currently selected index.
            int index = _newGameLayout.SelectionIndex;
            string tooltip = "";

            // Show a special message if the user tried to start without entering a name.
            if (_startError)
                return Game1.LanguageManager.GetString("tooltip_newgame_noname", "error");

            // Use the selected index to determine which tooltip to show.
            switch (index) 
            {
                case 1:  { tooltip = Game1.LanguageManager.GetString("tooltip_newgame_entry", "error"); break; }
                case 2:  { tooltip = Game1.LanguageManager.GetString("tooltip_newgame_keyboard", "error"); break; }
            }

            // Return the tooltip if index is between 1 and 3.
            if (index is >= 1 and <= 2)
                return tooltip;

            // If the index is 3 we need to get the sub-indexes.
            if (index == 3)
            {
                // Get the currently selected index.
                index = _gameTypeSlider.CurrentStep;

                // Use the selected index to determine which tooltip to show.
                switch (index) 
                {
                    case 0:  { tooltip = Game1.LanguageManager.GetString("tooltip_newgame_gametype0", "error"); break; }
                    case 1:  { tooltip = Game1.LanguageManager.GetString("tooltip_newgame_gametype1", "error"); break; }
                    case 2:  { tooltip = Game1.LanguageManager.GetString("tooltip_newgame_gametype2", "error"); break; }
                    case 3:  { tooltip = Game1.LanguageManager.GetString("tooltip_newgame_gametype3", "error"); break; }
                    case 4:  { tooltip = Game1.LanguageManager.GetString("tooltip_newgame_gametype4", "error"); break; }
                }
                // Return one of the tooltips.
                return tooltip;
            }

            // If the index is 4 we need to get the sub-indexes.
            if (index == 4)
            {
                // Get the currently selected index.
                index = _bottomLayout.SelectionIndex;

                // Use the selected index to determine which tooltip to show.
                switch (index) 
                {
                    case 0:  { tooltip = Game1.LanguageManager.GetString("tooltip_newgame_back", "error"); break; }
                    case 1:  { tooltip = Game1.LanguageManager.GetString("tooltip_newgame_start", "error"); break; }
                }
                // Return one of the two tooltips.
                return tooltip;
            }
            // Return the default tooltip.
            return "Select an option to view its tooltip.";
        }
    }
}
