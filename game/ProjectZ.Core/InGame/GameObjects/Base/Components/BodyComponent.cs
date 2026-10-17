using Microsoft.Xna.Framework;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Base.Components
{
    public class BodyComponent : Component
    {
        public delegate void MoveCollisionFunction(Values.BodyCollision collision);
        public delegate void HoleAbsorbFunction();
        public delegate void HoleOnPullFunction(Vector2 direction, float percentage);
        public delegate void DeepWaterFunction();

        public MoveCollisionFunction MoveCollision;
        public HoleAbsorbFunction HoleAbsorb;
        public HoleOnPullFunction HoleOnPull;
        public DeepWaterFunction OnDeepWaterFunction;

        public CBox BodyBox;
        public CPosition Position;

        public Vector3 Velocity;
        public Vector2 VelocityTarget;
        public Vector2 LastVelocityTarget;
        public Vector2 SlideOffset;
        public Vector2 HoleAbsorption;

        // used for the rolling bands
        // could probably be done in a better way (interface?)
        public Vector2 AdditionalMovementVT;
        public Vector2 LastAdditionalMovementVT;

        public RectangleF FieldRectangle = RectangleF.Empty;

        public MapStates.FieldStates CurrentFieldState = MapStates.FieldStates.Init;
        public Values.CollisionTypes CollisionTypes = Values.CollisionTypes.Normal;
        public Values.CollisionTypes CollisionTypesIgnore;
        public Values.CollisionTypes AvoidTypes;

        public Values.BodyCollision VelocityCollision;
        public Values.BodyCollision LastVelocityCollision;

        // Pushes a body to the side when colliding with a corner within a certain
        // pixel range. Used exclusively on Link but could be added to other "bodies".
        public bool CornerCorrection = false;
        public float CornerCorrectionThreshold = 2f;

        // Allows walking up "pixel stairs" that are 1-pixel high. This used to be used in 2D underground
        // sections, but goes largely unused due to useage of 1-way colliders and their "push up" mechanic.
        public bool EnableStepUp;
        public int MaxStepHeight = 1;

        public float JumpStartHeight;
        public float MaxJumpHeight = 4;
        public float Drag = 0.8f;
        public float DragAir = 0.9f;
        public float DragWater = 0.95f;
        public float Gravity = -0.25f;
        public float Gravity2D = 0.1f;
        public float Gravity2DWater = 0.025f;
        public float Bounciness = 0;
        public float Bounciness2D = 0;
        public float SpeedMultiply = 1;
        public float AbsorbPercentage = 0.85f;
        public float AbsorbStop = 0.15f;
        public float MaxSlideDistance = 6.0f;
        public float InsideCollisionEscape = 0.0f;

        public float Width
        {
            get => BodyBox.Box.Width;
            set => BodyBox.Box.Width = value;
        }
        public float Height
        {
            get => BodyBox.Box.Height;
            set => BodyBox.Box.Height = value;
        }
        public float Depth
        {
            get => BodyBox.Box.Depth;
            set => BodyBox.Box.Depth = value;
        }
        public float OffsetX
        {
            get => BodyBox.OffsetX;
            set => BodyBox.OffsetX = value;
        }
        public float OffsetY
        {
            get => BodyBox.OffsetY;
            set => BodyBox.OffsetY = value;
        }

        public int DeepWaterOffset = -3;

        public int Level = 0;
        
        public bool SimpleMovement = false;             // Used to make the X/Y movement happen in one step.
        public bool IgnoreCollision = false;            // Outright ignore all collision.
        public bool IgnoreInsideCollision = true;       // If already inside a collider, ignore remaining collision.

        public bool IsActive = true;                    // When "true" components are fully active and Update loops are ran.
        public bool IsGrounded = true;                  // Object attached to the body is on the ground this frame.
        public bool WasGrounded = true;                 // Object attached to the body was on the ground last frame.
        public bool IgnoresZ;                           // Ignores the Z-plane physics entirely (such as depth checks).
        public bool IgnoreHoles;                        // Ignores hole collision entirely; can not be pulled/absorbed.
        public bool IsPusher;                           // Can push objects with a "PushableComponent". Only "ObjLink" has this set to "true".
        public bool IgnoreHeight;                       // Skips the floor-height lookup in "UpdateVelocityZ", all height is treated as "0".
        public bool IsSlider;                           // When a move is blocked, bank a perpendicular offset to slip around the obstacle next frame. Used on Raft and Mimics.
        public bool IsAbsorbed;                         // The body was completely absorbed by a hole.
        public bool WasHolePulled;                      // Was under the influence of a hole on the last frame.
        public bool DisableVelocityTargetMultiplier;    // Allows controller input velocity to counteract forced velocities (ex: EnemyVacuum). Consumed each frame.
        public bool RestAdditionalMovement = true;      // Controls whether "AdditionalMovementVT" is consumed each frame.
        public bool SplashEffect = true;                // Show the splash graphics effect when entering water.
        public bool UpdateFieldState = true;            // Resample terrain under the body each frame. If false, "CurrentFieldState" is externally owned.
        public bool FieldLocked = true;                 // When Classic Camera is enabled, locks the object owning the body to the field that it is currently on.

        public new static int Index = 2;
        public static int Mask = 0x01 << Index;

        public BodyComponent(CPosition position, int offsetX, int offsetY, int width, int height, int depth)
        {
            Position = position;
            BodyBox = new CBox(position, offsetX, offsetY, width, height, depth);
        }
    }
}
