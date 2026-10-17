using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Things;

namespace ProjectZ.InGame.Map
{
    public class CameraField
    {
        public List<ObjCameraField> CameraFieldList = new List<ObjCameraField>();
        public List<ObjCameraFieldEnding> EndingCameraList = new List<ObjCameraFieldEnding>();
        public Vector2 CameraFieldCoords;

        public void FindClosestCoords()
        {
            // Always reset. If we fail to find anything, coords become Zero (not stale).
            CameraFieldList.Clear();
            CameraFieldCoords = Vector2.Zero;

            // Make sure a map exists before trying to find camera objects.
            var map = MapManager.ObjLink?.Map;
            var objects = map?.Objects;
            if (objects == null)
                return;

            // Get Link's position to determine the closest camera.
            var playerPos = MapManager.ObjLink.CenterPosition;
            var objList = objects.GetObjects((int)playerPos.X - 160, (int)playerPos.Y - 100, 320, 200);

            // Loop through the game objects.
            foreach (var gameObject in objList)
                if (gameObject is ObjCameraField camField)
                    CameraFieldList.Add(camField);

            // If the list is empty, there is no camera coords to obtain.
            if (CameraFieldList.Count <= 0)
                return;

            // Holds the closest camera to Link.
            ObjCameraField closestCam = null;
            var closestDist = float.MaxValue;

            // Loop through all the camera objects found.
            foreach (ObjCameraField fieldCenter in CameraFieldList)
            {
                // If there are multiple camera objects, find the closest camera to Link.
                float dist = Vector2.Distance(playerPos, fieldCenter.EntityPosition.Position);

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestCam = fieldCenter;
                }
            }
            // If the closest camera is null, there is no camera coords to obtain.
            if (closestCam == null)
                return;

            // Return the coordinates of the closest camera.
            CameraFieldCoords = new Vector2(closestCam.EntityPosition.X, closestCam.EntityPosition.Y);
        }

        public void ClearList()
        {
            // Clear the camera field object list and clear properties.
            CameraFieldList.Clear();
            CameraFieldCoords = Vector2.Zero;
        }

        public void SetEndingCameraList(IEnumerable endingCameras)
        {
            // Always reset. If we fail to find anything, the list is empty instead of stale.
            EndingCameraList.Clear();

            // This shouldn't happen but if it does, return early.
            if (endingCameras == null)
                return;

            // Loop through the game objects making sure they are the correct object type.
            foreach (var gameObject in endingCameras)
                if (gameObject is ObjCameraFieldEnding endingCamera)
                    EndingCameraList.Add(endingCamera);

            // Order the ending cameras by their assigned index when placed in the editor.
            EndingCameraList.Sort((a, b) => a.Index.CompareTo(b.Index));
        }

        public void ClearEndingCameraList()
        {
            EndingCameraList.Clear();
        }
    }
}
