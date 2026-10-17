using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Base.Components
{
    class DamageFieldComponent : Component
    {
        public new static int Index = 10;
        public static int Mask = 0x01 << Index;

        public delegate bool OnDamageTemplate();
        public OnDamageTemplate OnDamage;

        public delegate void OnDamagedPlayerTemplate();
        public OnDamagedPlayerTemplate OnDamagedPlayer;

        public CBox CollisionBox;
        public HitType HitType;

        public int Strength;
        public int Direction = -1;
        public double OverrideCooldown = 0;

        public bool IsActive = true;
        public bool DealtDamage;
        public bool Unblockable = false;

        public bool SingleAxisPush;
        public float PushMultiplier = 1.85f;
        public float DashPushMultiplier = 1.85f;

        public DamageFieldComponent(CBox collisionBox, HitType hitType, int damageStrength)
        {
            CollisionBox = collisionBox;
            HitType = hitType;
            Strength = damageStrength;
            OnDamage = DamagePlayer;
        }

        public bool DamagePlayer()
        {
            // Get the player character.
            ObjLink Link = MapManager.ObjLink;

            // Don't deal damage while falling down a hole.
            if (Link.HoleFalling == true)
                return false;

            // Running with boots may cause a further knockback than when walking.
            var pushMultiplier = Link.BootsRunning || Link.BootsWasRunning
                ? DashPushMultiplier
                : PushMultiplier; 

            // Run the damage function to see if damage was dealt.
            DealtDamage = Link.HitPlayer(CollisionBox.Box, HitType, Strength, pushMultiplier, Direction, OverrideCooldown, Unblockable, SingleAxisPush);

            // If damage was dealt, run the `OnDamagedPlayer` function.
            if (DealtDamage) 
            {
                if (OnDamagedPlayer != null)
                    OnDamagedPlayer();
            }
            // Return whether damage was dealt or not.
            return DealtDamage;
        }
    }
}
