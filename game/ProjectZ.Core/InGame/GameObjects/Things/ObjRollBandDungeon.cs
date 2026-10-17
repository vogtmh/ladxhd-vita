using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Enemies;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Things
{
    class ObjRollBandDungeon : GameObject
    {
        private readonly List<GameObject> _collidingObjects = new List<GameObject>();

        private readonly Box _collisionBox;
        private readonly Rectangle _sourceRectangle;
        private readonly Vector2 _vecRollBand;
        private readonly Vector2 _drawPosition;

        private readonly float _direction;
        private float _animationCount;
        private float _animationSpeed = 60f / 5;

        public ObjRollBandDungeon() : base("rollband_1") { }

        public ObjRollBandDungeon(Map.Map map, int posX, int posY, int direction) : base(map)
        {
            _sourceRectangle = Resources.SourceRectangle("rollband_1");

            EntityPosition = new CPosition(posX, posY, 0);
            EntitySize = new Rectangle(0, 0, 16, 16);

            _drawPosition = new Vector2(posX + 8, posY + 8);

            _direction = (float)(direction * Math.PI / 2f);

            _vecRollBand = AnimationHelper.DirectionOffset[direction] * 1 / 5;

            var marginX = direction == 0 || direction == 2 ? 0 : 3;
            var marginY = direction == 0 || direction == 2 ? 3 : 0;
            _collisionBox = new Box(posX + marginX, posY + marginY, 0, 16 - marginX * 2, 16 - marginY * 2, 8);

            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerBottom, EntityPosition));
        }

        private void Update()
        {
            _animationCount = (int)(Game1.TotalGameTime / 1000 * _animationSpeed) % 16;

            // get and move the components colliding with the rollband
            _collidingObjects.Clear();
            Map.Objects.GetComponentList(_collidingObjects,
                (int)_collisionBox.Left, (int)_collisionBox.Back, (int)_collisionBox.Width, (int)_collisionBox.Height, BodyComponent.Mask);

            foreach (var gameObject in _collidingObjects)
            {
                // Pairodds or Keese should not be pushed by conveyor belts.
                if (gameObject is EnemyPairodd) { continue; }
                if (gameObject is EnemyKeese) { continue; }

                var gameObjectBody = ((BodyComponent)gameObject.Components[BodyComponent.Index]);
                if (gameObjectBody.IsActive && gameObjectBody.IsGrounded && _collisionBox.Intersects(gameObjectBody.BodyBox.Box))
                {
                    var lastCollision = gameObjectBody.LastVelocityCollision;

                    // Did the body collide along this belt's axis of travel, or across it?
                    var collidedAlong = _vecRollBand.X != 0
                        ? (lastCollision & Values.BodyCollision.Horizontal) != 0
                        : (lastCollision & Values.BodyCollision.Vertical) != 0;

                    var collidedAcross = _vecRollBand.X != 0
                        ? (lastCollision & Values.BodyCollision.Vertical) != 0
                        : (lastCollision & Values.BodyCollision.Horizontal) != 0;

                    var ownedByThisBelt = gameObjectBody.LastAdditionalMovementVT == _vecRollBand;
                    var onNoBelt = gameObjectBody.LastAdditionalMovementVT == Vector2.Zero;

                    // Claim an unclaimed body; keep driving one already moving freely along this
                    // belt; take over one that another belt has jammed against a wall sideways.
                    if (onNoBelt || (ownedByThisBelt && !collidedAlong) || (!ownedByThisBelt && collidedAcross))
                        gameObjectBody.AdditionalMovementVT = _vecRollBand;
                }
            }
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(Resources.SprObjects, _drawPosition,
                new Rectangle(_sourceRectangle.X + (int)_animationCount % 16, _sourceRectangle.Y, 16, _sourceRectangle.Height),
                Color.White, _direction, new Vector2(8, 8), Vector2.One, (_direction % 2) != 0 ? SpriteEffects.FlipVertically : SpriteEffects.None, 0);
        }
    }
}
