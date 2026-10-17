using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Pools;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Base.Systems
{
    internal class SystemSolids
    {
        public ComponentPool Pool;
        private readonly List<GameObject> _solidList = new List<GameObject>();
        private readonly List<GameObject> _pushList = new List<GameObject>();

        public bool GameWorldFrozen;

        public void Update()
        {
            if (MapManager.Camera.FieldScrolling)
                return;

            if (Game1.TimeMultiplier <= 0) { return; }

            _solidList.Clear();

            // Classic Camera: Only update objects within the current field.
            if (Camera.ClassicMode && !Camera.LockCamera)
            {
                Pool.GetComponentList(_solidList, ObjectManager.UpdateField.X, ObjectManager.UpdateField.Y,
                    ObjectManager.UpdateField.Width, ObjectManager.UpdateField.Height, MovingSolidComponent.Mask);
                ObjectManager.FilterObjectsInField(_solidList, ObjectManager.ActualField);
            }
            else
            {
                Pool.GetComponentList(_solidList, ObjectManager.ViewportX, ObjectManager.ViewportY,
                    ObjectManager.ViewportW, ObjectManager.ViewportH, MovingSolidComponent.Mask);
            }

            for (int i = 0; i < _solidList.Count; i++)
            {
                var gameObject = _solidList[i];

                // During a freeze, only objects flagged FreezePersist keep updating.
                bool skipObject = !gameObject.IsActive || (GameWorldFrozen && !gameObject.FreezePersist);

                if (skipObject || gameObject.Components[MovingSolidComponent.Index] is not MovingSolidComponent solid || !solid.IsActive)
                {
                    if (gameObject.Components[MovingSolidComponent.Index] is MovingSolidComponent s)
                        s.Initialized = false;
                    continue;
                }
                ResolveSolid(gameObject, solid);
            }
        }

        private void ResolveSolid(GameObject owner, MovingSolidComponent solid)
        {
            var oldBox = solid.LastBox;
            var newBox = solid.SolidBox.Box;

            // First frame after spawn: seed the box, never push. Otherwise a stone
            // spawning on top of Link launches him across the room.
            if (!solid.Initialized)
            {
                solid.LastBox = newBox;
                solid.Initialized = true;
                return;
            }

            solid.Delta = new Vector2(newBox.X - oldBox.X, newBox.Y - oldBox.Y);
            solid.LastBox = newBox;

            if (solid.Delta == Vector2.Zero)
                return;

            // Sub-step so a fast solid cannot skip straight over a body.
            const float maxStep = 6f;
            var steps = Math.Max(1, (int)MathF.Ceiling(solid.Delta.Length() / maxStep));
            var stepDelta = solid.Delta / steps;

            var prevBox = oldBox;
            for (int s = 1; s <= steps; s++)
            {
                var stepBox = LerpBox(oldBox, newBox, s / (float)steps);
                PushBodies(owner, solid, prevBox, stepBox, stepDelta);
                prevBox = stepBox;
            }
        }

        private void PushBodies(GameObject owner, MovingSolidComponent solid, Box fromBox, Box toBox, Vector2 delta)
        {
            // Query the swept region, inflated by a body's worth of margin.
            var sweep = Union(fromBox, toBox);

            _pushList.Clear();
            Pool.GetComponentList(_pushList,
                (int)sweep.X - 16, (int)sweep.Y - 16,
                (int)sweep.Width + 32, (int)sweep.Height + 32, BodyComponent.Mask);

            var horizontal = Math.Abs(delta.X) >= Math.Abs(delta.Y);

            const float epsilon = 0.05f;
            const float minPenetration = 0.25f;

            for (int i = 0; i < _pushList.Count; i++)
            {
                var target = _pushList[i];
                if (target == owner || !target.IsActive) { continue; }
                if (target.Components[BodyComponent.Index] is not BodyComponent body) { continue; }
                if (!body.IsActive || body.IgnoreCollision) { continue; }
                if ((body.CollisionTypes & solid.PushTypes) == 0) { continue; }

                var bodyBox = body.BodyBox.Box;

                // Z separation: the solid passes under or over this body.
                if (toBox.Z + toBox.Depth <= bodyBox.Z || bodyBox.Z + bodyBox.Depth <= toBox.Z)
                    continue;

                if (!toBox.Intersects(bodyBox)) { continue; }

                if (horizontal)
                {
                    var push = delta.X > 0
                        ? toBox.Right - bodyBox.X + epsilon
                        : toBox.X - bodyBox.Right - epsilon;

                    if (Math.Sign(push) != Math.Sign(delta.X)) { continue; }

                    NotifySolidCollision(body, Values.BodyCollision.Horizontal |
                        (delta.X > 0 ? Values.BodyCollision.Left : Values.BodyCollision.Right));

                    if (Math.Abs(push) - epsilon < minPenetration) { continue; }

                    if (!TryPush(body, new Vector2(push, 0), delta.X < 0 ? 0 : 2))
                        solid.OnBlocked?.Invoke(body, new Vector2(push, 0));
                }
                else
                {
                    var push = delta.Y > 0
                        ? toBox.Front - bodyBox.Y + epsilon
                        : toBox.Y - bodyBox.Front - epsilon;

                    if (Math.Sign(push) != Math.Sign(delta.Y)) { continue; }

                    NotifySolidCollision(body, Values.BodyCollision.Vertical |
                        (delta.Y > 0 ? Values.BodyCollision.Top : Values.BodyCollision.Bottom));

                    if (Math.Abs(push) - epsilon < minPenetration) { continue; }

                    if (!TryPush(body, new Vector2(0, push), delta.Y < 0 ? 1 : 3))
                        solid.OnBlocked?.Invoke(body, new Vector2(0, push));
                }
            }
        }

        private static void NotifySolidCollision(BodyComponent body, Values.BodyCollision collision)
        {
            body.MoveCollision?.Invoke(collision);
        }

        // Returns true only if the body was pushed fully clear.
        private static bool TryPush(BodyComponent body, Vector2 push, int direction)
        {
            var testBox = Box.Empty;

            for (int i = 4; i >= 1; i--)
            {
                var step = push * (i / 4f);

                if (!SystemBody.Collision(body, body.Position.X + step.X, body.Position.Y + step.Y,
                        direction, body.CollisionTypes, false, ref testBox))
                {
                    body.Position.X += step.X;
                    body.Position.Y += step.Y;
                    body.Position.NotifyListeners();
                    return i == 4;
                }
            }
            return false;
        }

        private static Box LerpBox(Box a, Box b, float t) => new Box(
            MathHelper.Lerp(a.X, b.X, t), MathHelper.Lerp(a.Y, b.Y, t), b.Z,
            b.Width, b.Height, b.Depth);

        private static Box Union(Box a, Box b)
        {
            var x = Math.Min(a.X, b.X);
            var y = Math.Min(a.Y, b.Y);
            var z = Math.Min(a.Z, b.Z);

            return new Box(x, y, z,
                Math.Max(a.Right, b.Right) - x,
                Math.Max(a.Front, b.Front) - y,
                Math.Max(a.Z + a.Depth, b.Z + b.Depth) - z);
        }

    }
}
