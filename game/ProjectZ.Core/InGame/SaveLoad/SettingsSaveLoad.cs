using Microsoft.Xna.Framework;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.SaveLoad
{
    class SettingsSaveLoad
    {
        private static string SettingsFilePath => SaveManager.GetSettingsFile();

        public static void LoadSettings()
        {
            if (new SaveManager() is not { } saveManager || !saveManager.LoadFile(SettingsFilePath))
                return;

            // Game Settings
            Game1.LanguageManager.CurrentLanguageIndex = saveManager.GetInt("CurrentLanguage", Game1.LanguageManager.CurrentLanguageIndex);
            Game1.LanguageManager.CurrentSubLanguageIndex = saveManager.GetInt("CurrentSubLanguage", Game1.LanguageManager.CurrentSubLanguageIndex);
            GameSettings.MenuBorder = saveManager.GetInt("MenuBorder", GameSettings.MenuBorder);
            GameSettings.ClassicSword = saveManager.GetBool("ClassicSword", GameSettings.ClassicSword);
            GameSettings.StoreSavePos = saveManager.GetBool("StoreSavePos", GameSettings.StoreSavePos);
            GameSettings.LastSavePos = saveManager.GetInt("LastSavePos", GameSettings.LastSavePos);
            GameSettings.Autosave = saveManager.GetBool("Autosave", GameSettings.Autosave);
            GameSettings.SharedStorage = saveManager.GetBool("SharedStorage", GameSettings.SharedStorage);
            GameSettings.ItemsOnRight = saveManager.GetBool("ItemsOnRight", GameSettings.ItemsOnRight);
            GameSettings.HideAchievement = saveManager.GetBool("HideAchievement", GameSettings.HideAchievement);
            GameSettings.StreamAssets = saveManager.GetBool("StreamAssets", GameSettings.StreamAssets);
            GameSettings.ThreadMapLoad = saveManager.GetBool("ThreadMapLoad", GameSettings.ThreadMapLoad);

            // Redux Settings
            GameSettings.MapTeleport = saveManager.GetInt("MapTeleport", GameSettings.MapTeleport);
            GameSettings.VarWidthFont = saveManager.GetBool("VarWidthFont", GameSettings.VarWidthFont);
            GameSettings.NoHelperText = saveManager.GetBool("NoHelperText", GameSettings.NoHelperText);
            GameSettings.DialogSkip = saveManager.GetBool("DialogSkip", GameSettings.DialogSkip);
            GameSettings.Uncensored = saveManager.GetBool("Uncensored", GameSettings.Uncensored);
            GameSettings.Unmissables = saveManager.GetBool("Unmissables", GameSettings.Unmissables);
            GameSettings.PhotosColor = saveManager.GetBool("PhotosColor", GameSettings.PhotosColor);
            GameSettings.NoAnimalDamage = saveManager.GetBool("NoAnimalDamage", GameSettings.NoAnimalDamage);

            // Camera Settings
            GameSettings.CameraMode = saveManager.GetInt("CameraMode", GameSettings.CameraMode);
            GameSettings.ModernFogEnabled = saveManager.GetBool("ModernFogEnabled", GameSettings.ModernFogEnabled);
            GameSettings.ModernFogDungeon = saveManager.GetBool("ModernFogDungeon", GameSettings.ModernFogDungeon);
            GameSettings.ModernFogSharp = saveManager.GetFloat("ModernFogSharp", GameSettings.ModernFogSharp);
            GameSettings.ModernFogOpacity = saveManager.GetFloat("ModernFogOpacity", GameSettings.ModernFogOpacity);
            GameSettings.ModernFogFade = saveManager.GetInt("ModernFogFade", GameSettings.ModernFogFade);
            GameSettings.ClassicBorder = saveManager.GetInt("ClassicBorder", GameSettings.ClassicBorder);
            GameSettings.ClassicAlpha = saveManager.GetFloat("ClassicAlpha", GameSettings.ClassicAlpha);
            GameSettings.ClassicBias = saveManager.GetInt("ClassicBias", GameSettings.ClassicBias);
            GameSettings.ClassicScrolling = saveManager.GetBool("ClassicScrolling", GameSettings.ClassicScrolling);
            GameSettings.ClassicScaling = saveManager.GetBool("ClassicScaling", GameSettings.ClassicScaling);
            GameSettings.ClassicItemDrop = saveManager.GetBool("ClassicItemDrop", GameSettings.ClassicItemDrop);
            GameSettings.CameraLock = saveManager.GetBool("CameraLock", GameSettings.CameraLock);
            GameSettings.SmoothCamera = saveManager.GetBool("SmoothCamera", GameSettings.SmoothCamera);

            // Custom Camera Settings
            GameSettings.ClassicOverworld = saveManager.GetBool("ClassicOverworld", GameSettings.ClassicOverworld);
            GameSettings.ClassicHouses = saveManager.GetBool("ClassicHouses", GameSettings.ClassicHouses);
            GameSettings.ClassicCaves = saveManager.GetBool("ClassicCaves", GameSettings.ClassicCaves);
            GameSettings.ClassicDungeons = saveManager.GetBool("ClassicDungeons", GameSettings.ClassicDungeons);
            GameSettings.ClassicCastle = saveManager.GetBool("ClassicCastle", GameSettings.ClassicCastle);
            GameSettings.ClassicEgg = saveManager.GetBool("ClassicEgg", GameSettings.ClassicEgg);
            GameSettings.Classic2DMaps = saveManager.GetBool("Classic2DMaps", GameSettings.Classic2DMaps);
            GameSettings.ClassicBosses = saveManager.GetBool("ClassicBosses", GameSettings.ClassicBosses);

            // Video Settings
            GameSettings.WindowWidth  = saveManager.GetInt("WindowWidth",  GameSettings.WindowWidth);
            GameSettings.WindowHeight = saveManager.GetInt("WindowHeight", GameSettings.WindowHeight);
            GameSettings.WindowPosX = saveManager.GetInt("WindowPosX", GameSettings.WindowPosX);
            GameSettings.WindowPosY = saveManager.GetInt("WindowPosY", GameSettings.WindowPosY);
            GameSettings.GameScale = saveManager.GetInt("GameScale", GameSettings.GameScale);
            GameSettings.UiScale = saveManager.GetInt("UIScale", GameSettings.UiScale);
            GameSettings.ScreenMode = saveManager.GetInt("ScreenMode", GameSettings.ScreenMode);
            GameSettings.VerticalSync = saveManager.GetBool("VerticalSync", GameSettings.VerticalSync);
            GameSettings.DisableUiBlur = saveManager.GetBool("DisableUiBlur", GameSettings.DisableUiBlur);
            GameSettings.PixelSnapping = saveManager.GetBool("PixelSnapping", GameSettings.PixelSnapping);
            GameSettings.PixelGrid = saveManager.GetBool("PixelGrid", GameSettings.PixelGrid);
            GameSettings.EpilepsySafe = saveManager.GetBool("EpilepsySafe", GameSettings.EpilepsySafe);

            // Graphics Settings
            GameSettings.SeqScaleAmplify = saveManager.GetInt("SeqScaleAmplify", GameSettings.SeqScaleAmplify);
            GameSettings.ColorCorrection = saveManager.GetBool("ColorCorrection", GameSettings.ColorCorrection);
            GameSettings.EnableShadows = saveManager.GetBool("EnableShadows", GameSettings.EnableShadows);
            GameSettings.FogEffects = saveManager.GetBool("FogEffects", GameSettings.FogEffects);
            GameSettings.GlobalLights = saveManager.GetBool("GlobalLights", GameSettings.GlobalLights);
            GameSettings.ObjectLights = saveManager.GetBool("ObjectLights", GameSettings.ObjectLights);
            GameSettings.ScreenShake = saveManager.GetBool("ScreenShake", GameSettings.ScreenShake);
            GameSettings.ExScreenShake = saveManager.GetBool("ExScreenShake", GameSettings.ExScreenShake);
            GameSettings.ClassicSprites = saveManager.GetBool("ClassicSprites", GameSettings.ClassicSprites);

            // Audio Settings
            GameSettings.MusicVolume = saveManager.GetInt("MusicVolume", GameSettings.MusicVolume);
            GameSettings.EffectVolume = saveManager.GetInt("EffectVolume", GameSettings.EffectVolume);
            GameSettings.ClassicMusic = saveManager.GetBool("ClassicMusic", GameSettings.ClassicMusic);
            GameSettings.MuteInactive = saveManager.GetBool("MuteInactive", GameSettings.MuteInactive);
            GameSettings.HeartBeep = saveManager.GetBool("HeartBeep", GameSettings.HeartBeep);
            GameSettings.MutePowerups = saveManager.GetBool("MutePowerups", GameSettings.MutePowerups);
            GameSettings.MuteAchievement = saveManager.GetBool("MuteAchievement", GameSettings.MuteAchievement);

            // Controls Settings
            GameSettings.DeadZone = saveManager.GetFloat("DeadZone", GameSettings.DeadZone);
            GameSettings.Controller = saveManager.GetString("Controller", GameSettings.Controller);
            GameSettings.TriggersScale = saveManager.GetBool("TriggersScale", GameSettings.TriggersScale);
            GameSettings.SixButtons = saveManager.GetBool("SixButtons", GameSettings.SixButtons);
            GameSettings.SwapButtons = saveManager.GetBool("SwapButtons", GameSettings.SwapButtons);
            GameSettings.OldMovement = saveManager.GetBool("OldMovement", GameSettings.OldMovement);
            GameSettings.DigitalAnalog = saveManager.GetBool("DigitalAnalog", GameSettings.DigitalAnalog);

            // On-Screen Control Settings
            GameSettings.TouchGridSize = saveManager.GetInt("TouchGridSize", GameSettings.TouchGridSize);
            GameSettings.TouchControls = saveManager.GetInt("TouchControls", GameSettings.TouchControls);
            GameSettings.TouchMovement = saveManager.GetInt("TouchMovement", GameSettings.TouchMovement);
            GameSettings.TouchOpacity = saveManager.GetInt("TouchOpacity", GameSettings.TouchOpacity);
            GameSettings.ShadowOpacity = saveManager.GetInt("ShadowOpacity", GameSettings.ShadowOpacity);
            GameSettings.TouchScaling = saveManager.GetInt("TouchScaling", GameSettings.TouchScaling);
            GameSettings.TouchSticks = saveManager.GetBool("TouchSticks", GameSettings.TouchSticks);

            GameSettings.TouchLayout.Clear();
            foreach (var key in VirtualController.LayoutKeys)
            {
                float x = saveManager.GetFloat("TouchLayout" + key + "X", 0f);
                float y = saveManager.GetFloat("TouchLayout" + key + "Y", 0f);
                if (x != 0f || y != 0f)
                    GameSettings.TouchLayout[key] = new Vector2(x, y);
            }

            // Modifiers Settings
            GameSettings.EnemyBonusHP = saveManager.GetInt("EnemyBonusHP", GameSettings.EnemyBonusHP);
            GameSettings.DamageFactor = saveManager.GetInt("DamageFactor", GameSettings.DamageFactor);
            GameSettings.DmgCooldown = saveManager.GetInt("DmgCooldown", GameSettings.DmgCooldown);
            GameSettings.MoveSpeedAdded = saveManager.GetFloat("MoveSpeedAdded", GameSettings.MoveSpeedAdded);
            GameSettings.NoHeartDrops = saveManager.GetBool("NoHeartDrops", GameSettings.NoHeartDrops);
            GameSettings.NoDamageLaunch = saveManager.GetBool("NoDamageLaunch", GameSettings.NoDamageLaunch);
            GameSettings.MirrorReflects = saveManager.GetBool("MirrorReflects", GameSettings.MirrorReflects);

            // Sword Interact Settings
            GameSettings.SwGrabNormal = saveManager.GetBool("SwGrabNormal", GameSettings.SwGrabNormal);
            GameSettings.SwGrabWorldItem = saveManager.GetBool("SwGrabWorldItem", GameSettings.SwGrabWorldItem);
            GameSettings.SwGrabFairy = saveManager.GetBool("SwGrabFairy", GameSettings.SwGrabFairy);
            GameSettings.SwGrabSmallKey = saveManager.GetBool("SwGrabSmallKey", GameSettings.SwGrabSmallKey);
            GameSettings.SwBoomerang = saveManager.GetBool("SwBoomerang", GameSettings.SwBoomerang);
            GameSettings.SwSmackBombs = saveManager.GetBool("SwSmackBombs", GameSettings.SwSmackBombs);
            GameSettings.SwMissileBlock = saveManager.GetBool("SwMissileBlock", GameSettings.SwMissileBlock);
            GameSettings.SwBreakPots = saveManager.GetBool("SwBreakPots", GameSettings.SwBreakPots);
            GameSettings.SwBeamShrubs = saveManager.GetBool("SwBeamShrubs", GameSettings.SwBeamShrubs);

            // Cheats Settings
            GameSettings.ChInvincibility = saveManager.GetBool("ChInvincibility", GameSettings.ChInvincibility);
            GameSettings.ChInfinRupees = saveManager.GetBool("ChInfinRupees", GameSettings.ChInfinRupees);
            GameSettings.ChInfinPowder = saveManager.GetBool("ChInfinPowder", GameSettings.ChInfinPowder);
            GameSettings.ChInfinBombs = saveManager.GetBool("ChInfinBombs", GameSettings.ChInfinBombs);
            GameSettings.ChInfinArrows = saveManager.GetBool("ChInfinArrows", GameSettings.ChInfinArrows);
            GameSettings.ChNoClipping = saveManager.GetBool("ChNoClipping", GameSettings.ChNoClipping);
            GameSettings.ChOneHitKills = saveManager.GetBool("ChOneHitKills", GameSettings.ChOneHitKills);
            GameSettings.ChGiveAllItems = saveManager.GetBool("ChGiveAllItems", GameSettings.ChGiveAllItems);

            ControlHandler.LoadButtonMap(saveManager);
            ControlHandler.SetControllerIndex();
        }

        public static void SaveSettings()
        {
            var saveManager = new SaveManager();

            // Game Settings
            saveManager.SetInt("CurrentLanguage", Game1.LanguageManager.CurrentLanguageIndex);
            saveManager.SetInt("CurrentSubLanguage", Game1.LanguageManager.CurrentSubLanguageIndex);
            saveManager.SetInt("MenuBorder", GameSettings.MenuBorder);
            saveManager.SetBool("ClassicSword", GameSettings.ClassicSword);
            saveManager.SetBool("StoreSavePos", GameSettings.StoreSavePos);
            saveManager.SetInt("LastSavePos", GameSettings.LastSavePos);
            saveManager.SetBool("Autosave", GameSettings.Autosave);
            saveManager.SetBool("SharedStorage", GameSettings.SharedStorage);
            saveManager.SetBool("ItemsOnRight", GameSettings.ItemsOnRight);
            saveManager.SetBool("HideAchievement", GameSettings.HideAchievement);
            saveManager.SetBool("StreamAssets", GameSettings.StreamAssets);
            saveManager.SetBool("ThreadMapLoad", GameSettings.ThreadMapLoad);

            // Redux Settings
            saveManager.SetInt("MapTeleport", GameSettings.MapTeleport);
            saveManager.SetBool("VarWidthFont", GameSettings.VarWidthFont);
            saveManager.SetBool("NoHelperText", GameSettings.NoHelperText);
            saveManager.SetBool("DialogSkip", GameSettings.DialogSkip);
            saveManager.SetBool("Uncensored", GameSettings.Uncensored);
            saveManager.SetBool("Unmissables", GameSettings.Unmissables);
            saveManager.SetBool("PhotosColor", GameSettings.PhotosColor);
            saveManager.SetBool("NoAnimalDamage", GameSettings.NoAnimalDamage);

            // Camera Settings
            saveManager.SetInt("CameraMode", GameSettings.CameraMode);
            saveManager.SetBool("ModernFogEnabled", GameSettings.ModernFogEnabled);
            saveManager.SetBool("ModernFogDungeon", GameSettings.ModernFogDungeon);
            saveManager.SetFloat("ModernFogSharp", GameSettings.ModernFogSharp);
            saveManager.SetFloat("ModernFogOpacity", GameSettings.ModernFogOpacity);
            saveManager.SetInt("ModernFogFade", GameSettings.ModernFogFade);
            saveManager.SetInt("ClassicBorder", GameSettings.ClassicBorder);
            saveManager.SetFloat("ClassicAlpha", GameSettings.ClassicAlpha);
            saveManager.SetInt("ClassicBias", GameSettings.ClassicBias);
            saveManager.SetBool("ClassicScrolling", GameSettings.ClassicScrolling);
            saveManager.SetBool("ClassicScaling", GameSettings.ClassicScaling);
            saveManager.SetBool("ClassicItemDrop", GameSettings.ClassicItemDrop);
            saveManager.SetBool("CameraLock", GameSettings.CameraLock);
            saveManager.SetBool("SmoothCamera", GameSettings.SmoothCamera);

            // Custom Camera Settings
            saveManager.SetBool("ClassicOverworld", GameSettings.ClassicOverworld);
            saveManager.SetBool("ClassicHouses", GameSettings.ClassicHouses);
            saveManager.SetBool("ClassicCaves", GameSettings.ClassicCaves);
            saveManager.SetBool("ClassicDungeons", GameSettings.ClassicDungeons);
            saveManager.SetBool("ClassicCastle", GameSettings.ClassicCastle);
            saveManager.SetBool("ClassicEgg", GameSettings.ClassicEgg);
            saveManager.SetBool("Classic2DMaps", GameSettings.Classic2DMaps);
            saveManager.SetBool("ClassicBosses", GameSettings.ClassicBosses);

            // Video Settings
            saveManager.SetInt("WindowWidth", GameSettings.WindowWidth);
            saveManager.SetInt("WindowHeight", GameSettings.WindowHeight);
            saveManager.SetInt("WindowPosX", GameSettings.WindowPosX);
            saveManager.SetInt("WindowPosY", GameSettings.WindowPosY);
            saveManager.SetInt("GameScale", GameSettings.GameScale);
            saveManager.SetInt("UIScale", GameSettings.UiScale);
            saveManager.SetInt("ScreenMode", GameSettings.ScreenMode);
            saveManager.SetBool("VerticalSync", GameSettings.VerticalSync);
            saveManager.SetBool("DisableUiBlur", GameSettings.DisableUiBlur);
            saveManager.SetBool("PixelSnapping", GameSettings.PixelSnapping);
            saveManager.SetBool("PixelGrid", GameSettings.PixelGrid);
            saveManager.SetBool("EpilepsySafe", GameSettings.EpilepsySafe);

            // Graphics Settings
            saveManager.SetInt("SeqScaleAmplify", GameSettings.SeqScaleAmplify);
            saveManager.SetBool("ColorCorrection", GameSettings.ColorCorrection);
            saveManager.SetBool("EnableShadows", GameSettings.EnableShadows);
            saveManager.SetBool("FogEffects", GameSettings.FogEffects);
            saveManager.SetBool("GlobalLights", GameSettings.GlobalLights);
            saveManager.SetBool("ObjectLights", GameSettings.ObjectLights);
            saveManager.SetBool("ScreenShake", GameSettings.ScreenShake);
            saveManager.SetBool("ExScreenShake", GameSettings.ExScreenShake);
            saveManager.SetBool("ClassicSprites", GameSettings.ClassicSprites);

            // Audio Settings
            saveManager.SetInt("MusicVolume", GameSettings.MusicVolume);
            saveManager.SetInt("EffectVolume", GameSettings.EffectVolume);
            saveManager.SetBool("ClassicMusic", GameSettings.ClassicMusic);
            saveManager.SetBool("MuteInactive", GameSettings.MuteInactive);
            saveManager.SetBool("HeartBeep", GameSettings.HeartBeep);
            saveManager.SetBool("MutePowerups", GameSettings.MutePowerups);
            saveManager.SetBool("MuteAchievement", GameSettings.MuteAchievement);

            // Control Settings
            saveManager.SetFloat("DeadZone", GameSettings.DeadZone);
            saveManager.SetString("Controller", GameSettings.Controller);
            saveManager.SetBool("TriggersScale", GameSettings.TriggersScale);
            saveManager.SetBool("SixButtons", GameSettings.SixButtons);
            saveManager.SetBool("SwapButtons", GameSettings.SwapButtons);
            saveManager.SetBool("OldMovement", GameSettings.OldMovement);
            saveManager.SetBool("DigitalAnalog", GameSettings.DigitalAnalog);
            ControlHandler.SaveButtonMaps(saveManager);

            // On-Screen Control Settings
            saveManager.SetInt("TouchGridSize", GameSettings.TouchGridSize);
            saveManager.SetInt("TouchControls", GameSettings.TouchControls);
            saveManager.SetInt("TouchMovement", GameSettings.TouchMovement);
            saveManager.SetInt("TouchOpacity", GameSettings.TouchOpacity);
            saveManager.SetInt("ShadowOpacity", GameSettings.ShadowOpacity);
            saveManager.SetInt("TouchScaling", GameSettings.TouchScaling);
            saveManager.SetBool("TouchSticks", GameSettings.TouchSticks);

            foreach (var entry in GameSettings.TouchLayout)
            {
                saveManager.SetFloat("TouchLayout" + entry.Key + "X", entry.Value.X);
                saveManager.SetFloat("TouchLayout" + entry.Key + "Y", entry.Value.Y);
            }

            // Modifiers Settings
            saveManager.SetInt("EnemyBonusHP", GameSettings.EnemyBonusHP);
            saveManager.SetInt("DamageFactor", GameSettings.DamageFactor);
            saveManager.SetInt("DmgCooldown", GameSettings.DmgCooldown);
            saveManager.SetFloat("MoveSpeedAdded", GameSettings.MoveSpeedAdded);
            saveManager.SetBool("NoHeartDrops", GameSettings.NoHeartDrops);
            saveManager.SetBool("NoDamageLaunch", GameSettings.NoDamageLaunch);
            saveManager.SetBool("MirrorReflects", GameSettings.MirrorReflects);

            // Sword Interact Settings
            saveManager.SetBool("SwGrabNormal", GameSettings.SwGrabNormal);
            saveManager.SetBool("SwGrabWorldItem", GameSettings.SwGrabWorldItem);
            saveManager.SetBool("SwGrabFairy", GameSettings.SwGrabFairy);
            saveManager.SetBool("SwGrabSmallKey", GameSettings.SwGrabSmallKey);
            saveManager.SetBool("SwBoomerang", GameSettings.SwBoomerang);
            saveManager.SetBool("SwSmackBombs", GameSettings.SwSmackBombs);
            saveManager.SetBool("SwMissileBlock", GameSettings.SwMissileBlock);
            saveManager.SetBool("SwBreakPots", GameSettings.SwBreakPots);
            saveManager.SetBool("SwBeamShrubs", GameSettings.SwBeamShrubs);

            // Cheats Settings
            saveManager.SetBool("ChInvincibility", GameSettings.ChInvincibility);
            saveManager.SetBool("ChInfinRupees", GameSettings.ChInfinRupees);
            saveManager.SetBool("ChInfinPowder", GameSettings.ChInfinPowder);
            saveManager.SetBool("ChInfinBombs", GameSettings.ChInfinBombs);
            saveManager.SetBool("ChInfinArrows", GameSettings.ChInfinArrows);
            saveManager.SetBool("ChNoClipping", GameSettings.ChNoClipping);
            saveManager.SetBool("ChOneHitKills", GameSettings.ChOneHitKills);
            saveManager.SetBool("ChGiveAllItems", GameSettings.ChGiveAllItems);

            // Write the save file.
            saveManager.Save(SettingsFilePath, Values.SaveRetries);
        }
    }
}
