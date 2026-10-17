using System;
using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Base.Components
{
    public class MovingSolidComponent : Component
    {
        public new static int Index = 17;
        public static int Mask = 1 << Index;

        public CBox SolidBox;
        public Values.CollisionTypes PushTypes = Values.CollisionTypes.Normal;

        public bool CarriesRiders;
        public bool IsActive = true;

        public Box LastBox;
        public Vector2 Delta;
        public bool Initialized;

        public Action<BodyComponent, Vector2> OnBlocked;

        public MovingSolidComponent(CBox solidBox) => SolidBox = solidBox;
    }
}
