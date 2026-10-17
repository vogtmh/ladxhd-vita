using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    class AudioSettingsPage : InterfacePage
    {
        private readonly InterfaceListLayout _audioSettingsLayout;
        private readonly InterfaceListLayout _contentLayout;
        private readonly InterfaceListLayout _bottomBar;

        private readonly InterfaceSlider     _sliderMusicVolume;
        private readonly InterfaceSlider     _sliderSoundVolume;
        private readonly InterfaceListLayout _toggleAudioClassic;
        private readonly InterfaceListLayout _toggleMuteInactive;
        private readonly InterfaceListLayout _toggleHealthAlarm;
        private readonly InterfaceListLayout _togglePowerupMusic;
        private readonly InterfaceListLayout _toggleMuteAchievements;

        List<string> _tooltips = new List<string>();
        private bool _showTooltip;

        public void SetMusicVolume(int value)   => _sliderMusicVolume.SetValue(value);
        public void SetSoundVolume(int value)   => _sliderSoundVolume.SetValue(value);
        public void SetClassicAudio(bool state) => _toggleAudioClassic.Toggle.SetValue(state);
        public void SetMuteInactive(bool state) => _toggleMuteInactive.Toggle.SetValue(state);
        public void SetHealthAlarm(bool state)  => _toggleHealthAlarm.Toggle.SetValue(state);
        public void SetPowerupMusic(bool state) => _togglePowerupMusic.Toggle.SetValue(state);
        public void SetMuteAchieves(bool state) => _toggleMuteAchievements.Toggle.SetValue(state);

        public AudioSettingsPage(int width, int height)
        {
            EnableTooltips = true;
            var buttonWidth = 320;
            var buttonHeight = 14;

            // Audio Settings Layout
            _audioSettingsLayout = new InterfaceListLayout { Size = new Point(width, height - 12), Selectable = true };
            _audioSettingsLayout.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "settings_audio_header",
                new Point(buttonWidth, (int)(height * Values.MenuHeaderSize)), new Point(0, 0)));
            _contentLayout = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuContentSize - 12)), Selectable = true, ContentAlignment = InterfaceElement.Gravities.Top, Scrollable = false };

            // Slider: Music Volume
            _sliderMusicVolume = new InterfaceSlider("settings_audio_music_volume",
                buttonWidth, 11, new Point(1, 2), 0, 100, 5, GameSettings.MusicVolume, 
                number => { GameSettings.MusicVolume = number; })
                { SetString = number => " " + number + "%" };
            _contentLayout.AddElement(_sliderMusicVolume);
            _tooltips.Add("tooltip_audio_music_volume");

            // Slider: Sound Effects Volume
            _sliderSoundVolume = new InterfaceSlider("settings_audio_effect_volume",
                buttonWidth, 11, new Point(1, 2), 0, 100, 5, GameSettings.EffectVolume, 
                number => { Game1.AudioManager.UpdateSoundEffects(); GameSettings.EffectVolume = number; })
                { SetString = number => " " + number + "%" };
            _contentLayout.AddElement(_sliderSoundVolume);
            _tooltips.Add("tooltip_audio_effect_volume");

            // Toggle: Classic Music Tile Cues
            _toggleAudioClassic = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_audio_classic_music", GameSettings.ClassicMusic, 
                newState => { ToggleMusicCues(newState); });
            _contentLayout.AddElement(_toggleAudioClassic);
            _tooltips.Add("tooltip_audio_classic_music");

            if (Game1.PlatformWindow.SupportsInactiveWindowInput)
            {
                // Toggle: Mute Inactive Window
                _toggleMuteInactive = InterfaceToggle.GetToggleButton(
                    new Point(buttonWidth, buttonHeight), new Point(5, 2),
                    "settings_audio_mute_inactive", GameSettings.MuteInactive,
                    newState => { GameSettings.MuteInactive = newState; });
                _contentLayout.AddElement(_toggleMuteInactive);
                _tooltips.Add("tooltip_audio_mute_inactive");
            }

            // Toggle: Low Hearts Alarm
            _toggleHealthAlarm = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_audio_heartbeep", GameSettings.HeartBeep, 
                newState => { GameSettings.HeartBeep = newState; });
            _contentLayout.AddElement(_toggleHealthAlarm);
            _tooltips.Add("tooltip_audio_heartbeep");

            // Toggle: Mute Powerup Music
            _togglePowerupMusic = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_audio_mute_powerups", GameSettings.MutePowerups, 
                newState => { GameSettings.MutePowerups = newState; });
            _contentLayout.AddElement(_togglePowerupMusic);
            _tooltips.Add("tooltip_audio_mute_powerups");

            // Toggle: Mute Achievement Sound
            _toggleMuteAchievements = InterfaceToggle.GetToggleButton(
                new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_audio_mute_achievement", GameSettings.MuteAchievement, 
                newState => { GameSettings.MuteAchievement = newState; });
            _contentLayout.AddElement(_toggleMuteAchievements);
            _tooltips.Add("tooltip_audio_mute_achievement");

            // Bottom Bar / Back Button:
            _bottomBar = new InterfaceListLayout() { Size = new Point(width, (int)(height * Values.MenuFooterSize)), Selectable = true, HorizontalMode = true };
            _bottomBar.AddElement(new InterfaceButton(new Point(100, 18), new Point(2, 4), "settings_menu_back", element => { Game1.UiPageManager.PopPage(); }));
            _audioSettingsLayout.AddElement(_contentLayout);
            _audioSettingsLayout.AddElement(_bottomBar);
            PageLayout = _audioSettingsLayout;
        }

        private void ToggleMusicCues(bool newState)
        {
            // Set the new state.
            GameSettings.ClassicMusic = newState;

            // Get a reference to the map if it exists.
            var currentMap = MapManager.ObjLink?.Map;

            // If a map exists then find the overworld "music tile" if possible.
            if (currentMap != null )
            {
                // "GetObjectsOfType" is not something that should be used often, but in this case it's fine.
                List<GameObject> objects = currentMap.Objects.GetObjectsOfType(typeof(ObjMusicTile));

                // There should only be a single music tile object and only on the overworld.
                foreach (var musicTile in objects.OfType<ObjMusicTile>())
                    musicTile.UpdateMusicData();
            }
        }


        public override void Update(CButtons pressedButtons, GameTime gameTime)
        {
            base.Update(pressedButtons, gameTime);

            // The back button was pressed.
            if (ControlHandler.ButtonPressed(ControlHandler.CancelButton))
                Game1.UiPageManager.PopPage();

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

        public override void OnLoad(Dictionary<string, object> intent)
        {
            // the left button is always the first one selected
            _bottomBar.Deselect(false);
            _bottomBar.Select(InterfaceElement.Directions.Left, false);
            _bottomBar.Deselect(false);

            PageLayout.Deselect(false);
            PageLayout.Select(InterfaceElement.Directions.Top, false);
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, float height, float alpha)
        {
            // Always draw the menu even when not showing tooltips.
            base.Draw(spriteBatch, position, height, alpha);

            // If the user pressed the top most face button, show the tooltip window.
            if (_showTooltip)
            {
                string tooltipText = PageTooltip.GetTooltipIndex(_audioSettingsLayout, _contentLayout, _tooltips);
                PageTooltip.Draw(spriteBatch, tooltipText);
            }
        }
    }
}
