using System;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Enemies;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameSystems
{
    class MapShowSystem : GameSystem
    {
        private volatile bool _finishedLoading;
        private volatile bool _objectsLoadedInThread;
        private Thread _loadingThread;
        private Exception _loadingException;

        private const float ChangeTargetTime = 5350;
        private const float FadeTimeOut = 2500;
        private const float FadeTimeIn = 250;
        private const float FadeLock = 250;

        private float _counter;
        private bool _isActive;
        private bool _finished;
        private bool _init;
        private int _targetIndex;

        public Color TransitionColor = Color.White;

        private void ResetState()
        {
            _counter = 0;
            _targetIndex = 0;
            _isActive = false;
            _init = false;
            _finished = false;
            _finishedLoading = false;
            _objectsLoadedInThread = false;
            _loadingException = null;

            Camera.LockCamera = false;
        }

        public override void OnLoad()
        {
            if (_loadingThread != null && _loadingThread.IsAlive)
                _loadingThread.Join();

            ResetState();

            Game1.CameraObjects.ClearEndingCameraList();
        }

        public override void Update()
        {
            // Nothing to do unless the ending is currently playing.
            if (!_isActive)
                return;

            // The first stage. The screen is fading to white while the overworld loads
            // in the background. Wait here until both are done.
            if (!_init)
            {
                // Both the fade and the load have finished.
                if (_finishedLoading && _counter <= 0)
                {
                    // Start the countdown for the first field.
                    _counter = ChangeTargetTime;
                    _finishedLoading = false;
                    _init = true;

                    // Swap in the loaded map and point the camera at the first field.
                    FinishLoading();
                }
                // Still fading or still loading, so keep counting down and wait.
                else
                {
                    _counter -= Game1.DeltaTime;
                }
                return;
            }
            // Count down the time left on the current field.
            _counter -= Game1.DeltaTime;

            // The last field has already been shown and the screen has finished fading
            // back in on the ending map, so shut the system down and hand over control.
            if (_finished && _counter < ChangeTargetTime - FadeTimeIn - FadeLock)
            {
                _isActive = false;
                Camera.LockCamera = false;
                return;
            }

            // Time is up on the current field.
            if (_counter < 0)
            {
                // Top the countdown back up for whatever comes next. Adding rather than
                // assigning keeps any overshoot from this frame, so the fields do not drift.
                _counter += ChangeTargetTime;

                // Move to the next field in the list.
                _targetIndex++;

                // There is another field left, so jump the camera to it. The screen is
                // fully white right now, so the jump itself is never seen.
                if (_targetIndex < Game1.CameraObjects.EndingCameraList.Count)
                {
                    Game1.GameManager.MapManager.CurrentMap.CameraTarget = Game1.CameraObjects.EndingCameraList[_targetIndex].Position;
                    MapManager.Camera.ForceUpdate(Game1.GameManager.MapManager.GetCameraTarget());
                }
                // Every field has been shown, so wrap the whole thing up.
                else
                {
                    // Mark the show as over and give the camera back.
                    _finished = true;
                    Camera.LockCamera = false;

                    // Switch back to the ending map.
                    var oldMap = Game1.GameManager.MapManager.CurrentMap;
                    Game1.GameManager.MapManager.CurrentMap = Game1.GameManager.MapManager.NextMap;
                    Game1.GameManager.MapManager.NextMap = oldMap;

                    // Start the final sequence (ending sequence with Link in the ocean).
                    Game1.GameManager.InGameOverlay.StartSequence("final");
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            // Nothing to do unless the ending is currently playing.
            if (!_isActive)
                return;

            spriteBatch.Begin();

            var fadePercentage = 0f;

            // Slowly fade out the current camera target.
            if (_counter < FadeTimeOut + FadeLock)
                fadePercentage = 1 - MathF.Sin(MathHelper.Clamp((_counter - FadeLock) / FadeTimeOut, 0, 1) * MathF.PI * 0.5f);

            // Quickly fade back in to the next camera target.
            else if (ChangeTargetTime - FadeTimeIn - FadeLock < _counter)
                fadePercentage = MathF.Sin((1 - MathHelper.Clamp((ChangeTargetTime - FadeLock - _counter) / FadeTimeIn, 0, 1)) * MathF.PI * 0.5f);

            spriteBatch.Draw(Resources.SprWhite, new Rectangle(0, 0, Game1.RenderWidth, Game1.RenderHeight), TransitionColor * fadePercentage);
            spriteBatch.End();
        }

        public void StartEnding()
        {
            if (_isActive)
                return;

            ResetState();

            // Start fully faded out so the first camera target fades in.
            _counter = FadeLock + FadeTimeOut;
            _isActive = true;

            Camera.LockCamera = true;

            // Used to spawn different npcs on the overworld.
            Game1.GameManager.SaveManager.SetString("final_show", "1");
            Game1.GameManager.SaveManager.SetString("marin_state", "1");

            // Start loading the new map in a thread.
            _loadingThread = new Thread(o => ThreadLoading("overworld.map"));
            _loadingThread.IsBackground = true;
            _loadingThread.Start();
        }

        private void ThreadLoading(string mapFileName)
        {
            try
            {
                var nextMap = Game1.GameManager.MapManager?.NextMap;
                if (nextMap == null)
                    throw new NullReferenceException("NextMap was null inside ThreadLoading.");

                // Load the map file. This is always safe to do in a thread.
                SaveLoadMap.LoadMap(mapFileName, nextMap);
                nextMap.Objects ??= new ObjectManager(nextMap);

                // Some Android devices still crash when the objects are created in a thread, so
                // only do it here when the user has opted in. Otherwise it happens in FinishLoading.
                _objectsLoadedInThread = GameSettings.ThreadMapLoad;

                if (_objectsLoadedInThread)
                    SetUpMapObjects(nextMap.Objects);

                _loadingException = null;
            }
            catch (Exception ex)
            {
                _loadingException = ex;
                _objectsLoadedInThread = false;

                // If we crash, clear the camera list.
                Game1.CameraObjects.ClearEndingCameraList();
            }
            finally
            {
                // And signal that the load has finished.
                _finishedLoading = true;
            }
        }

        private void SetUpMapObjects(ObjectManager objects)
        {
            // Create the objects.
            objects.LoadObjects();

            // These damn birds are always causing me problems.
            var ravenList = objects.GetObjectsOfType(typeof(EnemyRaven));

            // Completely disable them from being able to do anything.
            foreach (EnemyRaven raven in ravenList)
                raven.IsActive = false;

            // Find all ending camera field objects on the overworld map.
            var cameraObjects = objects.GetObjectsOfType(typeof(ObjCameraFieldEnding));

            // Build the list we can reference which also sets the correct ordering for showing.
            Game1.CameraObjects.SetEndingCameraList(cameraObjects);
        }

        private void FinishLoading()
        {
            // If the loading thread failed, surface it here (main thread).
            if (_loadingException != null)
                System.Diagnostics.Debug.WriteLine($"MapShowSystem: ending map load failed - {_loadingException}");

            // When thread loading is disabled the objects still need to be created. Do it before the
            // map swap so NextMap still refers to the map being initialized.
            else if (!_objectsLoadedInThread)
                SetUpMapObjects(Game1.GameManager.MapManager.NextMap.Objects);

            // No camera targets means there is nothing to show. Abort before swapping
            // maps and go straight to the credits rather than throwing.
            if (Game1.CameraObjects.EndingCameraList.Count == 0)
            {
                _isActive = false;
                _finished = true;
                Camera.LockCamera = false;

                // Start the final sequence (ending sequence with Link in the ocean).
                Game1.GameManager.InGameOverlay.StartSequence("final");
                return;
            }

            // Switch to the new map.
            var oldMap = Game1.GameManager.MapManager.CurrentMap;
            Game1.GameManager.MapManager.CurrentMap = Game1.GameManager.MapManager.NextMap;
            Game1.GameManager.MapManager.NextMap = oldMap;

            // Center the camera on each field.
            Game1.GameManager.MapManager.CurrentMap.CameraTarget = Game1.CameraObjects.EndingCameraList[_targetIndex].Position;
            MapManager.Camera.ForceUpdate(Game1.GameManager.MapManager.GetCameraTarget());

            // Run the script which animates Tarin holding the mushroom and Marin singing.
            Game1.GameManager.StartDialogPath("final_show_map");
        }
    }
}
