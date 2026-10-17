using System;

namespace ProjectZ.InGame.Things
{
    public partial class Values
    {
        [Flags]
        public enum CollisionTypes
        {
            None              = 0,
            Normal            = 1 << 0,
            Hole              = 1 << 1,
            PlayerItem        = 1 << 2,
            Player            = 1 << 3,
            Enemy             = 1 << 4,
            Ladder            = 1 << 5,
            LadderTop         = 1 << 6,
            NPCWall           = 1 << 7,
            Item              = 1 << 8,
            DrownExclude      = 1 << 9,
            Hookshot          = 1 << 10,
            DeepWater         = 1 << 11,
            MovingPlatform    = 1 << 12,
            RaftExit          = 1 << 13,
            PushIgnore        = 1 << 14,
            Destroyable       = 1 << 15,
            ThrowIgnore       = 1 << 16,
            ThrowWeaponIgnore = 1 << 17,
            NPC               = 1 << 18,
            NonWater          = 1 << 19,
            Field             = 1 << 20,
            Passageway        = 1 << 21,
            Instrument        = 1 << 22,
            Bush              = 1 << 23,
            Switch            = 1 << 24,
            Barrier           = 1 << 25
        }

        [Flags]
        public enum BodyCollision
        {
            None        = 0,
            Floor       = 1,
            Left        = 2,
            Right       = 4,
            Top         = 8,
            Bottom      = 16,
            Horizontal  = 32,
            Vertical    = 64
        }

        [Flags]
        public enum HitCollision
        {
            None,
            Enemy = 1,
            Blocking = 2,
            NoneBlocking = 4,
            Particle = 8,
            Repelling = 16,
            RepellingParticle = 24,
            Repelling0 = 32,
            Repelling1 = 64,
            Repelling2 = 128,
            SpawnFire = 256,
        }

        [Flags]
        public enum GameObjectTag
        {
            None    = 0,
            Enemy   = 1,
            Trap    = 2,
            Damage  = 4,
            Hole    = 8,
            Lamp    = 16,
            Ocarina = 32,
            Utility = 64,
            Item    = 128
        }
    }
}
