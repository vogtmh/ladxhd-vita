using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.NPCs
{
    internal class ObjGhost : GameObjectFollower
    {
        private readonly AiComponent _aiComponent;
        private readonly Animator _animator;
        private readonly BodyComponent _body;
        private readonly BodyDrawShadowComponent _shadowComponent;
        private readonly CSprite _sprite;

        private const int FlyHeight = 14;

        private Vector2 _followVelocity;
        private Vector2 _targetPosition;

        private float _moveSpeed;
        private float _fadeTime;
        private float _fadeCounter;

        private bool _returning;
        private bool _fadingIn;

        private Vector2 _lastDialogPosition;

        public ObjGhost() : base("ghost") { }

        public ObjGhost(Map.Map map, int posX, int posY) : base(map)
        {
            FreezePersist = true;
            EntityPosition = new CPosition(posX + 8, posY + 16, FlyHeight);
            EntitySize = new Rectangle(-8, -32, 16, 32);

            var ghostState = Game1.GameManager.SaveManager.GetString("ghost_state");

            _animator = AnimatorSaveLoad.LoadAnimator("NPCs/ghost");

            _sprite = new CSprite(EntityPosition);
            _sprite.Color = Color.White * 0.75f;
            var animationComponent = new AnimationComponent(_animator, _sprite, Vector2.Zero);

            _body = new BodyComponent(EntityPosition, -4, -10, 8, 10, 8)
            {
                IgnoreHoles = true,
                IgnoresZ = true,
                Gravity = -0.15f,
                CollisionTypes = Values.CollisionTypes.None
            };

            _aiComponent = new AiComponent();

            var stateFade = new AiState(UpdateFade);
            var stateMove = new AiState(UpdateMoving);
            var stateStartFollow = new AiState(UpdateFollowPlayer);
            var stateFollow = new AiState(UpdateFollowPlayer);
            var stateReturn = new AiState(UpdateMoving) { Init = InitReturn };

            _aiComponent.States.Add("fade", stateFade);
            _aiComponent.States.Add("move", stateMove);
            _aiComponent.States.Add("startFollow", stateStartFollow);
            _aiComponent.States.Add("follow", stateStartFollow);
            _aiComponent.States.Add("return", stateReturn);

            _aiComponent.ChangeState("follow");

            AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(KeyChanged));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, _shadowComponent = new BodyDrawShadowComponent(_body, _sprite));
        }

        public override void Init()
        {
            _shadowComponent.IsActive = !Map.Is2dMap;
            new ObjSpriteShadow(Map, this, Values.LayerPlayer, "sprshadowm");
        }

        public override void SetPosition(Vector2 position)
        {
            _lastDialogPosition = position;
            EntityPosition.Set(position);
        }

        public override void SetFacingDirection(int direction)
        {
            _animator.Play("stand_" + direction);
        }

        public void StartFollowing()
        {
            // offset the position in a random direction
            var radiant = Game1.RandomNumber.Next(0, 314 * 2) / 100f;
            var offset = new Vector2(MathF.Sin(radiant), MathF.Cos(radiant)) * 48;
            EntityPosition.Offset(offset);
            _lastDialogPosition = EntityPosition.Position;

            _fadingIn = true;
            _fadeCounter = 0;
            _fadeTime = 500;

            // set up variables
            Game1.GameManager.StartDialogPath("ghost_start_following");

            _aiComponent.ChangeState("startFollow");
        }

        private void InitReturn()
        {
            var graveStone = Map.Objects.GetObjectOfType(
                (int)MapManager.ObjLink.EntityPosition.X - 32,
                (int)MapManager.ObjLink.EntityPosition.Y - 32, 64, 64, typeof(ObjMoveStone));
            if (graveStone != null)
            {
                _targetPosition = new Vector2(graveStone.EntityPosition.X + 8, graveStone.EntityPosition.Y);
                _moveSpeed = 0.5f;
            }
        }

        private void UpdateFollowPlayer()
        {
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            var playerDistance = playerDirection.Length();
            if (playerDirection != Vector2.Zero)
                playerDirection.Normalize();
            var movementSpeed = MathHelper.Clamp((playerDistance - 16) / 32, 0, 2);

            // move towards the player
            _followVelocity = AnimationHelper.MoveToTarget(_followVelocity, playerDirection * movementSpeed, 0.1f * Game1.DeltaTime);
            _body.VelocityTarget = _followVelocity;

            // fly up and down
            var targetPosZ = FlyHeight + MathF.Sin(((float)Game1.TotalGameTime / 1000) * MathF.PI * 2) * 1.5f;
            EntityPosition.Z = AnimationHelper.MoveToTarget(EntityPosition.Z, targetPosZ, 1 * Game1.TimeMultiplier);

            // play walk/stand animation
            if (_followVelocity.Length() > 0.1f)
                UpdateAnimation(_followVelocity);

            // start the dialog if we walked some amount
            var dialogDistance = _lastDialogPosition - EntityPosition.Position;
            if (dialogDistance.Length() > 160)
            {
                _lastDialogPosition = EntityPosition.Position;
                Game1.GameManager.StartDialogPath("ghost_return");
            }

            // fade in
            if (_fadingIn)
            {
                _fadeCounter += Game1.DeltaTime;
                if (_fadeCounter >= _fadeTime)
                {
                    _fadeCounter = _fadeTime;
                    _fadingIn = false;
                }

                var percentage = _fadeCounter / _fadeTime;
                _sprite.Color = Color.White * percentage;
                _shadowComponent.Transparency = percentage;
            }
        }

        private void UpdateMoving()
        {
            // move towards the target position
            var targetDirection = _targetPosition - EntityPosition.Position;
            if (targetDirection.Length() > _moveSpeed * Game1.TimeMultiplier)
            {
                targetDirection.Normalize();
                _body.VelocityTarget = targetDirection * _moveSpeed;
                UpdateAnimation(targetDirection * _moveSpeed);
            }
            // finished walking
            else
            {
                _body.VelocityTarget = Vector2.Zero;
                EntityPosition.Set(_targetPosition);
                if (_returning)
                    _animator.Play("right");
            }
        }

        private void UpdateFade()
        {
            if (_fadeTime <= 0)
                return;

            _fadeCounter -= Game1.DeltaTime;
            
            // Not even setting the map on creation works for this one, so reference the map from ObjLink I guess.
            if (_fadeCounter <= 0)
            {
                Map.Map mapRef;

                if (Map == null)
                    mapRef = MapManager.ObjLink.Map;
                else
                    mapRef = Map;

                // Remove the ghost from the follower list on ObjLink.
                MapManager.ObjLink.Followers.Remove(this);
                mapRef.Objects.DeleteObjects.Add(this);
            }
            else
            {
                EntityPosition.Z = AnimationHelper.MoveToTarget(EntityPosition.Z, 0, 0.5f * Game1.TimeMultiplier);

                var percentage = _fadeCounter / _fadeTime;
                _sprite.Color = Color.White * percentage;
                _shadowComponent.Transparency = percentage;
            }
        }

        private void UpdateAnimation(Vector2 direction)
        {
            var dir = Math.Abs(direction.X) / Math.Abs(direction.Y);
            if (direction.Y >= 0 || dir > 0.75f)
                _animator.Play(direction.X < 0 ? "left" : "right");
            else
                _animator.Play(direction.X < 0 ? "up_left" : "up_right");
        }

        private void KeyChanged()
        {
            if (!IsActive)
                return;

            var saveManager = Game1.GameManager.SaveManager;

            // start following
            var followValue = saveManager.GetString("ghost_follow");
            if (!string.IsNullOrEmpty(followValue))
            {
                _aiComponent.ChangeState("follow");
                saveManager.RemoveString("ghost_follow");
            }

            // start fading away?
            var fadeValue = saveManager.GetString("ghost_fade");
            if (!string.IsNullOrEmpty(fadeValue))
            {
                _fadeTime = int.Parse(fadeValue);
                _fadeCounter = _fadeTime;
                _aiComponent.ChangeState("fade");

                saveManager.RemoveString("ghost_fade");
            }

            // start moving? [set:ghost_move,-16,32,1]
            var moveValue = saveManager.GetString("ghost_move");
            if (!string.IsNullOrEmpty(moveValue))
            {
                var split = moveValue.Split(',');
                var offsetX = int.Parse(split[0]);
                var offsetY = int.Parse(split[1]);
                _moveSpeed = float.Parse(split[2], CultureInfo.InvariantCulture);
                _targetPosition = new Vector2(EntityPosition.X + offsetX, EntityPosition.Y + offsetY);
                _aiComponent.ChangeState("move");

                saveManager.RemoveString("ghost_move");
            }

            // Store that the house was seen.
            var seenHouse = saveManager.GetString("ghost_seenhouse", "0");
            if (seenHouse == "1")
            {
                saveManager.SetString("ghost_achieve_tracker", "1");
            }

            // Returned to the grave.
            var returnValue = saveManager.GetString("ghost_return");
            if (!string.IsNullOrEmpty(returnValue))
            {
                _returning = true;
                _aiComponent.ChangeState("return");
                saveManager.RemoveString("ghost_return");

                // If the player stopped at the house in this session then give the achievement.
                if (saveManager.GetString("ghost_achieve_tracker", "0") == "1")
                    AchievementManager.Earn(62);
                saveManager.RemoveString("ghost_achieve_tracker");
            }
        }
    }
}
