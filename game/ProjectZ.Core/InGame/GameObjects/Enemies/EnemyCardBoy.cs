using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Enemies
{
    internal class EnemyCardBoy : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly BodyComponent _body;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damgeField;
        private readonly Animator _animator;

        private readonly string _key;
        private readonly string _keyRespawn;
        private readonly int _index;

        private float _changeTime = 250;
        private float _changeCounter;
        private float _walkSpeed = 0.5f;
        private bool _isRemoving;
        private bool _isRespawn;
        private int _startingCardIndex = 0;
        private int _cardIndex;
        private int _dir;

        private string ActiveKey => _isRespawn ? _keyRespawn : _key;
        private int GetCardValue(int i) => Game1.GameManager.SaveManager.GetInt(ActiveKey + i, -1);
        private void SetSolved() => Game1.GameManager.SaveManager.SetString(ActiveKey, "1");

        public EnemyCardBoy() : base("card boy") { }

        public EnemyCardBoy(Map.Map map, int posX, int posY, int index, string key) : base(map)
        {
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 16, 0);
            ResetPosition  = new CPosition(posX + 8, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);
            CanReset = true;
            OnReset = Reset;

            _index = index;
            _startingCardIndex = 0;

            if (string.IsNullOrEmpty(key))
            {
                IsDead = true;
                return;
            }

            _key = key;
            _keyRespawn = key + "_respawn";

            _isRespawn = Game1.GameManager.SaveManager.GetString(_key) == "1";

            if (_isRespawn)
            {
                Game1.GameManager.SaveManager.SetString(_keyRespawn, "0");
                Game1.GameManager.SaveManager.RemoveInt(_keyRespawn + _index);
            }
            _animator = AnimatorSaveLoad.LoadAnimator("Enemies/card boy");

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, new Vector2(-8, -16));

            _body = new BodyComponent(EntityPosition, -7, -10, 14, 10, 8)
            {
                MoveCollision = OnCollision,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Enemy |
                                 Values.CollisionTypes.Player |
                                 Values.CollisionTypes.Field |
                                 Values.CollisionTypes.NPCWall,
                AvoidTypes =     Values.CollisionTypes.Hole,
                FieldRectangle = map.GetField(posX, posY),
                Drag = 0.75f,
            };

            var stateIdle = new AiState(Update);
            stateIdle.Trigger.Add(new AiTriggerRandomTime(ToWalking, 250, 500));
            var stateWalking = new AiState(Update);
            stateWalking.Trigger.Add(new AiTriggerRandomTime(ToIdle, 750, 1000));
            var stateWaiting = new AiState();
            var stateDamage = new AiState();
            stateDamage.Trigger.Add(new AiTriggerCountdown(400, DamageTick, FinishDamage));

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("walking", stateWalking);
            _aiComponent.States.Add("damage", stateDamage);
            _aiComponent.States.Add("waiting", stateWaiting);
            _aiComponent.ChangeState("idle");

            var damageBox   = new CBox(EntityPosition, -3,  -8, 0,  6,  6, 4);
            var hittableBox = new CBox(EntityPosition, -8, -15, 0, 16, 14, 8);
            var pushableBox = new CBox(EntityPosition, -7, -14, 0, 14, 13, 8);

            AddComponent(DamageFieldComponent.Index, _damgeField = new DamageFieldComponent(damageBox, HitType.Enemy, 2));
            AddComponent(HittableComponent.Index, new HittableComponent(hittableBox, OnHit));
            AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(KeyChanged));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(PushableComponent.Index, new PushableComponent(pushableBox, OnPush));
            AddComponent(DrawComponent.Index, new BodyDrawComponent(_body, _sprite, Values.LayerPlayer));
            AddComponent(DrawShadowComponent.Index, new DrawShadowCSpriteComponent(_sprite));
        }

        public override void Reset()
        {
            _aiComponent.ChangeState("idle");
            _aiComponent.ChangeState("idle");
            _changeCounter = _startingCardIndex * _changeTime;
            _cardIndex = _startingCardIndex;
            ResetPuzzle();
        }

        private void ToIdle()
        {
            _damgeField.IsActive = true;
            _aiComponent.ChangeState("idle");
            _body.VelocityTarget = Vector2.Zero;
        }

        private void ToWalking()
        {
            _aiComponent.ChangeState("walking");
            // random new direction
            _dir = Game1.RandomNumber.Next(0, 4);
            _body.VelocityTarget = AnimationHelper.DirectionOffset[_dir] * _walkSpeed;
        }

        private void Update()
        {
            _changeCounter += Game1.DeltaTime;

            if (_changeCounter > _changeTime * 4)
                _changeCounter -= _changeTime * 4;

            _cardIndex = (int)(_changeCounter / _changeTime);

            var time = _animator.FrameCounter;
            var frame = _animator.CurrentFrameIndex;
            _animator.Play((_cardIndex + 1).ToString(), frame, time);
            _animator.IsPlaying = _aiComponent.CurrentStateId == "walking";
        }

        private void KeyChanged()
        {
            // reset boy
            if (_aiComponent.CurrentStateId == "waiting" &&
                GetCardValue(_index) == -1)
                _aiComponent.ChangeState("idle");

            if (Game1.GameManager.SaveManager.GetString(ActiveKey) == "1")
                RemoveEntity();
            else
                CheckOther();
        }

        private void RemoveEntity()
        {
            if (_isRemoving)
                return;

            _isRemoving = true;

            Map.Objects.SpawnObject(new ObjAnimator(Map, (int)EntityPosition.X - 16, (int)EntityPosition.Y - 24, Values.LayerTop, "Particles/explosion", "run", true));
            Map.Objects.DeleteObjects.Add(this);

            string strObject = null;

            if (_cardIndex == 0)
                strObject = "heart";
            else if (_cardIndex == 1)
                strObject = "ruby";

            if (strObject != null)
            {
                var objItem = new ObjItem(Map, (int)EntityPosition.X - 8, (int)EntityPosition.Y - 8, "j", null, strObject, null, true);
                Map.Objects.SpawnObject(objItem);
            }
        }

        private void CheckOther()
        {
            // all boys set
            var resetBoys = true;
            // all boy states equal
            var allEqual = true;

            for (var i = 0; i < 3; i++)
            {
                var value = GetCardValue(i);

                if (value == -1)
                    resetBoys = false;
                if (value != _cardIndex)
                    allEqual = false;
            }

            if (!allEqual && resetBoys)
            {
                Game1.AudioManager.PlaySoundEffect("D360-29-1D");

                for (var i = 0; i < 3; i++)
                    Game1.GameManager.SaveManager.RemoveInt(ActiveKey + i);
            }

            // all card boys have the same state
            if (allEqual)
            {
                SetSolved();
                Game1.AudioManager.PlaySoundEffect("D378-19-13");
            }
        }

        private void AddDamage()
        {
            _aiComponent.ChangeState("damage");
            _body.VelocityTarget = Vector2.Zero;
            _animator.IsPlaying = false;
            _damgeField.IsActive = false;
        }

        private void DamageTick(double time)
        {
            _sprite.SpriteShader = time % 133 < 66 ? Resources.DamageSpriteShader0 : null;
        }

        private void FinishDamage()
        {
            _sprite.SpriteShader = null;
            _aiComponent.ChangeState("waiting");

            Game1.GameManager.SaveManager.SetInt(ActiveKey + _index, _cardIndex);
        }

        private void ResetPuzzle()
        {
            for (var i = 0; i < 3; i++)
                Game1.GameManager.SaveManager.RemoveInt(ActiveKey + i);

            Game1.GameManager.SaveManager.RemoveString(ActiveKey);
        }

        private Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            if (_aiComponent.CurrentStateId == "damage")
                return Values.HitCollision.None;

            Game1.AudioManager.PlaySoundEffect("D360-03-03");

            _body.Velocity.X = direction.X * 2.5f;
            _body.Velocity.Y = direction.Y * 2.5f;

            AddDamage();

            return Values.HitCollision.Enemy;
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (type == PushableComponent.PushType.Impact)
            {
                _body.Velocity = new Vector3(direction.X * 2.5f, direction.Y * 2.5f, _body.Velocity.Z);

                if (_aiComponent.CurrentStateId != "damage" &&
                    _aiComponent.CurrentStateId != "waiting")
                    AddDamage();
            }

            return true;
        }

        private void OnCollision(Values.BodyCollision direction)
        {
            if (_aiComponent.CurrentStateId != "walking")
                return;

            if (direction == Values.BodyCollision.Vertical)
                _body.VelocityTarget.Y = 0;
            else if (direction == Values.BodyCollision.Horizontal)
                _body.VelocityTarget.X = 0;
        }
    }
}
