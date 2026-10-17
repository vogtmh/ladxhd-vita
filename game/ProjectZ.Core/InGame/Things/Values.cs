using System.IO;
using System.Reflection;
using ProjectZ.InGame.SaveLoad;
using Microsoft.Xna.Framework;

namespace ProjectZ.InGame.Things
{
    public partial class Values
    {
        public static readonly string VersionString = "v" + Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion.Split('+')[0];

        public static Color ColorBackgroundLight = Color.Black * 0.8f;
        public static Color ColorBackgroundDark = Color.Black * 0.85f;
        public static Color ColorUiEditor = new Color(41, 57, 85) * 0.85f;

        public static Color TextboxBackgroundColor = new Color(0, 0, 0) * 0.85f;
        public static Color TextboxBlurColor = new Color(255, 255, 255, 255);

        public static Color MapTransitionColor = new Color(0, 0, 0, 255);
        public static Color MapFirstTransitionColor = new Color(0, 0, 0, 255);

        public static Color OverlayBackgroundColor = new Color(255, 255, 190) * 0.55f;
        public static Color OverlayBackgroundBlurColor = new Color(255, 255, 255, 255);

        public const int UnsetWindowPosition = int.MinValue;

        public static string PathSaveFolder => SaveManager.GetSaveFilePath();

        public static string PathDataFolder = "Data";

        private static string _resolvedMods;
        public static string PathMods => Game1.UserDataPaths.ModsRoot;
        public static string ResolvedMods => _resolvedMods ??= !string.IsNullOrEmpty(Game1.UserDataPaths.InternalModsRoot) &&
            GameFS.IsDirectory(Game1.UserDataPaths.InternalModsRoot)
            ? Game1.UserDataPaths.InternalModsRoot
            : PathMods;

        public static string PathAnimationMods => Path.Combine(ResolvedMods, "Animations");
        public static string PathDungeonMods => Path.Combine(ResolvedMods, "Dungeon");
        public static string PathGraphicsMods => Path.Combine(ResolvedMods, "Graphics");
        public static string PathMusicMods => Path.Combine(ResolvedMods, "Music");
        public static string PathLanguageMods => Path.Combine(ResolvedMods, "Languages");
        public static string PathLAHDMods => Path.Combine(ResolvedMods, "LAHDMods");
        public static string PathMapMods => Path.Combine(ResolvedMods, "Maps");
        public static string PathSoundEffectMods => Path.Combine(ResolvedMods, "SoundEffects");

        public const string EditorUiObjectEditor = "objectEditor";
        public const string EditorUiObjectSelection = "objectSelection";
        public const string EditorUiTileEditor = "tileEditor";
        public const string EditorUiTileSelection = "tileSelection";
        public const string EditorUiDigTileEditor = "digTileEditor";
        public const string EditorUiMusicTileEditor = "musicTileEditor";
        public const string EditorUiTileExtractor = "tileExtractor";
        public const string EditorUiTilesetEditor = "tilesetEditor";
        public const string EditorUiAnimation = "animationEditor";
        public const string EditorUiSpriteAtlas = "spriteAtlasEditor";

        public const string ScreenNameIntro = "INTRO";
        public const string ScreenNameMenu = "MENU";
        public const string ScreenNameGame = "GAME";
        public const string ScreenGameOver = "GAMEOVER";
        public const string ScreenNameMap = "MAP";
        public const string ScreenNameSettings = "SETTINGS";

        public const string ScreenNameEditor = "MAP_EDITOR";
        public const string ScreenNameEditorTileset = "TILESET_EDITOR";
        public const string ScreenNameEditorTilesetExtractor = "TILESET_EXTRACTOR";
        public const string ScreenNameEditorAnimation = "ANIMATION_EDITOR";
        public const string ScreenNameSpriteAtlasEditor = "SPRITE_ATLAS_EDITOR";

        public const float UiBackgroundRadius = 2.0f;
        public const float UiTextboxRadius = 3.0f;

        public static int TileSize = 16;
        public static int FieldWidth = 160;
        public static int FieldHeight = 128;

        public static int ToolBarHeight = 40;

        public static int LayerBackground = 0;  // layer behind tileset
        public static int LayerBottom = 1;      // layer under the player (grass, water, flowers, etc.)
        public static int LayerPlayer = 2;      // same layer as the player
        public static int LayerTop = 3;         // on top of the player

        public static int LightLayer0 = 0;      // lamp
        public static int LightLayer1 = 1;      // teleporter light
        public static int LightLayer2 = 2;      // dark room
        public static int LightLayer3 = 3;

        public static int HandItemSlots = 6;

        // The original game field size was 160x128. The minimum resolution for this port is 380x256 due to the
        // fact the menus were designed around this size. This means the minimum real scale is at least 2x.
        public static int MinWidth = 380;
        public static int MinHeight => Game1.PlatformPresentation.MinimumHeight;
        public static double MenuHeaderSize = 0.2;
        public static double MenuContentSize = 0.7;
        public static double MenuFooterSize = 0.2;

        public static int LetterWidth = 8;
        public static int LetterHeight = 8;

        public static int GameSaveBlackScreen = 250;
        public static int GameRespawnBlackScreen = 250;

        public static float ShadowHeightDefault = 0.75f;
        public static float ShadowRotationDefault = 0.0f;

        public static int SaveRetries = 10;
        public static int LoadRetries = 10;

        public const float SoundEffectVolumeMult = 0.85f;
    }
}
