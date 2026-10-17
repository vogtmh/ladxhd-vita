using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Graphics
{
    public class Resources
    {
        public static Effect RoundedCornerEffect;
        public static Effect BlurEffect;
        public static Effect RoundedCornerBlurEffect;
        public static Effect BlurEffectV;
        public static Effect BlurEffectH;
        public static Effect BBlurEffectV;
        public static Effect BBlurEffectH;
        public static Effect BBlurMapping;
        public static Effect FullShadowEffect;
        public static Effect SaturationEffect;
        public static Effect WobbleEffect;
        public static Effect CircleShader;
        public static Effect LightShader;
        public static Effect LightFadeShader;
        public static Effect ThanosShader;
        public static Effect PixelGrid;
        public static Effect ColorCorrect;

        // some sprites need different parameters set
        // we try to use as little different sprites effects as possible
        public static SpriteShader DamageSpriteShader0;
        public static SpriteShader DamageSpriteShader1;
        public static SpriteShader CloudShader;
        public static SpriteShader ThanosSpriteShader0;
        public static SpriteShader ThanosSpriteShader1;
        public static SpriteShader WindFishShader;
        public static SpriteShader ColorShader;
        public static SpriteShader ShockShader0;
        public static SpriteShader ShockShader1;

        // The texture used by the optional onscreen controller.
        public static Texture2D SprButtons;

        public static SpriteFont EditorFont, EditorFontMonoSpace, EditorFontSmallMonoSpace;
        public static SpriteFont GameHeaderFont;
        public static SpriteFont FontCredits, FontCreditsHeader;
        public static SpriteFont smallFont, smallFont_redux, smallFont_vwf, smallFont_vwf_redux;
        public static SpriteFont GameFont 
        {
            get
            {
                // Other languages use the "normal" fonts provided. Depending on certain settings there is variations.
                return (GameSettings.VarWidthFont, GameSettings.Uncensored) switch
                {
                    (true,  true)  => smallFont_vwf_redux,
                    (true,  false) => smallFont_vwf,
                    (false, true)  => smallFont_redux,
                    (false, false) => smallFont
                };
            }
        }
        public static BitmapFont smallFont_chn, smallFont_chn_redux;
        public static BitmapFont ChinaFont => GameSettings.Uncensored ? smallFont_chn_redux : smallFont_chn;

        public static Texture2D EditorEyeOpen, EditorEyeClosed, EditorIconDelete;
        public static Texture2D SprWhite, SprTiledBlock, SprObjectsAnimated, SprNpCs, SprNpCsRedux;
        public static Texture2D SprEnemies, SprMidBoss, SprNightmares;
        public static Texture2D SprShadow;
        public static Texture2D SprBlurTileset;
        public static Texture2D SprLink, SprLinkCloak;
        public static Texture2D SprGameSequences;
        public static Texture2D SprGameSequencesFinal;
        public static Texture2D SprFog;
        public static Texture2D SprLight;
        public static Texture2D SprLightRoomH;
        public static Texture2D SprLightRoomV;
        public static Texture2D SprMamuLight;
        public static Texture2D NoiseTexture;
        public static Texture2D SprIconOptions, SprIconErase, SprIconCopy, EditorIconEdit, EditorIconSelect;
        public static Texture2D sgbBorder;

        public static Dictionary<string, int> TilesetSizes = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, SoundEffect> SoundEffects = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, Texture2D> TextureList = new (StringComparer.OrdinalIgnoreCase);

        public static int GameFontHeight = 10;
        public static int EditorFontHeight;

        // Fields from this point on are affected by language and/or redux options.
        public static Texture2D SprPhotosEng, SprPhotosChn, SprPhotosDeu, SprPhotosEsp, SprPhotosFre, SprPhotosInd, SprPhotosIta, SprPhotosPor, SprPhotosRus, SprPhotosSwe;
        public static Texture2D SprPhotosEngRedux, SprPhotosChnRedux, SprPhotosDeuRedux, SprPhotosEspRedux, SprPhotosFreRedux, SprPhotosIndRedux, SprPhotosItaRedux, SprPhotosPorRedux, SprPhotosRusRedux, SprPhotosSweRedux;

        public static Texture2D SprObjectsEng, SprObjectsChn, SprObjectsDeu, SprObjectsEsp, SprObjectsFre, SprObjectsInd, SprObjectsIta, SprObjectsPor, SprObjectsRus, SprObjectsSwe;

        public static Texture2D SprObjects
        {
            get
            {
                return (Game1.LanguageManager.CurrentLanguageCode) switch
                {
                    // Note that "por" and "pte" are both "Portuguese" and share textures.
                    "chn" => SprObjectsChn,
                    "deu" => SprObjectsDeu,
                    "esp" => SprObjectsEsp,
                    "fre" => SprObjectsFre,
                    "ind" => SprObjectsInd,
                    "ita" => SprObjectsIta,
                    "por" => SprObjectsPor,
                    "pte" => SprObjectsPor,
                    "rus" => SprObjectsRus,
                    "swe" => SprObjectsSwe,
                    _     => SprObjectsEng
                };
            }
        }

        public static Texture2D SprMiniMapEng, SprMiniMapChn, SprMiniMapDeu, SprMiniMapEsp, SprMiniMapFre, SprMiniMapInd, SprMiniMapIta, SprMiniMapPor, SprMiniMapRus, SprMiniMapSwe;
        public static Texture2D SprMiniMap
        {
            get
            {
                return (Game1.LanguageManager.CurrentLanguageCode) switch
                {
                    // Note that "por" and "pte" are both "Portuguese" and share textures.
                    "chn" => SprMiniMapChn,
                    "deu" => SprMiniMapDeu,
                    "esp" => SprMiniMapEsp,
                    "fre" => SprMiniMapFre,
                    "ind" => SprMiniMapInd,
                    "ita" => SprMiniMapIta,
                    "por" => SprMiniMapPor,
                    "pte" => SprMiniMapPor,
                    "rus" => SprMiniMapRus,
                    "swe" => SprMiniMapSwe,
                    _     => SprMiniMapEng
                };
            }
        }

        public static Texture2D SprItemEng, SprItemChn, SprItemDeu, SprItemEsp, SprItemFre, SprItemInd, SprItemIta, SprItemPor, SprItemRus, SprItemSwe;
        public static Texture2D SprItemEngRedux, SprItemChnRedux, SprItemDeuRedux, SprItemEspRedux, SprItemFreRedux, SprItemIndRedux, SprItemItaRedux, SprItemPorRedux, SprItemRusRedux, SprItemSweRedux;
        public static Texture2D SprItem
        {
            get
            {
                return (Game1.LanguageManager.CurrentLanguageCode, GameSettings.Uncensored) switch
                {
                    // Note that "por" and "pte" are both "Portuguese" and share textures.
                    ("chn", false) => SprItemChn,
                    ("deu", false) => SprItemDeu,
                    ("esp", false) => SprItemEsp,
                    ("fre", false) => SprItemFre,
                    ("ind", false) => SprItemInd,
                    ("ita", false) => SprItemIta,
                    ("por", false) => SprItemPor,
                    ("pte", false) => SprItemPor,
                    ("rus", false) => SprItemRus,
                    ("swe", false) => SprItemSwe,
                    (_, false)     => SprItemEng,

                    ("chn", true)  => SprItemChnRedux,
                    ("deu", true)  => SprItemDeuRedux,
                    ("esp", true)  => SprItemEspRedux,
                    ("fre", true)  => SprItemFreRedux,
                    ("ind", true)  => SprItemIndRedux,
                    ("ita", true)  => SprItemItaRedux,
                    ("por", true)  => SprItemPorRedux,
                    ("pte", true)  => SprItemPorRedux,
                    ("rus", true)  => SprItemRusRedux,
                    ("swe", true)  => SprItemSweRedux,
                    (_, true)      => SprItemEngRedux,
                };
            }
        }
        private static readonly Dictionary<string, string> _atlasPathCache = new (StringComparer.OrdinalIgnoreCase);

        public static Dictionary<string, DictAtlasEntry> SpriteAtlas = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasChn = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasDeu = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasEsp = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasFre = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasInd = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasIta = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasPor = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasRus = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasSwe = new(StringComparer.OrdinalIgnoreCase);

        public static Dictionary<string, DictAtlasEntry> SpriteAtlasRedux = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasChnRedux = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasDeuRedux = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasEspRedux = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasFreRedux = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasIndRedux = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasItaRedux = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasPorRedux = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasRusRedux = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, DictAtlasEntry> SpriteAtlasSweRedux = new(StringComparer.OrdinalIgnoreCase);

        public enum AtlasLanguage { English, Chinese, German, Spanish, French, Indonesian, Italian, Portuguese, Russian, Swedish }
        public enum AtlasVariant { Default, Redux }

        private static (AtlasLanguage, AtlasVariant) ParseAtlasTags(string filePath)
        {
            string[] parts = Path.GetFileNameWithoutExtension(filePath)
                .ToLowerInvariant()
                .Split('_');

            AtlasLanguage lang = AtlasLanguage.English;
            AtlasVariant variant = AtlasVariant.Default;

            for (int i = 0; i < parts.Length; i++)
            {
                switch (parts[i])
                {
                    // Note that "por" and "pte" are both "Portuguese" and share textures.
                    case "chn": lang = AtlasLanguage.Chinese; break;
                    case "deu": lang = AtlasLanguage.German; break;
                    case "esp": lang = AtlasLanguage.Spanish; break;
                    case "fre": lang = AtlasLanguage.French; break;
                    case "ind": lang = AtlasLanguage.Indonesian; break;
                    case "ita": lang = AtlasLanguage.Italian; break;
                    case "por": lang = AtlasLanguage.Portuguese; break;
                    case "pte": lang = AtlasLanguage.Portuguese; break;
                    case "rus": lang = AtlasLanguage.Russian; break;
                    case "swe": lang = AtlasLanguage.Swedish; break;

                    case "redux": variant = AtlasVariant.Redux; break;
                }
            }
            return (lang, variant);
        }

        private static readonly Dictionary<(AtlasLanguage, AtlasVariant), Dictionary<string, DictAtlasEntry>> Atlases = new()
        {
            {(AtlasLanguage.English,    AtlasVariant.Default), SpriteAtlas},
            {(AtlasLanguage.English,    AtlasVariant.Redux),   SpriteAtlasRedux},
            {(AtlasLanguage.Chinese,    AtlasVariant.Default), SpriteAtlasChn},
            {(AtlasLanguage.Chinese,    AtlasVariant.Redux),   SpriteAtlasChnRedux},
            {(AtlasLanguage.German,     AtlasVariant.Default), SpriteAtlasDeu},
            {(AtlasLanguage.German,     AtlasVariant.Redux),   SpriteAtlasDeuRedux},
            {(AtlasLanguage.Spanish,    AtlasVariant.Default), SpriteAtlasEsp},
            {(AtlasLanguage.Spanish,    AtlasVariant.Redux),   SpriteAtlasEspRedux},
            {(AtlasLanguage.French,     AtlasVariant.Default), SpriteAtlasFre},
            {(AtlasLanguage.French,     AtlasVariant.Redux),   SpriteAtlasFreRedux},
            {(AtlasLanguage.Indonesian, AtlasVariant.Default), SpriteAtlasInd},
            {(AtlasLanguage.Indonesian, AtlasVariant.Redux),   SpriteAtlasIndRedux},
            {(AtlasLanguage.Italian,    AtlasVariant.Default), SpriteAtlasIta},
            {(AtlasLanguage.Italian,    AtlasVariant.Redux),   SpriteAtlasItaRedux},
            {(AtlasLanguage.Portuguese, AtlasVariant.Default), SpriteAtlasPor},
            {(AtlasLanguage.Portuguese, AtlasVariant.Redux),   SpriteAtlasPorRedux},
            {(AtlasLanguage.Russian,    AtlasVariant.Default), SpriteAtlasRus},
            {(AtlasLanguage.Russian,    AtlasVariant.Redux),   SpriteAtlasRusRedux},
            {(AtlasLanguage.Swedish,    AtlasVariant.Default), SpriteAtlasSwe},
            {(AtlasLanguage.Swedish,    AtlasVariant.Redux),   SpriteAtlasSweRedux},
        };

        public static void LoadIntro(GraphicsDevice graphics, ContentManager content)
        {
            SprWhite = new Texture2D(graphics, 1, 1);
            SprWhite.SetData(new[] { Color.White });

            if (Game1.PlatformInput.HasTouchInput)
            {
                var modButtons = GameFS.EnumerateFiles(GameFS.NormalizePath(Values.ResolvedMods), recursive: true,
                    name => name.Equals("buttons.png", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                if (modButtons != null)
                    LoadTexture(out SprButtons, modButtons);
                else
                    LoadTexture(out SprButtons, Path.Combine(Values.PathDataFolder, "Buttons", "buttons.png"));
            }

            // base first
            var introPath = GameFS.NormalizePath(Path.Combine(Values.PathDataFolder, "Intro"));
            LoadTexturesFromFolder(introPath, false);

            // mods second
            var graphicsModsPath = GameFS.NormalizePath(Values.PathGraphicsMods);

            if (GameFS.IsDirectory(graphicsModsPath))
            {
                var introDirs = GameFS.EnumerateDirectories(graphicsModsPath, recursive: true, 
                    acceptDirectory: dir => string.Equals(dir, "Intro", StringComparison.OrdinalIgnoreCase));

                foreach (var introDir in introDirs)
                    LoadTexturesFromFolder(introDir, false);
            }
            AddSoundEffect(content, "D378-15-0F");
            AddSoundEffect(content, "D378-12-0C");
            AddSoundEffect(content, "D378-25-19");
        }

        public static void LoadShaders(ContentManager content)
        {
            PixelGrid = content.Load<Effect>("Shader/PixelGrid");
            ColorCorrect = content.Load<Effect>("Shader/GBCColorCorrection");
        }

        public static void LoadBlurEffect(ContentManager content)
        {
            BlurEffect = content.Load<Effect>("Shader/EffectBlur");
            RoundedCornerBlurEffect = content.Load<Effect>("Shader/RoundedCornerEffectBlur");
        }

        private static void TryLoadTextures(ref Texture2D target, string inputPath)
        {
            inputPath = GameFS.NormalizePath(inputPath);
            try { LoadTexture(out target, inputPath); }
            catch { }
        }

        public static void LoadTextures(GraphicsDevice graphics, ContentManager content)
        {
            LoadTilesetSizes();

            // Load the editor icons.
            LoadTexture(out _, Path.Combine(Values.PathDataFolder, "Editor", "editorIcons4x.png"));

            // Load game sequence textures.
            LoadTexture(out SprGameSequences, Path.Combine(Values.PathDataFolder, "Sequences", "game sequences.png"));
            LoadTexture(out SprGameSequencesFinal, Path.Combine(Values.PathDataFolder, "Sequences", "end sequence.png"));

            // Load the photo textures.
            LoadTexture(out SprPhotosEng, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos.png"));
            TryLoadTextures(ref SprPhotosChn, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_chn.png"));
            TryLoadTextures(ref SprPhotosDeu, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_deu.png"));
            TryLoadTextures(ref SprPhotosEsp, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_esp.png"));
            TryLoadTextures(ref SprPhotosFre, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_fre.png"));
            TryLoadTextures(ref SprPhotosInd, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_ind.png"));
            TryLoadTextures(ref SprPhotosIta, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_ita.png"));
            TryLoadTextures(ref SprPhotosPor, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_por.png"));
            TryLoadTextures(ref SprPhotosRus, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_rus.png"));
            TryLoadTextures(ref SprPhotosSwe, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_swe.png"));

            // Load the colored photo textures.
            LoadTexture(out SprPhotosEngRedux, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_redux.png"));
            TryLoadTextures(ref SprPhotosChnRedux, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_redux_chn.png"));
            TryLoadTextures(ref SprPhotosDeuRedux, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_redux_deu.png"));
            TryLoadTextures(ref SprPhotosEspRedux, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_redux_esp.png"));
            TryLoadTextures(ref SprPhotosFreRedux, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_redux_fre.png"));
            TryLoadTextures(ref SprPhotosIndRedux, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_redux_ind.png"));
            TryLoadTextures(ref SprPhotosItaRedux, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_redux_ita.png"));
            TryLoadTextures(ref SprPhotosPorRedux, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_redux_por.png"));
            TryLoadTextures(ref SprPhotosRusRedux, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_redux_rus.png"));
            TryLoadTextures(ref SprPhotosSweRedux, Path.Combine(Values.PathDataFolder, "Photo Mode", "photos_redux_swe.png"));

            // Load the UI textures.
            Texture2D _nullTex = null;
            LoadTexture(out _, Path.Combine(Values.PathDataFolder, "ui.png"));
            TryLoadTextures(ref _nullTex, Path.Combine(Values.PathDataFolder, "ui_chn.png"));
            TryLoadTextures(ref _nullTex, Path.Combine(Values.PathDataFolder, "ui_deu.png"));
            TryLoadTextures(ref _nullTex, Path.Combine(Values.PathDataFolder, "ui_esp.png"));
            TryLoadTextures(ref _nullTex, Path.Combine(Values.PathDataFolder, "ui_fre.png"));
            TryLoadTextures(ref _nullTex, Path.Combine(Values.PathDataFolder, "ui_ind.png"));
            TryLoadTextures(ref _nullTex, Path.Combine(Values.PathDataFolder, "ui_ita.png"));
            TryLoadTextures(ref _nullTex, Path.Combine(Values.PathDataFolder, "ui_por.png"));
            TryLoadTextures(ref _nullTex, Path.Combine(Values.PathDataFolder, "ui_rus.png"));
            TryLoadTextures(ref _nullTex, Path.Combine(Values.PathDataFolder, "ui_swe.png"));

            // Load sequences and light graphics.
            LoadTexturesFromFolder(Path.Combine(Values.PathDataFolder, "Light"));
            LoadTexturesFromFolder(Path.Combine(Values.PathDataFolder, "Sequences"));

            // Load the tilesets and map objects.
            LoadTexturesFromFolder(Path.Combine(Values.PathDataFolder, "Map Objects"));
            LoadTexturesFromFolder(Path.Combine(Values.PathDataFolder, "Maps", "Tilesets"));

            // Load achievement icons (achievement0.png, achievement1.png, ...).
            LoadTexturesFromFolder(Path.Combine(Values.PathDataFolder, "Achievements"));

            // Load graphics mods last so they override base assets
            var graphicsModsPath = GameFS.NormalizePath(Values.PathGraphicsMods);

            if (GameFS.IsDirectory(graphicsModsPath))
                LoadTexturesFromFolder(graphicsModsPath, true);

            // Static resources that never change.
            SprLink = GetTexture("link0.png");
            SprLinkCloak = GetTexture("link_cloak.png");
            SprEnemies = GetTexture("enemies.png");
            SprObjectsAnimated = GetTexture("objects animated.png");
            SprMidBoss = GetTexture("midboss.png");
            SprNightmares = GetTexture("nightmares.png");
            SprBlurTileset = GetTexture("blur tileset.png");

            // Dynamic resources that change depending on options set.
            SprNpCs = GetTexture("npcs.png");
            SprNpCsRedux = GetTexture("npcs_redux.png");
            SprObjectsEng = GetTexture("objects.png");
            SprObjectsChn = GetTexture("objects_chn.png");
            SprObjectsDeu = GetTexture("objects_deu.png");
            SprObjectsEsp = GetTexture("objects_esp.png");
            SprObjectsFre = GetTexture("objects_fre.png");
            SprObjectsInd = GetTexture("objects_ind.png");
            SprObjectsIta = GetTexture("objects_ita.png");
            SprObjectsPor = GetTexture("objects_por.png");
            SprObjectsRus = GetTexture("objects_rus.png");
            SprObjectsSwe = GetTexture("objects_swe.png");
            SprMiniMapEng = GetTexture("minimap.png");
            SprMiniMapChn = GetTexture("minimap_chn.png");
            SprMiniMapDeu = GetTexture("minimap_deu.png");
            SprMiniMapEsp = GetTexture("minimap_esp.png");
            SprMiniMapFre = GetTexture("minimap_fre.png");
            SprMiniMapInd = GetTexture("minimap_ind.png");
            SprMiniMapIta = GetTexture("minimap_ita.png");
            SprMiniMapPor = GetTexture("minimap_por.png");
            SprMiniMapRus = GetTexture("minimap_rus.png");
            SprMiniMapSwe = GetTexture("minimap_swe.png");
            SprItemEng = GetTexture("items.png");
            SprItemChn = GetTexture("items_chn.png");
            SprItemDeu = GetTexture("items_deu.png");
            SprItemEsp = GetTexture("items_esp.png");
            SprItemFre = GetTexture("items_fre.png");
            SprItemInd = GetTexture("items_ind.png");
            SprItemIta = GetTexture("items_ita.png");
            SprItemPor = GetTexture("items_por.png");
            SprItemRus = GetTexture("items_rus.png");
            SprItemSwe = GetTexture("items_swe.png");
            SprItemEngRedux = GetTexture("items_redux.png");
            SprItemChnRedux = GetTexture("items_redux_chn.png");
            SprItemDeuRedux = GetTexture("items_redux_deu.png");
            SprItemEspRedux = GetTexture("items_redux_esp.png");
            SprItemFreRedux = GetTexture("items_redux_fre.png");
            SprItemIndRedux = GetTexture("items_redux_ind.png");
            SprItemItaRedux = GetTexture("items_redux_ita.png");
            SprItemPorRedux = GetTexture("items_redux_por.png");
            SprItemRusRedux = GetTexture("items_redux_rus.png");
            SprItemSweRedux = GetTexture("items_redux_swe.png");

            // Load various SpriteFonts. 
            EditorFont = content.Load<SpriteFont>("Fonts/editor font");
            EditorFontHeight = (int)EditorFont.MeasureString("H").Y;
            EditorFontMonoSpace = content.Load<SpriteFont>("Fonts/editor mono font");
            EditorFontSmallMonoSpace = content.Load<SpriteFont>("Fonts/editor small mono font");
            GameHeaderFont = content.Load<SpriteFont>("Fonts/newHeaderFont");
            FontCredits = content.Load<SpriteFont>("Fonts/credits font");
            FontCreditsHeader = content.Load<SpriteFont>("Fonts/credits header font");
            smallFont = content.Load<SpriteFont>("Fonts/smallFont");
            smallFont_redux = content.Load<SpriteFont>("Fonts/smallFont_redux");
            smallFont_vwf = content.Load<SpriteFont>("Fonts/smallFont_vwf");
            smallFont_vwf_redux = content.Load<SpriteFont>("Fonts/smallFont_vwf_redux");
            smallFont_chn = content.Load<BitmapFont>("Fonts/smallFont_chn");
            smallFont_chn_redux = content.Load<BitmapFont>("Fonts/smallFont_chn_redux");

            // load textures
            SprTiledBlock = new Texture2D(graphics, 2, 2);
            SprTiledBlock.SetData(new[] { Color.White, Color.LightGray, Color.LightGray, Color.White });

            EditorEyeOpen = content.Load<Texture2D>("Editor/eye_open");
            EditorEyeClosed = content.Load<Texture2D>("Editor/eye_closed");
            EditorIconDelete = content.Load<Texture2D>("Editor/delete");
            EditorIconEdit = content.Load<Texture2D>("Editor/edit");
            EditorIconSelect = content.Load<Texture2D>("Editor/select");

            // Lighting and shadow textures
            SprLight = content.Load<Texture2D>("Light/light");
            SprLightRoomH = content.Load<Texture2D>("Light/ligth room");
            SprLightRoomV = content.Load<Texture2D>("Light/ligth room vertical");
            SprMamuLight = content.Load<Texture2D>("Light/mamuLight");
            SprShadow = content.Load<Texture2D>("Light/shadow");
            LoadContentTextureWithAtlas(content, "Light/doorLight");

            // These are loaded in but appear to be unused
            SprIconOptions = content.Load<Texture2D>("Menu/gearIcon");
            SprIconErase = content.Load<Texture2D>("Menu/trashIcon");
            SprIconCopy = content.Load<Texture2D>("Menu/copyIcon");

            // Super Game Boy Border
            sgbBorder = content.Load<Texture2D>("Menu/sgb_border");

            // Fog texture
            SprFog = content.Load<Texture2D>("Objects/fog");

            // Load shader effects
            RoundedCornerEffect = content.Load<Effect>("Shader/RoundedCorner");
            BlurEffectH = content.Load<Effect>("Shader/BlurH");
            BlurEffectV = content.Load<Effect>("Shader/BlurV");
            BBlurEffectH = content.Load<Effect>("Shader/BBlurH");
            BBlurEffectV = content.Load<Effect>("Shader/BBlurV");
            FullShadowEffect = content.Load<Effect>("Shader/FullShadowEffect");

            // used in the inventory
            SaturationEffect = content.Load<Effect>("Shader/SaturationFilter");
            WobbleEffect = content.Load<Effect>("Shader/WobbleShader");
            CircleShader = content.Load<Effect>("Shader/CircleShader");
            LightShader = content.Load<Effect>("Shader/LightShader");
            LightFadeShader = content.Load<Effect>("Shader/LightFadeShader");

            var cloudShader = content.Load<Effect>("Shader/ColorCloud");
            CloudShader = new SpriteShader(cloudShader);
            CloudShader.FloatParameter.Add("scaleX", 1);
            CloudShader.FloatParameter.Add("scaleY", 1);

            NoiseTexture = GetTexture("thanos noise.png");
            ThanosShader = content.Load<Effect>("Shader/ThanosShader");
            ThanosShader.Parameters["NoiceTexture"].SetValue(NoiseTexture);

            // Only works for sprites using the sequence sprite. Use the logical size so
            // an HD sequence or noise texture keeps the same noise pixel density.
            ThanosShader.Parameters["Scale"].SetValue(new Vector2(
                    (float)HdTexture.LogicalWidth(SprGameSequencesFinal) / HdTexture.LogicalWidth(NoiseTexture),
                    (float)HdTexture.LogicalHeight(SprGameSequencesFinal) / HdTexture.LogicalHeight(NoiseTexture)));

            ThanosSpriteShader0 = new SpriteShader(ThanosShader);
            ThanosSpriteShader0.FloatParameter.Add("Percentage", 0);
            ThanosSpriteShader1 = new SpriteShader(ThanosShader);
            ThanosSpriteShader1.FloatParameter.Add("Percentage", 0);

            WindFishShader = new SpriteShader(content.Load<Effect>("Shader/WaleShader"));
            WindFishShader.FloatParameter.Add("Offset", 0);
            WindFishShader.FloatParameter.Add("Period", 0);

            ColorShader = new SpriteShader(content.Load<Effect>("Shader/ColorShader"));

            var damageShader = content.Load<Effect>("Shader/DamageShader");

            // crow needs mark1 to have a value bigger than 0.605333
            DamageSpriteShader0 = new SpriteShader(damageShader);
            DamageSpriteShader0.FloatParameter.Add("mark0", 0.1f);
            DamageSpriteShader0.FloatParameter.Add("mark1", 0.725f);
            DamageSpriteShader0.Vector4Parameter.Add("Color0", new Vector4(1.000f, 0.710f, 0.192f, 1.000f));
            DamageSpriteShader0.Vector4Parameter.Add("Color1", new Vector4(0.871f, 0.000f, 0.000f, 1.000f));
            DamageSpriteShader0.Vector4Parameter.Add("Color2", new Vector4(0.000f, 0.000f, 0.000f, 1.000f));

            // stone hinox needs mark1 to be below 0.553
            DamageSpriteShader1 = new SpriteShader(damageShader);
            DamageSpriteShader1.FloatParameter.Add("mark0", 0.1f);
            DamageSpriteShader1.FloatParameter.Add("mark1", 0.55f);
            DamageSpriteShader1.Vector4Parameter.Add("Color0", new Vector4(1.000f, 0.710f, 0.192f, 1.000f));
            DamageSpriteShader1.Vector4Parameter.Add("Color1", new Vector4(0.871f, 0.000f, 0.000f, 1.000f));
            DamageSpriteShader1.Vector4Parameter.Add("Color2", new Vector4(0.000f, 0.000f, 0.000f, 1.000f));

            var shockShader = content.Load<Effect>("Shader/ShockEffect");

            ShockShader0 = new SpriteShader(shockShader);
            ShockShader0.FloatParameter.Add("mark0", 0.0f);
            ShockShader0.FloatParameter.Add("mark1", 0.2675f);
            ShockShader0.FloatParameter.Add("mark2", 0.725f);

            ShockShader1 = new SpriteShader(shockShader);
            ShockShader1.FloatParameter.Add("mark0", 0.0f);
            ShockShader1.FloatParameter.Add("mark1", 0.35f);
            ShockShader1.FloatParameter.Add("mark2", 0.625f);
        }

        public static void AddSoundEffect(ContentManager content, string fileName)
        {
            // Check for WAV override in mods folder first.
            var wavPath = Path.Combine(Values.PathSoundEffectMods, fileName + ".wav");
            if (GameFS.Exists(wavPath))
            {
                using var stream = GameFS.OpenRead(wavPath);
                SoundEffects.TryAdd(fileName, SoundEffect.FromStream(stream));
                return;
            }

            // Fall back to XNB.
            var soundEffect = content.Load<SoundEffect>("SoundEffects/" + fileName);
            SoundEffects.TryAdd(fileName, soundEffect);
        }

        public static void LoadSounds(ContentManager content)
        {
            // RootDirectory is usually "Content"
            var dir = GameFS.NormalizePath(Path.Combine(content.RootDirectory, "SoundEffects"));

            foreach (var file in GameFS.EnumerateFiles(dir, recursive: false, acceptFile: name => 
            name.EndsWith(".xnb", StringComparison.OrdinalIgnoreCase)))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                AddSoundEffect(content, name);
            }
        }

        public static void LoadTexturesFromFolder(string path, bool recurse = false)
        {
            // Skip over mipmap files (textureName_mip#.png) as they are part of another texture.
            foreach (var full in GameFS.EnumerateFiles(path, recurse, name => name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && 
                !MipChain.IsMipFile(name), skipDirectory: dir => string.Equals(dir, "Intro", StringComparison.OrdinalIgnoreCase)))
            {
                string textureName = Path.GetFileName(full);

                try
                {
                    LoadTextureInternal(out var texture, full);

                    // Mods deliberately overwrite base textures, so release the one being replaced.
                    if (TextureList.TryGetValue(textureName, out var oldTexture) && oldTexture != texture)
                    {
                        // A texture pack may ship only the PNG, or an atlas copied from the base game (scale 1).
                        // Work out the HD scale by comparing against the texture being replaced.
                        HdTexture.InferScale(texture, oldTexture, full);

                        // Atlas entries that still point at the old texture have to follow the replacement,
                        // otherwise they keep drawing a texture that is about to be disposed.
                        RepointAtlasEntries(oldTexture, texture);

                        oldTexture.Dispose();
                    }
                    TextureList[textureName] = texture;
                }
                catch (Exception) { }
            }
        }

        private static void RepointAtlasEntries(Texture2D oldTexture, Texture2D newTexture)
        {
            int scale = HdTexture.GetScale(newTexture);
            var keys = new List<string>();

            foreach (var atlas in Atlases.Values)
            {
                keys.Clear();

                // Entries of the replaced texture, plus entries a mod atlas just created for the new texture.
                foreach (var pair in atlas)
                    if (pair.Value.Texture == oldTexture || pair.Value.Texture == newTexture)
                        keys.Add(pair.Key);

                foreach (var key in keys)
                {
                    var entry = atlas[key];
                    atlas[key] = new DictAtlasEntry(newTexture, entry.SourceRectangle, entry.Origin, scale);
                }
            }
        }

        // Note that "por" and "pte" are both "Portuguese" and share textures.
        private static readonly HashSet<string> _languageSet = new(StringComparer.OrdinalIgnoreCase) { "chn", "deu", "esp", "fre", "ind", "ita", "por", "pte", "rus", "swe" };

        private static string StripLanguageAndVariantTags(string fileNameWithoutExtension, bool stripRedux)
        {
            string[] parts = fileNameWithoutExtension.Split('_');
            var sb = new System.Text.StringBuilder(fileNameWithoutExtension.Length);

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];

                if (_languageSet.Contains(part))
                    continue;

                if (stripRedux && part.Equals("redux", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (sb.Length > 0)
                    sb.Append('_');

                sb.Append(part);
            }

            return sb.ToString();
        }

        private static readonly Dictionary<string, string> _textureFallbackNameCache = new(StringComparer.OrdinalIgnoreCase);

        public static Texture2D GetTexture(string name)
        {
            if (TextureList.TryGetValue(name, out var texture))
                return texture;

            if (!_textureFallbackNameCache.TryGetValue(name, out var newName))
            {
                string stripped = StripLanguageAndVariantTags(Path.GetFileNameWithoutExtension(name), stripRedux: false);
                newName = stripped + Path.GetExtension(name);
                _textureFallbackNameCache[name] = newName;
            }
            return TextureList.TryGetValue(newName, out texture) ? texture : null;
        }

        public static string FindAtlasFile(string textureName)
        {
            textureName = GameFS.NormalizePath(textureName);

            if (_atlasPathCache.TryGetValue(textureName, out var cached))
                return cached;

            string basePath = textureName.Replace(".png", "", StringComparison.OrdinalIgnoreCase);
            string fullAtlas = basePath + ".atlas";

            if (GameFS.Exists(fullAtlas))
                return _atlasPathCache[textureName] = fullAtlas;

            string stripped = StripLanguageAndVariantTags(Path.GetFileNameWithoutExtension(textureName), stripRedux: true);
            string fallbackName = stripped + ".atlas";
            string fallbackPath = GameFS.NormalizePath(Path.Combine(Path.GetDirectoryName(textureName) ?? "", fallbackName));

            if (GameFS.Exists(fallbackPath))
                return _atlasPathCache[textureName] = fallbackPath;

            return _atlasPathCache[textureName] = null;
        }

        public static void LoadContentTextureWithAtlas(ContentManager content, string filePath)
        {
            var texture = content.Load<Texture2D>(filePath);
            var atlasFileName = FindAtlasFile(Path.Combine(Values.PathDataFolder, filePath));

            if (atlasFileName == null)
                return;

            SpriteAtlasSerialization.LoadSourceDictionary(texture, atlasFileName, SpriteAtlas);
        }

        public static void LoadTexture(out Texture2D texture, string assetPath)
        {
            LoadTextureInternal(out texture, assetPath);
        }

        // Returns true if an atlas was found and loaded for the texture. The atlas also sets the texture's HD scale.
        private static bool LoadTextureInternal(out Texture2D texture, string assetPath)
        {
            assetPath = GameFS.NormalizePath(assetPath);

            // Use included mipmap levels if the texture ships "_mip1.png" etc, otherwise load it normally.
            texture = MipChain.TryLoad(assetPath);

            if (texture == null)
            {
                using (Stream stream = GameFS.OpenReadAny(assetPath))
                    texture = Texture2D.FromStream(Game1.Graphics.GraphicsDevice, stream);
            }

            string atlasFileName = FindAtlasFile(assetPath);

            if (atlasFileName == null)
                return false;

            var (lang, variant) = ParseAtlasTags(assetPath);

            if (!Atlases.TryGetValue((lang, variant), out var atlasDesignation))
                throw new InvalidOperationException($"No atlas found for {lang}/{variant}");

            return SpriteAtlasSerialization.LoadSourceDictionary(texture, atlasFileName, atlasDesignation) > 0;
        }

        public static Rectangle SourceRectangle(string id)
        {
            // We don't need to get any special atlas since all versions share the same dimensions.
            return SpriteAtlas.TryGetValue(id, out var entry) ? entry.SourceRectangle : Rectangle.Empty;
        }

        private static DictAtlasEntry GetSpriteInternal(string id, bool variation)
        {
            // All sprites use the current langage to search for variations, most sprites will also use "GameSettings.Uncensored" to determine if
            // there is a "redux" uncensored version, and the photograph sprites will use "GameSettings.PhotosColor" to get the colored versions.
            string lang = Game1.LanguageManager.CurrentLanguageCode;

            // All "search" chains in the switch below should end with "SpriteAtlas" as it will always contains an entry.
            // Note that "por" and "pte" are both "Portuguese" and share textures.
            var atlases = (lang, variation) switch
            {
                ("chn", false) => new[] { SpriteAtlasChn, SpriteAtlas },
                ("deu", false) => new[] { SpriteAtlasDeu, SpriteAtlas },
                ("esp", false) => new[] { SpriteAtlasEsp, SpriteAtlas },
                ("fre", false) => new[] { SpriteAtlasFre, SpriteAtlas },
                ("ind", false) => new[] { SpriteAtlasInd, SpriteAtlas },
                ("ita", false) => new[] { SpriteAtlasIta, SpriteAtlas },
                ("por", false) => new[] { SpriteAtlasPor, SpriteAtlas },
                ("pte", false) => new[] { SpriteAtlasPor, SpriteAtlas },
                ("rus", false) => new[] { SpriteAtlasRus, SpriteAtlas },
                ("swe", false) => new[] { SpriteAtlasSwe, SpriteAtlas },
                (_, false)     => new[] { SpriteAtlas },

                ("chn", true) => new[] { SpriteAtlasChnRedux, SpriteAtlasChn, SpriteAtlas },
                ("deu", true) => new[] { SpriteAtlasDeuRedux, SpriteAtlasDeu, SpriteAtlas },
                ("esp", true) => new[] { SpriteAtlasEspRedux, SpriteAtlasEsp, SpriteAtlas },
                ("fre", true) => new[] { SpriteAtlasFreRedux, SpriteAtlasFre, SpriteAtlas },
                ("ind", true) => new[] { SpriteAtlasIndRedux, SpriteAtlasInd, SpriteAtlas },
                ("ita", true) => new[] { SpriteAtlasItaRedux, SpriteAtlasIta, SpriteAtlas },
                ("por", true) => new[] { SpriteAtlasPorRedux, SpriteAtlasPor, SpriteAtlas },
                ("pte", true) => new[] { SpriteAtlasPorRedux, SpriteAtlasPor, SpriteAtlas },
                ("rus", true) => new[] { SpriteAtlasRusRedux, SpriteAtlasRus, SpriteAtlas },
                ("swe", true) => new[] { SpriteAtlasSweRedux, SpriteAtlasSwe, SpriteAtlas },
                (_, true)     => new[] { SpriteAtlasRedux, SpriteAtlas }
            };
            // Check each atlas and see if it contains the ID of the sprite we are trying to load. If not, go to the next in the chain.
            foreach (var atlas in atlases)
                if (atlas.TryGetValue(id, out var sprite))
                    return sprite;

            return SpriteAtlas.TryGetValue(id, out var fallback) ? fallback : null;
        }

        public static DictAtlasEntry GetSprite(string id) => GetSpriteInternal(id, GameSettings.Uncensored);

        public static DictAtlasEntry GetPhotoSprite(string id) => GetSpriteInternal(id, GameSettings.PhotosColor);

        public static void LoadTilesetSizes()
        {
            var fileName = Path.Combine(Values.PathDataFolder, "Maps", "Tilesets", "tileset size.txt");

            if (!GameFS.Exists(fileName))
                return;

            foreach (var line in GameFS.ReadAllLines(fileName))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                    continue;

                var split = line.Split(':', 2, StringSplitOptions.TrimEntries);
                if (split.Length == 2 && int.TryParse(split[1], out var value))
                    TilesetSizes[split[0]] = value;
            }
        }

        public static void RefreshDynamicResources()
        {
            // Reload photo album photos so proper photos are displayed.
            Game1.GameManager.InGameOverlay.RefreshPhotoOverlay();

            // Refresh title screen resources so proper logo is displayed.
            Game1.ScreenManager.Intro.RefreshIntroResources();

            // Reload the UI textures (hearts, rupee icon, small key icon, game over, etc). 
            ItemDrawHelper.RefreshImagesUI();
        }
    }
}
