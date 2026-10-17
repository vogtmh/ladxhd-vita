using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Things;
using ProjectZ.InGame.Map;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyPokey : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly DictAtlasEntry _spriteHead;
        private readonly DictAtlasEntry _spriteBody;
        private readonly HittableComponent _hitComponent;
        private readonly PushableComponent _pushComponent;

        private float _moveSpeed = 1 / 3f;
        private int _direction;
        private int _state;
        private int _lives = EnemyLives.Pokey;
        private int _dropIndex = 2;

        public EnemyPokey() : base("pokey") { }

        public EnemyPokey(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-10, -48, 20, 48);
            CanReset = true;
            OnReset = Reset;

            _spriteHead = Resources.GetSprite("pokey");
            _spriteBody = Resources.GetSprite("pokey body");

            _sprite = new CSprite("pokey body", EntityPosition);
            _body = new BodyComponent(EntityPosition, -7, -14, 14, 14, 8)
            {
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.Enemy,
                AvoidTypes = Values.CollisionTypes.Hole | Values.CollisionTypes.NPCWall,
                FieldRectangle = map.GetField(posX, posY),
                AbsorbPercentage = 0.75f,
                Gravity = -0.15f,
                Bounciness = 0.35f,
                Drag = 0.8f,
                DragAir = 0.8f,
                MaxJumpHeight = 4f,
                IgnoreHeight = true
            };

            var stateMoving = new AiState { Init = InitWalking };
            stateMoving.Trigger.Add(new AiTriggerRandomTime(ChangeDirection, 550, 850));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("moving", stateMoving);
            new AiFallState(_aiComponent, _body, null);
            _damageState = new AiDamageState(this, _body, _aiComponent, _sprite, _lives, _dropIndex) { OnBurn = OnBurn };

            _aiComponent.ChangeState("moving");

            var damageBox   = new CBox(EntityPosition, -3,  -8, 0,  6,  6, 16);
            var hittableBox = new CBox(EntityPosition, -7, -14, 0, 14, 14, 24);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(hittableBox, OnHit) { BoomerangMultiplier = true, MagicRodMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(PushableComponent.Index, _pushComponent = new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(DrawComponent.Index, new DrawComponent(Draw, Values.LayerPlayer, EntityPosition));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, _sprite) { ShadowWidth = 10, ShadowHeight = 5 });
        }

        public override void Reset()
        {
            _state = 0;
            _sprite.SetSprite(_spriteBody);
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _pushComponent.IsActive = true;
            _damageState.CurrentLives = EnemyLives.Pokey;
            _aiComponent.ChangeState("moving");
        }

        private void OnBurn()
        {
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
            _pushComponent.IsActive = false;
        }

        private void InitWalking()
        {
            ChangeDirection();
        }

        private void ChangeDirection()
        {
            // random new direction
            _direction = Game1.RandomNumber.Next(0, 4);
            _body.VelocityTarget = AnimationHelper.DirectionOffset[_direction] * _moveSpeed;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            return true;
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            // change the draw effect
            if (_sprite.SpriteShader != null)
            {
                spriteBatch.End();
                ObjectManager.SpriteBatchBegin(spriteBatch, _sprite.SpriteShader);
            }

            // draw the body
            var posY = EntityPosition.Y - EntityPosition.Z;
            if (_state == 0)
            {
                DrawHelper.DrawNormalized(spriteBatch, _spriteBody, new Vector2(EntityPosition.X, posY), _sprite.Color);
                posY -= 12;
            }

            var offsetX = 0.0f;
            if (_state <= 1)
            {
                // dont wobble at the floor
                if (_state == 0)
                    offsetX = (float)Math.Sin(Game1.TotalGameTime * 0.0125);

                DrawHelper.DrawNormalized(spriteBatch, _spriteBody, new Vector2(EntityPosition.X + offsetX, posY), _sprite.Color);
                posY -= 12;
            }

            // draw the head
            offsetX = -(float)Math.Sin(Game1.TotalGameTime * 0.0125) * (_state == 0 ? 2 : 1);
            DrawHelper.DrawNormalized(spriteBatch, _spriteHead, new Vector2(EntityPosition.X + offsetX, posY), _sprite.Color);

            // make sure to also move the shadow
            if (_state >= 2)
                _sprite.DrawOffset.X = offsetX;

            // change the draw effect
            // this would not be very efficient if a lot of sprite used effects
            if (_sprite.SpriteShader != null)
            {
                spriteBatch.End();
                ObjectManager.SpriteBatchBegin(spriteBatch, null);
            }
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // Don't deal damage if in damage state.
            if (_damageState.IsInDamageState())
                return Values.HitCollision.None;

            // 1 damage under state 2 is 1 damage, otherwise 0 damage. Higher passes through.
            damage = damage <= 1 ? _state < 2 ? 0 : 1 : damage;

            // If the damage is zero then don't play the hit sound.
            _damageState.PlayHitSound = damage == 0 ? false : true;

            // The sound played in damageState is fixed, and is wrong for this enemy. So it is
            // disabled above so that a custom sound effect can be played here.
            if (!_damageState.PlayHitSound)
                Game1.AudioManager.PlaySoundEffect("D360-03-03");

            // Remove knockback when just knocking his parts off. 
            _damageState.HitMultiplierX = damage == 0 ? 0 : 5;
            _damageState.HitMultiplierY = damage == 0 ? 0 : 5;

            // If there is still some lives left and only 1 damage was dealt.
            if (_damageState.CurrentLives > 0 && damage <= 1)
            {
                // Increment the state.
                _state += 1;

                // Spawn a bouncing pokey part.
                if (_state <= 2)
                {
                    EntityPosition.Z = 14;
                    _body.Velocity.Z = -0.5f;

                    var bodyPart = new EnemyPokeyPart(Map, EntityPosition.X, EntityPosition.Y, direction * 2f, _body.Velocity);
                    Map.Objects.SpawnObject(bodyPart);
                }
            }
            // Set the sprite to pokey head.
            if (_state == 2)
                _sprite.SetSprite(_spriteHead);

            // Register the hit.
            var hit = _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            // When a hit removes all lives disable components.
            if (_damageState.CurrentLives <= 0)
            {
                _damageField.IsActive = false;
                _hitComponent.IsActive = false;
                _pushComponent.IsActive = false;
            }
            // Return the hit.
            return hit;
        }
    }
}
