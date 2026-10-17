using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Dungeon
{
    internal class ObjEggTeleporter : GameObject
    {
        private readonly List<GameObject> _mapObjects = new List<GameObject>();

        struct RoomState
        {
            public int Direction;
            public float Light;
            public float LightTarget;
            public bool Lit;
        }

        private RoomState[,] RoomStates = new RoomState[4, 5];
        private RoomState[,] tempRoomStates = new RoomState[4, 5];

        private int roomX;
        private int roomY;
        private int lastRoomX = 1;
        private int lastRoomY = 2;

        private float lightSpeed = 0.065f;
        private float darknessSpeed = 0.08f;
        private float tLit = 0;
        private float tDark = 1;
        private float tLightBleed = 0.845f;

        private int[] _movedPath = new int[7];
        private int _pathIndex;

        private int[] _targetPath = new int[7];

        private bool _initLight;
        private bool _foundPath;

        public ObjEggTeleporter() : base("editor egg teleport")
        {
            EditorColor = Color.Green * 0.5f;
        }

        public ObjEggTeleporter(Map.Map map, int posX, int posY) : base(map)
        {
            for (var y = 0; y < RoomStates.GetLength(1); y++)
            {
                for (var x = 0; x < RoomStates.GetLength(0); x++)
                {
                    RoomStates[x, y] = new RoomState();
                    RoomStates[x, y].Light = tDark;
                    RoomStates[x, y].LightTarget = tDark;
                }
            }
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight));

            // Check to see if the player read the book that has the directions.
            var book_read = Game1.GameManager.SaveManager.GetString("egg_book_read");

            // If the player never read the book, it will default to path "0".
            var eggDirections = "0";

            // If the player read the book, then load the directions assigned at the start of the game.
            if (!string.IsNullOrEmpty(book_read) && book_read == "1")
                eggDirections = Game1.GameManager.SaveManager.GetString("eggDirections");

            // Set the target path based on the directions that were assigned.
            _targetPath = eggDirections switch
            {
                "0" => new int[] { 0, 0, 1, 2, 2, 1, 0 },
                "1" => new int[] { 2, 1, 1, 2, 1, 1, 2 },
                "2" => new int[] { 0, 1, 2, 1, 0, 1, 2 },
                "3" => new int[] { 2, 2, 2, 2, 1, 1, 1 },
                _   => new int[] { 0, 0, 1, 2, 2, 1, 0 }
            };
        }

        // Assume six rooms: 4 rooms in a square, 1 room above, 1 room below. Let's number those rooms:
        // above square: 0, square top left: 1, square top right: 2, square bottom left: 3, square bottom right: 4, below square: 5.
        // This hack teleports the player from room 1 to room 3 just before entering room 3, or rather to the entrance of room 5. This ensures when leaving the egg, the torch room
        // is always to the south. Classic camera mode bypasses normal behavior and does its own thing. Normally, rooms 1, 2, and 4 are never really entered. The player is almost
        // always teleported back to room 3. When classic camera is active, room 1 acts as room 3 when walking north. This is to have smooth camera transitions. When classic camera
        // is disabled, the player is always teleported back to room 3 when entering room 1. So the hack is needed to teleport back to room 3 when traveling south.

        private void LeaveEggTeleportHack()
        {
            if (!Camera.ClassicMode) { return; }

            var pos = MapManager.ObjLink.Position;
            var dir = AnimationHelper.GetDirection(MapManager.ObjLink.LastMoveVector);

            if ((roomY == 1) && (pos.Y > 240) && (pos.Y < 256) && (dir == 3))
            {
                var offset = new Vector2(0, Values.FieldHeight);
                var newpos = new Vector2(MapManager.ObjLink.EntityPosition.X, MapManager.ObjLink.EntityPosition.Y) + offset;

                Camera.SnapCameraTimer = 50f;
                MapManager.ObjLink.EntityPosition.Set(newpos);

                _mapObjects.Clear();
                Map.Objects.GetComponentList(_mapObjects, (int)MapManager.ObjLink.EntityPosition.X - 200, (int)MapManager.ObjLink.EntityPosition.Y - 200, 400, 400, DrawComponent.Mask);

                foreach (var gameObject in _mapObjects)
                {
                    if (gameObject is ObjBoomerang)
                        gameObject.EntityPosition.Offset(offset);

                    else if (gameObject is ObjAnimator)
                        Map.Objects.DeleteObjects.Add(gameObject);
                }
            }
        }

        private void Update()
        {
            LeaveEggTeleportHack();

            if (!_initLight)
                InitLight();

            UpdateRoom();

            var posX = MapManager.ObjLink.EntityPosition.X;
            var posY = MapManager.ObjLink.EntityPosition.Y - 4;
            roomX = (int)((posX + 80) / Values.FieldWidth);
            roomY = (int)(posY / Values.FieldHeight);

            for (int y = 0; y < RoomStates.GetLength(1); y++)
            {
                for (int x = 0; x < RoomStates.GetLength(0); x++)
                {
                    var center = new Vector2(-80 + (x + 0.5f) * Values.FieldWidth, (y + 0.5f) * Values.FieldHeight) -
                        new Vector2(MapManager.ObjLink.EntityPosition.X, MapManager.ObjLink.EntityPosition.Y - 4);
                    if (x == roomX && y == roomY ||
                        Values.FieldWidth / 2 - 8 < Math.Abs(center.X) && Math.Abs(center.X) < Values.FieldWidth / 2 + 8 && Math.Abs(center.Y) < 16 ||
                        Values.FieldHeight / 2 - 8 < Math.Abs(center.Y) && Math.Abs(center.Y) < Values.FieldHeight / 2 + 8 && Math.Abs(center.X) < 16)
                    {
                        if (!RoomStates[x, y].Lit)
                        {
                            RoomStates[x, y].Lit = true;
                            RoomStates[x, y].LightTarget = tLit;
                            RoomStates[x, y].Direction = AnimationHelper.GetDirection(-new Vector2(center.X, center.Y));
                        }
                    }
                    else
                    {
                        if (RoomStates[x, y].Lit)
                        {
                            RoomStates[x, y].Lit = false;
                            RoomStates[x, y].LightTarget = tLightBleed;
                            RoomStates[x, y].Direction = AnimationHelper.GetDirection(-new Vector2(center.X, center.Y));
                        }
                    }

                    var tSpeed = RoomStates[x, y].Light < RoomStates[x, y].LightTarget ? darknessSpeed : lightSpeed;
                    var newLightValue = AnimationHelper.MoveToTarget(RoomStates[x, y].Light, RoomStates[x, y].LightTarget, tSpeed * Game1.TimeMultiplier);

                    if (RoomStates[x, y].Lit)
                    {
                        // light up the side rooms
                        if (RoomStates[x, y].Direction % 2 == 0)
                        {
                            if (RoomStates[x, y].Light > 0.45f && newLightValue <= 0.45f)
                            {
                                SetRoomState(x, y - 1, tLightBleed, 3);
                                SetRoomState(x, y + 1, tLightBleed, 1);
                            }
                            if (RoomStates[x, y].Light > 0.125f && newLightValue <= 0.125f)
                            {
                                SetRoomState(x + (RoomStates[x, y].Direction == 0 ? 1 : -1), y, tLightBleed, RoomStates[x, y].Direction);
                            }
                        }
                        else if (RoomStates[x, y].Direction % 2 == 1)
                        {
                            if (RoomStates[x, y].Light > 0.385f && newLightValue <= 0.385f)
                            {
                                SetRoomState(x - 1, y, tLightBleed, 2);
                                SetRoomState(x + 1, y, tLightBleed, 0);
                            }
                            if (RoomStates[x, y].Light > 0.175f && newLightValue <= 0.175f)
                            {
                                SetRoomState(x, y + (RoomStates[x, y].Direction == 1 ? 1 : -1), tLightBleed, RoomStates[x, y].Direction);
                            }
                        }
                    }
                    else
                    {
                        // darken the side rooms
                        if (RoomStates[x, y].Direction % 2 == 0)
                        {
                            if (RoomStates[x, y].Light < 0.35f && newLightValue >= 0.35f)
                            {
                                SetRoomState(x, y - 1, tDark, 3);
                                SetRoomState(x, y + 1, tDark, 1);
                            }
                            if (RoomStates[x, y].Light <= 0.0f && newLightValue > 0.0f)
                            {
                                SetRoomState(x + (RoomStates[x, y].Direction == 0 ? 1 : -1), y, tDark, RoomStates[x, y].Direction);
                            }
                        }
                        else if (RoomStates[x, y].Direction % 2 == 1)
                        {
                            if (RoomStates[x, y].Light < 0.275f && newLightValue >= 0.275f)
                            {
                                SetRoomState(x - 1, y, tDark, 2);
                                SetRoomState(x + 1, y, tDark, 0);
                            }
                            if (RoomStates[x, y].Light < 0.1f && newLightValue >= 0.1f)
                            {
                                SetRoomState(x, y + (RoomStates[x, y].Direction == 1 ? 1 : -1), tDark, RoomStates[x, y].Direction);
                            }
                        }
                    }
                }
            }

            for (int y = 0; y < RoomStates.GetLength(1); y++)
            {
                for (int x = 0; x < RoomStates.GetLength(0); x++)
                {
                    var tSpeed = RoomStates[x, y].Light < RoomStates[x, y].LightTarget ? darknessSpeed : lightSpeed;
                    var newLightValue = AnimationHelper.MoveToTarget(RoomStates[x, y].Light, RoomStates[x, y].LightTarget, tSpeed * Game1.TimeMultiplier);
                    RoomStates[x, y].Light = newLightValue;
                }
            }

            lastRoomX = roomX;
            lastRoomY = roomY;
        }

        private void InitLight()
        {
            _initLight = true;

            var posX = MapManager.ObjLink.EntityPosition.X;
            var posY = MapManager.ObjLink.EntityPosition.Y - 4;
            roomX = (int)((posX + 80) / Values.FieldWidth);
            roomY = (int)(posY / Values.FieldHeight);

            SetRoomState(roomX, roomY, tLit, 0);
            SetRoomState(roomX - 1, roomY, tLightBleed, 2);
            SetRoomState(roomX + 1, roomY, tLightBleed, 0);
            SetRoomState(roomX, roomY - 1, tLightBleed, 3);
            SetRoomState(roomX, roomY + 1, tLightBleed, 1);
        }

        private void SetRoomState(int xIndex, int yIndex, float light, int direction)
        {
            if (0 <= xIndex && xIndex < RoomStates.GetLength(0) &&
                0 <= yIndex && yIndex < RoomStates.GetLength(1))
            {
                RoomStates[xIndex, yIndex].LightTarget = light;
                RoomStates[xIndex, yIndex].Direction = direction;
            }
        }

        private void UpdateRoom()
        {
            var posX = MapManager.ObjLink.EntityPosition.X;
            var posY = MapManager.ObjLink.EntityPosition.Y - 4;

            roomX = (int)((posX + 80) / Values.FieldWidth);
            roomY = (int)(posY / Values.FieldHeight);

            if (roomX != lastRoomX || roomY != lastRoomY)
            {
                var direction = new Vector2(roomX, roomY) - new Vector2(lastRoomX, lastRoomY);
                var dir = AnimationHelper.GetDirection(direction);

                _movedPath[_pathIndex] = dir;

                // Check if the player found the correct path.
                _foundPath = true;
                for (var i = 0; i < _movedPath.Length; i++)
                    if (_movedPath[(_pathIndex + i + 1) % 7] != _targetPath[i])
                    {
                        _foundPath = false;
                        break;
                    }

                _pathIndex = (_pathIndex + 1) % 7;
            }

            // Offset the player to not move outside.
            var dist = 16;

            // Up (Classic Camera): The path to the jump has been found. Room Y remains 1 during classic camera.
            if (_foundPath && posY < roomY * Values.FieldHeight + dist && (roomX == 1 || roomX == 2) && roomY == 1)
            {
                // Only room 2 needs shifting into place. Room 1 is already correct, and running the
                // teleport for a zero offset re-arms the snap timer and replaces the final scroll.
                if (roomX == 2)
                    OffsetPlayer(-1, 0);
            }
            // Up (Normal Camera): The path to the jump has been found. Room Y remains 2 during normal camera.
            else if (_foundPath && posY < roomY * Values.FieldHeight + dist && (roomX == 1 || roomX == 2) && roomY == 2)
                OffsetPlayer(roomX == 1 ? 0 : -1, -1);
            
            // Left
            else if (posX < 80 + dist && roomX == 1 && (roomY == 1 || roomY == 2))
                OffsetPlayer(1, 0);
            
            // Right
            else if (posX > 80 + Values.FieldWidth * 2 - dist && roomX == 2 && (roomY == 1 || roomY == 2))
                OffsetPlayer(-1, 0);
            
            // Down
            else if (posY > (roomY + 1) * Values.FieldHeight - dist && roomX == 2 && (roomY == 2))
                OffsetPlayer(-1, 0);
            
            // Up: With normal camera, always teleport the player from the top room to the room just above the exit.
            else if (!Camera.ClassicMode && !_foundPath && (roomX == 1 || roomX == 2) && (roomY == 1) &&
                (!RoomStates[roomX, roomY - 1].Lit) && (RoomStates[roomX, roomY - 1].Light) == (RoomStates[roomX, roomY - 1].LightTarget) &&
                (!RoomStates[roomX, roomY + 1].Lit) && (RoomStates[roomX, roomY + 1].Light) == (RoomStates[roomX, roomY + 1].LightTarget))
                OffsetPlayer(roomX == 2 ? -1 : 0, 1);
            
            // Up: With classic camera, always teleport the player back to the top room and rely on the teleport hack to move to the exit.
            else if (Camera.ClassicMode && !_foundPath && (roomX == 1 || roomX == 2) && (roomY == 1) && 
                (posY < roomY * Values.FieldHeight + dist) && AnimationHelper.GetDirection(MapManager.ObjLink.LastMoveVector) == 1)
                OffsetPlayer(roomX == 2 ? -1 : 0, 1);

        }

        private void OffsetPlayer(int offsetX, int offsetY)
        {
            // Offset the light map data with the player.
            for (int y = 0; y < RoomStates.GetLength(1); y++)
                for (int x = 0; x < RoomStates.GetLength(0); x++)
                    tempRoomStates[x, y] = RoomStates[x, y];

            for (int y = 0; y < RoomStates.GetLength(1); y++)
                for (int x = 0; x < RoomStates.GetLength(0); x++)
                    RoomStates[
                        (x + offsetX + RoomStates.GetLength(0)) % RoomStates.GetLength(0),
                        (y + offsetY + RoomStates.GetLength(1)) % RoomStates.GetLength(1)] = tempRoomStates[x, y];

            // Teleport Link to the previous room.
            var offset = new Vector2(offsetX * Values.FieldWidth, offsetY * Values.FieldHeight);
            var newpos = new Vector2(MapManager.ObjLink.EntityPosition.X, MapManager.ObjLink.EntityPosition.Y) + offset;
            MapManager.ObjLink.EntityPosition.Set(newpos);

            // Perform some camera magic.
            if (Camera.ClassicMode)
                Camera.SnapCameraTimer = 25f;
            else
            {
                var goalPosition = Game1.GameManager.MapManager.GetCameraTarget();
                MapManager.Camera.SoftUpdate(goalPosition);
            }
            _mapObjects.Clear();
            Map.Objects.GetComponentList(_mapObjects, (int)MapManager.ObjLink.EntityPosition.X - 200, (int)MapManager.ObjLink.EntityPosition.Y - 200, 400, 400, DrawComponent.Mask);

            foreach (var gameObject in _mapObjects)
            {
                if (gameObject is ObjBoomerang)
                    gameObject.EntityPosition.Offset(offset);

                else if (gameObject is ObjAnimator)
                    Map.Objects.DeleteObjects.Add(gameObject);
            }
        }

        private void DrawLight(SpriteBatch spriteBatch)
        {
            // Don't need to draw the light when classic camera is active.
            if (Camera.ClassicMode) { return; }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null, Resources.LightFadeShader, MapManager.Camera.TransformMatrix);

            for (int y = 0; y < RoomStates.GetLength(1); y++)
            {
                for (int x = 0; x < RoomStates.GetLength(0); x++)
                {
                    var lightValue = RoomStates[x, y].Light;
                    if (RoomStates[x, y].Direction == 0)
                        spriteBatch.Draw(Resources.SprLightRoomH, new Vector2(-80 + x * Values.FieldWidth, 0 + y * Values.FieldHeight), Color.Black * lightValue);
                    else if (RoomStates[x, y].Direction == 2)
                        spriteBatch.Draw(Resources.SprLightRoomH, new Vector2(-80 + x * Values.FieldWidth, 0 + y * Values.FieldHeight),
                            new Rectangle(0, 0, Resources.SprLightRoomH.Width, Resources.SprLightRoomH.Height), Color.Black * lightValue, 0, Vector2.Zero, 1, SpriteEffects.FlipHorizontally, 0);
                    else if (RoomStates[x, y].Direction == 1)
                        spriteBatch.Draw(Resources.SprLightRoomV, new Vector2(-80 + x * Values.FieldWidth, 0 + y * Values.FieldHeight),
                            new Rectangle(0, 0, Resources.SprLightRoomH.Width, Resources.SprLightRoomH.Height), Color.Black * lightValue, 0, Vector2.Zero, 1, SpriteEffects.FlipVertically, 0);
                    else
                        spriteBatch.Draw(Resources.SprLightRoomV, new Vector2(-80 + x * Values.FieldWidth, 0 + y * Values.FieldHeight), Color.Black * lightValue);
                }
            }
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, MapManager.LightBlendState, MapManager.Camera.Scale >= 1 ? SamplerState.PointWrap : SamplerState.AnisotropicWrap, null, null, null, MapManager.Camera.TransformMatrix);
        }
    }
}
