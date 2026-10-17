using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base.CObjects;

namespace ProjectZ.InGame.GameObjects.Base.Components
{
    public class PushableComponent : Component
    {
        public new static int Index = 13;
        public static int Mask = 0x01 << Index;

        public enum PushType { Impact, Continues }

        public delegate bool PushableTemplate(Vector2 direction, PushType pushType);
        public PushableTemplate Push;

        public CBox PushableBox;

        public Vector2 LastPushDirection;

        public double LastPushTime;
        public double LastWaitTime;
        
        public float RepelMultiplier = 1.85f;
        public float InertiaCounter;
        
        public int InertiaTime = 0;
        public int CooldownTime = 250;

        public bool IsActive = true;
        public bool RunActivate;
        public bool RepelParticle;

        protected PushableComponent() { }

        public PushableComponent(CBox rectangle, PushableTemplate push)
        {
            PushableBox = rectangle;
            Push = push;
        }
    }
}
