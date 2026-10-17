using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Identifiers;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Map
{
    public class DiscoveryFog
    {
        // How many mask texels make up one node along each axis. The soft edge between a hidden
        // and a revealed node is one texel wide, so field size / SubDiv gives the fade width.
        private int SubDivX;
        private int SubDivY;

        // Current fog strength per field. Fades out when revealed.
        // 0 = Revealed, 1 = Hidden.
        private float[,] _fog;
        private int _gridWidth;
        private int _gridHeight;

        // The SubDiv the mask texture was actually built with, so a change can be noticed.
        private int _builtSubDivX;
        private int _builtSubDivY;

        // Where the fog grid is currently coming from. Only one of these is ever set, the other
        // is always null. The overworld uses one big visibility array, a dungeon uses the
        // discovery flags on the minimap of the floor the player is standing on.
        private bool[,] _srcOverworld;
        private GameManager.MiniMap _srcDungeon;

        // World position of node 0,0 for whichever source is active.
        private Vector2 _origin;

        // Identifies the current source. The overworld is one map so its name is enough, a
        // dungeon needs the full name because every floor is a separate grid.
        private string _sourceKey;
        private string _builtSourceKey;

        private Texture2D _maskTexture;

        // Scratch buffer holding one node's block of texels. Reused for every upload.
        private Color[] _nodeBuffer;

        // Which nodes changed since the last upload. Only these get pushed to the texture.
        private bool[,] _nodeDirty;
        private bool _maskDirty = true;
        private bool _fullyRevealed;
        public float _sharpPercentage;

        // Whether the fog is currently freezing objects. Resolved once per frame in Update so
        // every system in that frame gets the same answer.
        private bool _freezeActive;
        public bool FreezeActive => _freezeActive;

        // The node the player is standing in. Discovery for this node is applied by
        // ObjOverworld/ObjDungeon during the same update pass, so it is never treated as
        // hidden. Without this everything in a newly entered field freezes for one frame.
        private int _playerNodeX = int.MinValue;
        private int _playerNodeY = int.MinValue;

        // User configurable settings.
        private bool  _overworldFog => GameSettings.ModernFogEnabled && (!Camera.ClassicMode || GameSettings.CameraMode == 2);
        private bool  _dungeonFog   => GameSettings.ModernFogDungeon && (!Camera.ClassicMode || GameSettings.CameraMode == 2);
        private float _sharpness    => GameSettings.ModernFogSharp;
        private float _opacity      => GameSettings.ModernFogOpacity;
        private float _fadeTime     => GameSettings.ModernFogFade;

        // The color of the fog.
        private Color _fogColor;

        // Values configurable via lahdmod.
        private int fog_red = 0;
        private int fog_grn = 0;
        private int fog_blu = 0;

        public void OnLoad()
        {
            // Force a rebuild on the next frame.
            _builtSourceKey = null;

            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "DiscoveryFog.lahdmod");
            ModFile.Parse(modFile, this);

            // Assemble the color from whatever the values are now. 
            _fogColor = new Color(fog_red, fog_grn, fog_blu);
        }

        public void Update()
        {
            // Work out where the fog grid comes from. Returns false if there is nothing to fog.
            if (!ResolveSource())
            {
                // No fog on this map means nothing to freeze either.
                _freezeActive = false;
                return;
            }

            // Loads in discovery values and creates fog texture if not created. Has to run
            // before the freeze is armed because IsHiddenAt bounds checks against the grid.
            EnsureGrid();

            // The fog is live on this map, so hidden fields freeze what is standing in them.
            _freezeActive = true;

            // Remember which node the player occupies so it can never be frozen.
            var link = MapManager.ObjLink;
            if (link?.EntityPosition != null)
            {
                _playerNodeX = Map.FloorDiv((int)link.EntityPosition.X - (int)_origin.X, Values.FieldWidth);
                _playerNodeY = Map.FloorDiv((int)link.EntityPosition.Y - (int)_origin.Y, Values.FieldHeight);
            }
            else
            {
                _playerNodeX = int.MinValue;
                _playerNodeY = int.MinValue;
            }

            // How far a node can move toward its target this frame. A step of 1 arrives instantly.
            var step = _fadeTime <= 0f ? 1f : Game1.DeltaTime / _fadeTime;

            // Start by assuming the whole map is revealed.
            _fullyRevealed = true;

            for (var y = 0; y < _gridHeight; y++)
            {
                for (var x = 0; x < _gridWidth; x++)
                {
                    var target = IsDiscovered(x, y) ? 0f : 1f;
                    var current = _fog[x, y];

                    if (current != target)
                    {
                        // Move one step toward the target without overshooting it.
                        current = target > current
                            ? MathHelper.Min(target, current + step)
                            : MathHelper.Max(target, current - step);

                        _fog[x, y] = current;

                        // Only this node moved, so only this node needs re-uploading.
                        _nodeDirty[x, y] = true;
                        _maskDirty = true;
                    }
                    // If any node that counts has not been discovered, it's not revealed.
                    if (current > 0f && CountsForReveal(x, y))
                        _fullyRevealed = false;
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // Work out where the fog grid comes from. Returns false if there is nothing to fog.
            if (!ResolveSource())
                return;

            // Loads in discovery values and creates fog texture if not created.
            EnsureGrid();

            // If the map is fully revealed, no point in drawing a grid.
            if (_fullyRevealed || _maskTexture == null)
                return;

            UploadMask();

            // Destination rectangle.
            var desX = (int)_origin.X - Values.FieldWidth;
            var desY = (int)_origin.Y - Values.FieldHeight;
            var desW = (_gridWidth + 2) * Values.FieldWidth;
            var desH = (_gridHeight + 2) * Values.FieldHeight;
            var destination = new Rectangle(desX, desY, desW, desH);

            // Source rectangle.
            var sourceX = -SubDivX;
            var sourceY = -SubDivY;
            var sourceW = (_gridWidth + 2) * SubDivX;
            var sourceH = (_gridHeight + 2) * SubDivY;
            var source = new Rectangle(sourceX, sourceY, sourceW, sourceH);

            // Draw the fog. Opacity rides on the tint instead of the texture, so it can be
            // changed at any time without rebuilding anything.
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null, null, MapManager.Camera.TransformMatrix);
            spriteBatch.Draw(_maskTexture, destination, source, _fogColor * _opacity);
            spriteBatch.End();
        }

        public void Dispose()
        {
            // Dispose the fog texture and clear all values.
            try
            {
                _maskTexture?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DiscoveryFog.Dispose failed: " + ex);
            }
            _fog = null;
            _maskTexture = null;
            _nodeBuffer = null;
            _nodeDirty = null;
            _srcOverworld = null;
            _srcDungeon = null;
            _gridWidth = 0;
            _gridHeight = 0;
            _builtSubDivX = 0;
            _builtSubDivY = 0;
            _builtSourceKey = null;
            _maskDirty = true;
            _freezeActive = false;
            _playerNodeX = int.MinValue;
            _playerNodeY = int.MinValue;
        }

        private bool ResolveSource()
        {
            // Always clear both first so a stale source can never be left pointing at old data.
            _srcOverworld = null;
            _srcDungeon = null;

            var gameManager = Game1.GameManager;
            var map = gameManager?.MapManager?.CurrentMap;
            if (map == null)
                return false;

            // The overworld grid is determined where "ObjOverworld" was placed.
            if (_overworldFog && map.IsOverworld && !map.Is2dMap)
            {
                if (gameManager.MapVisibility == null)
                    return false;

                _srcOverworld = gameManager.MapVisibility;
                _origin = ObjOverworld.GridOrigin;
                _sourceKey = map.MapName;
                return true;
            }

            // A dungeon floor uses the discovery flags on its minimap.
            if (_dungeonFog && map.IsDungeon && !map.Is2dMap)
            {
                var fullName = map.LocationFullName;
                if (fullName == null ||
                    !gameManager.DungeonMaps.TryGetValue(fullName, out var miniMap) ||
                    miniMap?.Tiles == null)
                    return false;

                _srcDungeon = miniMap;
                _origin = new Vector2(map.MapOffsetX * Values.TileSize, map.MapOffsetY * Values.TileSize);
                _sourceKey = fullName;
                return true;
            }

            return false;
        }

        private void GetSourceSize(out int width, out int height)
        {
            // The overworld array is exactly the grid.
            if (_srcOverworld != null)
            {
                width = _srcOverworld.GetLength(0);
                height = _srcOverworld.GetLength(1);
                return;
            }
            // A dungeon minimap can be bigger than the room grid of the map it belongs to, so
            // never let the fog run past the edge of the actual map.
            var map = Game1.GameManager.MapManager.CurrentMap;
            var fieldsX = (map.MapWidth  * Values.TileSize + Values.FieldWidth  - 1) / Values.FieldWidth;
            var fieldsY = (map.MapHeight * Values.TileSize + Values.FieldHeight - 1) / Values.FieldHeight;

            width  = Math.Min(_srcDungeon.Tiles.GetLength(0), Math.Max(1, fieldsX));
            height = Math.Min(_srcDungeon.Tiles.GetLength(1), Math.Max(1, fieldsY));
        }

        private bool IsDiscovered(int x, int y)
        {
            // Whichever source is active answers the question the same way.
            if (_srcOverworld != null)
                return _srcOverworld[x, y];
            return _srcDungeon.Tiles[x, y].DiscoveryState;
        }

        public bool IsHiddenAt(float worldX, float worldY)
        {
            // No fog on this map, or the fog has not resolved a source yet this frame.
            if (!_freezeActive)
                return false;

            // Convert the world position into a node on whichever grid is active.
            var nodeX = Map.FloorDiv((int)MathF.Floor(worldX) - (int)_origin.X, Values.FieldWidth);
            var nodeY = Map.FloorDiv((int)MathF.Floor(worldY) - (int)_origin.Y, Values.FieldHeight);

            // The player's own node is discovered later in this same update pass.
            if (nodeX == _playerNodeX && nodeY == _playerNodeY)
                return false;

            // Anything off the edge of the grid keeps updating normally.
            if (nodeX < 0 || nodeY < 0 || nodeX >= _gridWidth || nodeY >= _gridHeight)
                return false;

            // Return that the node was not discovered.
            return !IsDiscovered(nodeX, nodeY);
        }

        public Point NodeAt(float worldX, float worldY)
        {
            return new Point(
                Map.FloorDiv((int)MathF.Floor(worldX) - (int)_origin.X, Values.FieldWidth),
                Map.FloorDiv((int)MathF.Floor(worldY) - (int)_origin.Y, Values.FieldHeight));
        }

        private bool CountsForReveal(int x, int y)
        {
            if (_srcOverworld != null)
                return true;

            // Matches IsMiniMapFullyDiscovered: only real rooms count.
            return _srcDungeon.Tiles[x, y].TileIndex >= 5;
        }

        private void EnsureGrid()
        {
            // Convert the blur percentage into texels per node. 100% is half the field size,
            // which keeps the texels square and gives a 2 pixel gradient.
            _sharpPercentage = MathHelper.Clamp(_sharpness, 0f, 1f);
            SubDivX = Math.Max(1, (int)(Values.FieldWidth  * 0.5f * _sharpPercentage));
            SubDivY = Math.Max(1, (int)(Values.FieldHeight * 0.5f * _sharpPercentage));

            // Get the grid size of whichever source is active.
            GetSourceSize(out var width, out var height);

            // Early exit if fog is not null, grid matches up, SubDiv has not changed, the source
            // is the same one as last time, and the mask texture exists. Everything below only
            // runs on the first call, on a settings change, or when moving to a new grid.
            if (_fog != null && _gridWidth == width && _gridHeight == height &&
                _builtSubDivX == SubDivX && _builtSubDivY == SubDivY &&
                _builtSourceKey == _sourceKey && _maskTexture != null)
                return;

            // Set up the grid and fog values.
            _gridWidth = width;
            _gridHeight = height;
            _builtSubDivX = SubDivX;
            _builtSubDivY = SubDivY;
            _builtSourceKey = _sourceKey;
            _fog = new float[width, height];
            _nodeDirty = new bool[width, height];

            // Start by assuming the whole map is revealed.
            _fullyRevealed = true;

            // Loop through to see if any of the nodes have not been discovered. Building fresh
            // here is also what makes a new floor or map snap instead of cross-fading.
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    // Use the discovery state from the source to draw fog or not.
                    _fog[x, y] = IsDiscovered(x, y) ? 0f : 1f;

                    // The texture is brand new, so every node has to be written at least once.
                    _nodeDirty[x, y] = true;

                    // If any node that counts has not been discovered it's not revealed.
                    if (_fog[x, y] > 0f && CountsForReveal(x, y))
                        _fullyRevealed = false;
                }
            }
            // Create the fog texture and the single node sized buffer used to fill it.
            _maskTexture?.Dispose();
            _maskTexture = new Texture2D(Game1.Graphics.GraphicsDevice, width * SubDivX, height * SubDivY);
            _nodeBuffer = new Color[SubDivX * SubDivY];
            _maskDirty = true;
        }

        private void UploadMask()
        {
            // Nothing moved since the last upload.
            if (!_maskDirty)
                return;

            for (var y = 0; y < _gridHeight; y++)
            {
                for (var x = 0; x < _gridWidth; x++)
                {
                    // Skip every node that is sitting at the same value as last frame. During a
                    // fade this is usually all but one or two of them.
                    if (!_nodeDirty[x, y])
                        continue;

                    // Turn the fog value into an alpha byte. Only alpha matters, the color comes
                    // from the tint at draw time. SpriteBatch wants premultiplied alpha, so the
                    // RGB channels have to carry the same value.
                    var alpha = (byte)MathHelper.Clamp(_fog[x, y] * 255f, 0f, 255f);
                    var texel = new Color(alpha, alpha, alpha, alpha);

                    // Fill the buffer with that value. Every texel in a node is the same, so the
                    // gradient only ever lands on the seam between two nodes.
                    for (var i = 0; i < _nodeBuffer.Length; i++)
                        _nodeBuffer[i] = texel;

                    // Write the buffer into just this node's block of the texture instead of
                    // re-uploading the whole thing.
                    var block = new Rectangle(x * SubDivX, y * SubDivY, SubDivX, SubDivY);
                    _maskTexture.SetData(0, block, _nodeBuffer, 0, _nodeBuffer.Length);

                    _nodeDirty[x, y] = false;
                }
            }
            _maskDirty = false;
        }

        public static Point WorldToField(Vector2 worldPosition)
        {
            // Gets the position of a field based on the map coordinates. Currently unused.
            return new Point(
                (int)MathF.Floor((worldPosition.X - ObjOverworld.GridOrigin.X) / Values.FieldWidth),
                (int)MathF.Floor((worldPosition.Y - ObjOverworld.GridOrigin.Y) / Values.FieldHeight));
        }
    }
}
