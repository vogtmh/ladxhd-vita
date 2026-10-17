using System;
using Microsoft.Xna.Framework;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.NPCs
{
    internal class ObjBallChildrenAttacked : GameObject
    {
        private readonly AiComponent _aiComponent;
        private readonly ObjPersonNew _firstPerson;
        private readonly ObjPersonNew _secondPerson;
        private readonly Vector2 _startPosition0;
        private readonly Vector2 _startPosition1;
        private readonly Vector2 _centerPosition;

        private const int MoveTime = 500;

        private Vector2 _moveDirection0;
        private Vector2 _moveDirection1;
        private float _moveDistance0;
        private float _moveDistance1;
        private float _npcGroundCount;

        private bool _musicPlaying;

        private Rectangle _fieldRectangle;
        private bool _playerInField => _fieldRectangle.Contains(MapManager.ObjLink.CenterPosition);

        public ObjBallChildrenAttacked() : base("green_child") { }

        public ObjBallChildrenAttacked(Map.Map map, int posX, int posY, string spawnCondition) : base(map)
        {
            // check if the entity should get spawned
            if (!string.IsNullOrEmpty(spawnCondition))
            {
                var condition = SaveLoad.SaveCondition.GetConditionNode(spawnCondition);
                if (!condition.Check())
                {
                    IsDead = true;
                    return;
                }
            }
            EntityPosition = new CPosition(posX, posY + 16, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);

            // Get the field the object is in.
            if (map != null)
                _fieldRectangle = map.GetField(posX, posY);

            _centerPosition = new Vector2(posX + 24, posY);

            _firstPerson = new ObjPersonNew(Map, posX, posY, null, "npc_green_boy", "npc_kid_attacked", null, new Rectangle(0, 0, 14, 10));
            _secondPerson = new ObjPersonNew(Map, posX + 48, posY, null, "npc_red_boy", "npc_kid_attacked", null, new Rectangle(0, 0, 14, 10));

            _firstPerson.DisableRotating();
            _secondPerson.DisableRotating();

            _firstPerson.Animator.SpeedMultiplier = 2;
            _secondPerson.Animator.SpeedMultiplier = 2;

            Map.Objects.SpawnObject(_firstPerson);
            Map.Objects.SpawnObject(_secondPerson);

            _startPosition0 = _firstPerson.EntityPosition.Position;
            _startPosition1 = _secondPerson.EntityPosition.Position;

            var stateIdle = new AiState(UpdateIdle);
            var stateMove = new AiState { Init = InitMove };
            stateMove.Trigger.Add(new AiTriggerCountdown(MoveTime, MoveTick, MoveEnd));
            var stateMoved = new AiState(UpdateJumping);

            _aiComponent = new AiComponent();
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("move", stateMove);
            _aiComponent.States.Add("moved", stateMoved);
            _aiComponent.ChangeState("idle");

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));

            new ObjSpriteShadow(map, _firstPerson, Values.LayerPlayer, "sprshadowm");
            new ObjSpriteShadow(map, _secondPerson, Values.LayerPlayer, "sprshadowm");

            Map.Objects.RegisterAlwaysAnimateObject(this);
        }

        private void Update()
        {
            if (!_musicPlaying && _playerInField)
            {
                Game1.AudioManager.SetMusicFadeTransition(13, 2, 350);
                _musicPlaying = true;
            }
            else if (_musicPlaying && !_playerInField)
            {
                Game1.AudioManager.SetMusicFadeTransition(-1, 2, 350);
                _musicPlaying = false;
            }
        }

        private void InitMove()
        {
            _moveDirection0 = MapManager.ObjLink.Position - _firstPerson.EntityPosition.Position;
            _moveDirection1 = MapManager.ObjLink.Position - _secondPerson.EntityPosition.Position;

            if (_moveDirection0 != Vector2.Zero)
                _moveDirection0.Normalize();
            if (_moveDirection1 != Vector2.Zero)
                _moveDirection1.Normalize();

            // scale the distance the boys move so that they are approximately at the same height
            _moveDistance0 = 14 / (1 - MathF.Abs(_moveDirection0.X) / 1.85f);
            _moveDistance1 = 14 / (1 - MathF.Abs(_moveDirection1.X) / 1.85f);
        }

        private void MoveTick(double counter)
        {
            var playerDirection = MapManager.ObjLink.Position - _centerPosition;
            var playerDistance = playerDirection.Length();

            if (playerDistance > 30)
                MapManager.ObjLink.FreezePlayer();

            var moveAmount = 1 - (float)(counter / MoveTime);
            var person0Position = _startPosition0 + _moveDirection0 * _moveDistance0 * moveAmount;
            var person1Position = _startPosition1 + _moveDirection1 * _moveDistance1 * moveAmount;

            _firstPerson.EntityPosition.Set(person0Position);
            _secondPerson.EntityPosition.Set(person1Position);
        }

        private void MoveEnd()
        {
            MoveTick(1);
            _aiComponent.ChangeState("moved");
            Game1.GameManager.StartDialogPath("npc_kid_attacked");
        }

        private void UpdateIdle()
        {
            UpdateJumping();

            // start walking towards the player?
            var playerDirection = MapManager.ObjLink.Position - _centerPosition;
            var playerDistance = playerDirection.Length();

            if (playerDistance < 60 && _playerInField)
            {
                if (playerDistance > 30)
                {
                    _aiComponent.ChangeState("move");
                    var direction = playerDirection.Y < 0 ? "1" : "3";
                    _firstPerson.Animator.Play("stand_" + direction);
                    _secondPerson.Animator.Play("stand_" + direction);
                }
                else
                    MoveEnd();
            }
        }

        private void UpdateJumping()
        {
            if (_firstPerson.Body.IsGrounded)
            {
                _npcGroundCount -= Game1.DeltaTime;
                if (_npcGroundCount < 0)
                {
                    _firstPerson.Body.Velocity.Z = 1f;
                    _secondPerson.Body.Velocity.Z = 1f;
                    _npcGroundCount = 200;
                }
            }
            var playerDirection = MapManager.ObjLink.Position - _firstPerson.EntityPosition.Position;
            var playerDistance = playerDirection.Length();

            if (playerDistance < 64)
            {
                var direction = playerDirection.Y < 0 ? "1" : "3";
                _firstPerson.Animator.Play("stand_" + direction);
                _secondPerson.Animator.Play("stand_" + direction);
            }
        }
    }
}
