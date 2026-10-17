using ProjectZ.InGame.GameObjects.Base.CObjects;

namespace ProjectZ.InGame.GameObjects.Base.Components
{
    class InteractComponent : Component
    {
        public new static int Index = 8;
        public static int Mask = 0x01 << Index;

        public bool IsActive = true;

        public delegate bool InteractTemplate();
        public InteractTemplate InteractFunction;

        public CBox BoxInteractable;

        public InteractComponent(CBox box, InteractTemplate interactFunction)
        {
            BoxInteractable = box;
            InteractFunction = interactFunction;
        }
    }
}
