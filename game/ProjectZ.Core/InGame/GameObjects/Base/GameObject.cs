using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;
using System.Diagnostics;

namespace ProjectZ.InGame.GameObjects.Base
{
    public interface IHasVisibility
    {
        // Implemented in some objects that have visibility states (Green Zol, Great Fairy).
        // Used to track whether or not to show sprite shadows for the object.
        bool IsVisible { get; }
    }

    public interface IHasSpriteVisibility
    {
        // Implemented in objects where the sprite can be made invisible.
        CSprite Sprite { get; }
    }

    public class GameObject
    {
        // editor stuff
        public Texture2D SprEditorImage;
        public Rectangle EditorIconSource;
        public Color EditorColor = new Color(255, 255, 255) * 0.65f;
        public float EditorIconScale = 1.0f;

        public Map.Map Map;

        public Values.GameObjectTag Tags;

        // entity component system stuff
        public CPosition EntityPosition;
        public CPosition ResetPosition;

        // Components and their indexes:
        //  0: AiComponent
        //  1: BaseAnimationComponent
        //  2: BodyComponent
        //  3: CarriableComponent
        //  4: CollisionComponent
        //  5: DrawComponent
        //  6: DrawShadowComponent
        //  7: HittableComponent
        //  8: InteractComponent
        //  9: LightDrawComponent
        // 10: DamageFieldComponent
        // 11: ObjectCollisionComponent
        // 12: UpdateComponent
        // 13: PushableComponent
        // 14: KeyChangeListenerComponent
        // 15: OcarinaListenerComponent
        // 16: BlurDrawComponent
        // 17: MovingSolidComponent

        public Component[] Components = new Component[32];   // TODO_End: replace with actual component count
                                                             // should have probably used an enum...
        public Rectangle EntitySize = new Rectangle(-16, -16, 48, 48);
        public Point EntityPoolPosition;

        public int ComponentsMask;

        public virtual bool IsActive { get; set; } = true;
        public bool IsDead;
        public bool FreezePersist;     // Keeps objects moving when objects are frozen (owl sequence for example).
        public bool ShieldDeflect;     // Handles shield deflection by opting out of shield push functionality.
        public bool BowWowImmune;      // Objects that can not be eaten by BowWow.

        // Modern Camera: Animate enemies in fog not belonging to fields
        // they may have been knocked into that is currently under fog.
        public Point HomeFogNode = new Point(int.MinValue, int.MinValue);

        // Classic Camera: Reset enemy positions.
        public delegate void OnResetDelegate();
        public OnResetDelegate OnReset;
        public bool CanReset = false;

        // Modern/Classic Camera: Animate tiles inside fog or during "Classic Scroll".
        public bool AnimatedTile;

        public GameObject() { }

        // constructor used for the editor objects
        public GameObject(string spriteId)
        {
            var sprite = Resources.GetSprite(spriteId);

            SprEditorImage = sprite.Texture;
            EditorIconSource = sprite.ScaledRectangle;
            EditorIconScale = sprite.Scale;
        }

        public GameObject(Map.Map map, string spriteId) : this(spriteId)
        {
            Map = map;
        }

        public GameObject(Map.Map map)
        {
            Map = map;
        }

        public virtual void Init() { }

        public virtual void DrawEditor(SpriteBatch spriteBatch, Vector2 position)
        {
            if (SprEditorImage != null)
                spriteBatch.Draw(SprEditorImage, position, EditorIconSource, EditorColor,
                    0, Vector2.Zero, new Vector2(EditorIconScale), SpriteEffects.None, 0);
        }

        public virtual void Reset()
        {
            if (CanReset) { OnReset?.Invoke(); }
        }

        public void ResetToSpawn()
        {
            if (ResetPosition == null || EntityPosition == null)
                return;

            EntityPosition.Set(ResetPosition);
        }

        public void AddComponent(int index, Component newComponent)
        {
            ComponentsMask |= (0x01 << index);
            Components[index] = newComponent;
            newComponent.Owner = this;

#if DEBUG
            if (EntityPosition == null)
                return;

            if (newComponent is CarriableComponent)
            {
                var carriableComponent = (CarriableComponent)newComponent;
                var rectangle = carriableComponent.Rectangle.Rectangle;
                Debug.Assert(EntityPosition.X + EntitySize.X <= rectangle.X);
                Debug.Assert(EntityPosition.Y + EntitySize.Y <= rectangle.Y);
                Debug.Assert(rectangle.Right <= EntityPosition.X + EntitySize.X + EntitySize.Width);
                Debug.Assert(rectangle.Bottom <= EntityPosition.Y + EntitySize.Y + EntitySize.Height);
            }
            if (newComponent is ObjectCollisionComponent)
            {
                var objectCollider = (ObjectCollisionComponent)newComponent;
                var rectangle = objectCollider.CollisionRectangle.Rectangle;
                Debug.Assert(EntityPosition.X + EntitySize.X <= rectangle.X);
                Debug.Assert(EntityPosition.Y + EntitySize.Y <= rectangle.Y);
                Debug.Assert(rectangle.Right <= EntityPosition.X + EntitySize.X + EntitySize.Width);
                Debug.Assert(rectangle.Bottom <= EntityPosition.Y + EntitySize.Y + EntitySize.Height);
            }
            if (newComponent is BodyCollisionComponent)
            {
                var bodyComponent = (BodyCollisionComponent)newComponent;
                var bodyBox = bodyComponent.Body.BodyBox.Box;
                Debug.Assert(EntityPosition.X + EntitySize.X <= bodyBox.X);
                Debug.Assert(EntityPosition.Y + EntitySize.Y <= bodyBox.Y);
                Debug.Assert(bodyBox.Right <= EntityPosition.X + EntitySize.X + EntitySize.Width);
                Debug.Assert(bodyBox.Front <= EntityPosition.Y + EntitySize.Y + EntitySize.Height);
            }
            if (newComponent is BoxCollisionComponent)
            {
                var boxCollisionComponent = (BoxCollisionComponent)newComponent;
                var box = boxCollisionComponent.CollisionBox.Box;
                Debug.Assert(EntityPosition.X + EntitySize.X <= box.X);
                Debug.Assert(EntityPosition.Y + EntitySize.Y <= box.Y);
                Debug.Assert(box.Right <= EntityPosition.X + EntitySize.X + EntitySize.Width);
                Debug.Assert(box.Front <= EntityPosition.Y + EntitySize.Y + EntitySize.Height);
            }
            if (newComponent is DamageFieldComponent)
            {
                var damageComponent = (DamageFieldComponent)newComponent;
                var box = damageComponent.CollisionBox.Box;
                Debug.Assert(EntityPosition.X + EntitySize.X <= box.X);
                Debug.Assert(EntityPosition.Y + EntitySize.Y <= box.Y);
                Debug.Assert(box.Right <= EntityPosition.X + EntitySize.X + EntitySize.Width);
                Debug.Assert(box.Front <= EntityPosition.Y + EntitySize.Y + EntitySize.Height);
            }
            //if (newComponent is HittableComponent)
            //{
            //    var hittableComponent = (HittableComponent)newComponent;
            //    var box = hittableComponent.HittableBox.Box;
            //    Debug.Assert(EntityPosition.X + EntitySize.X <= box.X);
            //    Debug.Assert(EntityPosition.Y + EntitySize.Y <= box.Y);
            //    Debug.Assert(box.Right <= EntityPosition.X + EntitySize.X + EntitySize.Width);
            //    Debug.Assert(box.Front <= EntityPosition.Y + EntitySize.Y + EntitySize.Height);
            //}
            if (newComponent is InteractComponent)
            {
                var interactionComponent = (InteractComponent)newComponent;
                var box = interactionComponent.BoxInteractable.Box;
                Debug.Assert(EntityPosition.X + EntitySize.X <= box.X);
                Debug.Assert(EntityPosition.Y + EntitySize.Y <= box.Y);
                Debug.Assert(box.Right <= EntityPosition.X + EntitySize.X + EntitySize.Width);
                Debug.Assert(box.Front <= EntityPosition.Y + EntitySize.Y + EntitySize.Height);
            }
            if (newComponent is PushableComponent)
            {
                var pushableComponent = (PushableComponent)newComponent;
                var box = pushableComponent.PushableBox.Box;
                Debug.Assert(EntityPosition.X + EntitySize.X <= box.X);
                Debug.Assert(EntityPosition.Y + EntitySize.Y <= box.Y);
                Debug.Assert(box.Right <= EntityPosition.X + EntitySize.X + EntitySize.Width);
                Debug.Assert(box.Front <= EntityPosition.Y + EntitySize.Y + EntitySize.Height);
            }
#endif
        }

        public void RemoveComponent(int index)
        {
            ComponentsMask &= (ComponentsMask ^ (0x01 << index));
            Components[index].Owner = null;
            Components[index] = null;
        }
    }
}
