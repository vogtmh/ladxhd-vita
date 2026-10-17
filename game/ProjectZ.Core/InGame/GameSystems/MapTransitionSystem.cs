using System;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameSystems
{
    internal class MapTransitionSystem : GameSystem
    {
        public int AdditionalBlackScreenDelay;
        public bool StartDreamTransition;
        public bool StartTeleportTransition;
        public bool StartKnockoutTransition;

        public enum TransitionState
        {
            Idle,
            TransitionIn,
            TransitionBlank_0,
            TransitionBlank_1,
            TransitionOut,
            ColorMode
        }

        public TransitionState CurrentState = TransitionState.Idle;
        public const int ChangeMapTime = 350;
        public const int BlackScreenDelay = 75; // 125

        private readonly MapManager _gameMapManager;
        private readonly ObjTransition _transitionObject;

        private Thread _loadingThread;

        private string _nextMapName;
        private string _nextMapPosition;
        private bool _nextMapCenter;
        private bool _nextMapStartInMiddle;
        private Color _nextMapColor;
        private bool _nextColorMode;

        private float _changeMapCount;

        // will be reset after each transition
        private Exception _loadingException;
        private volatile bool _finishedLoading;

        private bool _centerCamera;
        private bool _fullColorMode;
        private bool _knockoutTransition;
        private bool _transitionEnded;
        private bool _introTransition;

        private const int DreamTransitionTimeAddition = 3000;
        private const int TeleportTransitionTimeAddition = 2000 - ChangeMapTime;
        private int _wobbleTransitionTime;
        private bool _wobbleTransitionOut;
        private bool _wobbleTransitionIn;

        private volatile bool _objectsLoadedInThread;
        private bool _wasClassicEnabled;

        public MapTransitionSystem(MapManager gameMapManager)
        {
            _gameMapManager = gameMapManager;
            _transitionObject = new ObjTransition(_gameMapManager.CurrentMap);
        }

        public override void OnLoad()
        {
            _nextMapName = null;
        }

        public override void Update()
        {
            // start map change
            if (_nextMapName != null)
            {
                if (!string.IsNullOrEmpty(_nextMapPosition))
                    MapManager.ObjLink.SetNextMapPosition(_nextMapPosition);

                LoadMapFromFile(_nextMapName, _nextMapCenter, _nextMapStartInMiddle, _nextMapColor, _nextColorMode);
                _nextMapName = null;
            }

            if (_transitionEnded)
            {
                _transitionEnded = false;
                Game1.GameManager.SaveManager.SetString("transition_ended", "0");
            }

            if (CurrentState != TransitionState.Idle)
                Game1.GameManager.InGameOverlay.DisableOverlayToggle = true;

            // This runs while Link is walking out of the current map.
            if (CurrentState == TransitionState.TransitionOut)
            {
                _changeMapCount += Game1.DeltaTime;

                var transitionState = _changeMapCount / ChangeMapTime;
                var percentage = MathHelper.Clamp((float)(Math.Sin(transitionState * 1.1) / Math.Sin(1.1)), 0, 1);
                MapManager.ObjLink.UpdateMapTransitionOut(percentage);

                if (!_wobbleTransitionOut && !_knockoutTransition)
                    Game1.GameManager.DrawPlayerOnTopPercentage = percentage;

                // slowly lower the volume of the music
                var newVolume = 1 - MathHelper.Clamp(transitionState, 0, 1);
                Game1.AudioManager.SetMusicVolumeMultiplier(newVolume);

                if (_wobbleTransitionOut)
                {
                    // fade out to a white screen
                    var wobblePercentage = MathHelper.Clamp(_changeMapCount / ChangeMapTime, 0, 1);
                    _transitionObject.Brightness = wobblePercentage;
                    _transitionObject.WobblePercentage = (_wobbleTransitionTime + _changeMapCount) / (_wobbleTransitionTime + ChangeMapTime);
                }

                if (_changeMapCount >= ChangeMapTime)
                    CurrentState = TransitionState.TransitionBlank_0;
            }

            // This runs in between map loading and transitions to + catches the first frame of the new map.
            else if (CurrentState == TransitionState.TransitionBlank_0)
            {
                _changeMapCount += Game1.DeltaTime;

                MapManager.ObjLink.UpdateMapTransitionOut(1);

                // new map is loaded?
                if (_finishedLoading && _changeMapCount >= ChangeMapTime + BlackScreenDelay + AdditionalBlackScreenDelay)
                {
                    if (_loadingException != null)
                        throw new Exception("Map loading thread failed.", _loadingException);

                    FinishLoading();
                    CurrentState = TransitionState.TransitionBlank_1;

                    // If the map enables Classic Camera then snap the camera.
                    if (Camera.ClassicMode)
                        Camera.SnapCamera = true;

                    // If the map is classic mode or the previous map was classic mode.
                    if (Camera.ClassicMode || _wasClassicEnabled)
                    {
                        Game1.ScaleChanged = true;
                        _wasClassicEnabled = false;
                    }
                }
            }

            // This runs on the new map until it is fully loaded in.
            else if (CurrentState == TransitionState.TransitionBlank_1)
            {
                _changeMapCount += Game1.DeltaTime;

                MapManager.ObjLink.UpdateMapTransitionIn(0);

                if (_changeMapCount >= ChangeMapTime + BlackScreenDelay * 2 + AdditionalBlackScreenDelay)
                {
                    CurrentState = TransitionState.TransitionIn;

                    if (_wobbleTransitionIn)
                        _changeMapCount = _wobbleTransitionTime;
                    else
                    {
                        _transitionObject.WobbleTransition = false;
                        _changeMapCount = ChangeMapTime;
                    }
                }
            }

            // This runs while Link is walking into the new map.
            else if (CurrentState == TransitionState.TransitionIn)
            {
                _changeMapCount -= Game1.DeltaTime;

                // Update the position of the player to walk into the new room.
                var percentage = MathHelper.Clamp(_changeMapCount / ChangeMapTime, 0, 1);
                MapManager.ObjLink.UpdateMapTransitionIn(1 - (float)(Math.Sin(percentage * 1.1) / Math.Sin(1.1)));

                // Slowly increase the volume of the music; the music is only playing.
                var newVolume = 1 - percentage;
                if (Game1.AudioManager.GetMusicVolumeMultiplier() < 1)
                    Game1.AudioManager.SetMusicVolumeMultiplier(newVolume);

                if (!_wobbleTransitionOut && !_knockoutTransition && !_introTransition)
                    Game1.GameManager.DrawPlayerOnTopPercentage = percentage;

                if (_wobbleTransitionOut)
                {
                    // Fade out to a white screen.
                    var wobblePercentage = 1 - MathHelper.Clamp((_wobbleTransitionTime - _changeMapCount) / ChangeMapTime, 0, 1);
                    _transitionObject.Brightness = wobblePercentage;
                    _transitionObject.WobblePercentage = _changeMapCount / _wobbleTransitionTime;
                }
                // Light up the scene.
                if (_changeMapCount <= 0)
                {
                    _transitionEnded = true;
                    Game1.GameManager.SaveManager.SetString("transition_ended", "1");

                    CurrentState = TransitionState.Idle;
                    EndTransition();
                }
            }

            if (_knockoutTransition)
            {
                var percentage = MathHelper.Clamp((_changeMapCount - 100) / (ChangeMapTime - 100), 0, 1);
                _transitionObject.TransitionColor = _nextMapColor * percentage;
                _transitionObject.Percentage = 1;

                Game1.GameManager.MapManager.UpdateCameraX = false;
                Game1.GameManager.MapManager.UpdateCameraY = false;
            }

            if (_fullColorMode)
            {
                _transitionObject.Percentage = 1;
                _transitionObject.TransitionColor = _nextMapColor * TransitionPercentage();
            }
            else if (!_knockoutTransition)
            {
                _transitionObject.Percentage = (float)Math.Sin(TransitionPercentage() * Math.PI / 2);
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            // Don't draw while there isn't a transition.
            if (CurrentState == TransitionState.Idle)
                return;

            // Hide Link behind the circle shader effect if Classic Camera, Modern Camera + Only Overworld, or on the first load into the world.
            if (Camera.ClassicMode || GameSettings.CameraMode == 1 || MapManager.ObjLink.BlackScreenOverride)
                Game1.GameManager.DrawPlayerOnTopPercentage = 0;

            _transitionObject.Draw(spriteBatch);
        }

        public void SetColorMode(Color color, float colorTransparency, bool playerOnTop = true)
        {
            if (colorTransparency > 0)
                CurrentState = TransitionState.ColorMode;
            else
                CurrentState = TransitionState.Idle;

            _changeMapCount = ChangeMapTime;

            _transitionObject.TransitionColor = color * colorTransparency;

            if (playerOnTop)
                Game1.GameManager.DrawPlayerOnTopPercentage = colorTransparency;
        }

        public bool IsTransitioningOut()
        {
            return CurrentState == TransitionState.TransitionOut;
        }

        public bool IsTransitioningIn()
        {
            return CurrentState == TransitionState.TransitionIn;
        }

        private void StartTransition()
        {
            // Do some transition start stuff on ObjLink.
            MapManager.ObjLink.StartTransitioning();

            _introTransition = Game1.GameManager.SaveManager.GetString("played_intro", "0") != "1";

            if (!_introTransition)
                Game1.GameManager.DrawPlayerOnTopPercentage = 1.0f;

            // If classic camera is active.
            if (Camera.ClassicMode)
            {
                // Store that the current map was using Classic Camera to force a scale change later.
                _wasClassicEnabled = true;

                // Find the closest camera to lock onto while exiting the map.
                if (MapManager.ObjLink.CurrentState != ObjLink.State.OcarinaTeleport)
                    Game1.CameraObjects.FindClosestCoords();
            }
        }

        private void EndTransition()
        {
            _knockoutTransition = false;

            // Do some transition end stuff on ObjLink.
            MapManager.ObjLink.EndTransitioning();

            // Start playing the next song.
            Game1.AudioManager.SetMusicVolumeMultiplier(1);
            Game1.AudioManager.PlayMusic();

            // Stop snapping the camera.
            Camera.SnapCamera = false;

            // Do a soft update to Link's location.
            MapManager.Camera.SoftUpdate(Game1.GameManager.MapManager.GetCameraTarget());

            // Always clear the list of camera field objects if loading into the overworld.
            Game1.CameraObjects.ClearList();
        }

        public float TransitionPercentage()
        {
            return MathHelper.Clamp(_changeMapCount / ChangeMapTime, 0, 1);
        }

        public void AppendMapChange(string mapName, string position)
        {
            AppendMapChange(mapName, position, false, false, Values.MapTransitionColor, false);
        }

        public void AppendMapChange(string mapName, string position, bool centerCamera, bool startFromMiddle, Color transitionColor, bool colorMode)
        {
            _nextMapName = mapName;
            _nextMapPosition = position;
            _nextMapCenter = centerCamera;
            _nextMapStartInMiddle = startFromMiddle;
            _nextMapColor = transitionColor;
            _nextColorMode = colorMode;

            MapManager.ObjLink.OnAppendMapChange();
        }

        public void LoadMapFromFile(string mapFileName, bool centerCamera, bool startFromMiddle, Color transitionColor, bool colorMode)
        {
            // abort the old loading thread if it is still running
            // TODO: thread abort is not supported so it just waits till the loading is finished
            if (_loadingThread != null && _loadingThread.IsAlive)
                _loadingThread.Join();

            //Debug.Assert(CurrentState == TransitionState.Idle ||
            //             CurrentState == TransitionState.ColorMode, "Tried transition while not in idle");

            _finishedLoading = false;

            // only show the opening transition
            if (startFromMiddle)
            {
                _changeMapCount = ChangeMapTime;
                CurrentState = TransitionState.TransitionBlank_0;
            }
            else
            {
                _changeMapCount = 0;
                CurrentState = TransitionState.TransitionOut;
            }

            // center after loading
            _centerCamera = centerCamera;

            // add the transition object to the map
            _knockoutTransition = false;
            _fullColorMode = colorMode;
            _nextMapColor = transitionColor;

            _wobbleTransitionOut = false;
            _wobbleTransitionIn = false;

            _transitionObject.WobbleTransition = false;

            _transitionObject.TransitionColor = transitionColor;
            _transitionObject.Percentage = startFromMiddle ? 1 : 0;

            // Start the transition.
            StartTransition();

            if (StartDreamTransition)
            {
                StartDreamTransition = false;
                DreamTransition();
            }
            if (StartTeleportTransition)
            {
                StartTeleportTransition = false;
                TeleportTransition();
            }

            if (StartKnockoutTransition)
            {
                StartKnockoutTransition = false;
                KnockoutTransition();
            }
            // Start loading the new map in a thread.
            _loadingThread = new Thread(o => ThreadLoading(mapFileName));
            _loadingThread.Start();
        }

        public void DreamTransition()
        {
            _transitionObject.WobbleTransition = true;
            _fullColorMode = true;
            _wobbleTransitionOut = true;

            Game1.GameManager.DrawPlayerOnTopPercentage = 0.0f;
            _nextMapColor = Color.White;

            // take longer
            _changeMapCount = -DreamTransitionTimeAddition;
            _wobbleTransitionTime = DreamTransitionTimeAddition;
            AdditionalBlackScreenDelay = 500;
        }

        public void TeleportTransition()
        {
            _transitionObject.WobbleTransition = true;
            _fullColorMode = true;
            _wobbleTransitionOut = true;
            _wobbleTransitionIn = true;

            Game1.GameManager.DrawPlayerOnTopPercentage = 0.0f;
            _nextMapColor = Color.White;

            // take longer
            _changeMapCount = -TeleportTransitionTimeAddition;
            _wobbleTransitionTime = TeleportTransitionTimeAddition;
            AdditionalBlackScreenDelay = 500;
        }

        public void KnockoutTransition()
        {
            _knockoutTransition = true;

            Game1.GameManager.DrawPlayerOnTopPercentage = 0.0f;
            _nextMapColor = Color.White;

            // take longer
            AdditionalBlackScreenDelay = 750;
        }

        public void ResetTransition()
        {
            _transitionObject.WobbleTransition = false;
            _fullColorMode = false;
            _wobbleTransitionOut = false;
            _wobbleTransitionIn = false;
            AdditionalBlackScreenDelay = 0;
        }

        public void ThreadLoading(string mapFileName)
        {
            try
            {
                Thread.Sleep(75);

                var nextMap = _gameMapManager?.NextMap;
                if (nextMap == null)
                    throw new NullReferenceException("NextMap was null inside ThreadLoading.");

                SaveLoadMap.LoadMap(mapFileName, nextMap);
                nextMap.Objects ??= new ObjectManager(nextMap);

                // Some Android devices still crash when the next map is loaded in a thread. The user can now
                // toggle an option to thread load the next map, which is default "false" on Android.
                _objectsLoadedInThread = GameSettings.ThreadMapLoad;

                if (_objectsLoadedInThread)
                    nextMap.Objects.LoadObjects();

                _loadingException = null;
            }
            catch (Exception ex)
            {
                _loadingException = ex;
                _objectsLoadedInThread = false;
            }
            finally
            {
                _finishedLoading = true;
            }
        }

        private void SetUpMusic(MapManager mm, int stage)
        {
            // Game manager and the current map.
            var gm = Game1.GameManager;
            var map = mm.CurrentMap;

            // Before "Game1.GameManager.MapManager.FinishLoadingMap" && "MapManager.ObjLink.UpdateMapTransitionIn".
            if (stage == 1)
            {
                // Music handling
                var nextTrack = -1;
                for (var i = 0; i < map.MapMusic.Length; i++)
                    if (map.MapMusic[i] >= 0)
                        nextTrack = map.MapMusic[i];

                // Check if we're supposed to be playing the intro music.
                bool introMusic = gm.SaveManager.GetString("introMusic", "0") == "1";

                // Don't restart the music when in dungeons, in caves, if a powerup is active, or intro music is playing.
                bool restart1 = !introMusic && (map.IsOverworld || !map.IsDungeon && !map.IsCastle && !map.IsCave);
                bool restart2 = !MapManager.ObjLink.HasPowerup;
                bool restart3 = map.IsOverworld;

                // Conditionally clear the music from all slots.
                if (restart1)
                    Game1.AudioManager.SetMusic(-1, 0);
                if (restart2)
                    Game1.AudioManager.SetMusic(-1, 1);
                if (restart3)
                    Game1.AudioManager.SetMusic(-1, 2);

                // Stop whatever music was playing.
                if (restart1)
                {
                    Game1.AudioManager.StopMusic();
                    Game1.GbsPlayer.Pump();
                }
            }
            // After "Game1.GameManager.MapManager.FinishLoadingMap" && "MapManager.ObjLink.UpdateMapTransitionIn".
            if (stage == 2)
            {
                // Set the music to play. Music will start in "EndTransition".
                for (var i = 0; i < map.MapMusic.Length; i++)
                {
                    if (map.MapMusic[i] >= 0)
                    {
                        Game1.AudioManager.SetMusic(map.MapMusic[i], i, false);
                    }
                }
            }
        }

        public void FinishLoading()
        {
            AdditionalBlackScreenDelay = 0;

            // If the loading thread failed, surface it here (main thread).
            if (_loadingException != null)
                System.Diagnostics.Debug.WriteLine("Map loading thread failed.");
            
            // Shorter reference for the map manager.
            var mm = Game1.GameManager.MapManager;

            // Swap in the newly loaded map (mm.NextMap becomes the old map after this).
            var oldMap = mm.CurrentMap;
            mm.CurrentMap = mm.NextMap;
            mm.NextMap = oldMap;

            mm.CurrentMap.Objects ??= new ObjectManager(mm.CurrentMap);

            if (!_objectsLoadedInThread)
            {
                // Many init-time objects set flags on MapManager.NextMap during LoadObjects(),
                // expecting NextMap to refer to the map being initialized.
                // After swapping, that map is now CurrentMap, so temporarily point NextMap
                // at CurrentMap so those objects configure the correct map.
                var restoreNext = mm.NextMap;
                mm.NextMap = mm.CurrentMap;
                try
                {
                    mm.CurrentMap.Objects.LoadObjects();
                }
                finally
                {
                    mm.NextMap = restoreNext;
                }
            }
            // Getting the music to work correct with the map loading changes has been a nightmare. The
            // easiest place to handle if it restarts is here so give it a method to set it all up.
            SetUpMusic(mm, 1);

            // Finish loading map
            mm.FinishLoadingMap(mm.CurrentMap);

            // If classic camera is enabled then find the closest camera. Do not try to find a camera
            // field object during ocarina teleport or it will pan to the nearest one that is found.
            if (Camera.ClassicMode && MapManager.ObjLink.CurrentState != ObjLink.State.OcarinaTeleport)
                Game1.CameraObjects.FindClosestCoords();

            // Update the transition into the new map.
            MapManager.ObjLink.UpdateMapTransitionIn(0);

            // Finish setting up the music. It needed to be split across two stages.
            SetUpMusic(mm, 2);

            // Center the camera after the map is done loading.
            var goalPosition = mm.GetCameraTarget();
            if (_centerCamera)
                MapManager.Camera.ForceUpdate(goalPosition);
            else
                MapManager.Camera.SoftUpdate(goalPosition);
        }

    }
}
