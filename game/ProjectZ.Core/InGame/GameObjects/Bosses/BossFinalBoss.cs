using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Components.AI;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects.Bosses
{
    class BossFinalBoss : GameObject, IHasVisibility
    {
        private readonly AiComponent _aiComponent;
        private readonly AiDamageState _aiDamageState;
        private readonly Animator _animator;
        private readonly Animator _animatorBody;
        private readonly Animator _animatorWeapon;
        private readonly Animator _animatorEye;
        private readonly BodyComponent _body;
        private readonly BodyDrawShadowComponent _bodyShadow;
        private readonly CBox _hittableBox;
        private readonly CBox _hittableBoxMan;
        private readonly CSprite _sprite;
        private readonly DamageFieldComponent _damageField;
        private readonly DrawComponent _drawComponent;
        private readonly HittableComponent _hittableComponent;

        private BossFinalBossFireball _objFireball;
        private DictAtlasEntry _spriteBody;
        private ObjSpriteShadow _spriteShadow;

        private Rectangle _roomRectangle;
        private Vector2 _bodyPosition;
        private Vector2 _bodyStartPosition;
        private Vector2 _bodyTargetPosition;
        private Vector2 _agahnimTargetPosition;
        private Vector2[] _fireballOffset = new Vector2[] { new Vector2(-15, -8), new Vector2(0, -24), new Vector2(15, -8), new Vector2(0, -4) };
        private Vector2[] _bodyParts = new Vector2[6];

        // Group up the lives in one place.
        private int _giantZolLives = EnemyLives.F_GiantZol;
        private int _agahnimLives = EnemyLives.F_Agahnim;
        private int _ganonLives = EnemyLives.F_Ganon;
        private int _moldormLives = EnemyLives.F_Moldorm;
        private int _dethILives = EnemyLives.F_DethI;

        // State: Giant Zol
        private const int GiantZolDamageTime = 2200;
        private const int RotateTime = 2500;
        private bool _giantZolForm;
        private float _shadowTimer;
        private string _saveKey;

        // State: Agahnim's Shadow
        private bool _agahnimInit = true;

        // State: Shadow of Ganon
        private Vector2 _ganonTargetPosition;

        private const int _ganonDeathTime = 2200;
        private bool _ganonForm;
        private float _batCounter;
        private int _batIndex;
        private int _batIndexStart;

        // State: Moldorm
        private const int TailMult = 8;
        private BossFinalBossMoldormTail[] _moldormTails = new BossFinalBossMoldormTail[4];
        private AiTriggerSwitch _moldormSpeedUp;
        private Vector2[] _moldormPositions = new Vector2[6 * TailMult];
        private Vector2[] _moldormPositionsNew = new Vector2[6 * TailMult];
        private float[] _partDist = new float[4];
        private float _moldormRadiant;
        private float _moldormChangeCounter;
        private float _moldormSpeed;
        private float _directionChangeMultiplier;
        private int _moldormDirection = 1;
        private bool _moldormHit;

        private const float MoldormSpeedNormal = 1.0f;
        private const float MoldormSpeedFast = 1.75f;

        private int _direction;
        private int _sideIndex;
        private int _moveDist = 40;
        private float _moveSpeed = 0.25f;
        private float _moldormSoundCounter;

        private int _moveCounter;

        // State: Lanmola
        private float _lanmolaParticleCounter;

        // State: DethI
        // objects used to deal damage
        private readonly BossFinalBossDethITail[] _dethIParts = new BossFinalBossDethITail[8];
        private readonly string[] _spriteDethIParts = new string[] { "final_part0", "final_part1", "final_part1", "final_part2" };
        private float[] _dethIPartDistance = new float[] { 18, 10, 10, 10 };

        private Vector2 _targetPosition;
        private float _dethIPartCounter;
        private float _dethIPart0 = -MathF.PI / 2;
        private float _dethIPart1 = -MathF.PI / 2;
        private float _dethIPartSpeed0 = 1 / 2500.0f * MathF.PI * 2;
        private float _dethIPartSpeed1 = 1 / 2500.0f * MathF.PI * 2;
        private int _dethIStateDeathCounter = 2500;
        private float _dethIEyeCounter;
        private int _dethIEyeState;
        private bool _dethIState;
        private bool _hideBody;
        private bool _hideHead;
        private bool _drawGanonWeapon;
        private bool _drawMoldormTail;
        private bool _pushRepel;

        public bool DealtPlayerDamage = false;
        public bool IsVisible { get; private set; }
        public CSprite Sprite => _sprite;

        public BossFinalBoss() : base("nightmare_head") { }

        // Values configurable via lahdmod.
        private bool  light_source = true;
        private int   light_red    = 255;
        private int   light_grn    = 255;
        private int   light_blu    = 140;
        private float light_bright = 0.75f;
        private int   light_size   = 28;

        public BossFinalBoss(Map.Map map, int posX, int posY, string saveKey) : base(map)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "BossFinalBoss.lahdmod");
            ModFile.Parse(modFile, this);

            IsVisible = true;
            Tags = Values.GameObjectTag.Enemy;

            EntityPosition = new CPosition(posX + 8, posY + 8, 0);
            EntityPosition.AddPositionListener(typeof(BossFinalBoss), OnUpdatePosition);
            EntitySize = new Rectangle(-16, -24, 32, 40);

            _saveKey = saveKey;
            if (!string.IsNullOrEmpty(_saveKey) &&
                Game1.GameManager.SaveManager.GetString(_saveKey) == "1")
            {
                IsDead = true;
                return;
            }

            _bodyStartPosition = EntityPosition.Position;
            _bodyTargetPosition = _bodyStartPosition - new Vector2(0, _moveDist);

            _animator = AnimatorSaveLoad.LoadAnimator("Nightmares/nightmare");
            _animator.Play("head");

            _animatorBody = AnimatorSaveLoad.LoadAnimator("Nightmares/nightmare");
            _animatorBody.Play("idle");

            _animatorWeapon = AnimatorSaveLoad.LoadAnimator("Nightmares/nightmare ganon weapon");
            _animatorEye = AnimatorSaveLoad.LoadAnimator("Nightmares/nightmare final");

            _spriteBody = Resources.GetSprite("nightmare_body");

            _bodyPosition = EntityPosition.Position;
            _hideBody = true;

            _sprite = new CSprite(EntityPosition);
            var animationComponent = new AnimationComponent(_animator, _sprite, Vector2.Zero);

            for (var i = 0; i < _dethIParts.Length; i++)
            {
                _dethIParts[i] = new BossFinalBossDethITail(map, this, _spriteDethIParts[i % 4], EntityPosition.Position);
                map.Objects.SpawnObject(_dethIParts[i]);
            }

            _roomRectangle = map.GetField(posX, posY);

            _body = new BodyComponent(EntityPosition, -7, -7, 14, 14, 8)
            {
                MoveCollision = OnCollision,
                AbsorbPercentage = 1f,
                Gravity = -0.085f,
                DragAir = 1.0f,
                Drag = 0.8f,
                FieldRectangle = map.GetField(posX, posY),
                CollisionTypes = Values.CollisionTypes.Normal
            };

            _aiComponent = new AiComponent();

            // floor
            var stateIdle = new AiState(UpdateIdle) { Init = InitIdle };
            var stateMoveBody = new AiState(UpdateMoveBody) { Init = InitMoveBody };
            var stateMoveHead = new AiState(UpdateMoveHead);
            var stateWobble = new AiState(UpdateWobble) { Init = InitWobble };
            var stateDespawn = new AiState(UpdateDespawn) { Init = InitStartDespawn };

            // giant zol
            var stateGiantZolSpawn = new AiState(UpdateGiantZolSpawn) { Init = InitGiantZolSpawn };
            var stateGiantZolJump = new AiState(UpdateGiantZolJump) { Init = InitGiantZolJump };
            var stateGiantZolWait = new AiState() { Init = InitGiantZolWait };
            stateGiantZolWait.Trigger.Add(new AiTriggerCountdown(800, null, () => _aiComponent.ChangeState("slimeJump")));
            var stateGiantZolDespawn = new AiState(UpdateGiantZolDespawn) { Init = InitGiantZolDespawn };
            var stateGiantZolHidden = new AiState() { Init = InitGiantZolHidden };
            stateGiantZolHidden.Trigger.Add(new AiTriggerCountdown(2200, null, EndGiantZolHidden));
            var stateGiantZolDamaged = new AiState() { Init = InitGiantZolDamaged };
            stateGiantZolDamaged.Trigger.Add(new AiTriggerCountdown(GiantZolDamageTime, TickGiantZolDamaged, EndGiantZolDamge));
            var stateGiantZolHideExplode = new AiState() { Init = InitGiantZolHideExplode };
            stateGiantZolHideExplode.Trigger.Add(new AiTriggerCountdown(2200, null, EndGiantZolHidden));
            var stateGiantZolExplode = new AiState() { Init = InitGiantZolExplode };
            stateGiantZolExplode.Trigger.Add(new AiTriggerCountdown(2800, null, GiantZolEnd));

            // agahnim
            var stateAgahnimPreAttack = new AiState(UpdateManPreAttack);
            stateAgahnimPreAttack.Trigger.Add(new AiTriggerCountdown(1100, null, () => _aiComponent.ChangeState("manAttack")));
            var stateAgahnimAttack = new AiState(UpdateManAttack) { Init = InitManAttack };
            var stateAgahnimPostAttack = new AiState(UpdateManPostAttack) { Init = InitPostAttack };
            stateAgahnimPostAttack.Trigger.Add(new AiTriggerCountdown(1300, null, () => _aiComponent.ChangeState("manDespawn")));
            var stateAgahnimDespawn = new AiState(UpdateManDespawn) { Init = InitManDespawn };
            var stateAgahnimMove = new AiState(UpdateManMove) { Init = InitManMove };
            var stateAgahnimMoveWait = new AiState();
            stateAgahnimMoveWait.Trigger.Add(new AiTriggerCountdown(650, null, () => _aiComponent.ChangeState("manSpawn")));
            var stateAgahnimSpawn = new AiState(UpdateManSpawn) { Init = InitManSpawn };
            var stateAgahnimRotate = new AiState() { Init = InitManRotate };
            stateAgahnimRotate.Trigger.Add(new AiTriggerCountdown(RotateTime, TickRotate, EndRotate));

            // explode
            var stateExplode = new AiState() { Init = InitExplode };
            stateExplode.Trigger.Add(new AiTriggerCountdown(950, null, () => _aiComponent.ChangeState("explodeDespawn")));
            var stateExplodeDespawn = new AiState(UpdateDepawn) { Init = InitDespawn };
            var stateMove = new AiState(UpdateMove) { Init = InitMove };
            var stateMoveWait = new AiState();
            stateMoveWait.Trigger.Add(new AiTriggerCountdown(650, null, EndMoveWait));

            // moldorm
            var stateMoldormSpawn = new AiState(UpdateMoldormSpawn) { Init = InitMoldormSpawn };
            var stateMoldorm = new AiState(UpdateMoldorm) { Init = InitMoldorm };
            stateMoldorm.Trigger.Add(_moldormSpeedUp = new AiTriggerSwitch(3000));
            var stateMoldormDying = new AiState(UpdateMoldormDying) { Init = InitMoldormDying };
            stateMoldormDying.Trigger.Add(new AiTriggerCountdown(1250, null, MoldormExplode));
            var stateMoldormDespawn = new AiState(UpdateMoldormDespawn);

            // ganon
            var stateGanonSpawn = new AiState(UpdateGanonSpawn) { Init = InitGanonSpawn };
            var stateGanonPreSpawnWeapon = new AiState(UpdateGanonPreSpawnWeapon);
            stateGanonPreSpawnWeapon.Trigger.Add(new AiTriggerCountdown(850, null, () => _aiComponent.ChangeState("ganonSpawnWeapon")));
            var stateGanonSpawnWeapon = new AiState(UpdateGanonSpawnWeapon) { Init = InitGanonSpawnWeapon };
            var stateGanon = new AiState(UpdateGanon) { Init = InitGanon };
            stateGanon.Trigger.Add(new AiTriggerCountdown(1000, null, () => _aiComponent.ChangeState("ganonBats")));
            var stateGanonBats = new AiState(UpdateGanonBats) { Init = InitGanonBats };
            var stateGanonThrow = new AiState(UpdateGanonThrow) { Init = InitGanonThrow };
            stateGanonThrow.Trigger.Add(new AiTriggerCountdown(800, null, ThrowWeapon));
            var stateGanonPostThrow = new AiState() { Init = InitGanonPostThrow };
            stateGanonPostThrow.Trigger.Add(new AiTriggerCountdown(1100, null, () => _aiComponent.ChangeState("ganonMove")));
            var stateGanonMove = new AiState(UpdateGanonMove) { Init = InitGanonMove };
            var stateGanonCatchWeapon = new AiState(UpdateGanonCatchWeapon) { Init = InitGanonCatchWeapon };
            var stateGanonWait = new AiState(UpdateGanonWait);
            stateGanonWait.Trigger.Add(new AiTriggerCountdown(1000, null, () => _aiComponent.ChangeState("ganonBats")));
            var stateGanonDeath = new AiState() { Init = InitGanonDeath };
            stateGanonDeath.Trigger.Add(new AiTriggerCountdown(_ganonDeathTime, TickGanonDamage, () => _aiComponent.ChangeState("ganonExplode")));
            var stateGanonExplode = new AiState() { Init = InitGanonExplode };
            stateGanonExplode.Trigger.Add(new AiTriggerCountdown(_ganonDeathTime, null, () => _aiComponent.ChangeState("face")));

            // lanmola
            var stateLanmola = new AiState(UpdateLanmola) { Init = InitLanmola };
            var stateLanmolaExplode = new AiState() { Init = InitLanmolaExplose };
            stateLanmolaExplode.Trigger.Add(new AiTriggerCountdown(2200, null, () => _aiComponent.ChangeState("faceMove")));
            var stateLanmolaMove = new AiState(UpdateLanmolaMove) { Init = InitLanmolaMove };
            var stateLanmolaHidden = new AiState() { Init = InitLanmolaHidden };
            stateLanmolaHidden.Trigger.Add(new AiTriggerCountdown(1000, null, () => _aiComponent.ChangeState("faceDespawn")));
            var stateLanmolaDespawn = new AiState(UpdateLanmolaDespawn) { Init = InitLanmolaDespawn };

            // dethI
            var stateDethISpawn = new AiState(UpdateFianalSpawn) { Init = InitFinalSpawn };
            var stateDethI = new AiState(UpdateFinal) { Init = InitFinal };
            var stateDethIBlink = new AiState() { Init = InitFinalBlink };
            stateDethIBlink.Trigger.Add(new AiTriggerCountdown(_dethIStateDeathCounter, TickFinalDespawn, () => _aiComponent.ChangeState("finalDeath")));
            var stateDethIDeath = new AiState(UpdateFinalDeath) { Init = InitFinalDeath };

            // spawning
            _aiComponent.States.Add("idle", stateIdle);
            _aiComponent.States.Add("moveBody", stateMoveBody);
            _aiComponent.States.Add("moveHead", stateMoveHead);
            _aiComponent.States.Add("wobble", stateWobble);
            _aiComponent.States.Add("despawn", stateDespawn);

            // giantZol
            _aiComponent.States.Add("slimeSpawn", stateGiantZolSpawn);
            _aiComponent.States.Add("slimeJump", stateGiantZolJump);
            _aiComponent.States.Add("slimeWait", stateGiantZolWait);
            _aiComponent.States.Add("slimeDespawn", stateGiantZolDespawn);
            _aiComponent.States.Add("slimeHidden", stateGiantZolHidden);
            _aiComponent.States.Add("slimeDamaged", stateGiantZolDamaged);
            _aiComponent.States.Add("slimeHideExplode", stateGiantZolHideExplode);
            _aiComponent.States.Add("slimeExplode", stateGiantZolExplode);

            // man
            _aiComponent.States.Add("manPreAttack", stateAgahnimPreAttack);
            _aiComponent.States.Add("manAttack", stateAgahnimAttack);
            _aiComponent.States.Add("manPostAttack", stateAgahnimPostAttack);
            _aiComponent.States.Add("manDespawn", stateAgahnimDespawn);
            _aiComponent.States.Add("manMove", stateAgahnimMove);
            _aiComponent.States.Add("manMoveWait", stateAgahnimMoveWait);
            _aiComponent.States.Add("manSpawn", stateAgahnimSpawn);
            _aiComponent.States.Add("manRotate", stateAgahnimRotate);

            // moldorm
            _aiComponent.States.Add("moldormSpawn", stateMoldormSpawn);
            _aiComponent.States.Add("moldorm", stateMoldorm);
            _aiComponent.States.Add("moldormDying", stateMoldormDying);
            _aiComponent.States.Add("moldormDespawn", stateMoldormDespawn);

            // ganon
            _aiComponent.States.Add("ganonSpawn", stateGanonSpawn);
            _aiComponent.States.Add("ganonPreSpawnWeapon", stateGanonPreSpawnWeapon);
            _aiComponent.States.Add("ganonSpawnWeapon", stateGanonSpawnWeapon);
            _aiComponent.States.Add("ganon", stateGanon);
            _aiComponent.States.Add("ganonBats", stateGanonBats);
            _aiComponent.States.Add("ganonThrow", stateGanonThrow);
            _aiComponent.States.Add("ganonPostThrow", stateGanonPostThrow);
            _aiComponent.States.Add("ganonMove", stateGanonMove);
            _aiComponent.States.Add("ganonCatchWeapon", stateGanonCatchWeapon);
            _aiComponent.States.Add("ganonWait", stateGanonWait);
            _aiComponent.States.Add("ganonDeath", stateGanonDeath);
            _aiComponent.States.Add("ganonExplode", stateGanonExplode);

            // face
            _aiComponent.States.Add("face", stateLanmola);
            _aiComponent.States.Add("faceExplode", stateLanmolaExplode);
            _aiComponent.States.Add("faceMove", stateLanmolaMove);
            _aiComponent.States.Add("faceHidden", stateLanmolaHidden);
            _aiComponent.States.Add("faceDespawn", stateLanmolaDespawn);

            // final
            _aiComponent.States.Add("finalSpawn", stateDethISpawn);
            _aiComponent.States.Add("final", stateDethI);
            _aiComponent.States.Add("finalBlink", stateDethIBlink);
            _aiComponent.States.Add("finalDeath", stateDethIDeath);

            _aiComponent.States.Add("explode", stateExplode);
            _aiComponent.States.Add("explodeDespawn", stateExplodeDespawn);
            _aiComponent.States.Add("move", stateMove);
            _aiComponent.States.Add("moveWait", stateMoveWait);

            _aiDamageState = new AiDamageState(this, _body, _aiComponent, _sprite, 8, 0, false);
            _aiDamageState.OnDeath = OnZeroLives;
            _aiDamageState.BossHitSound = true;

            var damageCollider = new CBox(EntityPosition, -6, -6, 0, 12, 12, 8);
            _hittableBox = new CBox(EntityPosition, -7, -7, 0, 14, 14, 8);
            _hittableBoxMan = new CBox(EntityPosition, -6, -12, 0, 12, 18, 8);

            AddComponent(AiComponent.Index, _aiComponent);
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, animationComponent);
            AddComponent(PushableComponent.Index, new PushableComponent(_body.BodyBox, OnPush));
            AddComponent(HittableComponent.Index, _hittableComponent = new HittableComponent(_hittableBox, OnHit));
            AddComponent(DamageFieldComponent.Index, _damageField = new DamageFieldComponent(damageCollider, HitType.Enemy, 2) { IsActive = false, OnDamagedPlayer = OnDamagedPlayer });
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, _drawComponent = new DrawComponent(Draw, Values.LayerPlayer, EntityPosition));
            AddComponent(DrawShadowComponent.Index, _bodyShadow = new BodyDrawShadowComponent(_body, _sprite) { ShadowWidth = 14, ShadowHeight = 5 });
            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight));

            _aiComponent.ChangeState("idle");

            // Set the body parts under the face so they don't appear in the top left corner.
            for (int i = 0; i < _bodyParts.Length; i++)
                _bodyParts[i] = new Vector2(EntityPosition.X, EntityPosition.Y);
        }

        private void OnDamagedPlayer()
        {
            DealtPlayerDamage = true;
        }

        #region State: Init

        private void InitIdle()
        {
            Game1.GameManager.StartDialogPath("final_boss_intro");
        }

        private void UpdateIdle()
        {
            if (!Game1.GameManager.DialogIsRunning() &&
                !Game1.GameManager.InGameOverlay.TextboxOverlay.IsOpen)
            {
                _aiComponent.ChangeState("moveBody");
            }
        }

        private void InitMoveBody()
        {
            _hideBody = false;
            Game1.AudioManager.PlaySoundEffect("D360-53-35");
        }

        private void UpdateBodyPartPosition(float state)
        {
            var direction = _bodyPosition - EntityPosition.Position;

            // this is supposed to make it look better by moving the first element more than the following elements
            // TODO: should be replaced by constant speed up moving up?
            var stateRad = MathF.PI / 2 - state * MathF.PI / 2;
            var max = MathF.Sin(stateRad);
            var min = MathF.Sin(stateRad - MathF.PI / 2);
            for (var i = 0; i < _bodyParts.Length; i++)
            {
                var percentage = stateRad - (float)(i + 1) / (_bodyParts.Length + 1) * MathF.PI / 2;
                var sinState = (MathF.Sin(percentage) - min) / (max - min);
                _bodyParts[i] = EntityPosition.Position + direction * (1 - sinState);
            }
        }

        private void UpdateMoveBody()
        {
            _moveSpeed = AnimationHelper.MoveToTarget(_moveSpeed, 1.75f, 0.05f * Game1.TimeMultiplier);
            _bodyPosition = AnimationHelper.MoveToTarget(_bodyPosition, _bodyTargetPosition, _moveSpeed * Game1.TimeMultiplier);

            var bodyState = Math.Clamp(0.5f - (_bodyPosition.Y - _bodyTargetPosition.Y) / (_moveDist * 0.5f), 0, 0.5f);
            UpdateBodyPartPosition(bodyState);

            if (_bodyPosition == _bodyTargetPosition)
            {
                _moveSpeed = 0.25f;
                _animatorBody.Play("spawn");
                _aiComponent.ChangeState("moveHead");
            }
        }

        private void UpdateMoveHead()
        {
            _moveSpeed = AnimationHelper.MoveToTarget(_moveSpeed, 1.75f, 0.05f * Game1.TimeMultiplier);
            var newPosition = AnimationHelper.MoveToTarget(EntityPosition.Position, _bodyTargetPosition, _moveSpeed * Game1.TimeMultiplier);
            EntityPosition.X = newPosition.X;
            EntityPosition.Y = newPosition.Y;

            var bodyState = Math.Clamp(2.5f - (EntityPosition.Y - _bodyTargetPosition.Y) / (_moveDist * 0.5f), 0.5f, 1.0f);
            UpdateBodyPartPosition(bodyState);

            if (EntityPosition.Position == _bodyTargetPosition)
                _aiComponent.ChangeState("wobble");
        }

        private void InitWobble()
        {
            _animatorBody.Play("wobble");
        }

        private void UpdateWobble()
        {
            if (!_animatorBody.IsPlaying)
                _aiComponent.ChangeState("despawn");
        }

        private void InitStartDespawn()
        {
            _hideBody = true;
            _animator.Play("despawn");
            Game1.AudioManager.PlaySoundEffect("D360-04-04");
        }

        private void UpdateDespawn()
        {
            if (!_animator.IsPlaying)
                _aiComponent.ChangeState("slimeSpawn");
        }

        #endregion

        #region State: Giant Zol

        private void InitGiantZolSpawn()
        {
            _giantZolForm = true;
            _hideHead = false;
            _pushRepel = true;
            _bodyShadow.IsActive = true;
            _damageField.IsActive = true;

            _hittableComponent.HittableBox = new CBox(EntityPosition, -8, -8, 0, 16, 16, 8, true);
            _hittableComponent.IsActive = true;

            _drawComponent.Layer = Values.LayerPlayer;
            _animator.Play("slime_spawn");

            if (_spriteShadow == null)
                _spriteShadow = new ObjSpriteShadow(Map, this, -8, -7, Values.LayerPlayer, "sprshadowl");
        }

        private void UpdateGiantZolSpawn()
        {
            if (!_animator.IsPlaying)
                _aiComponent.ChangeState("slimeJump");

            _shadowTimer += Game1.DeltaTime;

            if (_shadowTimer > 400)
            {
                IsVisible = true;
                _shadowTimer = 0;
            }
        }

        private void InitGiantZolJump()
        {
            _animator.Play("slime_jump");

            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            if (playerDirection != Vector2.Zero)
                playerDirection.Normalize();

            _body.IsGrounded = false;
            _body.Velocity.X = playerDirection.X * 0.5f;
            _body.Velocity.Y = playerDirection.Y * 0.5f;
            _body.Velocity.Z = 1.75f;
        }

        private void UpdateGiantZolJump()
        {
            if (_body.IsGrounded)
            {
                Game1.AudioManager.PlaySoundEffect("D360-32-20");

                if (Game1.RandomNumber.Next(0, 3) == 0)
                    _aiComponent.ChangeState("slimeDespawn");
                else
                    _aiComponent.ChangeState("slimeWait");
            }
            else if (_body.Velocity.Y < -0.5f)
            {
                _animator.Play("slime_land");
            }
        }

        private void InitGiantZolWait()
        {
            _animator.Play("slime");
        }

        private void InitGiantZolDespawn()
        {
            _bodyShadow.IsActive = false;
            _damageField.IsActive = false;
            _hittableComponent.IsActive = false;
            _pushRepel = false;

            _animator.Play("slime_despawn");

            EntityPosition.Offset(new Vector2(0, -2));
            IsVisible = false;
        }

        private void UpdateGiantZolDespawn()
        {
            if (!_animator.IsPlaying)
                _aiComponent.ChangeState("slimeHidden");
        }

        private void InitGiantZolHidden()
        {
            _hideHead = true;
        }

        private void EndGiantZolHidden()
        {
            // random new position
            EntityPosition.Set(RandomRoomPosition());

            _aiComponent.ChangeState("slimeSpawn");
        }

        private void InitGiantZolDamaged()
        {
            _animator.Play("slime_damaged");
            Game1.AudioManager.PlaySoundEffect("D360-55-37");
        }

        private void TickGiantZolDamaged(double counter)
        {
            var time = GiantZolDamageTime - counter;
            if (time < AiDamageState.StaticCooldown && time % (AiDamageState.BlinkTime * 2) < AiDamageState.BlinkTime)
                _sprite.SpriteShader = Resources.DamageSpriteShader0;
            else
                _sprite.SpriteShader = null;
        }

        private void EndGiantZolDamge()
        {
            _giantZolForm = false;

            if (_giantZolLives <= 0)
            {
                _aiComponent.ChangeState("slimeExplode");
                _spriteShadow.Destroy();
            }
            else
                _aiComponent.ChangeState("slimeDespawn");
        }

        private void InitGiantZolHideExplode()
        {
            IsVisible = false;
            _pushRepel = false;
            _hideHead = true;
            _bodyShadow.IsActive = false;
            _damageField.IsActive = false;
            _hittableComponent.IsActive = false;

            InitGiantZolExplode();
        }

        private void InitGiantZolExplode()
        {

            EntityPosition.Offset(new Vector2(0, -2));
            Game1.AudioManager.PlaySoundEffect("D370-33-21");

            _body.VelocityTarget = Vector2.Zero;
            ExplodeAnimation();
        }

        #endregion

        #region State: Agahnim's Shadow

        // @TODO: type 2 spawn rate

        private void GiantZolEnd()
        {
            _aiComponent.ChangeState("manMove");
            _agahnimTargetPosition = new Vector2(_roomRectangle.Center.X, _roomRectangle.Y + 43);
        }

        private void InitManSpawn()
        {
            _animator.Play("man_spawn");
        }

        private void UpdateManSpawn()
        {
            if (_animator.IsPlaying)
                return;

            _pushRepel = true;
            _damageField.IsActive = true;
            Game1.AudioManager.PlaySoundEffect("D370-35-23");
            _aiComponent.ChangeState("manPreAttack");
        }

        private void UpdateManPreAttack()
        {
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            _direction = AnimationHelper.GetDirection(playerDirection);

            if (_agahnimInit)
            {
                _animator.Play("man_" + _direction);
                _animator.Pause();
            }
            else
            {
                _animator.Play("man_attack_" + _direction);
            }
        }

        private void InitManAttack()
        {
            _agahnimInit = false;
            _animator.Play("man_" + _direction);
            Game1.AudioManager.PlaySoundEffect("D370-34-22");

            _objFireball = new BossFinalBossFireball(this, EntityPosition.Position + _fireballOffset[_direction]);
            Map.Objects.SpawnObject(_objFireball);
        }

        private void UpdateManAttack()
        {
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            var newDirection = AnimationHelper.GetDirection(playerDirection);

            if (newDirection != _direction)
            {
                _direction = newDirection;
                _animator.Play("man_" + _direction);
                _objFireball.EntityPosition.Set(EntityPosition.Position + _fireballOffset[_direction]);
            }

            if (_objFireball.IsReady)
            {
                _aiComponent.ChangeState("manPostAttack");
                _objFireball.Fire();
            }
        }

        private void InitPostAttack()
        {
            _animator.Play("man_attack_" + _direction);
        }

        private void UpdateManPostAttack() { }

        private void InitManDespawn()
        {
            _pushRepel = false;
            _damageField.IsActive = false;
            _animator.Play("man_despawn");
        }

        private void UpdateManDespawn()
        {
            if (!_animator.IsPlaying)
                _aiComponent.ChangeState("manMove");
        }

        private void InitManMove()
        {
            Game1.AudioManager.PlaySoundEffect("D360-53-35");
            _body.CollisionTypes = Values.CollisionTypes.None;
            _agahnimTargetPosition = RandomRoomPositionSide();
        }

        private void UpdateManMove()
        {
            var direction = _agahnimTargetPosition - EntityPosition.Position;

            if (direction.Length() < 2)
            {
                _body.CollisionTypes = Values.CollisionTypes.Normal;
                _aiComponent.ChangeState("manMoveWait");
            }
            else
            {
                direction.Normalize();
                var targetVelocity = AnimationHelper.MoveToTarget(new Vector2(_body.Velocity.X, _body.Velocity.Y), direction, 0.15f * Game1.TimeMultiplier);
                _body.Velocity.X = targetVelocity.X;
                _body.Velocity.Y = targetVelocity.Y;
            }
        }

        private void InitManRotate()
        {
            Game1.AudioManager.PlaySoundEffect("D360-54-36");
            _animator.Play("man_rotate");
            // first frame = up
            // make sure to not jump to a different direction
            _animator.SetFrame((_direction + 3) % 4);
        }

        private void TickRotate(double time)
        {
            // speed up the rotation speed
            var rotateSpeed = Math.Clamp((RotateTime - (float)time) / (RotateTime * 0.65f), 0, 1);
            _animator.SpeedMultiplier = rotateSpeed * 3 + 1;
        }

        private void EndRotate()
        {
            _pushRepel = false;
            EntityPosition.Set(new Vector2(EntityPosition.X, EntityPosition.Y - 4));

            _animator.SpeedMultiplier = 1;
            _aiComponent.ChangeState("explode");
        }

        #endregion

        #region State: Moldorm

        private void InitMoldormSpawn()
        {
            _animator.Play("meldorm_spawn");
        }

        private void UpdateMoldormSpawn()
        {
            if (!_animator.IsPlaying)
                _aiComponent.ChangeState("moldorm");
        }

        private void InitMoldorm()
        {
            _pushRepel = true;
            _body.CollisionTypes = Values.CollisionTypes.Normal;
            _drawMoldormTail = true;
            _damageField.IsActive = true;

            for (var i = 0; i < _moldormPositions.Length; i++)
                _moldormPositions[i] = EntityPosition.Position;

            for (var i = 0; i < _moldormTails.Length; i++)
            {
                string animationId;
                if (i == _moldormTails.Length - 1)
                    animationId = "moldorm_tail";
                else if (i == _moldormTails.Length - 2)
                    animationId = "moldorm_body_2";
                else
                    animationId = "moldorm_body";

                _moldormTails[i] = new BossFinalBossMoldormTail(Map, this, animationId, i == _moldormTails.Length - 1);
                Map.Objects.SpawnObject(_moldormTails[i]);
            }
        }

        private void UpdateMoldormTail()
        {
            // blinking tail
            _moldormTails[3].Sprite.SpriteShader = Game1.TotalGameTime % (AiDamageState.BlinkTime * 2) < AiDamageState.BlinkTime ? Resources.DamageSpriteShader0 : null;
        }

        private void UpdateMoldorm()
        {
            UpdateMoldormTail();

            // change the direction?
            _moldormChangeCounter -= Game1.DeltaTime;
            if (_moldormChangeCounter < 0)
            {
                _moldormChangeCounter += Game1.RandomNumber.Next(500, 2000);
                _moldormDirection = -_moldormDirection;
            }

            _moldormRadiant += Game1.TimeMultiplier * 0.065f * _moldormDirection;

            _moldormSpeed = _moldormSpeedUp.State ? MoldormSpeedNormal : MoldormSpeedFast;
            if (_moldormHit)
            {
                _moldormSpeed = 0;
                UpdateMoldormTail(EntityPosition);
            }
            else
            {
                _moldormSoundCounter -= Game1.DeltaTime * _moldormSpeed;
                if (_moldormSoundCounter < 0)
                {
                    _moldormSoundCounter += 250;
                    Game1.AudioManager.PlaySoundEffect("D360-56-38");
                }
            }

            _body.VelocityTarget = new Vector2(MathF.Sin(_moldormRadiant), MathF.Cos(_moldormRadiant)) * _moldormSpeed;

            _directionChangeMultiplier = AnimationHelper.MoveToTarget(_directionChangeMultiplier, 1, 0.1f * Game1.TimeMultiplier);
        }

        private void InitMoldormDying()
        {
            _body.VelocityTarget = Vector2.Zero;
        }

        private void UpdateMoldormDying()
        {
            UpdateMoldormTail();

            _moldormRadiant += Game1.TimeMultiplier * 0.065f * _moldormDirection;

            UpdateMoldormTail(EntityPosition);
        }

        private void MoldormExplode()
        {
            _aiComponent.ChangeState("moldormDespawn");
            _animator.Play("slime_despawn");

            _drawMoldormTail = false;

            for (var i = 0; i < _moldormTails.Length; i++)
            {
                if (_moldormTails[i] != null)
                {
                    Map.Objects.DeleteObjects.Add(_moldormTails[i]);
                    _moldormTails[i] = null;
                }
            }
            _pushRepel = false;
            _damageField.IsActive = false;
            _bodyShadow.IsActive = false;
            _drawComponent.Layer = Values.LayerBottom;

            Game1.AudioManager.PlaySoundEffect("D378-55-37");

            ExplosionParticle();
        }

        private void UpdateMoldormDespawn()
        {
            if (!_animator.IsPlaying)
                _aiComponent.ChangeState("move");
        }

        private void OnUpdatePosition(CPosition position)
        {
            if (_aiComponent.CurrentStateId == "moldorm")
                UpdateMoldormTail(position);
        }

        private void UpdateMoldormTail(CPosition position)
        {
            // set the rotation to be 0 < rotation < Pi * 2 to allow for correct calculations
            while (_moldormRadiant < MathF.PI * 2)
                _moldormRadiant += MathF.PI * 2;
            _moldormRadiant %= MathF.PI * 2;

            var radiantIndex = (int)(((_moldormRadiant + MathF.PI / 8) / (2 * MathF.PI)) * 8) % 8;
            _animator.Play("moldorm_head_" + (8 - radiantIndex) % 8);

            _moldormPositions[0] = position.Position;

            if (!_aiDamageState.IsInDamageState())
            {
                if (_moldormHit)
                {
                    _moldormHit = false;
                    _moldormSpeedUp.Reset();
                }

                _partDist = new float[] { 12, 12, 12, 10 };
            }
            else
            {
                for (int i = 0; i < _partDist.Length; i++)
                {
                    if (_partDist[i] > 0)
                    {
                        _partDist[i] -= Game1.TimeMultiplier * 1.75f;
                        if (_partDist[i] < 0)
                            _partDist[i] = 0;
                        break;
                    }
                }
            }

            var partPos = 0f;
            var partIndex = 0;
            var targetDist = _partDist[0] / TailMult;

            for (int i = 1; i < _moldormPositions.Length; i++)
            {
                // this loop is only used to make sure to not have and endless loop incase of a problem with the code below
                while (partIndex + 1 < _moldormPositions.Length)
                {
                    var dist = (_moldormPositions[partIndex + 1] - _moldormPositions[partIndex]).Length();
                    if (dist - partPos >= targetDist)
                    {
                        var percentage = dist > 0 ? (dist - partPos - targetDist) / dist : 1;
                        var newPosition = Vector2.Lerp(_moldormPositions[partIndex + 1], _moldormPositions[partIndex], percentage);
                        partPos += targetDist;
                        if (i / TailMult < _partDist.Length)
                            targetDist = _partDist[i / TailMult] / TailMult;

                        _moldormPositionsNew[i] = newPosition;

                        break;
                    }
                    else
                    {
                        partIndex++;
                        targetDist -= (dist - partPos);
                        partPos = 0;
                    }
                }

                // the tail is not expaneded
                if (partIndex + 1 >= _moldormPositions.Length)
                    _moldormPositionsNew[i] = _moldormPositions[_moldormPositions.Length - 1];
            }

            for (int i = 1; i < _moldormPositions.Length; i++)
                _moldormPositions[i] = _moldormPositionsNew[i];

            // set the newly calculated positions
            for (var i = 0; i < _moldormTails.Length; i++)
                _moldormTails[i].EntityPosition.Set(_moldormPositions[(i + 1) * TailMult]);
        }

        #endregion

        #region State: Lanmola

        private void InitLanmolaDespawn()
        {
            _damageField.IsActive = false;
            _animator.Play("slime_despawn");
        }

        private void UpdateLanmolaDespawn()
        {
            // TODO: delay
            if (!_animator.IsPlaying)
                _aiComponent.ChangeState("finalSpawn");
        }

        private void InitLanmolaHidden()
        {
            _animator.Play("face_hidden");
        }

        private void InitLanmolaExplose()
        {
            Game1.AudioManager.StopAllMusic(20);
            Game1.AudioManager.PlaySoundEffect("D370-16-10");
            _body.VelocityTarget = Vector2.Zero;
            ExplodeAnimation();
        }

        private void InitLanmolaMove()
        {
            Game1.AudioManager.PlaySoundEffect("D360-53-35");
        }

        private void UpdateLanmolaMove()
        {
            var targetPosition = new Vector2(_body.FieldRectangle.X + 80, _body.FieldRectangle.Y + 40);
            var direction = targetPosition - EntityPosition.Position;
            var moveSpeed = 1.25f;

            if (direction.Length() > moveSpeed * Game1.TimeMultiplier)
            {
                direction.Normalize();
                _body.VelocityTarget = AnimationHelper.MoveToTarget(_body.VelocityTarget, direction * moveSpeed, 0.075f * Game1.TimeMultiplier);
            }
            else
            {
                _body.VelocityTarget = Vector2.Zero;
                _aiComponent.ChangeState("faceHidden");
            }
        }

        private void InitLanmola()
        {
            _damageField.IsActive = true;
            _damageField.CollisionBox = new CBox(EntityPosition, -6, -6, 0, 12, 12, 8);
            _hittableComponent.HittableBox = new CBox(EntityPosition, -6, -6, 0, 12, 12, 8);
            _drawComponent.Layer = Values.LayerPlayer;
        }

        private void UpdateLanmola()
        {
            // spawn paticles
            _lanmolaParticleCounter -= Game1.DeltaTime;
            if (_lanmolaParticleCounter < 0)
            {
                _lanmolaParticleCounter += 125;
                var objParticle = new ObjAnimator(Map, (int)EntityPosition.X, (int)EntityPosition.Y, Values.LayerBottom, "Nightmares/nightmare particle", "face_particle", true);
                Map.Objects.SpawnObject(objParticle);
            }

            // move towards the player
            var playerDirection = MapManager.ObjLink.Position - new Vector2(EntityPosition.Position.X, EntityPosition.Position.Y + 4);
            if (playerDirection != Vector2.Zero)
            {
                playerDirection.Normalize();
                _body.VelocityTarget = AnimationHelper.MoveToTarget(_body.VelocityTarget, playerDirection * 1.5f, Game1.TimeMultiplier * 0.035f);
            }
        }

        #endregion

        #region State: Shadow of Ganon

        public void CatchWeapon()
        {
            _body.VelocityTarget = Vector2.Zero;

            Game1.AudioManager.PlaySoundEffect("D378-25-19");

            if (_ganonLives > 0)
                _aiComponent.ChangeState("ganonCatchWeapon");
        }

        private void InitGanonDeath()
        {
            _ganonForm = false;
            _animator.Pause();
            _animatorWeapon.Pause();

            Game1.AudioManager.PlaySoundEffect("D370-16-10");
        }

        private void InitGanonExplode()
        {
            _drawGanonWeapon = false;
            EntityPosition.Set(new Vector2(EntityPosition.X, EntityPosition.Y - 4));

            Game1.AudioManager.PlaySoundEffect("D378-55-37");
            ExplodeAnimation();
        }

        private void TickGanonDamage(double counter)
        {
            var time = _ganonDeathTime - counter;
            _sprite.SpriteShader = time % (AiDamageState.BlinkTime * 2) < AiDamageState.BlinkTime ? Resources.DamageSpriteShader0 : null;
        }

        private void UpdateGanonWait()
        {
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            var dir = playerDirection.X < 0 ? -1 : 1;

            _animator.Play("ganon_" + dir);
            _animatorWeapon.Play("ganon_" + dir);
        }

        private void InitGanonCatchWeapon()
        {
            _drawGanonWeapon = true;

            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            var dir = playerDirection.X < 0 ? -1 : 1;

            _animator.Play("ganon_weapon_spawn_" + dir);
            _animatorWeapon.Play("ganon_weapon_spawn_" + dir);
        }

        private void UpdateGanonCatchWeapon()
        {
            if (!_animatorWeapon.IsPlaying)
                _aiComponent.ChangeState("ganonWait");
        }

        private void InitGanonMove()
        {
            _ganonTargetPosition = RandomRoomPosition();
        }

        private void UpdateGanonMove()
        {
            var moveDirection = _ganonTargetPosition - EntityPosition.Position;
            if (moveDirection.Length() > 8)
            {
                if (moveDirection != Vector2.Zero)
                    moveDirection.Normalize();

                _body.VelocityTarget = AnimationHelper.MoveToTarget(_body.VelocityTarget, moveDirection * 1.5f, 0.075f * Game1.TimeMultiplier);

                _direction = moveDirection.X < 0 ? -1 : 1;
                _animator.Play("ganon_" + _direction);
            }
            else
            {
                _body.VelocityTarget = Vector2.Zero;
            }
        }

        private void InitGanonPostThrow()
        {
            _drawGanonWeapon = false;
            _animator.Play("ganon_throw_" + _direction);
        }

        private void InitGanonThrow()
        {
            _drawGanonWeapon = true;
        }

        private void UpdateGanonThrow()
        {
            UpdateDirection();
            _animator.Play("ganon_swing_up_" + _direction);
            _animatorWeapon.Play("ganon_swing_up_" + _direction);
        }

        private void ThrowWeapon()
        {
            _aiComponent.ChangeState("ganonPostThrow");

            var position = EntityPosition.Position - new Vector2(-_direction * 12, 22);
            var objWeapon = new BossFinalBossWeapon(Map, this, (int)position.X, (int)position.Y, _direction);
            Map.Objects.SpawnObject(objWeapon);
        }

        private void UpdateDirection()
        {
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            _direction = playerDirection.X < 0 ? -1 : 1;
        }

        private void InitGanonBats()
        {
            _batIndex = 0;
            _batCounter = 550 - 350;

            UpdateDirection();

            _batIndexStart = _direction < 0 ? 3 : 0;

            _animator.Play("ganon_swing_" + _direction);
            _animatorWeapon.Play("ganon_swing_" + _direction);
        }

        private void UpdateGanonBats()
        {
            _batCounter += Game1.DeltaTime;
            if (_batCounter > 550)
            {
                _batIndex++;
                _batCounter -= 550;

                if (_batIndex > 8)
                {
                    _aiComponent.ChangeState("ganonThrow");
                    return;
                }
                if (_batIndex >= 7)
                    return;

                var radians = (_batIndexStart + _batIndex - 1) / 3f * MathF.PI;
                var position = EntityPosition.Position - new Vector2(_direction * 9, 26) + new Vector2(MathF.Cos(radians), _direction * -MathF.Sin(radians)) * 24;
                var objBat = new BossFinalBossBat(Map, this, (int)position.X, (int)position.Y);
                Map.Objects.SpawnObject(objBat);
            }
        }

        private void InitGanonSpawn()
        {
            _ganonForm = true;

            _pushRepel = true;
            _damageField.IsActive = true;
            _damageField.CollisionBox = new CBox(EntityPosition, -12, 7 - 24, 24, 24, 8);
            _hittableComponent.HittableBox = new CBox(EntityPosition, -12, 7 - 24, 24, 24, 8);

            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            var dir = playerDirection.X < 0 ? -1 : 1;

            Game1.AudioManager.PlaySoundEffect("D370-35-23");

            _animator.Play("ganon_spawn_" + dir);
        }

        private void UpdateGanonSpawn()
        {
            if (!_animator.IsPlaying)
                _aiComponent.ChangeState("ganonPreSpawnWeapon");
        }

        private void UpdateGanonPreSpawnWeapon()
        {
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            var dir = playerDirection.X < 0 ? -1 : 1;

            _animator.Play("ganon_weapon_" + dir);
        }

        private void InitGanonSpawnWeapon()
        {
            _drawGanonWeapon = true;

            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            var dir = playerDirection.X < 0 ? -1 : 1;

            Game1.AudioManager.PlaySoundEffect("D360-57-39");

            _animator.Play("ganon_weapon_spawn_" + dir);
            _animatorWeapon.Play("ganon_weapon_spawn_" + dir);
        }

        private void UpdateGanonSpawnWeapon()
        {
            if (!_animatorWeapon.IsPlaying)
                _aiComponent.ChangeState("ganon");
        }

        private void InitGanon()
        {
        }

        private void UpdateGanon()
        {
            var playerDirection = MapManager.ObjLink.Position - EntityPosition.Position;
            var dir = playerDirection.X < 0 ? -1 : 1;

            _animator.Play("ganon_" + dir);
            _animatorWeapon.Play("ganon_" + dir);
        }

        #endregion

        #region State: DethI

        private void InitFinalDeath()
        {
            _sprite.SpriteShader = null;

            Game1.AudioManager.PlaySoundEffect("D370-16-10");
            Game1.AudioManager.PlaySoundEffect("D378-60-3D");
        }

        private void UpdateFinalDeath()
        {
            // spawn the parts
            _dethIPartCounter -= Game1.DeltaTime;

            // despawn?
            if (_dethIPartCounter < -300)
                FinalExplose();

            for (var i = 0; i < 4; i++)
            {
                if (i * 300 >= _dethIPartCounter)
                {
                    _dethIParts[i].SetActive(false);
                    _dethIParts[i + 4].SetActive(false);
                }
            }
        }

        private void FinalExplose()
        {
            if (!string.IsNullOrEmpty(_saveKey))
                Game1.GameManager.SaveManager.SetString(_saveKey, "1");

            Map.Objects.DeleteObjects.Add(this);

            ExplodeAnimation();
        }

        private void InitFinalBlink()
        {
            Game1.AudioManager.SetMusic(93, 2);

            Game1.AudioManager.StopSoundEffect("D360-61-3D");
            Game1.AudioManager.PlaySoundEffect("D370-16-10");

            // hack to not allow anymore attacks
            _body.VelocityTarget = Vector2.Zero;
            Game1.GameManager.StartDialog("nightmareFinal0");

            // deactivate the damge fields
            _damageField.IsActive = false;
            for (var i = 0; i < 8; i++)
                _dethIParts[i].DeactivateDamageField();
        }

        private void TickFinalDespawn(double counter)
        {
            var time = _dethIStateDeathCounter - counter;
            _sprite.SpriteShader = time % (AiDamageState.BlinkTime * 2) < AiDamageState.BlinkTime ? Resources.DamageSpriteShader0 : null;
        }

        private void InitFinalSpawn()
        {
            Game1.AudioManager.PlaySoundEffect("D370-35-23");
            Game1.AudioManager.SetMusic(79, 2);

            _targetPosition = EntityPosition.Position;
            _animator.Play("final_spawn");
            _damageField.IsActive = true;
            _damageField.CollisionBox = new CBox(EntityPosition, -8, -12, 16, 16, 8);
            _hittableComponent.HittableBox = new CBox(EntityPosition, -6, -8, 12, 14, 8);
        }

        private void UpdateFianalSpawn()
        {
            if (!_animator.IsPlaying)
                _aiComponent.ChangeState("final");
        }

        private Vector2 RandomRoomPositionFinal()
        {
            var posY = 0;
            // make it more likeley to move around at the top
            if (Game1.RandomNumber.Next(1, 10) <= 6)
                posY = Game1.RandomNumber.Next(8, 6 * 8);
            else if (Game1.RandomNumber.Next(1, 10) <= 6)
                posY = Game1.RandomNumber.Next(8, 9 * 8);

            int posX;
            if (16 < posY || posY < 64 - 16)
                posX = Game1.RandomNumber.Next(16, 6 * 16);
            else
                posX = Game1.RandomNumber.Next(0, 8 * 16);

            return new Vector2(_roomRectangle.X + 24 + posX, _roomRectangle.Y + 24 + posY);
        }

        private void InitFinal()
        {
            _dethIState = true;
            _animator.Play("final");
            _hittableComponent.BoomerangMultiplier = true;
        }

        private void UpdateFinal()
        {
            _animatorEye.Update();

            Game1.AudioManager.PlaySoundEffect("D360-61-3D", false);

            // move to the target position
            var distance = _targetPosition - EntityPosition.Position;
            if (distance.Length() > 4)
            {
                distance.Normalize();
                _body.VelocityTarget = AnimationHelper.MoveToTarget(_body.VelocityTarget, distance * 0.5f, 0.0125f * Game1.TimeMultiplier);
            }
            else
            {
                // generate new target position
                _targetPosition = RandomRoomPositionFinal();
            }

            if (!_animatorEye.IsPlaying)
                _dethIEyeCounter -= Game1.DeltaTime;

            if (_dethIEyeCounter <= 0)
            {
                // open the eye
                if (_dethIEyeState == 0)
                {
                    _dethIEyeState = 1;
                    _dethIEyeCounter += 1250 + Game1.RandomNumber.Next(750);
                    _animatorEye.Play("eye_open");
                }
                // close the eye
                else if (_dethIEyeState == 1)
                {
                    _dethIEyeState = 0;
                    _dethIEyeCounter += 2500 + Game1.RandomNumber.Next(2500);
                    _animatorEye.Play("eye_close");
                }
            }

            // spawn the parts
            if (_dethIPartCounter < 4 * 300)
                _dethIPartCounter += Game1.DeltaTime;

            if (_dethIPartCounter > 300)
            {
                _dethIPart0 += Game1.DeltaTime * _dethIPartSpeed0;
                _dethIPart1 += Game1.DeltaTime * _dethIPartSpeed1;

                var directionPart0 = new Vector2(MathF.Cos(_dethIPart0), MathF.Sin(_dethIPart0));
                SetFinalPartsPosition(directionPart0, 0);
                var directionPart1 = new Vector2(-MathF.Cos(_dethIPart1), MathF.Sin(_dethIPart1));
                SetFinalPartsPosition(directionPart1, 1);
            }
        }

        private void DrawLight(SpriteBatch spriteBatch)
        {
            if (!GameSettings.ObjectLights || !light_source || !_dethIState)
                return;

            int size;

            // Opening: 3 frames ramping up. Holds at full once the animation stops.
            if (_dethIEyeState == 1)
            {
                size = _animatorEye.IsPlaying
                    ? _animatorEye.CurrentFrameIndex switch
                    {
                        0 => (int)(light_size * 0.80f),
                        1 => (int)(light_size * 0.90f),
                        _ => light_size
                    }
                    : light_size;
            }
            // Closing: 2 frames ramping down. Nothing left once the animation stops.
            else if (_animatorEye.IsPlaying)
            {
                size = _animatorEye.CurrentFrameIndex switch
                {
                    0 => (int)(light_size * 0.90f),
                    _ => (int)(light_size * 0.80f)
                };
            }
            else
            {
                return;
            }

            if (size <= 0)
                return;

            var lightRectangle = new Rectangle(
                (int)EntityPosition.X - size / 2,
                (int)EntityPosition.Y - size / 2,
                size, size);

            spriteBatch.Draw(Resources.SprLight, lightRectangle, new Color(light_red, light_grn, light_blu) * light_bright);
        }

        private void SetFinalPartsPosition(Vector2 direction, int index)
        {
            var position = new Vector2(EntityPosition.X, EntityPosition.Y - 5);

            for (var i = 0; i < 4; i++)
            {
                if ((i + 1) * 300 > _dethIPartCounter)
                    break;

                _dethIParts[i + index * 4].SetActive(true);

                position += direction * _dethIPartDistance[i];
                _dethIParts[i + index * 4].EntityPosition.Set(position);
            }
        }

        #endregion

        #region State: Explode

        private void InitExplode()
        {
            _pushRepel = false;
            Game1.AudioManager.PlaySoundEffect("D378-55-37");
            ExplodeAnimation();
        }

        private void ExplodeAnimation()
        {
            _animator.Play("head");

            _damageField.IsActive = false;
            _bodyShadow.IsActive = false;
            _drawComponent.Layer = Values.LayerBottom;

            ExplosionParticle();
        }

        private void ExplosionParticle()
        {
            for (int i = 0; i < 8; i++)
            {
                var radiant = i / 4f * MathF.PI;
                var velocity = new Vector2(MathF.Sin(radiant), MathF.Cos(radiant)) * 2.5f;
                var objParticle0 = new BossFinalBossParticle(Map, new Vector2(EntityPosition.X, EntityPosition.Y - EntityPosition.Z), velocity);
                Map.Objects.SpawnObject(objParticle0);
            }
        }

        private void InitDespawn()
        {
            _animator.Play("slime_despawn");
        }

        private void UpdateDepawn()
        {
            if (!_animator.IsPlaying)
                _aiComponent.ChangeState("move");
        }

        private void InitMove()
        {
            Game1.AudioManager.PlaySoundEffect("D360-53-35");
            _body.CollisionTypes = Values.CollisionTypes.None;
        }

        private void UpdateMove()
        {
            var direction = new Vector2(_roomRectangle.Center.X, _roomRectangle.Y + 43) - EntityPosition.Position;

            if (direction.Length() < 2)
            {
                _body.CollisionTypes = Values.CollisionTypes.Normal;
                _aiComponent.ChangeState("moveWait");
            }
            else
            {
                direction.Normalize();
                var targetVelocity = AnimationHelper.MoveToTarget(new Vector2(_body.Velocity.X, _body.Velocity.Y), direction, 0.15f * Game1.TimeMultiplier);
                _body.Velocity.X = targetVelocity.X;
                _body.Velocity.Y = targetVelocity.Y;
            }
        }

        private void EndMoveWait()
        {
            if (_moveCounter == 0)
                _aiComponent.ChangeState("moldormSpawn");
            else if (_moveCounter == 1)
                _aiComponent.ChangeState("ganonSpawn");

            _moveCounter++;
        }

        #endregion

        public bool HitBoss(GameObject origin, Vector2 direction, CBox damageBox)
        {
            //------------------------------------------------------------------------------
            // Agahnim's Shadow
            //------------------------------------------------------------------------------
            // An energy ball was reflected back at the boss.
            if (!_aiDamageState.IsInDamageState() && _aiComponent.CurrentStateId == "manPostAttack" && damageBox.Box.Intersects(_hittableBoxMan.Box))
            {
                // Show the boss taking a hit.
                _aiDamageState.OnHit(origin, direction, HitType.Boss, 1, false);

                // Reduce the bosses lives by 1.
                if (GameSettings.ChOneHitKills)
                    _agahnimLives -= 1000;
                else
                    _agahnimLives--;

                // Death is determined by lives direction rather than damage state.
                if (_agahnimLives <= 0)
                    _aiComponent.ChangeState("manRotate");

                // Return that a hit was taken.
                return true;
            }
            return false;
        }

        private void OnZeroLives(bool pieceOfPower) { }

        private Vector2 RandomRoomPositionSide()
        {
            // always change the side
            _sideIndex = (_sideIndex + Game1.RandomNumber.Next(1, 4)) % 4;
            if (_sideIndex == 0)
                return new Vector2(_roomRectangle.X + 16 + 16, _roomRectangle.Y + 32 + 16 + Game1.RandomNumber.Next(0, 2 * 16));
            else if (_sideIndex == 2)
                return new Vector2(_roomRectangle.X + 128, _roomRectangle.Y + 32 + 16 + Game1.RandomNumber.Next(0, 2 * 16));
            else if (_sideIndex == 1)
                return new Vector2(_roomRectangle.X + 32 + 16 + Game1.RandomNumber.Next(0, 4 * 16), _roomRectangle.Y + 32);
            else
                return new Vector2(_roomRectangle.X + 32 + 16 + Game1.RandomNumber.Next(0, 4 * 16), _roomRectangle.Y + 100);
        }

        private Vector2 RandomRoomPosition()
        {
            var posY = Game1.RandomNumber.Next(0, 5 * 16);
            int posX;
            if (16 < posY || posY < 64 - 16)
                posX = Game1.RandomNumber.Next(16, 6 * 16);
            else
                posX = Game1.RandomNumber.Next(0, 8 * 16);

            return new Vector2(_roomRectangle.X + 24 + posX, _roomRectangle.Y + 24 + posY);
        }

        private void Update()
        {
            _animatorBody.Update();

            if (_drawGanonWeapon)
                _animatorWeapon.Update();
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            // draw the tail
            if (_drawMoldormTail)
                for (var i = _moldormTails.Length - 1; i >= 0; i--)
                {
                    if (i != _moldormTails.Length - 1)
                        _moldormTails[i].Sprite.SpriteShader = _sprite.SpriteShader;
                    _moldormTails[i].Sprite.Draw(spriteBatch);
                }

            // draw the body parts
            if (!_hideBody)
                for (var i = 0; i < _bodyParts.Length; i++)
                    DrawHelper.DrawNormalized(spriteBatch, _spriteBody, _bodyParts[i], Color.White);

            if (!_hideBody)
                _animatorBody.Draw(spriteBatch, _bodyPosition, Color.White);

            if (_sprite.SpriteShader != null)
            {
                spriteBatch.End();
                ObjectManager.SpriteBatchBegin(spriteBatch, _sprite.SpriteShader);
            }

            // draw the weapon of ganon
            if (_drawGanonWeapon)
                _animatorWeapon.Draw(spriteBatch, EntityPosition.Position, Color.White);

            if (!_hideHead)
                _sprite.Draw(spriteBatch);

            if (_sprite.SpriteShader != null)
            {
                spriteBatch.End();
                ObjectManager.SpriteBatchBegin(spriteBatch, _sprite.SpriteShader);
            }

            if (_dethIState && (_dethIEyeState == 1 || (_dethIEyeState == 0 && _animatorEye.IsPlaying)))
                _animatorEye.Draw(spriteBatch, new Vector2(EntityPosition.X, EntityPosition.Y + 1), Color.White);

            if (_sprite.SpriteShader != null)
            {
                spriteBatch.End();
                ObjectManager.SpriteBatchBegin(spriteBatch, null);
            }
        }

        public Values.HitCollision OnHit(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            //------------------------------------------------------------------------------
            // Giant Zol
            //------------------------------------------------------------------------------
            if (_giantZolForm && _aiComponent.CurrentStateId == "slimeJump" || _aiComponent.CurrentStateId == "slimeWait")
            {
                // The blob only takes damage from Magic Poweder.
                if ((hitType & HitType.MagicPowder) != 0)
                {
                    _body.Velocity.X = direction.X;
                    _body.Velocity.Y = direction.Y;
                    if (_body.Velocity.Z > 0)
                        _body.Velocity.Z = 0;

                    // Change it to damaged state which jiggles the slime.
                    _aiComponent.ChangeState("slimeDamaged");

                    // Kill in one shot with cheat or subtract a hit point.
                    if (GameSettings.ChOneHitKills)
                        _giantZolLives -= 1000;
                    else
                        _giantZolLives--;

                    return Values.HitCollision.Enemy;
                }
                // Sword hits cause the blob to disperse and deal no damage.
                if ((hitType & (HitType.Sword | HitType.SwordHold)) != 0)
                {
                    _aiComponent.ChangeState("slimeHideExplode");
                    return Values.HitCollision.Enemy;
                }
            }
            //------------------------------------------------------------------------------
            // Moldorm
            //------------------------------------------------------------------------------
            if (_aiComponent.CurrentStateId == "moldorm")
            {
                // Hitting Moldorm's tail is handled elsewhere. If hit directly repel the attack.
                return Values.HitCollision.RepellingParticle;
            }
            //------------------------------------------------------------------------------
            // Shadow of Ganon
            //------------------------------------------------------------------------------
            if (_ganonForm && !_aiDamageState.IsInDamageState() && (((hitType & HitType.PegasusBootsSword) != 0) || (hitType & HitType.SwordSpin) != 0))
            {
                // Subtract the hit points from the boss.
                _ganonLives -= damage;

                // Enter the death state when lives are fully removed.
                if (_ganonLives <= 0)
                    _aiComponent.ChangeState("ganonDeath");

                // Play the death sound and put boss in damage state.
                Game1.AudioManager.PlaySoundEffect("D370-07-07");
                _aiDamageState.SetDamageState();

                // Knock the player back when dealing a hit.
                return Values.HitCollision.Repelling;
            }
            // If it's any other attack type then repel the attack.
            if (_ganonForm && (hitType & HitType.Sword) != 0)
            {
                return Values.HitCollision.Repelling;
            }
            //------------------------------------------------------------------------------
            // Lanmola
            //------------------------------------------------------------------------------
            if (_aiComponent.CurrentStateId == "face" && !_aiDamageState.IsInDamageState())
            {
                // Only certain hit types will defeat it.
                if ((hitType & (HitType.Hookshot | HitType.MagicRod | HitType.Bomb | HitType.Boomerang | HitType.Bow | HitType.SwordSpin | HitType.PegasusBootsSword)) != 0)
                {
                    _aiDamageState.SetDamageState();
                    _aiComponent.ChangeState("faceExplode");
                    return Values.HitCollision.Enemy;
                }
                // If it's a hit type that does not work, repel the player.
                else
                {
                    _aiDamageState.SetDamageState(false);

                    // The direction the player is knocked back.
                    _body.Velocity.X += direction.X;
                    _body.Velocity.Y += direction.Y;

                    // Play either piece of power hit or the "bump" sound effect.
                    if (pieceOfPower)
                        Game1.AudioManager.PlaySoundEffect("D370-17-11");
                    else
                        Game1.AudioManager.PlaySoundEffect("D360-09-09");

                    // Knock the player back.
                    return Values.HitCollision.Repelling | Values.HitCollision.Repelling1;
                }
            }
            //------------------------------------------------------------------------------
            // DethI
            //------------------------------------------------------------------------------
            if (_aiComponent.CurrentStateId == "final")
            {
                // bow hit from below with an open eye
                if (!_aiDamageState.IsInDamageState() && 
                    ((_dethIEyeState == 0 && _animatorEye.CurrentFrameIndex < 1) || (_dethIEyeState == 1 && _animatorEye.CurrentFrameIndex >= 1)) && 
                    ((hitType & HitType.Bow) != 0 || (hitType & HitType.Boomerang) != 0 || (hitType & HitType.Bomb) != 0) &&
                    MathF.Abs(direction.Y) > MathF.Abs(direction.X) && direction.Y < 0)
                {
                    _aiDamageState.SetDamageState();

                    // One hit kills or the boomerang destroys Dethl in one shot, but the way they do it differs. One shot kill = dead, 
                    // while Boomerang works by multiplying the damage dealt. But if the Boss's lives are modified it won't kill in one hit.
                    if (GameSettings.ChOneHitKills)
                        _dethILives -= 1000;
                    else
                        _dethILives -= damage;

                    // When the boss is destroyed, if the player is low health disable the beep.
                    if (_dethILives <= 0)
                    {
                        MapManager.ObjLink.ToggleLowHealthBeep(false);
                        _aiComponent.ChangeState("finalBlink");

                        // Achievement: If the player didn't take damage, unlock the achievement.
                        if (!DealtPlayerDamage)
                            AchievementManager.Earn(107);
                    }
                    // Play the boss hit sound effect.
                    Game1.AudioManager.PlaySoundEffect("D370-07-07");

                    // Randomly change the speed of the two parts.
                    _dethIPartSpeed0 = (1 / 2500.0f * MathF.PI * 2) * (1 + (Game1.RandomNumber.Next(0, 101) - 50) / 500f);
                    _dethIPartSpeed1 = (1 / 2500.0f * MathF.PI * 2) * (1 + (Game1.RandomNumber.Next(0, 101) - 50) / 500f);

                    return Values.HitCollision.Enemy;
                }
                else
                {
                    return Values.HitCollision.RepellingParticle;
                }
            }
            return Values.HitCollision.None;
        }

        public Values.HitCollision HitTail(GameObject gameObject, Vector2 direction, HitType hitType, int damage, bool pieceOfPower)
        {
            //------------------------------------------------------------------------------
            // Moldorm
            //------------------------------------------------------------------------------
            // If it's the Moldorm nightmare only sword is valid.
            if (_aiComponent.CurrentStateId == "moldorm" && !_aiDamageState.IsInDamageState() && ((hitType & HitType.Sword) != 0))
            {
                // Defeat in one shot if cheat is enabled.
                if (GameSettings.ChOneHitKills)
                    damage = 1000;

                // Subtract the lives from the Moldorm.
                _moldormLives -= damage;

                // When it runs out of lives change it to dying state.
                if (_moldormLives <= 0)
                    _aiComponent.ChangeState("moldormDying");

                // Play the hit sound and stop its "running" sound effect.
                Game1.AudioManager.PlaySoundEffect("D370-07-07");
                Game1.AudioManager.StopSoundEffect("D360-56-38");

                // Register a hit on the boss.
                _moldormHit = true;
                _aiDamageState.SetDamageState(true);
                return Values.HitCollision.Enemy;
            }
            return Values.HitCollision.None;
        }

        private void OnCollision(Values.BodyCollision collision)
        {
            if (_aiComponent.CurrentStateId == "moldorm")
            {
                if (Game1.RandomNumber.Next(0, 2) == 0)
                    _moldormDirection = -_moldormDirection;

                if ((collision & Values.BodyCollision.Horizontal) != 0)
                    _moldormRadiant = (float)Math.Atan2(-_body.VelocityTarget.X * _directionChangeMultiplier, _body.VelocityTarget.Y);
                else if ((collision & Values.BodyCollision.Vertical) != 0)
                    _moldormRadiant = (float)Math.Atan2(_body.VelocityTarget.X, -_body.VelocityTarget.Y * _directionChangeMultiplier);

                _directionChangeMultiplier *= 0.75f;
            }

            if (_aiComponent.CurrentStateId == "face")
            {
                if ((collision & Values.BodyCollision.Horizontal) != 0)
                {
                    _body.Velocity.X = -_body.VelocityTarget.X * 0.125f;
                    _body.VelocityTarget.X = 0;
                }
                else if ((collision & Values.BodyCollision.Vertical) != 0)
                {
                    _body.Velocity.Y = -_body.VelocityTarget.Y * 0.125f;
                    _body.VelocityTarget.Y = 0;
                }
            }
        }

        private bool OnPush(Vector2 direction, PushableComponent.PushType type)
        {
            if (!_pushRepel)
                return false;

            return true;
        }
    }
}
