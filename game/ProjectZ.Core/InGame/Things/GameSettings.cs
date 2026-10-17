using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ProjectZ.InGame.Things
{
    public class GameSettings
    {
        // Game Settings
        public static int     MenuBorder       =  0;
        public static bool    ClassicSword     =  false;
        public static bool    StoreSavePos     =  false;
        public static bool    Autosave         =  true;
        public static bool    SharedStorage    =  false;
        public static int     LastSavePos      =  0;
        public static bool    ItemsOnRight     =  false;
        public static bool    HideAchievement  =  false;
        public static bool    StreamAssets     =  false;
        public static bool    ThreadMapLoad    =  false;

        // Redux Settings
        public static int     MapTeleport      =  0;
        public static bool    VarWidthFont     =  false;
        public static bool    NoHelperText     =  false;
        public static bool    DialogSkip       =  false;
        public static bool    Uncensored       =  false;
        public static bool    Unmissables      =  false;
        public static bool    PhotosColor      =  false;
        public static bool    NoAnimalDamage   =  false;

        // Camera Settings
        public static int     CameraMode       =  0;
        public static bool    ModernFogEnabled =  true;
        public static bool    ModernFogDungeon =  true;
        public static float   ModernFogSharp   =  0.25f;
        public static float   ModernFogOpacity =  0.95f;
        public static int     ModernFogFade    =  250;
        public static int     ClassicBorder    =  1;
        public static float   ClassicAlpha     =  1.00f;
        public static int     ClassicBias      =  0;
        public static bool    ClassicScrolling =  true;
        public static bool    ClassicScaling   =  true;
        public static bool    ClassicItemDrop  =  false;
        public static bool    CameraLock       =  true;
        public static bool    SmoothCamera     =  true;

        // Custom Camera Settings
        public static bool    ClassicOverworld = false;
        public static bool    ClassicHouses    = false;
        public static bool    ClassicCaves     = false;
        public static bool    ClassicDungeons  = false;
        public static bool    ClassicCastle    = false;
        public static bool    ClassicEgg       = false;
        public static bool    Classic2DMaps    = false;
        public static bool    ClassicBosses    = false;

        // Video Settings
        public static int     WindowWidth      =  0;
        public static int     WindowHeight     =  0;
        public static int     WindowPosX       =  Values.UnsetWindowPosition;
        public static int     WindowPosY       =  Values.UnsetWindowPosition;
        public static int     GameScale        =  Game1.MaxGameScale + 1;
        public static int     UiScale          =  11;
        public static int     ScreenMode       =  0;
        public static bool    VerticalSync     =  true;
        public static bool    DisableUiBlur    =  false;
        public static bool    PixelSnapping    =  false;
        public static bool    PixelGrid        =  false;
        public static bool    EpilepsySafe     =  false;

        // Graphics Settings
        public static int     SeqScaleAmplify  =  0;
        public static bool    ColorCorrection  =  false;
        public static bool    EnableShadows    =  true;
        public static bool    FogEffects       =  true;
        public static bool    GlobalLights     =  true;
        public static bool    ObjectLights     =  true;
        public static bool    ScreenShake      =  true;
        public static bool    ExScreenShake    =  false;
        public static bool    ClassicSprites   =  false;

        // Audio Settings
        private static int    _musicVolume     =  100;
        private static int    _effectVolume    =  100;
        public static bool    ClassicMusic     =  false;
        public static bool    MuteInactive     =  true;
        public static bool    HeartBeep        =  true;
        public static bool    MutePowerups     =  false;
        public static bool    MuteAchievement  =  false;

        // Control Settings
        public static float   DeadZone         =  0.10f;
        public static string  Controller       =  "XBox";
        public static bool    TriggersScale    =  false;
        public static bool    SixButtons       =  false;
        public static bool    SwapButtons      =  false;
        public static bool    OldMovement      =  false;
        public static bool    DigitalAnalog    =  false;

        // On-Screen Control Settings
        public static int     TouchGridSize    =  20;
        public static int     TouchControls    =  1;
        public static int     TouchMovement    =  0;
        public static int     TouchOpacity     =  60;
        public static int     ShadowOpacity    =  15;
        public static int     TouchScaling     =  7;
        public static bool    TouchSticks      =  false;

        // Per-control position offsets from the default layout, in layout units (pixels / touch scale).
        // Keys are VirtualController.LayoutKeys. A missing key means "use the default position".
        public static readonly Dictionary<string, Vector2> TouchLayout = new Dictionary<string, Vector2>();

        // Modifiers Settings
        public static int     EnemyBonusHP     =  0;
        public static int     DamageFactor     =  4;
        public static int     DmgCooldown      =  16;
        public static float   MoveSpeedAdded   =  0;
        public static bool    NoHeartDrops     =  false;
        public static bool    NoDamageLaunch   =  false;
        public static bool    MirrorReflects   =  false;

        // Sword Collection
        public static bool    SwGrabNormal     =  true;
        public static bool    SwGrabWorldItem  =  false;
        public static bool    SwGrabFairy      =  false;
        public static bool    SwGrabSmallKey   =  false;
        public static bool    SwBoomerang      =  false;
        public static bool    SwSmackBombs     =  false;
        public static bool    SwMissileBlock   =  false;
        public static bool    SwBreakPots      =  false;
        public static bool    SwBeamShrubs     =  false;

        // Cheats Settings
        public static bool    ChInvincibility  =  false;
        public static bool    ChInfinRupees    =  false;
        public static bool    ChInfinPowder    =  false;
        public static bool    ChInfinBombs     =  false;
        public static bool    ChInfinArrows    =  false;
        public static bool    ChNoClipping     =  false;
        public static bool    ChOneHitKills    =  false;
        public static bool    ChGiveAllItems   =  false;

        public static int MusicVolume
        {
            get => _musicVolume;
            set
            {
                _musicVolume = value;
                Game1.AudioManager?.SetMusicVolume(value / 100.0f);
            }
        }

        public static int EffectVolume
        {
            get => _effectVolume;
            set { _effectVolume = value; }
        }

        public static void RestoreDefaults()
        {
            // Game Settings
            MenuBorder       =  0;
            ClassicSword     =  false;
            StoreSavePos     =  false;
            LastSavePos      =  0;
            Autosave         =  true;
            ItemsOnRight     =  false;
            HideAchievement  =  false;
            StreamAssets     =  false;
            ThreadMapLoad    =  Game1.UserDataPaths.DefaultMapThreadLoad;

            // Redux Settings
            MapTeleport      =  0;
            VarWidthFont     =  false;
            NoHelperText     =  false;
            DialogSkip       =  false;
            Uncensored       =  false;
            Unmissables      =  false;
            PhotosColor      =  false;
            NoAnimalDamage   =  false;

            // Camera Settings
            CameraMode       =  0;
            ModernFogEnabled =  true;
            ModernFogDungeon =  true;
            ModernFogSharp   =  0.25f;
            ModernFogOpacity =  0.95f;
            ModernFogFade    =  250;
            ClassicBorder    =  1;
            ClassicAlpha     =  1.00f;
            ClassicBias      =  1;
            ClassicScrolling =  true;
            ClassicScaling   =  true;
            ClassicItemDrop  =  false;
            CameraLock       =  true;
            SmoothCamera     =  true;

            // Custom Camera Settings
            ClassicOverworld =  false;
            ClassicHouses    =  false;
            ClassicCaves     =  false;
            ClassicDungeons  =  false;
            ClassicCastle    =  false;
            ClassicEgg       =  false;
            Classic2DMaps    =  false;
            ClassicBosses    =  false;

            // Video Settings
            GameScale        =  Game1.MaxGameScale + 1;
            UiScale          =  11;
            ScreenMode       =  0;
            VerticalSync     =  true;
            DisableUiBlur    =  false;
            PixelSnapping    =  false;
            PixelGrid        =  false;
            EpilepsySafe     =  false;

            // Graphics Settings
            ColorCorrection  =  false;
            EnableShadows    =  true;
            FogEffects       =  true;
            GlobalLights     =  true;
            ObjectLights     =  true;
            ScreenShake      =  true;
            ExScreenShake    =  false;
            ClassicSprites   =  false;

            // Audio Settings
            ClassicMusic     =  false;
            MuteInactive     =  true;
            HeartBeep        =  true;
            MutePowerups     =  false;
            MuteAchievement  =  false;

            // Control Settings
            DeadZone         =  0.10f;
            Controller       =  "XBox";
            TriggersScale    =  false;
            SixButtons       =  false;
            SwapButtons      =  false;
            OldMovement      =  false;
            DigitalAnalog    =  false;

            // On-Screen Control Settings
            TouchGridSize    =  20;
            TouchControls    =  1;
            TouchMovement    =  0;
            TouchOpacity     =  60;
            ShadowOpacity    =  15;
            TouchScaling     =  7;
            TouchSticks      =  false;
            TouchLayout.Clear();

            // Modifiers Settings
            EnemyBonusHP     =  0;
            DamageFactor     =  4;
            DmgCooldown      =  16;
            MoveSpeedAdded   =  0;
            NoHeartDrops     =  false;
            NoDamageLaunch   =  false;
            MirrorReflects   =  false;

            // Sword Collection
            SwGrabNormal     =  true;
            SwGrabWorldItem  =  false;
            SwGrabFairy      =  false;
            SwGrabSmallKey   =  false;
            SwBoomerang      =  false;
            SwSmackBombs     =  false;
            SwMissileBlock   =  false;
            SwBreakPots      =  false;
            SwBeamShrubs     =  false;

            // Cheats Settings
            ChInvincibility  =  false;
            ChInfinRupees    =  false;
            ChInfinPowder    =  false;
            ChInfinBombs     =  false;
            ChInfinArrows    =  false;
            ChNoClipping     =  false;
            ChOneHitKills    =  false;
            ChGiveAllItems   =  false;
        }
    }
}
