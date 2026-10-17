using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Graphics
{
    public static class SpriteAtlasSerialization
    {
        private static readonly Dictionary<string, SpriteAtlas> _atlasCache = new(StringComparer.OrdinalIgnoreCase);

        public static void ClearAtlasCache()
        {
            _atlasCache.Clear();
        }

        private static SpriteAtlas CloneAtlas(SpriteAtlas source)
        {
            var clone = new SpriteAtlas { Scale = source.Scale };

            for (int i = 0; i < source.Data.Count; i++)
            {
                var e = source.Data[i];
                clone.Data.Add(new AtlasEntry
                {
                    EntryId = e.EntryId,
                    SourceRectangle = e.SourceRectangle,
                    Origin = e.Origin
                });
            }
            return clone;
        }

        public sealed class SpriteAtlas
        {
            public List<AtlasEntry> Data { get; } = new();
            public int Scale = 1;
        }

        public sealed class AtlasEntry
        {
            public string EntryId = "";
            public Rectangle SourceRectangle;
            public Vector2 Origin;

            public override string ToString() => EntryId;
        }

        // ----------------------------------------------------------------------------------------------------
        //  LOAD
        // ----------------------------------------------------------------------------------------------------
        public static bool LoadSpriteAtlas(string filePath, SpriteAtlas spriteAtlas, bool clearExisting = true)
        {
            if (spriteAtlas == null) throw new ArgumentNullException(nameof(spriteAtlas));
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            filePath = GameFS.NormalizePath(filePath);

            if (!GameFS.Exists(filePath))
                return false;

            if (_atlasCache.TryGetValue(filePath, out var cachedAtlas))
            {
                if (clearExisting)
                    spriteAtlas.Data.Clear();

                var clone = CloneAtlas(cachedAtlas);
                spriteAtlas.Scale = clone.Scale;
                spriteAtlas.Data.AddRange(clone.Data);
                return true;
            }

            if (clearExisting)
                spriteAtlas.Data.Clear();

            using var stream = GameFS.OpenRead(filePath);
            using var reader = new StreamReader(stream);

            _ = reader.ReadLine(); // version

            var scaleLine = reader.ReadLine();
            if (!int.TryParse(scaleLine, NumberStyles.Integer, CultureInfo.InvariantCulture, out var scale) || scale <= 0)
                scale = 1;

            spriteAtlas.Scale = scale;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int colon = line.IndexOf(':');
                if (colon <= 0 || colon >= line.Length - 1)
                    continue;

                var id = line.Substring(0, colon);
                var data = line.Substring(colon + 1);

                var parts = data.Split(',');
                if (parts.Length < 4)
                    continue;

                if (!TryInt(parts, 0, out int x) ||
                    !TryInt(parts, 1, out int y) ||
                    !TryInt(parts, 2, out int w) ||
                    !TryInt(parts, 3, out int h))
                    continue;

                float ox = 0, oy = 0;
                if (parts.Length >= 6)
                {
                    TryFloat(parts, 4, out ox);
                    TryFloat(parts, 5, out oy);
                }
                spriteAtlas.Data.Add(new AtlasEntry
                {
                    EntryId = id,
                    SourceRectangle = new Rectangle(x, y, w, h),
                    Origin = new Vector2(ox, oy)
                });
            }
            _atlasCache[filePath] = CloneAtlas(spriteAtlas);
            return true;
        }

        // ----------------------------------------------------------------------------------------------------
        //  DICTIONARY POPULATION
        // ----------------------------------------------------------------------------------------------------
        // Loads the atlas, registers the texture's scale from the second line of the atlas, and
        // fills the dictionary. Returns the scale that was applied, or 0 if no atlas could be loaded.
        public static int LoadSourceDictionary(Texture2D texture, string fileName, Dictionary<string, DictAtlasEntry> dictionary)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));

            var spriteAtlas = new SpriteAtlas();
            if (!LoadSpriteAtlas(fileName, spriteAtlas))
                return 0;

            // Atlas rectangles are logical, the scale says how many texels each logical pixel has.
            HdTexture.SetScale(texture, spriteAtlas.Scale, fileName);

            // Read it back since SetScale can reject a scale that doesn't divide the texture size.
            int scale = HdTexture.GetScale(texture);

            for (int i = 0; i < spriteAtlas.Data.Count; i++)
            {
                var e = spriteAtlas.Data[i];
                var dictEntry = new DictAtlasEntry(texture, e.SourceRectangle, e.Origin, scale);
                dictionary[e.EntryId] = dictEntry;
            }
            return scale;
        }

        // ----------------------------------------------------------------------------------------------------
        //  PARSE HELPERS
        // ----------------------------------------------------------------------------------------------------
        private static bool TryInt(string[] parts, int index, out int value)
        {
            value = 0;
            if (index >= parts.Length) return false;
            return int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryFloat(string[] parts, int index, out float value)
        {
            value = 0;
            if (index >= parts.Length) return false;

            // Accept int-like strings too.
            if (float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;

            // Fallback: sometimes files have stray whitespace
            return float.TryParse(parts[index].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
