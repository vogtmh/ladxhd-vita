using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    class SettingsPage : InterfacePage
    {
        private readonly InterfaceListLayout _settingsLayout;
        private readonly InterfaceListLayout _contentLayout;
        private readonly InterfaceListLayout _bottomBar;
        private readonly InterfaceLabel _versionLabel;

        // Each settings entry: button text key, tooltip key, and the page it opens.
        private readonly (string Text, string Tooltip, Type Page)[] _entries;

        // The horizontal row layouts that hold the buttons (2 per row).
        private readonly List<InterfaceListLayout> _rows = new List<InterfaceListLayout>();

        // Horizontal gap between the two columns, in pixels.
        private const int _buttonSize = 20;
        private const int _rowGap = 5;
        private const int _columnGap = 5;

        // The column the user wants to stay in while moving up/down between rows.
        private int _desiredColumn;
        private bool _showTooltip;

        public SettingsPage(int width, int height)
        {
            EnableTooltips = true;

            _entries = new (string, string, Type)[]
            {
                ("settings_menu_game",     "tooltip_menu_game",     typeof(GameSettingsPage)),
                ("settings_menu_redux",    "tooltip_menu_redux",    typeof(ReduxSettingsPage)),
                ("settings_menu_video",    "tooltip_menu_video",    typeof(VideoSettingsPage)),
                ("settings_menu_audio",    "tooltip_menu_audio",    typeof(AudioSettingsPage)),
                ("settings_menu_graphics", "tooltip_menu_graphics", typeof(GraphicsSettingsPage)),
                ("settings_menu_camera",   "tooltip_menu_camera",   typeof(CameraSettingsPage)),
                ("settings_menu_controls", "tooltip_menu_controls", typeof(ControlSettingsPage)),
                ("settings_menu_mods",     "tooltip_menu_mods",     typeof(ModifierSettingsPage)),
                ("settings_menu_cheats",   "tooltip_menu_cheats",   typeof(CheatsSettingsPage)),
                ("settings_menu_presets",  "tooltip_menu_presets",  typeof(PresetOptionsPage)),
                ("settings_menu_achieve",  "tooltip_menu_achieve",  typeof(AchievementsPage)),
            };

            // Settings Page Layout
            _settingsLayout = new InterfaceListLayout { Size = new Point(width, height - 12), Selectable = true };
            var headerLayout = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuHeaderSize)), ContentAlignment = InterfaceElement.Gravities.Left, HorizontalMode = true };
            {
                _versionLabel = new InterfaceLabel("", new Point((width - 150) / 2 - 2, headerLayout.Size.Y - 22), new Point(5, 0)) { Translate = false, TextAlignment = InterfaceElement.Gravities.Left | InterfaceElement.Gravities.Top };
                _versionLabel.SetText(Values.VersionString);
                headerLayout.AddElement(_versionLabel);
                headerLayout.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "settings_menu_header", new Point(150, (int)(height * Values.MenuHeaderSize)), new Point(-8, 0)));
            }
            _settingsLayout.AddElement(headerLayout);
            _contentLayout = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuContentSize) - 12), Selectable = true };

            // Fixed-width buttons so the two columns form a centered cluster.
            var buttonSize = new Point(120, _buttonSize);

            for (int i = 0; i < _entries.Length; i += 2)
            {
                var row = new InterfaceListLayout
                {
                    Size = new Point(width, _buttonSize + _rowGap),
                    Selectable = true,
                    HorizontalMode = true
                };

                // Left column.
                AddSettingsButton(row, buttonSize, _entries[i]);

                // Right column (only if a second entry exists for this row).
                if (i + 1 < _entries.Length)
                    AddSettingsButton(row, buttonSize, _entries[i + 1]);

                _rows.Add(row);
                _contentLayout.AddElement(row);
            }

            // Bottom Bar / Exit Button:
            _bottomBar = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuFooterSize)), Selectable = true };
            _bottomBar.AddElement(new InterfaceButton(new Point(100, 18), new Point(2, 4), "settings_menu_back", element => { ExitPage(); }));
            _settingsLayout.AddElement(_contentLayout);
            _settingsLayout.AddElement(_bottomBar);
            PageLayout = _settingsLayout;
        }

        private void AddSettingsButton(InterfaceListLayout row, Point size, (string Text, string Tooltip, Type Page) entry)
        {
            // entry is a per-call parameter, so the closure captures the correct page.
            row.AddElement(new InterfaceButton(size, new Point(_columnGap / 2, 2), entry.Text,
                element => { Game1.UiPageManager.ChangePage(entry.Page); }));
        }

        public override void Update(CButtons pressedButtons, GameTime gameTime)
        {
            // Capture selection state BEFORE navigation is processed.
            bool contentBefore = _settingsLayout.SelectionIndex == 1;
            int oldRow = _contentLayout.SelectionIndex;
            int oldCol = (oldRow >= 0 && oldRow < _rows.Count) ? _rows[oldRow].SelectionIndex : _desiredColumn;

            // Navigation happens synchronously inside here (PageLayout.PressedButton).
            base.Update(pressedButtons, gameTime);

            // Reconcile the column after navigation.
            bool contentAfter = _settingsLayout.SelectionIndex == 1;
            int newRow = _contentLayout.SelectionIndex;

            if (contentAfter && newRow >= 0 && newRow < _rows.Count)
            {
                if (contentBefore && newRow == oldRow)
                {
                    // Same row: only a left/right move could have changed the column.
                    int newCol = _rows[newRow].SelectionIndex;
                    if (newCol != oldCol)
                        _desiredColumn = newCol;
                }
                else
                {
                    // Vertical move, or focus just (re)entered the content area:
                    // snap the now-focused row to the column we want to keep.
                    ForceColumn(newRow, _desiredColumn);
                }
            }

            // The back button was pressed.
            if (ControlHandler.ButtonPressed(ControlHandler.CancelButton))
                ExitPage();

            // The tooltip button was pressed.
            if (ControlHandler.ButtonPressed(CButtons.Y))
            {
                _showTooltip = !_showTooltip;
                if (_showTooltip)
                    Game1.AudioManager.PlaySoundEffect("D360-21-15");
            }
            // Hide the tooltip when pressing anything.
            else if (ControlHandler.AnyButtonPressed())
                _showTooltip = false;
        }

        private void ForceColumn(int rowIndex, int column)
        {
            var row = _rows[rowIndex];

            // Clamp to what this row actually holds (the last row may have a single button).
            int target = MathHelper.Clamp(column, 0, row.Elements.Count - 1);

            // Select(Directions.Top) leaves a horizontal row's index untouched, so it may
            // already be on the right column -> nothing to do.
            if (row.SelectionIndex == target)
                return;

            row.Deselect(false);
            row.Select(target, true);
        }

        public override void OnLoad(Dictionary<string, object> intent)
        {
            PageLayout.Deselect(false);

            // Reset all rows selection index.
            foreach (var row in _rows)
                row.SetSelectionIndex(0);

            PageLayout.Select(InterfaceElement.Directions.Top, false);

            _desiredColumn = 0;

            // only show the version in the main menu
            if (Game1.ScreenManager.CurrentScreenId == Values.ScreenNameGame)
                _versionLabel.TextColor = Color.Transparent;
            else
                _versionLabel.TextColor = InterfaceElement.MainTextColor;
        }

        private void ExitPage()
        {
            // save the new settings
            SettingsSaveLoad.SaveSettings();

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
            // Back button (bottom bar) is child index 2 of the settings layout.
            if (_settingsLayout.SelectionIndex == 2)
                return Game1.LanguageManager.GetString("tooltip_default", "error");

            // Row = which horizontal layout is selected; Column = which button within it.
            int row = _contentLayout.SelectionIndex;
            if (row < 0 || row >= _rows.Count)
                return "Select an option to view its tooltip.";

            int col = _rows[row].SelectionIndex;
            int index = row * 2 + col;
            if (index < 0 || index >= _entries.Length)
                return "Select an option to view its tooltip.";

            return Game1.LanguageManager.GetString(_entries[index].Tooltip, "error");
        }
    }
}
