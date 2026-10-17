using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    class MainMenuPage : InterfacePage
    {
        enum State { Select, Delete, Copy }

        public InterfaceListLayout[] SaveEntries = new InterfaceListLayout[SaveStateManager.SaveCount];

        private float[] _playerSelectionState = new float[SaveStateManager.SaveCount];
        private InterfaceButton[] _saveButtons = new InterfaceButton[SaveStateManager.SaveCount];
        private Dictionary<string, object> _newGameIntent = new Dictionary<string, object>();

        private Animator _playerAnimation = new Animator();
        private Animator _swordAnimation = new Animator();

        private InterfaceImagePlayer[] _playerImage = new InterfaceImagePlayer[SaveStateManager.SaveCount];

        private DictAtlasEntry _heartSprite;
        private InterfaceElement[][] _heartImage = new InterfaceElement[SaveStateManager.SaveCount][];

        private DictAtlasEntry _rupeeSprite;
        private InterfaceImage[] _rupeeImages = new InterfaceImage[SaveStateManager.SaveCount];

        private DictAtlasEntry _clockSprite;
        private InterfaceImage[] _clockImages = new InterfaceImage[SaveStateManager.SaveCount];

        private DictAtlasEntry _deathSprite;
        private InterfaceImage[] _deathImages = new InterfaceImage[SaveStateManager.SaveCount];

        private DictAtlasEntry _shellSprite;
        private InterfaceImage[] _shellImages = new InterfaceImage[SaveStateManager.SaveCount];

        private InterfaceLabel[] _deathLabels = new InterfaceLabel[SaveStateManager.SaveCount];
        private InterfaceLabel[] _saveShells = new InterfaceLabel[SaveStateManager.SaveCount];

        private DictAtlasEntry[] _instrumentSprites = new DictAtlasEntry[8];
        private InterfaceElement[][] _instrumentImages = new InterfaceElement[SaveStateManager.SaveCount][];

        private InterfaceGravityLayout[] _saveButtonLayouts = new InterfaceGravityLayout[SaveStateManager.SaveCount];

        private InterfaceLabel[] _saveNames = new InterfaceLabel[SaveStateManager.SaveCount];
        private InterfaceLabel[] _saveRupees = new InterfaceLabel[SaveStateManager.SaveCount];
        private InterfaceLabel[] _savePlaytime = new InterfaceLabel[SaveStateManager.SaveCount];

        private InterfaceListLayout[] _deleteCopyLayouts = new InterfaceListLayout[SaveStateManager.SaveCount];

        private InterfaceListLayout _mainLayout;
        private InterfaceListLayout _newGameButtonLayout;
        private InterfaceListLayout _menuBottomBar;
        private InterfaceListLayout _saveFileList;

        private float textGoldTimer;

        private string[] cloakColors = new string[SaveStateManager.SaveCount];

        private int _selectedSaveIndex;
        private bool _selectStoredSave;

        public MainMenuPage(int width, int height)
        {
            var smallButtonWidth = 100;
            var smallButtonMargin = 2;
            var saveButtonRec = new Point(204, 32);
            var sideSize = 70;

            _heartSprite = Resources.GetSprite("heart menu");
            _rupeeSprite = Resources.GetSprite("ui ruby");
            _clockSprite = Resources.GetSprite("ui clock");
            _deathSprite = Resources.GetSprite("ui skull");
            _shellSprite = Resources.GetSprite("ui shell");

            for (int i = 0; i < 8; i++)
                _instrumentSprites[i] = Resources.GetSprite($"instrument{i}");

            _playerAnimation = AnimatorSaveLoad.LoadAnimator("menu_link");
            _playerAnimation.Play("green");

            _swordAnimation = AnimatorSaveLoad.LoadAnimator("menu_link");
            _swordAnimation.Play("sword");

            _newGameButtonLayout = new InterfaceListLayout { Size = saveButtonRec };
            _newGameButtonLayout.AddElement(new InterfaceLabel("main_menu_new_game"));

            // Save File List
            _saveFileList = new InterfaceListLayout() { Size = new Point(width, (int)(height * Values.MenuContentSize) - 12), Selectable = true };
            for (var i = 0; i < SaveStateManager.SaveCount; i++)
            {
                _saveButtonLayouts[i] = new InterfaceGravityLayout { Size = new Point(saveButtonRec.X, saveButtonRec.Y) };

                // To make this much simpler to tweak, use fixed sizes for everything.
                var numberWidth = 12;
                var heartsWidth = 60;
                var middleWidth = 64;
                var instrumentsWidth = 64;

                var saveSlotNumber = new InterfaceLabel(null, new Point(numberWidth, 28), Point.Zero) { Gravity = InterfaceElement.Gravities.Left };

                saveSlotNumber.SetText((i + 1).ToString());
                _saveButtonLayouts[i].AddElement(saveSlotNumber);

                var saveInfoLayout = new InterfaceListLayout { HorizontalMode = true, Size = new Point(saveButtonRec.X - numberWidth, saveButtonRec.Y), Gravity = InterfaceElement.Gravities.Right };

                // Heart Count / Death Count / Shell Count
                {
                    // Heart Count
                    var hearts = new InterfaceListLayout { Size = new Point(heartsWidth, 40) };
                    var rowOne = new InterfaceListLayout { Size = new Point(heartsWidth - 4, 7), Margin = new Point(2, 1), HorizontalMode = true, ContentAlignment = InterfaceElement.Gravities.Left };
                    var rowTwo = new InterfaceListLayout { Size = new Point(heartsWidth - 4, 7), Margin = new Point(2, 1), HorizontalMode = true, ContentAlignment = InterfaceElement.Gravities.Left };

                    _heartImage[i] = new InterfaceElement[14];

                    for (var j = 0; j < 7; j++)
                    {
                        int k = j + 7;
                        _heartImage[i][j] = rowOne.AddElement(new InterfaceImage(Resources.SprItem, _heartSprite.ScaledRectangle, Point.Zero, new Point(1, 1)) { Gravity = InterfaceElement.Gravities.Left });
                        _heartImage[i][k] = rowTwo.AddElement(new InterfaceImage(Resources.SprItem, _heartSprite.ScaledRectangle, Point.Zero, new Point(1, 1)) { Gravity = InterfaceElement.Gravities.Left });
                    }
                    hearts.AddElement(rowOne);
                    hearts.AddElement(rowTwo);
                    hearts.AddElement(new InterfaceListLayout { Size = new Point(1, 1) });

                    // Death Count
                    var deathRow = new InterfaceListLayout { HorizontalMode = true, Size = new Point(heartsWidth / 2 - 2, 10), Margin = new Point(3, 5), ContentAlignment = InterfaceElement.Gravities.Left };

                    _deathImages[i] = new InterfaceImage(_deathSprite.Texture, _deathSprite.SourceRectangle, Point.Zero, new Point(1, 1)) { Margin = new Point(0, 1) };
                    _deathLabels[i] = new InterfaceLabel(null, new Point(heartsWidth / 2 - 12, 10), Point.Zero) { Margin = new Point(2, 0), TextAlignment = InterfaceElement.Gravities.Left };

                    deathRow.AddElement(_deathImages[i]);
                    deathRow.AddElement(_deathLabels[i]);

                    // Shell Count
                    var shellRow = new InterfaceListLayout { HorizontalMode = true, Size = new Point(heartsWidth - 6, 10), Margin = new Point(3, 0), ContentAlignment = InterfaceElement.Gravities.Left };
                    var shellLabel = new InterfaceLabel(null, new Point(heartsWidth - 20, 10), Point.Zero) { Margin = new Point(2, 0), TextAlignment = InterfaceElement.Gravities.Left };

                    _shellImages[i] = new InterfaceImage(_shellSprite.Texture, _shellSprite.SourceRectangle, Point.Zero, new Point(1, 1)) { Margin = new Point(0, 1) };

                    shellRow.AddElement(_shellImages[i]);
                    shellRow.AddElement(shellLabel);

                    _saveShells ??= new InterfaceLabel[SaveStateManager.SaveCount];
                    _saveShells[i] = shellLabel;

                    // Add Death Count and Shell Count to a row below hearts.
                    var statsRow = new InterfaceListLayout { HorizontalMode = true, Size = new Point(heartsWidth, 10), Margin = new Point(0, 0), ContentAlignment = InterfaceElement.Gravities.Left };

                    statsRow.AddElement(deathRow);
                    statsRow.AddElement(shellRow);
                    hearts.AddElement(statsRow);
                    saveInfoLayout.AddElement(hearts);
                }

                // Name / Rupees / Playtime
                {
                    // Name
                    var middle = new InterfaceListLayout { Size = new Point(middleWidth, 30), Margin = new Point(2, 0), Gravity = InterfaceElement.Gravities.Left };
                    middle.AddElement(_saveNames[i] = new InterfaceLabel(null, new Point(middleWidth - 18, 10), Point.Zero) { Margin = new Point(1, 0), TextAlignment = InterfaceElement.Gravities.Left | InterfaceElement.Gravities.Bottom });

                    // Rupees
                    var rupeeRow = new InterfaceListLayout{HorizontalMode = true, Size = new Point(middleWidth - 17, 10), ContentAlignment = InterfaceElement.Gravities.Left };
                    _rupeeImages[i] = new InterfaceImage(_rupeeSprite.Texture, _rupeeSprite.SourceRectangle, Point.Zero, new Point(1, 1)) { Margin = new Point(0, 1) };
                    rupeeRow.AddElement(_rupeeImages[i]);
                    _saveRupees[i] = new InterfaceLabel(null, new Point(middleWidth - 25, 10), Point.Zero) { Margin = new Point(2, 0), TextAlignment = InterfaceElement.Gravities.Left };
                    rupeeRow.AddElement(_saveRupees[i]);
                    middle.AddElement(rupeeRow);

                    //Playtime
                    var playtimeRow = new InterfaceListLayout{HorizontalMode = true, Size = new Point(middleWidth - 17, 10), ContentAlignment = InterfaceElement.Gravities.Left };
                    _clockImages[i] = new InterfaceImage(_clockSprite.Texture, _clockSprite.SourceRectangle, Point.Zero, new Point(1, 1)) { Margin = new Point(0, 1) };
                    playtimeRow.AddElement(_clockImages[i]);
                    _savePlaytime[i] = new InterfaceLabel(null, new Point(middleWidth - 25, 10), Point.Zero) { Margin = new Point(2, 0), TextAlignment = InterfaceElement.Gravities.Left };
                    playtimeRow.AddElement(_savePlaytime[i]);
                    middle.AddElement(playtimeRow);
                    saveInfoLayout.AddElement(middle);
                }

                // Instruments
                {
                    var instrumentWidth = 64;
                    var instruments = new InterfaceListLayout { Margin = new Point(2, 1), Size = new Point(instrumentsWidth, 0), Gravity = InterfaceElement.Gravities.Left };

                    // Spacer to push instruments down by 1 pixel.
                    instruments.AddElement(new InterfaceListLayout { Size = new Point(1, 1) });

                    var rowOne = new InterfaceListLayout { Margin = new Point(1, 1), HorizontalMode = true, Size = new Point(instrumentWidth, 14), ContentAlignment = InterfaceElement.Gravities.Left };
                    var rowTwo = new InterfaceListLayout { Margin = new Point(1, 1), HorizontalMode = true, Size = new Point(instrumentWidth, 14), ContentAlignment = InterfaceElement.Gravities.Left };

                    _instrumentImages[i] = new InterfaceElement[8];

                    for (int j = 0; j < 4; j++)
                    {
                        _instrumentImages[i][j] = rowOne.AddElement(new InterfaceImageInstrument(_instrumentSprites[j]));
                        _instrumentImages[i][j+4] = rowTwo.AddElement(new InterfaceImageInstrument(_instrumentSprites[j + 4]));
                    }
                    instruments.AddElement(rowOne);
                    instruments.AddElement(rowTwo);
                    saveInfoLayout.AddElement(instruments);
                }

                var i1 = i;
                _saveButtonLayouts[i].AddElement(saveInfoLayout);
                _saveButtons[i] = new InterfaceButton{ InsideElement = _saveButtonLayouts[i], Size = new Point(saveButtonRec.X, saveButtonRec.Y), Margin = new Point(0, 2), ClickFunction = e => OnClickSave(i1) };

                SaveEntries[i] = new InterfaceListLayout { HorizontalMode = true, Gravity = InterfaceElement.Gravities.Right, AutoSize = true, Selectable = true };
                SaveEntries[i].AddElement(new InterfaceListLayout { Size = new Point(sideSize - 20, 20) });
                SaveEntries[i].AddElement(_playerImage[i] = new InterfaceImagePlayer(_playerAnimation, _swordAnimation, _playerAnimation.SprTexture, _playerAnimation.CurrentFrame.SourceRectangle, new Point(24, 16), new Point(0, 0)));
                SaveEntries[i].AddElement(_saveButtons[i]);

                // Copy / Delete Options
                var currentSlot = i;
                _deleteCopyLayouts[i] = new InterfaceListLayout
                {
                    Gravity = InterfaceElement.Gravities.Right,
                    Size = new Point(sideSize, saveButtonRec.Y),
                    PreventSelection = true,
                    Selectable = true,
                    Visible = false
                };

                var insideCopy = new InterfaceListLayout() { Size = new Point(sideSize - 4, 13) };
                insideCopy.AddElement(new InterfaceLabel("main_menu_copy") { Size = new Point(40, 12), TextAlignment = InterfaceElement.Gravities.Bottom });
                _deleteCopyLayouts[i].AddElement(new InterfaceButton(new Point(sideSize - 4, 13), new Point(0, 1), insideCopy, element => OnClickCopy(currentSlot)));

                var insideDelete = new InterfaceListLayout() { Size = new Point(sideSize - 4, 13) };
                insideDelete.AddElement(new InterfaceLabel("main_menu_erase") { Size = new Point(40, 12), TextAlignment = InterfaceElement.Gravities.Bottom });
                _deleteCopyLayouts[i].AddElement(new InterfaceButton(new Point(sideSize - 4, 13), new Point(0, 1), insideDelete, element => OnClickDelete(currentSlot)));

                SaveEntries[i].AddElement(_deleteCopyLayouts[i]);
                _saveFileList.AddElement(SaveEntries[i]);
            }
            var buttonHeight = 18;

            // Bottom Bar
            _menuBottomBar = new InterfaceListLayout
            {
                Size = new Point(saveButtonRec.X, (int)(height * Values.MenuFooterSize)),
                HorizontalMode = true,
                Selectable = true
            };
            _menuBottomBar.AddElement(new InterfaceButton(new Point(smallButtonWidth, buttonHeight), new Point(smallButtonMargin, 0), "main_menu_settings", element => OpenSettingsPage()));
            _menuBottomBar.AddElement(new InterfaceButton(new Point(smallButtonWidth, buttonHeight), new Point(smallButtonMargin, 0), "main_menu_quit", element => OpenExitGamePage()));

            _mainLayout = new InterfaceListLayout { Size = new Point(width, height - 12), Gravity = InterfaceElement.Gravities.Left, Selectable = true };
            _mainLayout.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "main_menu_select_header", new Point(width, (int)(height * Values.MenuHeaderSize)), new Point(0, 0)));
            _mainLayout.AddElement(_saveFileList);
            _mainLayout.AddElement(_menuBottomBar);

            PageLayout = _mainLayout;
            PageLayout.Select(InterfaceElement.Directions.Top, false);
        }

        public void HideInstruments()
        {
            for (var i = 0; i < SaveStateManager.SaveCount; i++)
                for (var j = 0; j < _instrumentSprites.Length; j++)
                    _instrumentImages[i][j].Hidden = true;
        }

        public void OpenSettingsPage()
        {
            HideInstruments();
            Game1.UiPageManager.ChangePage(typeof(SettingsPage));
        }

        public void OpenExitGamePage()
        {
            HideInstruments();
            Game1.UiPageManager.ChangePage(typeof(ExitGamePage));
        }

        public override void OnLoad(Dictionary<string, object> intent)
        {
            // Sync newer saves in from shared storage before reading from scoped.
            SharedSaveSync.SyncFromSharedIfEnabled();

            SaveStateManager.LoadSaveData();

            UpdateUi();

            // This likes to stick around so clear it whenever we show this page.
            Game1.GameManager.GameCleared = false;

            for (var i = 0; i < _deleteCopyLayouts.Length; i++)
                _deleteCopyLayouts[i].Visible = i == 0 && SaveStateManager.SaveStates[i] != null;

            PageLayout = _mainLayout;
            PageLayout.Deselect(false);

            // If the player wants to automatically select the last save file accessed.
            if (GameSettings.StoreSavePos && !_selectStoredSave)
            {
                _mainLayout.SetSelectionIndex(_mainLayout.Elements.IndexOf(_saveFileList));
                _saveFileList.Select(GameSettings.LastSavePos, false);
            }
            else
            {
                PageLayout.Select(InterfaceElement.Directions.Top, false);
            }
            // To prevent bad selection on settings page pop, always disable after menu loads.
            _selectStoredSave = true;

            UpdatePlayerAnimation();
        }

        public override void OnReturn(Dictionary<string, object> intent)
        {
            // Reload from disk so newly created saves are picked up.
            SaveStateManager.LoadSaveData();

            UpdateUi();

            base.OnReturn(intent);

            if (intent != null && intent.TryGetValue("deleteReturn", out var deleteReturn) && (bool)deleteReturn)
            {
                // select the savestate
                _saveFileList.Elements[_selectedSaveIndex].Deselect(false);
                _saveFileList.Elements[_selectedSaveIndex].Select(InterfaceElement.Directions.Left, false);
            }

            // delete the save state?
            if (intent != null && intent.TryGetValue("deleteSavestate", out var deleteSaveState) && (bool)deleteSaveState)
            {
                SaveGameSaveLoad.DeleteSaveFile(_selectedSaveIndex);
                ReloadSaves();
            }

            // copy save state
            if (intent != null && intent.TryGetValue("copyTargetSlot", out var targetSlot))
            {
                SaveGameSaveLoad.CopySaveFile(_selectedSaveIndex, (int)targetSlot);
                ReloadSaves();

                // select the savestate
                _saveFileList.Elements[_selectedSaveIndex].Deselect(false);
                _saveFileList.Elements[_selectedSaveIndex].Select(InterfaceElement.Directions.Left, false);
                _saveFileList.Elements[_selectedSaveIndex].Deselect(false);

                // select the target slot
                _saveFileList.Select((int)targetSlot, false);
            }
        }

        public override void Update(CButtons pressedButtons, GameTime gameTime)
        {
            // Cycle through the instrument colors.
            ItemDrawHelper.Update();

            // If the player used the command line to automatically load a save slot.
            if (Game1.FinishedLoading && Game1.AutoLoadSave)
            {
                // Make sure the slot is within range.
                int LoadSlot = Game1.AutoLoadSlot is >= 0 and <= 3 ? Game1.AutoLoadSlot : 0;

                // Set the autoload to false.
                Game1.AutoLoadSave = false;

                // Load the save file slot.
                LoadSave(LoadSlot);
            }
            // The update must be here or it breaks auto-selecting a save file.
            base.Update(pressedButtons, gameTime);

            // Update the Link animation on the menu.
            UpdatePlayerAnimation();

            // Only show the copy/delete buttons for the saveslot that is currently selected.
            var selectedSaveIndex = -1;
            for (var i = 0; i < _deleteCopyLayouts.Length; i++)
            {
                _deleteCopyLayouts[i].Visible = _saveFileList.Elements[i].Selected && SaveStateManager.SaveStates[i] != null;
                if (_saveFileList.Elements[i].Selected)
                    selectedSaveIndex = i;
            }

            // Exit back to the video/title screen if pressing the cancel button.
            if (ControlHandler.ButtonPressed(ControlHandler.CancelButton))
            {
                _selectedSaveIndex = selectedSaveIndex;
                _selectStoredSave = false;

                // Close the menu page and change to the intro screen.
                HideInstruments();
                Game1.ScreenManager.ChangeScreen(Values.ScreenNameIntro);
                Game1.UiPageManager.PopPage(null, PageManager.TransitionAnimation.TopToBottom, PageManager.TransitionAnimation.TopToBottom, true);
            }
            // Get the selected index to get the cloak color.
            var index = _saveFileList.SelectionIndex;

            // Update the cloak color.
            if (index is >= 0 and <= 4)
            {
                if (!string.IsNullOrEmpty(cloakColors[index]))
                    _playerAnimation.Play(cloakColors[index]);
                else
                    _playerAnimation.Play("green");
            }
            // Cycle the text to a gold color if certain criteria is met.
            Color textInputColor = InterfaceElement.MainTextColor;
            Color textGoldColor  = new Color(255, 215, 0);

            // Lerp between them using a sine wave timer
            textGoldTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            float cycle = 3.75f;
            float speed = MathF.Tau / cycle;
            float timer = (MathF.Sin(textGoldTimer * speed) + 1f) * 0.5f;
            Color textOutputColor = Color.Lerp(textInputColor, textGoldColor, timer);

            // Cylcle the gold color if conditions are met.
            for (var i = 0; i < SaveStateManager.SaveCount; i++)
            {
                if (SaveStateManager.SaveStates[i] == null)
                    continue;

                bool goldNames = SaveStateManager.SaveStates[i].GameCleared && !SaveStateManager.SaveStates[i].Thief;
                bool goldRupee = SaveStateManager.SaveStates[i].CurrentRupees >= 999;
                bool goldDeath = SaveStateManager.SaveStates[i].Deaths == 0 && SaveStateManager.SaveStates[i].GameCleared;
                bool goldShell = SaveStateManager.SaveStates[i].CurrentShells >= 26;
                bool goldTimer = SaveStateManager.SaveStates[i].GameCleared;

                _saveNames[i].TextColor    = goldNames ? textOutputColor : InterfaceElement.MainTextColor;
                _saveRupees[i].TextColor   = goldRupee ? textOutputColor : InterfaceElement.MainTextColor;
                _deathLabels[i].TextColor  = goldDeath ? textOutputColor : InterfaceElement.MainTextColor;
                _saveShells[i].TextColor   = goldShell ? textOutputColor : InterfaceElement.MainTextColor;
                _savePlaytime[i].TextColor = goldTimer ? textOutputColor : InterfaceElement.MainTextColor;
            }
        }

        private void UpdatePlayerAnimation()
        {
            // update the animation
            _playerAnimation.Update();

            for (var i = 0; i < SaveStateManager.SaveCount; i++)
            {
                _playerSelectionState[i] = AnimationHelper.MoveToTarget(_playerSelectionState[i], _saveButtons[i].Selected ? 1 : 0, 1);

                _playerImage[i].ImageColor = Color.Lerp(Color.Transparent, Color.White, _playerSelectionState[i]);
                _playerImage[i].SourceRectangle = _playerAnimation.CurrentFrame.SourceRectangle;
                _playerImage[i].Offset = new Vector2(
                    _playerAnimation.CurrentAnimation.Offset.X + _playerAnimation.CurrentFrame.Offset.X,
                    _playerAnimation.CurrentAnimation.Offset.Y + _playerAnimation.CurrentFrame.Offset.Y);
                _playerImage[i].Effects =
                    (_playerAnimation.CurrentFrame.MirroredV ? SpriteEffects.FlipVertically : SpriteEffects.None) |
                    (_playerAnimation.CurrentFrame.MirroredH ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
            }
        }

        private void OnClickCopy(int number)
        {
            _selectedSaveIndex = number;

            var intent = new Dictionary<string, object>();
            intent.Add("selectedSlot", number);
            HideInstruments();
            Game1.UiPageManager.ChangePage(typeof(CopyPage), intent, PageManager.TransitionAnimation.Fade, PageManager.TransitionAnimation.Fade);
        }

        private void OnClickDelete(int number)
        {
            _selectedSaveIndex = number;
            HideInstruments();
            Game1.UiPageManager.ChangePage(typeof(DeleteSaveSlotPage), null, PageManager.TransitionAnimation.Fade, PageManager.TransitionAnimation.Fade);
        }

        private void OnClickSave(int number)
        {
            _selectedSaveIndex = number;

            // load the save file
            LoadSave(number);
        }

        private void LoadSave(int saveIndex)
        {
            // load game or create new save
            if (SaveStateManager.SaveStates[saveIndex] != null)
            {
                // change to the game screen
                Game1.ScreenManager.ChangeScreen(Values.ScreenNameGame);
                // load the save
                Game1.GameManager.LoadSaveFile(saveIndex);
                // Hide the instruments.
                HideInstruments();
                // close the menu page
                Game1.UiPageManager.PopPage(null, PageManager.TransitionAnimation.TopToBottom, PageManager.TransitionAnimation.TopToBottom, true);
                // Store the last game save that was selected.
                GameSettings.LastSavePos = saveIndex;
                _selectStoredSave = false;
            }
            else
            {
                // change to the NewGamePage
                _newGameIntent["SelectedSaveSlot"] = saveIndex;
                HideInstruments();
                Game1.UiPageManager.ChangePage(typeof(NewGamePage), _newGameIntent);
                // Store the last game save that was selected.
                GameSettings.LastSavePos = saveIndex;
            }
        }

        private void ReloadSaves()
        {
            // load the savestates
            SaveStateManager.LoadSaveData();

            // update the UI
            UpdateUi();
        }

        private void UpdateUi()
        {
            for (var i = 0; i < SaveStateManager.SaveCount; i++)
            {
                // The current file has no save data. We may be here after deleting a
                // save file, so it needs to return the data to default settings.
                if (SaveStateManager.SaveStates[i] == null)
                {
                    // Default to the green tunic.
                    cloakColors[i] ="green";

                    // No sword and shield should be visible.
                    _playerImage[i].ShowSword  = false;
                    _playerImage[i].ShowShield = false;

                    // Show a new game button.
                    _saveButtons[i].InsideElement = _newGameButtonLayout;
                    continue;
                }
                // The save file is occupied so show the player's statistics.
                else
                    _saveButtons[i].InsideElement = _saveButtonLayouts[i];

                // Does the player have: level 2 sword, mirror shield, colored tunic.
                var sword = SaveStateManager.SaveStates[i].SwordLevel2;
                var cloak = SaveStateManager.SaveStates[i].CloakType;
                var shield = SaveStateManager.SaveStates[i].MirrorShield;

                // The "animation" ID to play based on tunic color.
                string baseColor = cloak switch
                {
                    1 => "blue",
                    2 => "red",
                    _ => "green"
                };
                // Player has the Level 2 sword so show it on Link's sprite.
                _playerImage[i].ShowSword  = sword;
                _playerImage[i].ShowShield = shield;

                // Player has the mirror shield so show it on Link's sprite.
                cloakColors[i] = shield ? baseColor + "s" : baseColor;

                // Load the player's hearts.
                for (var j = 0; j < 14; j++)
                {
                    _heartImage[i][j].Hidden = SaveStateManager.SaveStates[i].MaxHearts <= j;
                    var state = 4 - MathHelper.Clamp(SaveStateManager.SaveStates[i].CurrentHealth - (j * 4), 0, 4);
                    ((InterfaceImage)_heartImage[i][j]).SourceRectangle = new Rectangle(
                        _heartSprite.ScaledRectangle.X + (_heartSprite.ScaledRectangle.Width + _heartSprite.TextureScale) * state,
                        _heartSprite.ScaledRectangle.Y,
                        _heartSprite.ScaledRectangle.Width, _heartSprite.ScaledRectangle.Height);
                }
                // Load the player's death count.
                int deaths = SaveStateManager.SaveStates[i].Deaths;
                _deathLabels[i].SetText(deaths.ToString());

                // Load the player's seashell count.
                int shells = SaveStateManager.SaveStates[i].CurrentShells;
                _saveShells[i].SetText(shells.ToString());

                // Load the player's name. If the player has stolen replace it with the "Thief".
                _saveNames[i].SetText(SaveStateManager.SaveStates[i].Thief 
                    ? Game1.LanguageManager.GetString("savename_thief", "error") 
                    : SaveStateManager.SaveStates[i].Name);

                // Load the players rupee count.
                int rupees = SaveStateManager.SaveStates[i].CurrentRupees;
                _saveRupees[i].SetText(rupees.ToString());

                // Playtime format displays as: HH:MM
                var totalMinutes = SaveStateManager.SaveStates[i].TotalPlaytime;
                var hours = (int)(totalMinutes / 60);
                var minutes = (int)(totalMinutes % 60);
                var playtimeText = $"{hours:D2}:{minutes:D2}";
                _savePlaytime[i].SetText(playtimeText);

                // Load the player's collected instruments.
                for (var j = 0; j < _instrumentSprites.Length; j++)
                    _instrumentImages[i][j].Hidden = !SaveStateManager.SaveStates[i].Instruments[j];
            }
        }
    }
}
