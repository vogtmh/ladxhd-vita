using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    class PresetOptionsPage : InterfacePage
    {
        private readonly InterfaceListLayout _presetSettingsLayout;
        private readonly InterfaceListLayout _contentLayout;
        private readonly InterfaceListLayout _bottomBar;

        List<string> _tooltips = new List<string>();
        private bool _showTooltip;

        public PresetOptionsPage(int width, int height)
        {
            EnableTooltips = true;

            // Audio Settings Layout
            _presetSettingsLayout = new InterfaceListLayout { Size = new Point(width, height - 12), Selectable = true };

            var buttonWidth = 320;
            var buttonSize = new Point(150, 14);

            _presetSettingsLayout.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "settings_preset_header",
                new Point(buttonWidth, (int)(height * Values.MenuHeaderSize)), new Point(0, 0)));
            _contentLayout = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuContentSize - 12)), Selectable = true, ContentAlignment = InterfaceElement.Gravities.Top };

            // Button: Set Default Option Values
            _contentLayout.AddElement(new InterfaceButton(buttonSize, new Point(1, 2), "settings_preset_setdefault", element => { RestoreDefaults(); }));
            _tooltips.Add("tooltip_preset_setdefault");

            // Button: Set Modern Values
            _contentLayout.AddElement(new InterfaceButton(buttonSize, new Point(1, 2), "settings_preset_setmodern", element => { SetModernValues(); }));
            _tooltips.Add("tooltip_preset_setmodern");

            // Button: Set Classic Values
            _contentLayout.AddElement(new InterfaceButton(buttonSize, new Point(1, 2), "settings_preset_setclassic", element => { SetClassicValues(); }));
            _tooltips.Add("tooltip_preset_setclassic");

            // Button: Set Hybrid Values
            _contentLayout.AddElement(new InterfaceButton(buttonSize, new Point(1, 2), "settings_preset_sethybrid", element => { SetHybridValues(); }));
            _tooltips.Add("tooltip_preset_sethybrid");

            // Button: Set Purist Values
            _contentLayout.AddElement(new InterfaceButton(buttonSize, new Point(1, 2), "settings_preset_purist", element => { SetPuristValues(); }));
            _tooltips.Add("tooltip_preset_purist");

            // Bottom Bar / Back Button:
            _bottomBar = new InterfaceListLayout() { Size = new Point(width, (int)(height * Values.MenuFooterSize)), Selectable = true, HorizontalMode = true };
            _bottomBar.AddElement(new InterfaceButton(new Point(100, 18), new Point(2, 4), "settings_menu_back", element => { Game1.UiPageManager.PopPage(); }));
            _presetSettingsLayout.AddElement(_contentLayout);
            _presetSettingsLayout.AddElement(_bottomBar);
            PageLayout = _presetSettingsLayout;
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

        private void RestoreDefaults()
        {
            GameSettings.RestoreDefaults();
            UpdateSettingsGUI();
        }

        public void SetModernValues()
        {
            GameSettings.ClassicSword = false;
            GameSettings.VarWidthFont = true;
            GameSettings.Unmissables = true;
            GameSettings.PhotosColor = true;
            GameSettings.MapTeleport = 1;
            GameSettings.CameraMode = 0;
            GameSettings.ModernFogEnabled = true;
            GameSettings.ModernFogDungeon = true;
            GameSettings.ModernFogSharp = 0.25f;
            GameSettings.ModernFogOpacity = 0.95f;
            GameSettings.ModernFogFade =  250;
            GameSettings.ClassicOverworld = false;
            GameSettings.ClassicHouses = false;
            GameSettings.ClassicCaves = false;
            GameSettings.ClassicDungeons = false;
            GameSettings.ClassicCastle = false;
            GameSettings.ClassicEgg = false;
            GameSettings.Classic2DMaps = false;
            GameSettings.ClassicBosses = false;
            GameSettings.ClassicBorder = 0;
            GameSettings.CameraLock = false;
            GameSettings.GlobalLights = true;
            GameSettings.ObjectLights = true;
            GameSettings.ScreenShake = true;
            GameSettings.ExScreenShake = true;
            GameSettings.ClassicSprites = false;
            GameSettings.FogEffects = true;
            GameSettings.PixelSnapping = false;
            GameSettings.EnableShadows = true;
            GameSettings.ClassicMusic = false;
            GameSettings.OldMovement = false;
            GameSettings.DigitalAnalog = false;
            GameSettings.EnemyBonusHP = 0;
            GameSettings.DamageFactor = 4;
            GameSettings.DmgCooldown = 16;
            GameSettings.MoveSpeedAdded = 0;
            GameSettings.NoHeartDrops = false;
            GameSettings.NoDamageLaunch = false;
            GameSettings.MirrorReflects = true;
            GameSettings.SwGrabNormal = true;
            GameSettings.SwGrabWorldItem = false;
            GameSettings.SwGrabFairy = true;
            GameSettings.SwGrabSmallKey = false;
            GameSettings.SwBoomerang = true;
            GameSettings.SwSmackBombs = true;
            GameSettings.SwMissileBlock = false;
            GameSettings.SwBreakPots = true;
            GameSettings.SwBeamShrubs = false;
            UpdateSettingsGUI();
        }

        public void SetClassicValues()
        {
            GameSettings.ClassicSword = false;
            GameSettings.Unmissables = true;
            GameSettings.PhotosColor = false;
            GameSettings.MapTeleport = 0;
            GameSettings.CameraMode = 1;
            GameSettings.ClassicScrolling = true;
            GameSettings.ClassicOverworld = false;
            GameSettings.ClassicHouses = false;
            GameSettings.ClassicCaves = false;
            GameSettings.ClassicDungeons = false;
            GameSettings.ClassicCastle = false;
            GameSettings.ClassicEgg = false;
            GameSettings.Classic2DMaps = false;
            GameSettings.ClassicBosses = false;
            GameSettings.ClassicBorder = 1;
            GameSettings.ClassicAlpha =  1.00f;
            GameSettings.CameraLock = true;
            GameSettings.GlobalLights = true;
            GameSettings.ObjectLights = false;
            GameSettings.ScreenShake = true;
            GameSettings.ExScreenShake = true;
            GameSettings.ClassicSprites = true;
            GameSettings.FogEffects = false;
            GameSettings.PixelSnapping = false;
            GameSettings.EnableShadows = true;
            GameSettings.ClassicMusic = true;
            GameSettings.OldMovement = false;
            GameSettings.DigitalAnalog = false;
            GameSettings.EnemyBonusHP = 0;
            GameSettings.DamageFactor = 4;
            GameSettings.DmgCooldown = 16;
            GameSettings.MoveSpeedAdded = 0;
            GameSettings.NoHeartDrops = false;
            GameSettings.NoDamageLaunch = false;
            GameSettings.MirrorReflects = false;
            GameSettings.SwGrabNormal = true;
            GameSettings.SwGrabWorldItem = false;
            GameSettings.SwGrabFairy = false;
            GameSettings.SwGrabSmallKey = false;
            GameSettings.SwBoomerang = false;
            GameSettings.SwSmackBombs = false;
            GameSettings.SwMissileBlock = false;
            GameSettings.SwBreakPots = false;
            GameSettings.SwBeamShrubs = false;
            UpdateSettingsGUI();
        }

        public void SetHybridValues()
        {
            GameSettings.ClassicSword = false;
            GameSettings.Unmissables = true;
            GameSettings.PhotosColor = true;
            GameSettings.MapTeleport = 1;
            GameSettings.CameraMode = 2;
            GameSettings.ModernFogEnabled = true;
            GameSettings.ModernFogDungeon = true;
            GameSettings.ModernFogSharp = 0.25f;
            GameSettings.ModernFogOpacity = 0.95f;
            GameSettings.ModernFogFade =  250;
            GameSettings.ClassicScrolling = true;
            GameSettings.ClassicOverworld = false;
            GameSettings.ClassicHouses = false;
            GameSettings.ClassicCaves = false;
            GameSettings.ClassicDungeons = true;
            GameSettings.ClassicCastle = true;
            GameSettings.ClassicEgg = true;
            GameSettings.Classic2DMaps = false;
            GameSettings.ClassicBosses = false;
            GameSettings.ClassicBorder = 1;
            GameSettings.ClassicAlpha =  1.00f;
            GameSettings.CameraLock = false;
            GameSettings.GlobalLights = true;
            GameSettings.ObjectLights = true;
            GameSettings.ScreenShake = true;
            GameSettings.ExScreenShake = true;
            GameSettings.ClassicSprites = false;
            GameSettings.FogEffects = true;
            GameSettings.PixelSnapping = false;
            GameSettings.EnableShadows = true;
            GameSettings.ClassicMusic = false;
            GameSettings.HeartBeep = true;
            GameSettings.OldMovement = false;
            GameSettings.DigitalAnalog = false;
            GameSettings.EnemyBonusHP = 0;
            GameSettings.DamageFactor = 4;
            GameSettings.DmgCooldown = 16;
            GameSettings.MoveSpeedAdded = 0;
            GameSettings.NoHeartDrops = false;
            GameSettings.NoDamageLaunch = false;
            GameSettings.MirrorReflects = true;
            GameSettings.SwGrabNormal = true;
            GameSettings.SwGrabWorldItem = false;
            GameSettings.SwGrabFairy = true;
            GameSettings.SwGrabSmallKey = false;
            GameSettings.SwBoomerang = false;
            GameSettings.SwSmackBombs = false;
            GameSettings.SwMissileBlock = false;
            GameSettings.SwBreakPots = false;
            GameSettings.SwBeamShrubs = false;
            UpdateSettingsGUI();
        }

        public void SetPuristValues()
        {
            GameSettings.ClassicSword = true;
            GameSettings.VarWidthFont = false;
            GameSettings.NoHelperText = false;
            GameSettings.DialogSkip = false;
            GameSettings.Unmissables = false;
            GameSettings.PhotosColor = false;
            GameSettings.MapTeleport = 0;
            GameSettings.CameraMode = 1;
            GameSettings.ClassicScrolling = true;
            GameSettings.ClassicOverworld = false;
            GameSettings.ClassicHouses = false;
            GameSettings.ClassicCaves = false;
            GameSettings.ClassicDungeons = false;
            GameSettings.ClassicCastle = false;
            GameSettings.ClassicEgg = false;
            GameSettings.Classic2DMaps = false;
            GameSettings.ClassicBosses = false;
            GameSettings.ClassicBorder = 1;
            GameSettings.ClassicAlpha =  1.00f;
            GameSettings.CameraLock = true;
            GameSettings.GlobalLights = false;
            GameSettings.ObjectLights = false;
            GameSettings.ScreenShake = true;
            GameSettings.ExScreenShake = false;
            GameSettings.ClassicSprites = true;
            GameSettings.FogEffects = false;
            GameSettings.PixelSnapping = true;
            GameSettings.EnableShadows = false;
            GameSettings.ClassicMusic = true;
            GameSettings.HeartBeep = true;
            GameSettings.OldMovement = true;
            GameSettings.DigitalAnalog = true;
            GameSettings.EnemyBonusHP = 0;
            GameSettings.DamageFactor = 4;
            GameSettings.DmgCooldown = 16;
            GameSettings.MoveSpeedAdded = 0;
            GameSettings.NoHeartDrops = false;
            GameSettings.NoDamageLaunch = false;
            GameSettings.MirrorReflects = false;
            GameSettings.SwGrabNormal = true;
            GameSettings.SwGrabWorldItem = false;
            GameSettings.SwGrabFairy = false;
            GameSettings.SwGrabSmallKey = false;
            GameSettings.SwBoomerang = false;
            GameSettings.SwSmackBombs = false;
            GameSettings.SwMissileBlock = false;
            GameSettings.SwBreakPots = false;
            GameSettings.SwBeamShrubs = false;
            UpdateSettingsGUI();
        }

        public void UpdateSettingsGUI()
        {
            if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(GameSettingsPage), out var gamePage))
            {
                var GameSettingsPage = (GameSettingsPage)gamePage;
                GameSettingsPage.SetClassicSword(GameSettings.ClassicSword);
                GameSettingsPage.SetItemSlotsRight(GameSettings.ItemsOnRight);
                GameSettingsPage.SetMenuBrickTex(GameSettings.MenuBorder);
                GameSettingsPage.SetSavePosition(GameSettings.StoreSavePos);
                GameSettingsPage.SetAutomaticSave(GameSettings.Autosave);
                GameSettingsPage.SetAchieveNotify(GameSettings.HideAchievement);
                GameSettingsPage.SetIntroThreading(GameSettings.StreamAssets);
                GameSettingsPage.SetLoadMapThread(GameSettings.ThreadMapLoad);
            }
            if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(ReduxSettingsPage), out var reduxPage))
            {
                var ReduxSettingsPage = (ReduxSettingsPage)reduxPage;
                ReduxSettingsPage.SetMapTeleportValue(GameSettings.MapTeleport);
                ReduxSettingsPage.SetVariableWidthFont(GameSettings.VarWidthFont);
                ReduxSettingsPage.SetDisableHelperText(GameSettings.NoHelperText);
                ReduxSettingsPage.SetEnableDialogSkip(GameSettings.DialogSkip);
                ReduxSettingsPage.SetDisableCensorship(GameSettings.Uncensored);
                ReduxSettingsPage.SetEnableUnmissables(GameSettings.Unmissables);
                ReduxSettingsPage.SetColoredPhotographs(GameSettings.PhotosColor);
                ReduxSettingsPage.SetNoAnimalDamage(GameSettings.NoAnimalDamage);
                
            }
            if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(CameraSettingsPage), out var camPage))
            {
                var CameraSettingsPage = (CameraSettingsPage)camPage;
                CameraSettingsPage.SetCameraActiveType(GameSettings.CameraMode);
                CameraSettingsPage.SetModernFogEnabled(GameSettings.ModernFogEnabled);
                CameraSettingsPage.SetModernFogDungeons(GameSettings.ModernFogDungeon);
                CameraSettingsPage.SetModernFogSharpness((int)MathF.Round(GameSettings.ModernFogSharp * 100));
                CameraSettingsPage.SetModernFogOpacity((int)MathF.Round(GameSettings.ModernFogOpacity * 100));
                CameraSettingsPage.SetModernFogFadeTime(GameSettings.ModernFogFade);
                CameraSettingsPage.SetClassicCamBorder(GameSettings.ClassicBorder);
                CameraSettingsPage.SetClassicBorderAlpha((int)MathF.Round(GameSettings.ClassicAlpha * 100));
                CameraSettingsPage.SetClassicBorderBias(GameSettings.ClassicBias);
                CameraSettingsPage.SetClassicScrolling(GameSettings.ClassicScrolling);
                CameraSettingsPage.SetClassicScaleLock(GameSettings.ClassicScaling);
                CameraSettingsPage.SetClassicItemDrops(GameSettings.ClassicItemDrop);
                CameraSettingsPage.SetCameraLock(GameSettings.CameraLock);
                CameraSettingsPage.SetCameraSmoothCam(GameSettings.SmoothCamera);
            }
            if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(CameraCustomPage), out var customCamPage))
            {
                var CameraCustomPage = (CameraCustomPage)customCamPage;
                CameraCustomPage.UpdateAllButtons();
            }
            if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(VideoSettingsPage), out var videoPage))
            {
                var VideoSettingsPage = (VideoSettingsPage)videoPage;
                VideoSettingsPage.SetGameScaleValue(GameSettings.GameScale);
                VideoSettingsPage.SetInterfaceScale(GameSettings.UiScale);
                VideoSettingsPage.SetVerticalSync(GameSettings.VerticalSync);
                VideoSettingsPage.SetNoBlurEffects(GameSettings.DisableUiBlur);
                VideoSettingsPage.SetPixelSnapping(GameSettings.PixelSnapping);
                VideoSettingsPage.SetShowPixelGrid(GameSettings.PixelGrid);
                VideoSettingsPage.SetEpilepsySafe(GameSettings.EpilepsySafe);
            }
            if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(GraphicsSettingsPage), out var graphicsPage))
            {
                var GraphicsSettingsPage = (GraphicsSettingsPage)graphicsPage;
                GraphicsSettingsPage.SetSeqScaleAmplifier(GameSettings.SeqScaleAmplify);
                GraphicsSettingsPage.SetColorCorrection(GameSettings.ColorCorrection);
                GraphicsSettingsPage.SetDynamicShadows(GameSettings.EnableShadows);
                GraphicsSettingsPage.SetForestFogEffects(GameSettings.FogEffects);
                GraphicsSettingsPage.SetGlobalLighting(GameSettings.GlobalLights);
                GraphicsSettingsPage.SetObjectLighting(GameSettings.ObjectLights);
                GraphicsSettingsPage.SetCamScreenShake(GameSettings.ScreenShake);
                GraphicsSettingsPage.SetCamExScreenShake(GameSettings.ExScreenShake);
                GraphicsSettingsPage.SetClassicSprites(GameSettings.ClassicSprites);
            }
            if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(AudioSettingsPage), out var audioPage))
            {
                var AudioSettingsPage = (AudioSettingsPage)audioPage;
                AudioSettingsPage.SetMusicVolume(GameSettings.MusicVolume);
                AudioSettingsPage.SetSoundVolume(GameSettings.EffectVolume);
                AudioSettingsPage.SetClassicAudio(GameSettings.ClassicMusic);
                if (Game1.PlatformWindow.SupportsInactiveWindowInput)
                    AudioSettingsPage.SetMuteInactive(GameSettings.MuteInactive);
                AudioSettingsPage.SetHealthAlarm(GameSettings.HeartBeep);
                AudioSettingsPage.SetPowerupMusic(GameSettings.MutePowerups);
                AudioSettingsPage.SetMuteAchieves(GameSettings.MuteAchievement);
            }
            if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(ControlSettingsPage), out var controlPage))
            {
                var ControlSettingsPage = (ControlSettingsPage)controlPage;
                ControlSettingsPage.SetDeadZoneValue((int)MathF.Round(GameSettings.DeadZone * 100));
                ControlSettingsPage.SetTriggerScale(GameSettings.TriggersScale);
                ControlSettingsPage.SetSixButtons(GameSettings.SixButtons);
                ControlSettingsPage.SetSwapButtons(GameSettings.SwapButtons);
                ControlSettingsPage.SetClassicMove(GameSettings.OldMovement);
                ControlSettingsPage.SetDigitalAnalog(GameSettings.DigitalAnalog);
            }
            if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(ModifierSettingsPage), out var modPage))
            {
                var ModifiersPage = (ModifierSettingsPage)modPage;
                ModifiersPage.SetEnemyHitPoints(GameSettings.EnemyBonusHP);
                ModifiersPage.SetDamageTaken(GameSettings.DamageFactor);
                ModifiersPage.SetDamageCooldown(GameSettings.DmgCooldown);
                ModifiersPage.SetMovementSpeed((int)MathF.Round(GameSettings.MoveSpeedAdded * 10));
                ModifiersPage.SetNoHeartDrops(GameSettings.NoHeartDrops);
                ModifiersPage.SetNoDamageLaunch(GameSettings.NoDamageLaunch);
                ModifiersPage.SetMirrorReflects(GameSettings.MirrorReflects);
            }
            if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(SwordInteractPage), out var swordPage))
            {
                var SwordInteractPage = (SwordInteractPage)swordPage;
                SwordInteractPage.SetSwordCollectNormal(GameSettings.SwGrabNormal);
                SwordInteractPage.SetSwordCollectStatic(GameSettings.SwGrabWorldItem);
                SwordInteractPage.SetSwordCollectFairy(GameSettings.SwGrabFairy);
                SwordInteractPage.SetSwordCollectKeys(GameSettings.SwGrabSmallKey);
                SwordInteractPage.SetSwordBounceBoomerang(GameSettings.SwBoomerang);
                SwordInteractPage.SetSwordBounceBombs(GameSettings.SwSmackBombs);
                SwordInteractPage.SetSwordBlockProjectile(GameSettings.SwMissileBlock);
                SwordInteractPage.SetSwordSmashesPots(GameSettings.SwBreakPots);
                SwordInteractPage.SetSwordBeamCutsShrubs(GameSettings.SwBeamShrubs);
            }
            if (Game1.UiPageManager.InsideElement.TryGetValue(typeof(CheatsSettingsPage), out var cheatPage))
            {
                var CheatSettingsPage = (CheatsSettingsPage)cheatPage;
                CheatSettingsPage.SetCheatInvincibility(GameSettings.ChInvincibility);
                CheatSettingsPage.SetCheatInfiniteRupees(GameSettings.ChInfinRupees);
                CheatSettingsPage.SetCheatInfinitePowder(GameSettings.ChInfinPowder);
                CheatSettingsPage.SetCheatInfiniteBombs(GameSettings.ChInfinBombs);
                CheatSettingsPage.SetCheatInfiniteArrows(GameSettings.ChInfinArrows);
                CheatSettingsPage.SetCheatDisableClipping(GameSettings.ChNoClipping);
                CheatSettingsPage.SetCheatOneHitKills(GameSettings.ChOneHitKills);
                CheatSettingsPage.SetCheatGiveAllItems(GameSettings.ChGiveAllItems);
            }
            Game1.ScaleChanged = true;
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, float height, float alpha)
        {
            // Always draw the menu even when not showing tooltips.
            base.Draw(spriteBatch, position, height, alpha);

            // If the user pressed the top most face button, show the tooltip window.
            if (_showTooltip)
            {
                string tooltipText = PageTooltip.GetTooltipIndex(_presetSettingsLayout, _contentLayout, _tooltips);
                PageTooltip.Draw(spriteBatch, tooltipText);
            }
        }
    }
}
