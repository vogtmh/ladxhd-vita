using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyBomber : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _damageState;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly DamageFieldComponent _damageField;
        private readonly HittableComponent _hitComponent;

        private ObjBomb _objBomb;

        private Vector2 _startPosition;

        private float _flyHeight = 14;
        private int _lives = EnemyLives.Bomber;
        private int _dropIndex = 7;

        private float _dodgeRange = 32f;
        private float _fleeCounter;

        public EnemyBomber() : base("bomber") { }

        public EnemyBomber(Map.Map map, int posX, int posY) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, _flyHeight);
            ResetPosition  = new CPosition(posX + 8, posY + 16, _flyHeight);
            EntitySize = new Rectangle(-12, -32, 24, 32);
            CanReset = true;
            OnReset = Reset;

            _startPosition = EntityPosition.Position;

            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/bomber");
            _animator.Play("idle");

            var sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, sprite, new Vector2(-12, -16));

            _body = new BodyComponent(EntityPosition, -8, -12, 16, 12, 8)
            {
                CollisionTypes = Values.CollisionTypes.NPCWall |
                                 Values.CollisionTypes.Field,
                FieldRectangle = map.GetField(posX, posY),
                DragAir = 0.975f,
                Gravity = -0.175f,
                IgnoreHoles = true,
                IgnoresZ = true,
            };

            var stateWaiting = new AiState() { Init = InitWaiting };
            stateWaiting.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("moving"), 500, 1000));
            var stateMoving = new AiState() { Init = InitMoving };
            stateMoving.Trigger.Add(new AiTriggerRandomTime(() => _aiComponent.ChangeState("waiting"), 500, 1000));
            var stateFleeing = new AiState(UpdateFleeing);

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("waiting", stateWaiting);
            _aiComponent.States.Add("moving", stateMoving);
            _aiComponent.States.Add("fleeing", stateFleeing);
            _aiComponent.ChangeState("waiting");
            _damageState = new AiDamageState(this, _body, _aiComponent, sprite, _lives, _dropIndex) { OnBurn = OnBurn, OnDeath = OnDeath };

            var damageBox = new CBox(EntityPosition, -3, -8, 0, 6, 6, 4, true);

            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageBox, HitType.Enemy, 4));
            AddComponent(HittableComponent.Index, _hitComponent = new HittableComponent(damageBox, OnHit) { ArrowMultiplier = true, BoomerangMultiplier = true });
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new BodyDrawShadowComponent(_body, sprite) { ShadowWidth = 12, ShadowHeight = 4 });
            AddComponent(UpdateComponent.Index, new UpdateComponent(UpdateDodgeCheck));

            var spriteShadow = new ObjSpriteShadow(map, this, Values.LayerPlayer, "sprshadowm");
            Map.Objects.RegisterAlwaysAnimateObject(this);
            Map.Objects.RegisterAlwaysAnimateObject(spriteShadow);
        }

        public override void Reset()
        {
            _animator.Continue();
            _damageField.IsActive = true;
            _hitComponent.IsActive = true;
            _aiComponent.ChangeState("waiting");
            _aiComponent.ChangeState("waiting");
            _body.Bounciness = 0;
            _body.DragAir = 0.975f;
            _damageState.CurrentLives = EnemyLives.Bomber;

            if (_objBomb != null)
                Map.Objects.DeleteObjects.Add(_objBomb);
        }

        private void OnBurn()
        {
            _animator.Pause();
            _body.IgnoresZ = false;
            _body.DragAir = 0.9f;
            _body.Bounciness = 0.5f;
            _damageField.IsActive = false;
            _hitComponent.IsActive = false;
        }

        private void InitWaiting()
        {
            _body.VelocityTarget = Vector2.Zero;

            // Prevent the bombers from dropping bombs when not on the same field as them.
            if (Camera.ClassicMode && !_body.FieldRectangle.Contains(MapManager.ObjLink.CenterPosition))
                return;

            var positionLink = MapManager.ObjLink.Position;
            var playerDistance = positionLink - EntityPosition.Position;
            var distance = playerDistance.Length();

            // Try to drop a bomb when Link is close enough to the Bomber. 
            if (distance < 80 && Game1.RandomNumber.Next(0, 4) != 4 && _body.FieldRectangle.Contains(positionLink))
            {
                Vector2 throwDirection;

                if (distance < 64)
                {
                    // throw towards the player
                    if (playerDistance != Vector2.Zero)
                        playerDistance.Normalize();
                    throwDirection = playerDistance * (distance / 64) * 1.0f;
                }
                else
                {
                    // throw into a random direction
                    var randomRadius = Game1.RandomNumber.Next(0, 620) / 100;
                    throwDirection = new Vector2((float)Math.Sin(randomRadius), (float)Math.Cos(randomRadius)) * 0.75f;
                }

                // spawn a bomb
                _objBomb = new ObjBomb(Map, 0, 0, false, true);
                _objBomb.EntityPosition.Set(new Vector3(EntityPosition.X, EntityPosition.Y, 20));
                _objBomb.Body.Velocity = new Vector3(throwDirection, 0);
                _objBomb.Body.CollisionTypes = Values.CollisionTypes.None;
                _objBomb.Body.Gravity = -0.1f;
                _objBomb.Body.DragAir = 1.0f;
                _objBomb.Body.Bounciness = 0.5f;
                Map.Objects.SpawnObject(_objBomb);
                Map.Objects.RegisterAlwaysAnimateObject(_objBomb);
                new ObjSpriteShadow(Map, _objBomb, Values.LayerPlayer, "sprshadowm");
            }
        }

        private void InitMoving()
        {
            // Prevent the bombers from moving around when not on the same field as them.
            if (Camera.ClassicMode && !_body.FieldRectangle.Contains(MapManager.ObjLink.CenterPosition))
            {
                _aiComponent.ChangeState("waiting");
                return;
            }
            // The farther away the enemy is from the origin the more likely it becomes that he will move towards the start position.
            var directionToStart = _startPosition - EntityPosition.Position;
            var radiusToStart = Math.Atan2(directionToStart.Y, directionToStart.X);

            var maxDistance = 80.0f;
            var randomDir = radiusToStart + (Math.PI - Game1.RandomNumber.Next(0, 628) / 100f) *
                Math.Clamp(((maxDistance - directionToStart.Length()) / maxDistance), 0, 1);

            _body.VelocityTarget = new Vector2((float)Math.Cos(randomDir), (float)Math.Sin(randomDir)) * 0.5f;
        }

        private void UpdateDodgeCheck()
        {
            TryDodge();
        }

        private void OnDeath(bool pieceOfPower)
        {
            _damageState.BaseOnDeath(pieceOfPower);
        }

        private bool CheckDodgeConditions()
        {
            // If it's on fire, do not allow it to dodge.
            if (_aiComponent.CurrentStateId == "burning")
                return false;

            // It must be within the dodge range.
            var link = MapManager.ObjLink;
            var distance = link.Position - EntityPosition.Position;
            if (Math.Abs(distance.X) >= _dodgeRange || Math.Abs(distance.Y) >= _dodgeRange)
                return false;

            // Only dodges when Link is facing it.
            var linkToBomber = -distance;
            int facingDir = Math.Abs(linkToBomber.X) > Math.Abs(linkToBomber.Y)
                ? (linkToBomber.X < 0 ? 0 : 2)
                : (linkToBomber.Y < 0 ? 1 : 3);
            if (link.Direction != facingDir)
                return false;

            // We're here so dodge the attack.
            return true;
        }

        private void StartFlee()
        {
            var fleeDirection = EntityPosition.Position - MapManager.ObjLink.Position;
            if (fleeDirection != Vector2.Zero)
                fleeDirection.Normalize();
            _body.VelocityTarget = fleeDirection * 2f;
            _fleeCounter = 18 * (1000f / 60f);
            _aiComponent.ChangeState("fleeing");
        }

        private void TryDodge()
        {
            if (CheckDodgeConditions() && MapManager.ObjLink.IsSwordThreatActive())
                StartFlee();
        }

        private void UpdateFleeing()
        {
            _fleeCounter -= Game1.DeltaTime;
            if (_fleeCounter <= 0)
                _aiComponent.ChangeState("moving");
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            // If fleeing don't deal damage.
            if (_aiComponent.CurrentStateId == "fleeing")
                return Values.HitCollision.None;

            // Any type of sword will cause it to dodge.
            if (((hitType & HitType.Sword) != 0 || (hitType & HitType.SwordHold) != 0 || (hitType & HitType.PegasusBootsSword) != 0) && CheckDodgeConditions())
            {
                StartFlee();
                return Values.HitCollision.None;
            }
            // Immune to Bombs, Bomb Arrows, and the Hookshot.
            if (hitType == HitType.Bomb || hitType == HitType.BombArrow || hitType == HitType.Hookshot)
            {
                return Values.HitCollision.Blocking;
            }
            // Boomerang & Magic Powder have a unique death.
            else if ((hitType == HitType.Boomerang || hitType == HitType.MagicPowder))
            {
                // Cancel out the normal death effects and run BaseOnDeath for item drops.
                _damageState.NullifyDeathEffects();
                _damageState.DropTableIndex = 9;
                _damageState.BaseOnDeath(pieceOfPower);

                // Play the crunch sound and show the smoke effect.
                Game1.AudioManager.PlaySoundEffect("D360-03-03");
                var explosionAnimation = new ObjAnimator(Map, (int)EntityPosition.X-8, (int)EntityPosition.Y-26, Values.LayerTop, "Particles/spawn", "run", true);
                Map.Objects.SpawnObject(explosionAnimation);
                return Values.HitCollision.Blocking;
            }
            // Register the hit.
            var hit = _damageState.OnHit(gameObject, direction, hitType, damage, pieceOfPower);

            // When a hit removes all lives disable components.
            if (_damageState.CurrentLives <= 0)
            {
                _damageField.IsActive = false;
                _hitComponent.IsActive = false;
            }
            // Return the hit.
            return hit;
        }
    }
}
