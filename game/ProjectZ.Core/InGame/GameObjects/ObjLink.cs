using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectZ.Base;
using ProjectZ.Base.UI;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects.Base;
using ProjectZ.InGame.GameObjects.Base.CObjects;
using ProjectZ.InGame.GameObjects.Base.Components;
using ProjectZ.InGame.GameObjects.Base.Systems;
using ProjectZ.InGame.GameObjects.Dungeon;
using ProjectZ.InGame.GameObjects.Effects;
using ProjectZ.InGame.GameObjects.NPCs;
using ProjectZ.InGame.GameObjects.Things;
using ProjectZ.InGame.GameSystems;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.GameObjects
{
    public partial class ObjLink : GameObject
    {
        public enum State
        {
            Idle, Pushing, Grabbing, Pulling, PreCarrying, Carrying, Throwing, CarryingItem, PickingUp, Falling,
            Attacking, Blocking, BlockPushing, AttackBlocking, Charging, ChargeBlocking, Jumping, AttackJumping, ChargeJumping,
            Ocarina, OcarinaTeleport, Rafting, Pushed,
            FallRotateEntry,
            Drowning, Drowned, Swimming, AttackSwimming, ChargeSwimming,
            Teleporting, MagicRod, Hookshot, Bombing, Powdering, Digging, BootKnockback,
            TeleporterUpWait, TeleporterUp, TeleportFallWait, TeleportFall,
            Dying, InitStunned, Stunned, Knockout,
            SwordShow0, SwordShow1, SwordShow2, SwordShowLv2, SwordShowPhoto,
            ShowInstrumentPart0, ShowInstrumentPart1, ShowInstrumentPart2, ShowInstrumentPart3,
            ShowToadstool,
            CloakShow0, CloakShow1,
            Intro, BedTransition,
            Sequence, FinalInstruments,
            Frozen, FinalStand
        }
        public State CurrentState;

        // Link Animator
        public readonly Animator Animation;
        private int _animationOffsetX = -7;
        private int _animationOffsetY = -16;

        // Weapon Animator
        private Animator AnimatorWeapons;

        // Link Sprite
        private CSprite _sprite;
        public float SpriteTransparency;
        private bool _isVisible;
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                _sprite.IsVisible = value;
            }
        }
        // Link Position
        public Vector2 Position => EntityPosition.Position;
        public Vector2 CenterPosition => new Vector2(EntityPosition.X, EntityPosition.Y - 4);
        public float PosX => EntityPosition.X;
        public float PosY => EntityPosition.Y;
        public float PosZ => EntityPosition.Z;

        // Link Movement
        public bool CanWalk;
        public bool DisableInput;
        private bool _isWalking;
        private bool _forceWalking;
        private float WalkSpeed = 1.0f;
        private float WalkSpeedPoP = 1.25f;
        private float BootsRunningSpeed = 2.0f;
        private float SwimSpeed = 0.5f;
        private float SwimSpeedA = 1.0f;
        private float _currentWalkSpeed;
        private float _waterSoundCounter;

        public Vector2 LastMoveVector;
        private Vector2 _moveVelocity;
        private Vector2 _lastMoveVelocity;
        private Vector2 _lastBaseMoveVelocity;

        // Set by ObjMoveStone while a stone Link is pushing is in motion.
        public float PushingTimer;
        private Box _pushingBox;
        public void NotifyPushing(Box stoneBox)
        {
            PushingTimer = 50;
            _pushingBox = stoneBox;
        }

        // Link Direction
        public int Direction;
        private readonly Vector2[] _walkDirection = 
        { 
            new Vector2(-1, 0), 
            new Vector2(0, -1), 
            new Vector2(1, 0), 
            new Vector2(0, 1) 
        };
        public Vector2 ForwardVector { get => _walkDirection[Direction]; }

        // Link Body
        private BodyComponent _body;
        private BodyDrawComponent _drawBody;
        private BodyDrawShadowComponent _shadowComponent;
        private DrawComponent.DrawTemplate _bodyDrawFunction;
        public Point CollisionBoxSize;
        public BodyComponent Body { get => _body; }
        public RectangleF BodyRectangle => _body.BodyBox.Box.Rectangle();
        public RectangleF PlayerRectangle => new RectangleF(PosX - 4, PosY - 12 - PosZ, 8, 12);

        // Damage State
        public static int BlinkTime = 66;
        public static int CooldownTime = BlinkTime * GameSettings.DmgCooldown;
        private double _hitCount;
        public bool InDamageState => _hitCount > 0;
        public bool IsLowHealth;

        // Holes
        private Point _lastTilePosition;
        private Vector3 _holeResetPosition;
        private Vector3 _altHoleResetPosition;
        private float _holeFallCounter;
        private bool _isFallingIntoHole;
        private double _holeTeleportCounter;
        public int HoleTeleporterId;
        public bool WasHoleReset;
        public bool HoleFalling;
        public string HoleResetRoom;
        public string HoleResetEntryId;

        // Transitions
        public Vector2? MapTransitionStart;
        public Vector2? MapTransitionEnd;
        public Vector2? NextMapPositionStart;
        public Vector2? NextMapPositionEnd;
        public string NextMapPositionId;
        public int DirectionEntry;
        public bool IsTransitioning;
        public bool TransitioningIn;
        public bool TransitioningOut;
        public bool NextMapFallStart;
        public bool NextMapFallRotateStart;
        public bool TransitionOutWalking;
        public bool TransitionInWalking;
        public bool BlackScreenOverride;
        public bool OcarinaDungeonTeleport;
        private double _fallEntryCounter;
        private bool _wasTransitioning;
        private bool _startBedTransition;

        // Classic Camera: Field Properties
        private bool _fieldChangePending;
        public Rectangle CurrentField = Rectangle.Empty;
        public Rectangle PreviousField = Rectangle.Empty;
        public Rectangle ContrastField = Rectangle.Empty;
        public ObjFieldBarrier[] FieldBarrier;
        public bool FieldChange;
        public bool FieldChangeComplete;

        // Classic Field Scrolling
        private int _fieldCrossDirection = -1;
        private bool _wasFieldScrolling;
        private bool _fieldSyncPending;
        private Vector2 _fieldScrollStart;
        private Vector2 _fieldScrollEnd;
        private Vector2 _lastFieldCheckPosition;

        // Classic Field Transition Point
        private int _scrollDirection = 3;

        private void UpdateScrollDirection()
        {
            // When flying with the rooster we need to get it's velocity instead of Link's body velocity.
            var velocity = (_isFlying && _objRooster != null) ? _objRooster.FlyVelocity : _body.VelocityTarget;

            // Use the velocity from above to determine the scroll direction.
            _scrollDirection = (!MapManager.Camera.FieldScrollActive && velocity.LengthSquared() >= 0.0001f)
                ? ToDirection(velocity)
                : _scrollDirection;
        }

        public Vector2 FieldScrollPoint => 
            (!Camera.ClassicMode || !GameSettings.ClassicScrolling || IsTransitioning || PositionControlled)
                ? CenterPosition
                : _scrollDirection switch
                {
                    0 => new Vector2(EntityPosition.X + EntitySize.X + 4, CenterPosition.Y),  // Left
                    1 => new Vector2(CenterPosition.X, EntityPosition.Y + EntitySize.Y + 4),  // Up
                    2 => new Vector2(EntityPosition.X + EntitySize.X + 12, CenterPosition.Y), // Right
                    3 => new Vector2(CenterPosition.X, EntityPosition.Y + EntitySize.Y + 12), // Down
                    _ => CenterPosition
                };

        // Rail Jump (jumping from cliffside)
        private bool _railJump;
        private bool _startedJumping;
        private bool _hasStartedJumping;
        private float _railJumpPositionZ;
        private float _railJumpPercentage;
        private float _railJumpHeight;
        private Vector2 _railJumpStartPosition;
        private Vector2 _railJumpTargetPosition;

        // Followers
        public List<GameObjectFollower> Followers = new List<GameObjectFollower>();
        private ObjCock _objRooster;
        private ObjMarin _objMarin;
        private ObjBowWow _objBowWow;

        // Ghost Stuff
        private ObjGhost _objGhost;
        private bool _spawnGhost;

        // Rooster Stuff
        private float _flyStartZPos;
        private bool _isFlying;
        private bool _wasFlying;
        private float _flyingSpeed = 0.5f;

        // Egg Follower Turnaround
        bool _eggPreventStart;
        float _eggPreventTimer;

        // Trapped State
        private int _trapInteractionCount;
        private bool _isTrapped;
        private bool _trappedDisableItems;

        // Sword 
        private bool _isHoldingSword;
        private bool _pickingUpSword;
        private Vector2 _hitVelocity;
        private Vector2 _repelVelocity;
        private Vector2 _knockBackVelocity;
        public Box SwordDamageBox;
        public Box SwordClassicBox;
        public CBox DamageCollider;
        public bool CarrySword;
        public bool IsHoldingSword => _isHoldingSword;

        private double _hitRepelTime;
        private double _hitParticleTime;
        private float _baseRepelStrength = 2.45f;
        private float _swimRepelStrength = 2.25f;
        private float _repelCancelFactor = 0.12f;

        private bool _stopCharging;
        private float _swordChargeCounter = 100;
        private bool _isSwordSpinning;
        private bool _isSwordSpinAttack;

        private Point[] _pokeAnimationOffset;
        private bool _pokeStart;
        private bool _swordPoked;
        private bool _pokeRepelFix;
        private float _swordPokeTime = 100;
        private float _swordPokeCounter;
        private float _pokeRepelTimer;
        public bool IsPoking;

        // Sword Level 2
        private float _showSwordLv2Counter;
        private float _showSwordL2ParticleCounter;
        private bool _shownSwordLv2Dialog;

        // Sword Shot
        private Vector2[] _shootSwordOffset;
        private bool _shotSword;
        private int _beamDirection;

        // Sword NPC Avoidance
        private Vector2 _avoidanceStartPos;
        private int _avoidanceDirection;
        private bool _npcSwordCross;
        private bool _npcCrossSword;

        // Shield
        public Vector2 ShieldVelocity 
        { 
            get => _shieldVelocity; 
            set => _shieldVelocity = value; 
        }
        private Vector2 _shieldVelocity;
        private float _shieldNudgeScale = 1.0f;
        private float _shieldCancelFactor = 0.05f;
        private bool _wasBlocking;
        private bool _blockButton;
        public Box ShieldBlockBox;
        public bool CarryShield;

        // Items: Pickup / Show
        public GameItem ShowItem;
        private Vector2 _showItemOffset;
        private GameItemCollected _collectedShowItem;
        private string _pickupDialogOverride;
        private string _additionalPickupDialog;
        private double _itemShowCounter;
        private bool _showItem;
        private bool _savedPreItemPickup;
        private bool _itemPickupWasCarrying;
        private bool _pickupWhileSwimming;
        public bool SavePreItemPickup { get => _savedPreItemPickup; }

        // Items: Disable
        public bool DisableItems;
        public float DisableItemCounter;

        // Items: Store Item
        public GameItem StoreItem;
        private int _storeItemWidth;
        private int _storeItemHeight;
        private Vector2 _storePickupPosition;
        private bool _showStealMessage;

        // Magic Powder
        private Vector2[] _powderOffset;

        // Bombs
        public List<ObjBomb> BombList = new List<ObjBomb>();
        private List<GameObject> _destroyableWallList = new List<GameObject>();
        private Vector2[] _bombOffset;

        // Flippers
        public bool HasFlippers;
        private int _lastSwimDirection;
        private Vector2 _swimVelocity;
        private float _swimBoostCount;
        private float _diveCounter;

        // No Flippers: Drowning
        private MapStates.FieldStates _lastFieldState;
        private Vector2 _drownResetPosition;
        private float _drownResetCounter;
        private bool _drownedInLava;

        // Pegasus Boots
        private bool _bootsHolding;
        private bool _bootsButtonHeld;
        private bool _bootsRunning;
        private bool _bootsWasRunning;
        private bool _bootsStop;
        private bool _bootsReset;
        private float _bootsCounter;
        private float _bootsParticleTime = 120f;
        private float _bootsMaxSpeed = 2.0f;
        private int _bootsLastDirection;
        private bool _bootsRunJump;
        private Box _crystalSmashBox;
        public bool BootsRunning => _bootsRunning;
        public bool BootsWasRunning => _bootsWasRunning;

        // Arrows
        private Vector2[] _arrowOffset;

        // Hookshot
        public ObjHookshot Hookshot = new ObjHookshot();
        private Vector2[] _hookshotOffset;
        private bool _hookshotPull;
        private bool _hookshotActive;
        private float _hookshotCounter;
        private float _hookshotCooldown;

        // Boomerang
        public ObjBoomerang Boomerang = new ObjBoomerang();
        public Vector2[] _boomerangOffset;

        // Magic Rod
        private Vector2[] _magicRodOffset;

        // Shovel
        private Vector2[] _shovelOffset;
        private Point _digPosition;
        private bool _hasDug;
        private bool _canDig;

        // Ocarina
        private List<GameObject> _ocarinaList = new List<GameObject>();
        private float _ocarinaCounter;
        private int _ocarinaNoteIndex;
        private int _ocarinaSong;
        private int _preOcarinaDirection;
        public bool ManboTeleport;

        // Power Bracelet
        private RectangleF GrabRectangle;
        private const float PullTime = 100;
        private const float PullMaxTime = 400;
        private const float PullResetTime = -133;
        private const float PreCarryTime = 200;
        private float _preCarryCounter;
        private float _pullCounter;
        private bool _isPulling;
        private bool _wasPulling;
        private GameObject _instantPickupObject;
        private bool _instantPickup;
        private bool _swimRoosterPickup;
        private bool _braceletThrowLock;
        private int _carryJumpDirection;

        // Power Bracelet: Carry Object
        private GameObject _carriedGameObject;
        private DrawComponent _carriedObjDrawComp;
        private CarriableComponent _carriedComponent;
        private Vector3 _carryStartPosition;
        public GameObject CarriedObject { get => _carriedGameObject; }

        // Roc's Feather: Jumping
        private bool _canJump = true;
        private bool _landedFromJump;
        private float _railJumpSpeed;
        public float _jumpStartZPos;

        // Roc's Feather: 2D Jumping
        private bool _jump2DHold;
        private bool _jump2DHeld;

        // Tunic Color Transition (Color Dungeon Reward)
        private int CloakTransitionTime = 2200;
        private float _cloakTransitionCounter;
        private float _cloakPercentage;
        private int CloakTransitionOutTime = 2500;
        private float _cloakTransitionOutCounter;

        // Teleporting
        private ObjDungeonTeleporter _teleporter;
        private string _teleportMap;
        private string _teleporterId;
        private float _teleportCounter;
        private float _teleportCounterFull;
        private int _teleportState;

        // Instruments
        private bool[] _noteInit = { false, false };
        private int[] _noteSpriteIndex = { 0, 0 };
        private double _instrumentPickupTime;
        private float _instrumentCounter;
        private int _instrumentIndex;
        private int _instrumentCycleTime = 1000;
        private bool _drawInstrumentEffect;
        private bool _pickingUpInstrument;
        private const int dist0 = 30;
        private const int dist1 = 15;
        private readonly Vector2[] _showInstrumentOffset = 
        {
            new Vector2(-dist1, -dist0), 
            new Vector2(dist1, -dist0), 
            new Vector2(dist0, dist1), 
            new Vector2(dist0, -dist1),
            new Vector2(dist1, dist0),
            new Vector2(-dist1, dist0),
            new Vector2(-dist0, -dist1),
            new Vector2(-dist0, dist1) 
        };
        private Rectangle[] _noteSourceRectangles = 
        {
            new Rectangle(145, 97, 10, 12),
            new Rectangle(156, 97, 6, 12) 
        };
        private readonly int[] _instrumentMusicIndex = { 31, 39, 40, 41, 42, 43, 44, 45 };

        // Raft
        private ObjRaft _objRaft;
        private bool _isRafting;

        // Pushing
        private Vector2 _pushStart;
        private Vector2 _pushEnd;
        private float _pushCounter;
        private int _pushTime;
        public bool WasPushing;

        // Vacuum Enemy
        private float _rotationCounter;
        private bool _isRotating;
        private bool _wasRotating;
        public int _rotateDirection;

        // Stunned 
        private float _stunnedCounter;
        private bool _stunnedParticles;

        // Final Sequence
        private int _finalIndex;
        private double _finalSeqCounter;

        // Save Position
        public string SaveMap;
        public Vector2 SavePosition;
        public int SaveDirection;

        // Low Heart Alarm
        private float _lowHealthBeepCounter;
        private bool _enableHealthBeep;

        // Sprite Shadows
        private ObjSpriteShadow _spriteShadow;

        // Prevents Enemy Position Reset
        public bool PreventReset;
        public float PreventResetTimer;

        // Prevent Damage Hits (No Collision)
        private float PreventDamageTimer;

        // Game & Map
        private Map.Map _previousMap;
        public bool UpdatePlayer;
        public bool FreezeWorldAroundPlayer;
        public bool FreezeWorldForEvents;

        // Miscellaneous
        private DictAtlasEntry _stunnedParticleSprite;
        private bool _pickingUpAnglerKey;
        public bool NoFadeObjMusicTile;

        // Powerups
        public bool DisableGuardianAcorn => disable_acorn_spawn;
        public bool DisablePieceOfPower => disable_power_spawn;
        public bool HasPowerup => Game1.GameManager.PieceOfPowerIsActive || Game1.GameManager.GuardianAcornIsActive;

        // Values configurable via lahdmod.
        private bool   disable_acorn_spawn    = false;
        private bool   disable_power_spawn    = false;
        private bool   swordbeam_level1       = false;
        private bool   swordbeam_always       = false;
        private bool   swordpoke_keeps_charge = false;
        private bool   feather_swimming2d     = false;
        private bool   bracelet_fast_pickup   = false;
        private float  sword_charge_time      = 670;
        private float  boots_charge_time      = 533;
        private float  feather_velocity       = 2.35f;
        private float  corner_sidestep        = 2.50f;
        private bool   light_source           = false;
        private int    light_red              = 255;
        private int    light_grn              = 255;
        private int    light_blu              = 255;
        private float  light_bright           = 1.0f;
        private int    light_size             = 120;
        private float  dmg_shader_mark0       = 0.100f;
        private float  dmg_shader_mark1       = 0.725f;
        private float  dmg_shader_color1_red  = 255;
        private float  dmg_shader_color1_grn  = 181;
        private float  dmg_shader_color1_blu  = 49;
        private float  dmg_shader_color2_red  = 222;
        private float  dmg_shader_color2_grn  = 0;
        private float  dmg_shader_color2_blu  = 0;
        private float  dmg_shader_color3_red  = 0;
        private float  dmg_shader_color3_grn  = 0;
        private float  dmg_shader_color3_blu  = 0;

        public ObjLink() : base((Map.Map)null)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathLAHDMods, "ObjLink.lahdmod");
            ModFile.Parse(modFile, this);

            Resources.DamageSpriteShader0.FloatParameter["mark0"] = dmg_shader_mark0;
            Resources.DamageSpriteShader0.FloatParameter["mark1"] = dmg_shader_mark1;
            Resources.DamageSpriteShader0["Color0"] = new Vector4(dmg_shader_color1_red/255f, dmg_shader_color1_grn/255f, dmg_shader_color1_blu/255f, 1.0f);
            Resources.DamageSpriteShader0["Color1"] = new Vector4(dmg_shader_color2_red/255f, dmg_shader_color2_grn/255f, dmg_shader_color2_blu/255f, 1.0f);
            Resources.DamageSpriteShader0["Color2"] = new Vector4(dmg_shader_color3_red/255f, dmg_shader_color3_grn/255f, dmg_shader_color3_blu/255f, 1.0f);

            EntityPosition = new CPosition(0, 0, 0);
            EntitySize = new Rectangle(-8, -16, 16, 16);

            // load the player + sword animations
            Animation = AnimatorSaveLoad.LoadAnimator("link0");
            AnimatorWeapons = AnimatorSaveLoad.LoadAnimator("Objects/sword");

            _stunnedParticleSprite = Resources.GetSprite("stunned particle");

            CollisionBoxSize = new Point(8, 8);

            _body = new BodyComponent(EntityPosition, -4, -10, 8, 10, 8)
            {
                IsPusher = true,
                IsSlider = true,
                MaxJumpHeight = 3,
                Drag = 0.72f,
                DragAir = 0.72f,
                Gravity = -0.15f,
                Gravity2D = 0.1f,
                AbsorbStop = 0.25f,
                AbsorbPercentage = 1f,
                HoleOnPull = OnHolePull,
                HoleAbsorb = OnHoleAbsorb,
                MoveCollision = OnMoveCollision,
                CornerCorrection = true,
                CornerCorrectionThreshold = corner_sidestep,
                CollisionTypes = Values.CollisionTypes.Normal |
                                 Values.CollisionTypes.Enemy |
                                 Values.CollisionTypes.PlayerItem |
                                 Values.CollisionTypes.LadderTop,
            };

            DamageCollider = new CBox(EntityPosition, -5, -10, 10, 10, 8);

            _powderOffset = new[]
            {
                new Vector2(-12, 0),
                new Vector2(-2, -CollisionBoxSize.Y -5),
                new Vector2(12, 0),
                new Vector2(2, 10)
            };

            _boomerangOffset = new[]
            {
                new Vector2(-10, -3),
                new Vector2(-2, -CollisionBoxSize.Y -1),
                new Vector2(10, -3),
                new Vector2(2, 6)
            };

            _arrowOffset = new[]
            {
                new Vector2(-6, -2),
                new Vector2(-2, -CollisionBoxSize.Y -1),
                new Vector2(6, -2),
                new Vector2(2, 2)
            };

            _magicRodOffset = new[]
            {
                new Vector2(-10, -4),
                new Vector2(-4, -CollisionBoxSize.Y - 4),
                new Vector2(10, -4),
                new Vector2(3, 2)
            };

            _shootSwordOffset = new[]
            {
                new Vector2(-6, -4),
                new Vector2(-4, -CollisionBoxSize.Y - 3),
                new Vector2(6, -4),
                new Vector2(3, 2)
            };

            _hookshotOffset = new[]
            {
                new Vector2(-5, -4),
                new Vector2(-3, -CollisionBoxSize.Y - 2),
                new Vector2(5, -4),
                new Vector2(3, 0)
            };

            _shovelOffset = new[]
            {
                new Vector2(-9, -1),
                new Vector2(0, -14),
                new Vector2(9, -1),
                new Vector2(0, 1)
            };

            _bombOffset = new[]
            {
                new Vector2(-10, 0),
                new Vector2(0, -CollisionBoxSize.Y - 2),
                new Vector2(10, 0),
                new Vector2(0, 8)
            };

            _pokeAnimationOffset = new[]
            {
                new Point(-16, -4),
                new Point(-4, -CollisionBoxSize.Y - 16),
                new Point(16, -4),
                new Point(5, 12)
            };

            _sprite = new CSprite(EntityPosition);
            _drawBody = new BodyDrawComponent(_body, DrawLink, Values.LayerPlayer);
            _bodyDrawFunction = _drawBody.Draw;
            _drawBody.Draw = Draw;

            AddComponent(KeyChangeListenerComponent.Index, new KeyChangeListenerComponent(OnKeyChange));
            AddComponent(BodyComponent.Index, _body);
            AddComponent(BaseAnimationComponent.Index, new AnimationComponent(Animation, _sprite, new Vector2(_animationOffsetX, _animationOffsetY)));
            AddComponent(CollisionComponent.Index, new BodyCollisionComponent(_body, Values.CollisionTypes.Player));
            AddComponent(UpdateComponent.Index, new UpdateComponent(Update));
            AddComponent(DrawComponent.Index, _drawBody);
            AddComponent(DrawShadowComponent.Index, _shadowComponent = new BodyDrawShadowComponent(_body, _sprite));
            AddComponent(LightDrawComponent.Index, new LightDrawComponent(DrawLight));

            EntityPosition.AddPositionListener(typeof(CarriableComponent), UpdatePositionCarriedObject);

            // Set the move speed value the user chose.
            AlterMoveSpeed(GameSettings.MoveSpeedAdded);

            // If attacking in a jumping state, return to jumping state after attack.
            AnimatorWeapons.OnAnimationFinished = () =>
            {
                if (!_body.IsGrounded && CurrentState == State.AttackJumping)
                {
                    if (_isHoldingSword)
                    {
                        string shieldString = CarryShield
                            ? Game1.GameManager.ShieldLevel == 2 ? "ms_" : "s_"
                            : "_";

                        CurrentState = State.ChargeJumping;
                        PlayWeaponAnimation("stand", Direction);
                        Animation.Play("cjump" + shieldString + Direction);
                        _swordPokeCounter = _swordPokeTime;
                    }
                    else
                    {
                        CurrentState = State.Jumping;
                        Animation.Play("jump_" + Direction);
                    }
                }
            };
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  UPDATE CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        public void Update()
        {
            // If the player has enabled these cheats, refresh their item counts every loop.
            CheatSystem.RefreshItemCheat(GameSettings.ChInfinRupees, "", 999, 999, "ruby");
            CheatSystem.RefreshItemCheat(GameSettings.ChInfinPowder, "upgradePowder", 20,  40,  "powder");
            CheatSystem.RefreshItemCheat(GameSettings.ChInfinBombs,  "upgradeBomb",   30,  60,  "bomb");
            CheatSystem.RefreshItemCheat(GameSettings.ChInfinArrows, "upgradeBow",    30,  60,  "bow");

            // Update the current field and make a field barrier if Classic Camera is enabled.
            UpdateCurrentField();

            // Variable that prevents "HitPlayer" method from firing.
            if (PreventDamageTimer > 0)
            {
                // Timer must be active for it to remain true.
                PreventDamageTimer -= Game1.DeltaTime;
                if (PreventDamageTimer <= 0)
                    PreventDamageTimer = 0;
            }
            // Update falling into a map transition (I think).
            if (CurrentState == State.FallRotateEntry)
            {
                _fallEntryCounter += Game1.DeltaTime;
                Direction = (int)(DirectionEntry + (_fallEntryCounter + 96) / 48) % 4;

                if (_body.IsGrounded)
                    CurrentState = State.Idle;

                UpdateAnimation();
            }
            // @HACK
            // this is only needed because the player should not be able to step into the door 1 frame
            // after finishing the transition this would cause the door transition to not start
            if (IsTransitioning || _wasTransitioning)
            {
                _wasTransitioning = IsTransitioning;
                return;
            }

            // Photo Mouse when rejecting having a picture taken.
            if (CurrentState == State.Pushed)
            {
                _pushCounter += Game1.DeltaTime;

                // push towards the target position
                if (_pushCounter > _pushTime)
                {
                    EntityPosition.Set(_pushEnd);
                    CurrentState = State.Idle;
                }
                else
                {
                    var percentage = MathF.Sin((_pushCounter / _pushTime) * MathF.PI * 0.5f);
                    var newPosition = Vector2.Lerp(_pushStart, _pushEnd, percentage);
                    EntityPosition.Set(newPosition);
                }
            }

            // need to update the bomb to make sure it does not explode while the player is not getting updated
            if (_carriedComponent != null && _carriedComponent.IsPickedUp)
            {
                // used to updated the position to match the animation
                // gets called twice when moving
                // not sure how this could be done better
                UpdatePositionCarriedObject(EntityPosition);
            }
            // If Link is currently locked. Usually set when a dialog is open.
            if (!UpdatePlayer)
            {
                // If holding a toadstool then disable inventory.
                if (CurrentState == State.ShowToadstool)
                    Game1.GameManager.InGameOverlay.DisableInventoryToggle = true;

                UpdatePlayer = true;

                // Only update Link's animation.
                if (!Is2DMode)
                    UpdateAnimation();
                else
                    UpdateFrozen2D();

                UpdateDive();
                UpdateOcarinaAnimation();
                UpdateDrawComponents();
                return;
            }
            // Low health trigger.
            UpdateLowHealthFlag();

            if (CurrentState == State.FinalInstruments)
            {
                _finalSeqCounter -= Game1.DeltaTime;
                if (_finalIndex == 0)
                {
                    if (_finalSeqCounter <= 0)
                    {
                        _finalIndex = 1;
                        _finalSeqCounter += 2250;
                        Animation.Play("show1");
                        Game1.AudioManager.PlaySoundEffect("D360-52-34");
                    }
                }
                else if (_finalIndex == 1)
                {
                    if (_finalSeqCounter <= 0)
                        ((MapShowSystem)Game1.GameManager.GameSystems[typeof(MapShowSystem)]).StartEnding();
                }
                return;
            }
            else if (CurrentState == State.CloakShow0)
            {
                _cloakTransitionCounter += Game1.DeltaTime;
                _cloakPercentage = _cloakTransitionCounter / CloakTransitionTime;

                if (_cloakTransitionCounter > CloakTransitionTime)
                {
                    _cloakPercentage = 1;

                    if (ShowItem == null)
                    {
                        Game1.GameManager.StartDialog("cloak_green");
                        Game1.GameManager.CloakType = GameManager.CloakGreen;
                    }
                    else if (ShowItem.Name == "cloakBlue")
                        Game1.GameManager.StartDialog("cloak_blue");
                    else if (ShowItem.Name == "cloakRed")
                        Game1.GameManager.StartDialog("cloak_red");

                    CurrentState = State.CloakShow1;

                    // add the item to the inventory
                    if (_collectedShowItem != null)
                    {
                        Game1.GameManager.CollectItem(_collectedShowItem, 0);
                        _collectedShowItem = null;
                    }

                    ShowItem = null;
                }
            }
            else if (CurrentState == State.CloakShow1)
            {
                _cloakTransitionOutCounter += Game1.DeltaTime;

                var transitionSystem = (MapTransitionSystem)Game1.GameManager.GameSystems[typeof(MapTransitionSystem)];
                transitionSystem.SetColorMode(Color.White, MathHelper.Clamp(_cloakTransitionOutCounter / 1000f, 0, 1));

                if (_cloakTransitionOutCounter > CloakTransitionOutTime)
                {
                    Game1.GameManager.StartDialogPath("color_fairy_4");

                    Direction = 3;
                    MapTransitionStart = EntityPosition.Position;
                    MapTransitionEnd = MapTransitionStart;
                    TransitionOutWalking = false;

                    // append a map change
                    ((MapTransitionSystem)Game1.GameManager.GameSystems[typeof(MapTransitionSystem)]).AppendMapChange("overworld.map", "cloakOut", false, true, Color.White, true);
                }
            }
            else if (CurrentState == State.ShowToadstool)
            {
                CurrentState = State.Idle;
            }
            else if (CurrentState == State.SwordShowLv2)
            {
                _showSwordL2ParticleCounter += Game1.DeltaTime;
                if (_showSwordL2ParticleCounter > 4800 && !_shownSwordLv2Dialog)
                {
                    _shownSwordLv2Dialog = true;
                    _showSwordL2ParticleCounter = 0;
                    Game1.AudioManager.SetMusic(-1, 2);
                    Game1.GameManager.StartDialogPath("sword2Collected");
                }
                // make sure to show the sword while the dialog box is open
                else if (_shownSwordLv2Dialog)
                {
                    ShowItem = null;
                    CurrentState = State.Idle;
                }
            }
            else if (CurrentState == State.PickingUp && !_pickingUpInstrument && !_pickingUpSword && !_pickingUpAnglerKey)
            {
                Game1.GameManager.InGameOverlay.DisableInventoryToggle = true;

                // Link should drop to the ground before freezing the world unless he is swimming
                // in a 2D map. This only happens when picking up Angler Fish heart container.
                if (_body.IsGrounded || _pickupWhileSwimming)
                    FreezeWorldAroundPlayer = true;
            }
            else if (CurrentState == State.TeleporterUpWait)
            {
                _holeTeleportCounter += Game1.DeltaTime;
                if (_holeTeleportCounter > 1000)
                {
                    CurrentState = State.TeleporterUp;

                    _holeTeleportCounter -= 1000;
                    _shadowComponent.Transparency = 0;

                    Game1.AudioManager.PlaySoundEffect("D360-37-25");

                    // Rise together with Link.
                    foreach (var follower in Followers)
                    {
                        if (follower is ObjMarin marin)
                            marin.StartWorldTeleportRise(FollowerTeleportRiseDuration);
                        else if (follower is ObjCock cock)
                            cock.StartWorldTeleportRise(FollowerTeleportRiseDuration);
                    }
                    if (_objBowWow != null)
                        _objBowWow.StartWorldTeleportRise(FollowerTeleportRiseDuration);
                }
            }
            else if (CurrentState == State.TeleporterUp)
            {
                _holeTeleportCounter += Game1.DeltaTime;
                var time = 400;

                EntityPosition.Z = (float)(_holeTeleportCounter / time) * 128;
                Direction = (int)(_holeTeleportCounter / 64) % 4;

                // fade in
                var percentage = MathHelper.Clamp(1 - ((float)_holeTeleportCounter - (time - 100)) / 100, 0, 1);
                SpriteTransparency = percentage;
                _shadowComponent.Transparency = percentage;

                if (_holeTeleportCounter > time)
                {
                    _holeTeleportCounter -= time;

                    if (ObjOverworldTeleporter.TeleporterDictionary.TryGetValue(HoleTeleporterId, out var teleporter))
                        teleporter.SetNextTeleporterPosition();
                    else
                        CurrentState = State.Idle;
                }
            }
            else if (CurrentState == State.TeleportFallWait)
            {
                _holeTeleportCounter += Game1.DeltaTime;
                var time = 350;

                if (_holeTeleportCounter > time)
                {
                    _holeTeleportCounter -= time - 50;
                    _body.Velocity = new Vector3(0, 0, 0);
                    CurrentState = State.TeleportFall;
                }
            }
            else if (CurrentState == State.TeleportFall)
            {
                _holeTeleportCounter += Game1.DeltaTime;
                Direction = (int)(_holeTeleportCounter / 64) % 4;

                // fade in
                var percentage = MathHelper.Clamp((float)_holeTeleportCounter / 100, 0, 1);

                if (_body.IsGrounded)
                {
                    percentage = 1;
                    CurrentState = State.Idle;

                    UpdateSaveLocation();

                    // save settings?
                    if (GameSettings.Autosave)
                    {
                        SettingsSaveLoad.SaveSettings();
                        SaveGameSaveLoad.SaveGame(Game1.GameManager, true);
                    }
                    Camera.SnapCamera = false;
                }
                SpriteTransparency = percentage;
                _shadowComponent.Transparency = percentage;
            }

            if (CurrentState == State.Knockout)
                return;

            // Stunned
            if (CurrentState == State.InitStunned && _hitVelocity.Length() < 0.25f)
            {
                Animation.Play("stunned");
                CurrentState = State.Stunned;
            }

            if (CurrentState == State.Stunned)
            {
                if (_stunnedCounter > 0)
                {
                    _body.DragAir = 0.95f;
                    _stunnedCounter -= Game1.DeltaTime;
                }
                if (_stunnedCounter <= 0)
                {
                    _body.DragAir = 0.9f;
                    CurrentState = State.Idle;
                }
            }
            AnimatorWeapons.Update();

            // update all the item stuff
            // this need to be before the update method to correctly start jumping?
            UpdateItem();

            if (Is2DMode)
                Update2D();
            else
                Update3D();

            UpdateOcarina();

            UpdateDamageShader();
            _hitCount -= Game1.DeltaTime;

            if (_savedPreItemPickup && (CurrentState == State.Idle || CurrentState == State.Swimming))
                EndPickup();

            // die?
            if (Game1.GameManager.CurrentHealth <= 0 && !Game1.GameManager.UseShockEffect)
                OnDeath();

            UpdateDrawComponents();

            if (PushingTimer > 0)
                PushingTimer -= Game1.DeltaTime;

            if (DisableItemCounter > 0)
                DisableItemCounter -= Game1.DeltaTime;

            if (DisableItemCounter <= 0)
                DisableItems = false;

            HoleResetRoom = null;
            CanWalk = true;
            _canJump = true;
            _isLocked = false;

            _hasStartedJumping = _startedJumping;
            _startedJumping = false;

            _currentWalkSpeed = Game1.GameManager.PieceOfPowerIsActive ? WalkSpeedPoP : WalkSpeed;

            // Press the toggle HUD key (InGame/GameObjects/Things/Values.cs) to hide the UI.
            if (InputHandler.KeyPressed(Keys.OemTilde) || InputHandler.KeyPressed(Keys.Delete))
                UiManager.HideOverlay = !UiManager.HideOverlay;

            // Capture the current field now so it can be compared on the next frame to see if
            // the field has changed. We only want to update the FieldBarrier on field changes.
            ContrastField = CurrentField;

            // ContrastField now describes this map, so crossing detection can be trusted again.
            // This line is only reached when not transitioning, which is exactly what we want.
            _fieldSyncPending = false;

            // If input was disabled, enable it now.
            DisableInput = false;

            // Clear the sword damage box if none of the below is true.
            if (!IsAttackingState() && !IsChargingState() && !AnimatorWeapons.IsPlaying && !_bootsRunning)
                SwordDamageBox = Box.Empty;

            // Clear the shield box if it's not being utilized.
            if (!IsBlockingState() && !_bootsRunning)
                ShieldBlockBox = Box.Empty;

            // Reset this flag now as collision checks happen after object update loops.
            IsPoking = false;
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  UPDATE 3D CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void Update3D()
        {
            UpdateNPCAvoidance();
            UpdateIntro();
            UpdateBedTransition();
            UpdateRafting();
            UpdateFlying();
            UpdateTeleporting();
            UpdateSwordSequence();
            UpdateInstrumentSequence();
            UpdateSwimmingPartOne();
            UpdateIgnoresZ();
            UpdateIgnoreHeight();
            UpdateDrownResetPosition();
            UpdateWalking();
            UpdateSwimmingPartTwo();
            UpdateMovementPhysics();
            UpdateJump();
            UpdateSavePosition();
            UpdateFallingIntoHole();
            UpdateAnimation();
            UpdateGhostSpawn();
            UpdateSpriteShadow();

            // Stop pushing animation but store it for use in other places.
            WasPushing = false;
            if (IsPushingState())
            {
                // WasPushing can be used outside of ObjLink to know if he was pushing or not.
                WasPushing = true;

                if (CurrentState == State.BlockPushing)
                    CurrentState = State.Blocking;
                else
                    CurrentState = State.Idle;
            }
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  DRAWING CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UpdateDrawComponents()
        {
            if (_drawInstrumentEffect)
                _drawBody.Layer = Values.LayerTop;
            else
                _drawBody.Layer = (CurrentState == State.Swimming && _diveCounter > 0) ? Values.LayerBottom : Values.LayerPlayer;

            if ((CurrentState == State.Swimming && _diveCounter > 0) ||
                CurrentState == State.Drowning ||
                CurrentState == State.Drowned ||
                CurrentState == State.BedTransition || _isTrapped)
                _shadowComponent.IsActive = false;
            else
                _shadowComponent.IsActive = true;
        }

        private void Draw(SpriteBatch spriteBatch)
        {
            if (!IsVisible)
                return;

            // Draw the player sprite behind the sword.
            if (Direction != 1 && Direction != 2 && !_isTrapped && CurrentState != State.ChargeSwimming)
                _bodyDrawFunction(spriteBatch);

            // Draw the sword or the magic rod if any of these are true.
            if (IsAttackingState() || IsChargingState() || CurrentState == State.SwordShow1 || CurrentState == State.SwordShowPhoto || CurrentState == State.MagicRod || (_bootsRunning && CarrySword))
            {
                // Flash the sword if charging.
                var changeColor = _swordChargeCounter <= 0 &&
                            Game1.TotalGameTime % (8 / 0.06) >= 4 / 0.06 &&
                            ObjectManager.CurrentEffect != Resources.DamageSpriteShader0.Effect;

                // Change the draw shader.
                if (changeColor)
                {
                    spriteBatch.End();
                    ObjectManager.SpriteBatchBegin(spriteBatch, Resources.DamageSpriteShader0);
                }

                // Draw the sword. Use offset of 6 instead of 7 when 2D Link is swimming and charging.
                var swordXOffset = (Is2DMode && CurrentState == State.ChargeSwimming) ? 6 : 7;
                var swordOffsetY = 0;

                // During the fishing photo sequence the sword offset needs to be adjusted.
                if (CurrentState == State.SwordShowPhoto)
                {
                    swordXOffset = 3;
                    swordOffsetY = 5;
                }
                // Draw the sword at whatever position offset it is currently at.
                AnimatorWeapons.Draw(spriteBatch, new Vector2(EntityPosition.X - swordXOffset, EntityPosition.Y - 16 - EntityPosition.Z - swordOffsetY), Color.White);

                // Change the draw shader
                if (changeColor)
                {
                    spriteBatch.End();
                    ObjectManager.SpriteBatchBegin(spriteBatch, null);
                }
            }

            // draw the sword after the first pickup
            if (CurrentState == State.SwordShow2)
            {
                var itemSword = Game1.GameManager.ItemManager["sword1"];
                var position = new Vector2(
                    BodyRectangle.X - itemSword.SourceRectangle.Value.Width / 2f,
                    (EntityPosition.Y - EntityPosition.Z - 15) - itemSword.SourceRectangle.Value.Height);

                ItemDrawHelper.DrawItem(spriteBatch, itemSword, position, Color.White, 1, true);
            }

            // draw the toadstool
            if (CurrentState == State.ShowToadstool)
            {
                var itemToadstool = Game1.GameManager.ItemManager["toadstool"];
                var position = new Vector2(
                    BodyRectangle.X - itemToadstool.SourceRectangle.Value.Width / 2f,
                    (EntityPosition.Y - EntityPosition.Z - 15) - itemToadstool.SourceRectangle.Value.Height);

                ItemDrawHelper.DrawItem(spriteBatch, itemToadstool, position, Color.White, 1);
            }

            // draw the player sprite in front of the sword
            if ((Direction == 1 || Direction == 2) && !_isTrapped || CurrentState == State.ChargeSwimming)
                _bodyDrawFunction(spriteBatch);

            if (_drawInstrumentEffect)
                DrawInstrumentEffect(spriteBatch);

            // draw the picked up store item
            if (StoreItem != null)
                ItemDrawHelper.DrawItem(spriteBatch, StoreItem, _storePickupPosition, Color.White, 1, true);

            // draw the shown item
            if (ShowItem != null)
            {
                var itemPosition = EntityPosition.Position + _showItemOffset;
                itemPosition.Y -= EntityPosition.Z;

                if (CurrentState == State.CloakShow0)
                {
                    ItemDrawHelper.DrawItem(spriteBatch, ShowItem, itemPosition, Color.White * (1 - _cloakPercentage), 1, true);
                }
                else if (ShowItem.Name == "sword2")
                {
                    var swordImage = Resources.GetSprite("sword2Show");
                    DrawHelper.DrawNormalized(spriteBatch, swordImage.Texture, itemPosition, swordImage.ScaledRectangle, Color.White, swordImage.Scale);
                }
                else
                    ItemDrawHelper.DrawItem(spriteBatch, ShowItem, itemPosition, Color.White, 1, true);
            }

            // draw the object the player is carrying
            if (_carriedObjDrawComp != null)
            {
                _carriedObjDrawComp.IsActive = true;
                _carriedObjDrawComp.Draw(spriteBatch);
                _carriedObjDrawComp.IsActive = false;
            }

            // draw the dots over the head in the stunned state
            if (CurrentState == State.Stunned && _stunnedParticles)
            {
                var rotation = (float)(Game1.TotalGameTime / 1200) * MathF.PI * 2;
                var offset0 = new Vector2(MathF.Cos(rotation) * 8 - 2, MathF.Sin(rotation) * 3 - 2);
                DrawHelper.DrawNormalized(spriteBatch, _stunnedParticleSprite,
                    offset0 + new Vector2(EntityPosition.X, EntityPosition.Y - EntityPosition.Z - 18), Color.White);

                var offset1 = new Vector2(MathF.Cos(rotation + MathF.PI) * 8 - 2, MathF.Sin(rotation + MathF.PI) * 3 - 2);
                DrawHelper.DrawNormalized(spriteBatch, _stunnedParticleSprite,
                    offset1 + new Vector2(EntityPosition.X, EntityPosition.Y - EntityPosition.Z - 18), Color.White);
            }

            if (CurrentState == State.SwordShowLv2)
                DrawSwordL2Particles(spriteBatch);

            // draw the notes while showing an instrument
            {
                var leftNotePosition = new Vector2(EntityPosition.X - 8, EntityPosition.Y - 24);
                DrawNote(spriteBatch, leftNotePosition, new Vector2(-0.4f, -1.0f), 0);

                var rightNotePosition = new Vector2(EntityPosition.X + 8, EntityPosition.Y - 24);
                DrawNote(spriteBatch, rightNotePosition, new Vector2(0.4f, -1.0f), 1);
            }

            if (CurrentState == State.FinalInstruments)
                DrawFinalInstruments(spriteBatch);

            // Draw boxes when pressing F2 and Debug/Editor is enabled.
            if (Game1.DebugMode)
            {
                // Draw the save hole position.
                spriteBatch.Draw(Resources.SprWhite,
                    new Vector2(_holeResetPosition.X - 5, _holeResetPosition.Y - 5), new Rectangle(0, 0,
                       10, 10), Color.HotPink * 0.65f);

                // Draw weapon damage rectangle.
                var swordRectangle = SwordDamageBox.Rectangle();
                spriteBatch.Draw(Resources.SprWhite,
                    new Vector2(swordRectangle.X, swordRectangle.Y), new Rectangle(0, 0,
                        (int)swordRectangle.Width, (int)swordRectangle.Height), Color.Blue * 0.75f);

                // Draw classic damage rectangle.
                var classicRectangle = SwordClassicBox.Rectangle();
                spriteBatch.Draw(Resources.SprWhite,
                    new Vector2(classicRectangle.X, classicRectangle.Y), new Rectangle(0, 0,
                        (int)classicRectangle.Width, (int)classicRectangle.Height), Color.Orange * 0.75f);

                // Draw shield rectangle.
                var shieldRectangle = ShieldBlockBox.Rectangle();
                spriteBatch.Draw(Resources.SprWhite,
                    new Vector2(shieldRectangle.X, shieldRectangle.Y), new Rectangle(0, 0,
                        (int)shieldRectangle.Width, (int)shieldRectangle.Height), Color.Green * 0.75f);

                // Draw dash smash rectangle.
                var dashRectangle = _crystalSmashBox.Rectangle();
                spriteBatch.Draw(Resources.SprWhite,
                    new Vector2(dashRectangle.X, dashRectangle.Y), new Rectangle(0, 0,
                        (int)dashRectangle.Width, (int)dashRectangle.Height), Color.Red * 0.75f);

                // Draw grab rectangle.
                spriteBatch.Draw(Resources.SprWhite,
                    new Vector2(GrabRectangle.X, GrabRectangle.Y), new Rectangle(0, 0,
                        (int)GrabRectangle.Width, (int)GrabRectangle.Height), Color.Yellow * 0.75f);

                // Draw the field barrier.
                if (FieldBarrier != null)
                {
                    foreach (var barrier in FieldBarrier)
                    {
                        spriteBatch.Draw(Resources.SprWhite,
                            new Vector2(barrier.CollisionBox.X, barrier.CollisionBox.Y), new Rectangle(0, 0,
                            (int)barrier.CollisionBox.Width, (int)barrier.CollisionBox.Height), Color.Blue * 0.75f);
                    }
                }
            }
        }

        private void DrawSwordL2Particles(SpriteBatch spriteBatch)
        {
            DrawSwordParticle(spriteBatch, new Vector2(EntityPosition.X - 4, EntityPosition.Y - 22), new Vector2(-32, -16), -125, 300, 200, 0);
            DrawSwordParticle(spriteBatch, new Vector2(EntityPosition.X - 4, EntityPosition.Y - 22), new Vector2(-32, -16), -125 - 250, 300, 200, 0);

            DrawSwordParticle(spriteBatch, new Vector2(EntityPosition.X - 4, EntityPosition.Y - 22), new Vector2(-32, -32), 0, 300, 200, 1);
            DrawSwordParticle(spriteBatch, new Vector2(EntityPosition.X - 4, EntityPosition.Y - 22), new Vector2(-32, -32), -250, 300, 200, 1);

            DrawSwordParticle(spriteBatch, new Vector2(EntityPosition.X - 4, EntityPosition.Y - 22), new Vector2(-24, -52), -50, 450, 50, 2);
            DrawSwordParticle(spriteBatch, new Vector2(EntityPosition.X - 4, EntityPosition.Y - 22), new Vector2(-24, -52), -50 - 250, 450, 50, 2);

            DrawSwordParticle(spriteBatch, new Vector2(EntityPosition.X - 4, EntityPosition.Y - 22), new Vector2(0, -64), -75, 450, 50, 3);
            DrawSwordParticle(spriteBatch, new Vector2(EntityPosition.X - 4, EntityPosition.Y - 22), new Vector2(0, -64), -75 - 250, 450, 50, 3);

            DrawSwordParticle(spriteBatch, new Vector2(EntityPosition.X - 4, EntityPosition.Y - 22), new Vector2(24, -52), -50, 450, 50, 4);
            DrawSwordParticle(spriteBatch, new Vector2(EntityPosition.X - 4, EntityPosition.Y - 22), new Vector2(24, -52), -50 - 250, 450, 50, 4);

            DrawSwordParticle(spriteBatch, new Vector2(EntityPosition.X - 4, EntityPosition.Y - 22), new Vector2(32, -32), 0, 300, 200, 5);
            DrawSwordParticle(spriteBatch, new Vector2(EntityPosition.X - 4, EntityPosition.Y - 22), new Vector2(32, -32), -250, 300, 200, 5);

            DrawSwordParticle(spriteBatch, new Vector2(EntityPosition.X - 4, EntityPosition.Y - 22), new Vector2(32, -16), -125, 300, 200, 6);
            DrawSwordParticle(spriteBatch, new Vector2(EntityPosition.X - 4, EntityPosition.Y - 22), new Vector2(32, -16), -125 - 250, 300, 200, 6);
        }

        private void DrawInstrumentEffect(SpriteBatch spriteBatch)
        {
            var fadeTime = 100;
            var speed = 500;
            var center = new Vector2(EntityPosition.X, EntityPosition.Y - 20);
            {
                var time = (float)(Game1.TotalGameTime % speed);
                var state = MathF.Sin((time / speed) * MathF.PI * 0.475f);
                var distance = 32 - 20 * state;
                var transparency = MathHelper.Clamp(time / fadeTime, 0, 1) *
                                   MathHelper.Clamp((speed - time) / fadeTime, 0, 1);
                var sourceRectangle = time < (speed / 1.65f) ? new Rectangle(194, 114, 12, 12) : new Rectangle(194, 98, 12, 12);
                for (var y = 0; y < 2; y++)
                    for (var x = 0; x < 2; x++)
                    {
                        var rawPosition = new Vector2(
                            center.X - 6 + (x * 2 - 1) * distance,
                            center.Y - 6 + (y * 2 - 1) * distance);
                        var position = GameSettings.PixelSnapping
                            ? new Vector2(MathF.Round(rawPosition.X), MathF.Round(rawPosition.Y))
                            : rawPosition;
                        spriteBatch.Draw(Resources.SprItem, position, sourceRectangle,
                            Color.White * transparency, 0, Vector2.Zero, Vector2.One,
                            (x == 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None) |
                            (y == 0 ? SpriteEffects.FlipVertically : SpriteEffects.None), 0);
                    }
            }
            {
                var time = (float)((Game1.TotalGameTime + speed / 2) % speed);
                var state = MathF.Sin((time / speed) * MathF.PI * 0.475f);
                var distance = 40 - 34 * state;
                var transparency = MathHelper.Clamp(time / fadeTime, 0, 1) *
                                   MathHelper.Clamp((speed - time) / fadeTime, 0, 1);
                var sourceRectangle = time < (speed / 1.65f) ? new Rectangle(176, 116, 16, 8) : new Rectangle(176, 100, 16, 8);
                for (var y = 0; y < 2; y++)
                    for (var x = 0; x < 2; x++)
                    {
                        var rotation = (float)((x * 2 + y) * Math.PI / 2);
                        var rawPosition = new Vector2(
                            center.X + (y == 0 ? (x * 2 - 1) * distance : 0),
                            center.Y + (y == 0 ? 0 : (x * 2 - 1) * distance));
                        var position = GameSettings.PixelSnapping
                            ? new Vector2(MathF.Round(rawPosition.X), MathF.Round(rawPosition.Y))
                            : rawPosition;
                        spriteBatch.Draw(Resources.SprItem, position, sourceRectangle,
                            Color.White * transparency, rotation, new Vector2(16, 4), Vector2.One, SpriteEffects.None, 0);
                    }
            }
        }

        private void DrawFinalInstruments(SpriteBatch spriteBatch)
        {
            if (_finalIndex != 1)
                return;

            var percentage = 0.25f + Math.Clamp((float)(2500 - _finalSeqCounter) / 2000, 0, 1) * 0.75f;

            // draw the instruments
            for (var i = 0; i < 8; i++)
            {
                var itemInstrument = Game1.GameManager.ItemManager["instrument" + i];
                var position = new Vector2(EntityPosition.X - 8, EntityPosition.Y - 60) + _showInstrumentOffset[i] * percentage;
                ItemDrawHelper.DrawItem(spriteBatch, itemInstrument, position, Color.White, 1, true);
            }
        }

        private void DrawLink(SpriteBatch spriteBatch)
        {
            _sprite.Draw(spriteBatch);

            // draw the colored cloak
            var texture = _sprite.SprTexture;

            var cloakColor = Game1.GameManager.CloakColor;
            if (CurrentState == State.CloakShow0 && ShowItem == null)
                cloakColor = Color.Lerp(cloakColor, ItemDrawHelper.CloakColors[0], _cloakPercentage);
            else if (CurrentState == State.CloakShow0 && ShowItem != null && ShowItem.Name == "cloakBlue")
                cloakColor = Color.Lerp(cloakColor, ItemDrawHelper.CloakColors[1], _cloakPercentage);
            else if (CurrentState == State.CloakShow0 && ShowItem != null && ShowItem.Name == "cloakRed")
                cloakColor = Color.Lerp(cloakColor, ItemDrawHelper.CloakColors[2], _cloakPercentage);

            _sprite.Color = cloakColor * SpriteTransparency;
            _sprite.SprTexture = Resources.SprLinkCloak;
            _sprite.Draw(spriteBatch);

            _sprite.Color = Color.White * SpriteTransparency;
            _sprite.SprTexture = texture;
        }

        private void DrawNote(SpriteBatch spriteBatch, Vector2 position, Vector2 direction, int noteIndex)
        {
            var timeOffset = noteIndex * _instrumentCycleTime / 2;

            if (_instrumentCounter < timeOffset ||
                (CurrentState != State.ShowInstrumentPart1 || _drawInstrumentEffect) &&
                ((_instrumentCounter - timeOffset) / _instrumentCycleTime + 1) * _instrumentCycleTime + timeOffset > 0)
                return;

            var time = (_instrumentCounter + timeOffset) % _instrumentCycleTime;

            var transparency = 1.0f;
            // fade out
            if (time > _instrumentCycleTime - 100)
            {
                _noteInit[noteIndex] = false;
                transparency = (_instrumentCycleTime - time) / 100f;
            }
            // fade in
            else if (time < 100)
            {
                if (!_noteInit[noteIndex])
                {
                    _noteInit[noteIndex] = true;
                    _noteSpriteIndex[noteIndex] = Game1.RandomNumber.Next(0, 2);

                }
                transparency = time / 100;
            }
            position += direction * time * 0.02f + new Vector2(-direction.X, direction.Y) * (float)Math.Sin(time * 0.015) * 0.75f;
            position += new Vector2(
                -_noteSourceRectangles[_noteSpriteIndex[noteIndex]].Width / 2f,
                -_noteSourceRectangles[_noteSpriteIndex[noteIndex]].Height);

            spriteBatch.Draw(Resources.SprItem, position,
                _noteSourceRectangles[_noteSpriteIndex[noteIndex]], Color.White * transparency);
        }

        private void DrawSwordParticle(SpriteBatch spriteBatch, Vector2 position, Vector2 direction, int timeOffset, int fullTime, int timeDelay, int index)
        {
            var fadeTime = 50;
            var particleTime = (_showSwordL2ParticleCounter + timeOffset) % (fullTime + timeDelay);
            var percentage = particleTime / fullTime;
            var colorTransparency = Math.Min((fullTime - particleTime) / fadeTime, particleTime / fadeTime);
            var particlePosition = position + percentage * direction;
            var spriteParticle = Resources.GetSprite("sword_particle_" + index);

            if (0 < particleTime && particleTime < fullTime)
                DrawHelper.DrawNormalized(spriteBatch, spriteParticle.Texture,
                    particlePosition - spriteParticle.Origin, spriteParticle.ScaledRectangle, Color.White * colorTransparency, spriteParticle.Scale);
        }

        private void DrawLight(SpriteBatch spriteBatch)
        {
            if (light_source && GameSettings.ObjectLights)
            {
                var _lightColor = new Color(light_red, light_grn, light_blu);
                var _lightRectangle = new Rectangle((int)_body.Position.X - light_size / 2, (int)_body.Position.Y - (int)_body.Position.Z - light_size / 2 - 6, light_size, light_size);
                spriteBatch.Draw(Resources.SprLight, _lightRectangle, _lightColor * light_bright);
            }
        }

        public void DrawTransition(SpriteBatch spriteBatch)
        {
            if (!IsVisible)
                return;

            _bodyDrawFunction(spriteBatch);

            if (_drawInstrumentEffect)
                DrawInstrumentEffect(spriteBatch);

            // draw the shown item
            if (ShowItem != null)
            {
                var itemPosition = EntityPosition.Position + _showItemOffset;
                ItemDrawHelper.DrawItem(spriteBatch, ShowItem, itemPosition, Color.White, 1, true);
            }
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  KEY LISTENER CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void OnKeyChange()
        {
            // Get a reference to the SaveManager.
            var gameManager = Game1.GameManager;
            var saveManager = gameManager.SaveManager;

            // Freeze the game if the value is set.
            var strFreeze = "freezeGame";
            var FreezeGame = saveManager.GetString(strFreeze, "0");
            if (FreezeGame == "1")
                FreezeWorldForEvents = true;
            else if (FreezeGame == "0")
                FreezeWorldForEvents = false;

            // Change the color of the tunic in the color dungeon.
            var strCloak = "cloak_transition";
            var cloakTransition = saveManager.GetString(strCloak);
            if (cloakTransition == "1")
            {
                _cloakTransitionCounter = 0;
                _cloakPercentage = 0;
                _cloakTransitionOutCounter = 0;
                saveManager.RemoveString(strCloak);
                saveManager.SetString(strCloak, "0");
                CurrentState = State.CloakShow0;
            }
            if (cloakTransition == "2")
            {
                _cloakTransitionCounter = 0;
                _cloakPercentage = 0;
                _cloakTransitionOutCounter = 0;
                saveManager.RemoveString(strCloak);
                saveManager.SetString(strCloak, "0");
                CurrentState = State.CloakShow1;
            }

            // Movement was forced through "script.zScript".
            var moveValue = saveManager.GetString("link_move");
            if (!string.IsNullOrEmpty(moveValue))
            {
                CurrentState = State.Idle;

                // If the player was dashing reset anything related.
                _bootsHolding = false;
                _bootsRunning = false;
                _bootsWasRunning = false;
                Animation.SpeedMultiplier = 1.0f;

                var split = moveValue.Split(',');
                var directionX = float.Parse(split[0], CultureInfo.InvariantCulture);
                var directionY = float.Parse(split[1], CultureInfo.InvariantCulture);

                var velocity = new Vector2(directionX, directionY);
                _body.VelocityTarget = velocity;
                Direction = AnimationHelper.GetDirection(velocity);
                _forceWalking = true;

                saveManager.SetString("link_move_collision", "0");
                saveManager.RemoveString("link_move");
            }

            // Idle state was forced through "script.zScript".
            var idleValue = saveManager.GetString("link_idle");
            if (!string.IsNullOrEmpty(idleValue))
            {
                CurrentState = State.Idle;
                saveManager.RemoveString("link_idle");
            }

            // Facing was forced through "script.zScript".
            var strAnimation = "link_direction";
            var newDirection = saveManager.GetString(strAnimation);
            if (!string.IsNullOrEmpty(newDirection))
            {
                Direction = int.Parse(newDirection);
                UpdateAnimation();
                saveManager.SetString(strAnimation, null);
            }

            // Animation was forced through "script.zScript".
            var animationValue = saveManager.GetString("link_animation");
            if (!string.IsNullOrEmpty(animationValue))
            {
                Animation.Play(animationValue);
                CurrentState = State.Sequence;
                saveManager.RemoveString("link_animation");
            }

            // Diving was forced through "script.zScript".
            var diveValue = saveManager.GetString("link_dive");
            if (!string.IsNullOrEmpty(diveValue))
            {
                _diveCounter = int.Parse(diveValue);
                CurrentState = State.Swimming;
                saveManager.RemoveString("link_dive");
            }

            // Holds up the sword during the fishing photo.
            var fishSequence = saveManager.GetString("link_fishing_photo");
            if (!string.IsNullOrEmpty(fishSequence))
            {
                if (fishSequence == "1")
                {
                    CurrentState = State.SwordShowPhoto;
                    AnimatorWeapons.Play("stand_1");
                }
                if (fishSequence == "0")
                {
                    CurrentState = State.Idle;
                    saveManager.RemoveString("link_fishing_photo");
                }
            }

            // Hide the HUD was forced through "script.zScript".
            var hideHudValue = saveManager.GetString("hide_hud");
            if (!string.IsNullOrEmpty(hideHudValue))
            {
                gameManager.InGameOverlay.HideHud(true);
                saveManager.RemoveString("hide_hud");
            }

            // Photo Mouse pushes Link back (used in photo sequences in "script.zScript").
            var pushValue = saveManager.GetString("link_push");
            if (!string.IsNullOrEmpty(pushValue))
            {
                var split = pushValue.Split(',');

                if (split.Length == 1)
                {
                    _pushStart = EntityPosition.Position;
                    _pushEnd = new Vector2(80, 94);
                    _pushTime = int.Parse(split[0]);
                }
                else
                {
                    var offsetX = float.Parse(split[0], CultureInfo.InvariantCulture);
                    var offsetY = float.Parse(split[1], CultureInfo.InvariantCulture);
                    _pushStart = EntityPosition.Position;
                    _pushEnd = _pushStart + new Vector2(offsetX, offsetY);
                    _pushTime = int.Parse(split[2]);
                }
                _pushCounter = 0;
                CurrentState = State.Pushed;
                saveManager.RemoveString("link_push");
            }

            // Used during the ending sequence to stop Link from walking.
            var linkFinalStairStand = saveManager.GetString("finalstairstand");
            if (!string.IsNullOrEmpty(linkFinalStairStand))
            {
                _forceWalking = false;
                CurrentState = State.FinalStand;
                saveManager.RemoveString("finalstairstand");
            }

            // Used during the ghost house sequence to stop Link from walking.
            var ghostAutoWalk = saveManager.GetString("ghost_autowalk");
            if (!string.IsNullOrEmpty(ghostAutoWalk) && ghostAutoWalk == "1")
            {
                _forceWalking = false;
                saveManager.RemoveString("ghost_autowalk");
            }

            // Used during the ending sequence when talking to the Wind Fish and showing the 8 instruments.
            var linkFinal = saveManager.GetString("link_final");
            if (!string.IsNullOrEmpty(linkFinal))
            {
                _finalIndex = 0;
                _finalSeqCounter = 1500;
                Animation.Play("final_stand_down");
                CurrentState = State.FinalInstruments;
                Game1.AudioManager.SetMusic(62, 2);
                saveManager.RemoveString("link_final");
            }

            // Mountain photo sequence: Drop the rooster if flying when it starts.
            var mntPhoto = saveManager.GetString("photo_12", "0") == "1";
            var hasRooster = saveManager.GetString("has_rooster", "0") == "1";
            var dropRooster = saveManager.GetString("drop_rooster", "0") == "1";

            if (mntPhoto && hasRooster && dropRooster)
            {
                ReleaseCarriedObject();
                ReturnToIdle();
                saveManager.RemoveString("drop_rooster");
            }
            // Deletes a death from the death counter.
            var removeDeath = saveManager.GetString("remove_death", "0") == "1";
            if (removeDeath)
            {
                gameManager.DeathCount--;
                saveManager.RemoveString("remove_death");
            }
            // Boomerang Trade: Hidden Goriya
            // Can be exchanged for: Shovel, Feather, Magic Rod, and Hookshot
            var boomerangValue = saveManager.GetString("boomerang_trade");
            if (!string.IsNullOrEmpty(boomerangValue))
            {
                // Get info about the item from the item slot based on "SwapButtons".
                var index       = GameSettings.SwapButtons ? 0 : 1;
                var item        = gameManager.Equipment[index];
                var itemName    = item != null ? gameManager.Equipment[index].Name : "";

                // Remove the string that initated the trade.
                saveManager.RemoveString("boomerang_trade");

                // Check if each item has been obtained legit.
                var shovelCheck   = item != null && itemName == "shovel"   && saveManager.GetString("store_shovel", "0") == "1";
                var featherCheck  = item != null && itemName == "feather"  && saveManager.GetString("store_feather", "0") == "1";
                var magicRodCheck = item != null && itemName == "magicRod" && saveManager.GetString("store_magicRod", "0") == "1";
                var hookshotCheck = item != null && itemName == "hookshot" && saveManager.GetString("store_hookshot", "0") == "1";

                // Check the name of the item to see if it can be traded.
                if (shovelCheck || featherCheck || magicRodCheck || hookshotCheck)
                {
                    // Store the traded item name, null the equipment index so boomerang
                    // can be added, and start the DialogPath to finish the trade.
                    saveManager.SetString("tradded_item", itemName);
                    gameManager.Equipment[index] = null;
                    gameManager.StartDialogPath("npc_hidden_boomerang");

                    // Null out the item has been obtained or it will be restored on save load.
                    saveManager.RemoveString("store_" + itemName);
                }
                // The NPC rejected the item.
                else gameManager.StartDialogPath("npc_hidden_reject");
            }

            // Boomerang Return: Hidden Goriya
            var boomerangReturnValue = saveManager.GetString("boomerang_trade_return");
            if (!string.IsNullOrEmpty(boomerangReturnValue))
            {
                // Remove the boomerang and store that it was traded back.
                gameManager.RemoveItem("boomerang", 1);
                saveManager.RemoveString("store_boomerang");
                saveManager.RemoveString("boomerang_trade_return");

                // Return the traded item.
                var trade = saveManager.GetString("tradded_item");
                var item = new GameItemCollected(trade) { Count = 1 };
                PickUpItem(item, true);
                _pickupDialogOverride = "npc_hidden_4";
                saveManager.RemoveString("tradded_item");
            }

            // Spawn the Ghost who wants to go to the house by the bay.
            var spawnGhostValue = saveManager.GetString("spawn_ghost");
            if (!string.IsNullOrEmpty(spawnGhostValue))
            {
                _spawnGhost = true;
            }

            // We don't need this sticking around so remove it.
            var mountainPhoto = saveManager.GetString("start_mountain_photo");
            if (!string.IsNullOrEmpty(mountainPhoto))
            {
                saveManager.RemoveString("start_mountain_photo");
            }

            // Borrow the rooster from the hen house (after dungeon 8 is finished).
            var borrowRooster = saveManager.GetString("borrow_rooster");
            if (borrowRooster == "0")
            {
                saveManager.RemoveString("borrow_rooster");
                Followers.Remove(_objRooster);
                Map.Objects.RemoveObject(_objRooster);
                _objRooster = null;
            }
            else if (borrowRooster == "1")
            {
                saveManager.RemoveString("borrow_rooster");
                var itemRooster = new GameItemCollected("rooster") { Count = 1 };
                PickUpItem(itemRooster, false, false, true);
                _objRooster = new ObjCock(Map,
                    (int)(EntityPosition.X + AnimationHelper.DirectionOffset[Direction].X),
                    (int)(EntityPosition.Y + AnimationHelper.DirectionOffset[Direction].X),
                    "borrow_rooster");
                Map.Objects.SpawnObject(_objRooster);
                Map.Objects.RegisterAlwaysAnimateObject(_objRooster);
                _objRooster.BorrowRooster();
                Followers.Add(_objRooster);
            }

            // Take a walk with Marin (after dungeon 8 is finished).
            var borrowMarin = saveManager.GetString("borrow_marin");
            if (borrowMarin == "0")
            {
                saveManager.RemoveString("borrow_marin");
                Followers.Remove(_objMarin);
                _objMarin = null;
            }
            else if (borrowMarin == "1")
            {
                saveManager.RemoveString("borrow_marin");
                var itemMarin = new GameItemCollected("marin") { Count = 1 };
                PickUpItem(itemMarin, false, false, true);
                SpawnMarin();
            }

            // Prevent entry to Egg with a follower during second chance.
            var egg_turn_around = saveManager.GetString("egg_turn_around");

            // Stop walking, reset timer, remove strings from SaveManager.
            if (egg_turn_around == "0")
            {
                _eggPreventStart = false;
                _forceWalking = false;
                _eggPreventTimer = 0;
                saveManager.RemoveString("link_move");
                saveManager.RemoveString("egg_turn_around");
            }
            // Drop any objects (like rooster), walk in reverse, start timer to disable.
            else if (egg_turn_around == "1")
            {
                ReleaseCarriedObject();
                saveManager.SetString("link_move", "0,1");
                SeqLockPlayer();
                _eggPreventStart = true;
            }
            // Progress the timer to stop the follower turnaround sequence.
            if (_eggPreventStart)
            {
                _eggPreventTimer += Game1.DeltaTime;
                if (_eggPreventTimer > 2000)
                    saveManager.SetString("egg_turn_around", "0");
            }

            // When the player collects the slime key without damage.
            var slime_key_grabbed = saveManager.GetString("slimekeychallenge");
            if (!string.IsNullOrEmpty(slime_key_grabbed)) 
            {
                // See if the player didn't take damage.
                var pothole_achievement = saveManager.GetString("pothole_field_achievement", "0");
                
                // Grant the achievement for no damage.
                if (pothole_achievement == "1")
                    AchievementManager.Earn(34);

                // Remove the strings.
                saveManager.RemoveString("slimekeychallenge");
                saveManager.RemoveString("pothole_field_achievement");
            }

            // If the player kills the four ghosts with the giant one included.
            var gy_achievement = saveManager.GetString("graveyard_achievement");
            if (!string.IsNullOrEmpty(gy_achievement)) 
            {
                if (gy_achievement == "1")
                    AchievementManager.Earn(41);
                saveManager.RemoveString("graveyard_achievement");
            }

            // When the player collects the angler key without damage.
            var angler_key_grabbed = saveManager.GetString("anglerkeychallenge");
            if (!string.IsNullOrEmpty(angler_key_grabbed)) 
            {
                // See if the player didn't take damage.
                var yarna_achievement = saveManager.GetString("yarna_desert_achievement", "0");
                
                // Grant the achievement for no damage.
                if (yarna_achievement == "1")
                    AchievementManager.Earn(52);

                // Remove the strings.
                saveManager.RemoveString("anglerkeychallenge");
                saveManager.RemoveString("yarna_desert_achievement");
            }

            // When the player collects the face key without damage.
            var face_key_grabbed = saveManager.GetString("facekeychallenge");
            if (!string.IsNullOrEmpty(face_key_grabbed)) 
            {
                // See if the player didn't take damage.
                var ruins_achievement = saveManager.GetString("ancient_ruins_achievement", "0");
                
                // Grant the achievement for no damage.
                if (ruins_achievement == "1")
                    AchievementManager.Earn(75);

                // Remove the strings.
                saveManager.RemoveString("facekeychallenge");
                saveManager.RemoveString("ancient_ruins_achievement");
            }
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  HIT PLAYER CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private bool WasBlocked(Box box, RectangleF boxRect, Vector2 boxCenter, Vector2 bodyCenter, int direction)
        {
            // Nothing can be blocked unless the shield is out.
            if (!IsBlockingState() && (!_bootsRunning || !CarryShield))
                return false;

            // Get the difference between the centers.
            Vector2 delta = bodyCenter - boxCenter;

            float halfW = boxRect.Width / 2f;
            float halfH = boxRect.Height / 2f;

            // A check to see if the two boxes are colliding.
            bool inside = Math.Abs(delta.X) <= halfW && Math.Abs(delta.Y) <= halfH;

            // Get the opposite direction.
            bool facingDir = Direction == ReverseDirection(direction);

            // If everything passes, it's a block.
            return (!inside || box.Intersects(ShieldBlockBox)) && facingDir;
        }

        public bool HitPlayer(Box box, HitType type, int damage, float pushMultiplier = 1.75f, int missileDir = -1, double damageCooldown = 0, bool unblockable = false, bool singleAxisPush = false)
        {
            // Prevent hits when playing the ocarina.
            if (PreventDamageTimer > 0)
                return false;

            // Get the box as a floats rectangle.
            RectangleF boxRect = box.Rectangle();

            // Get the centers of the rectangles.
            Vector2 boxCenter = new Vector2(boxRect.X + boxRect.Width / 2f, boxRect.Y + boxRect.Height / 2f); ;
            Vector2 bodyCenter = BodyRectangle.Center;
            Vector2 boxDir = bodyCenter - boxCenter;
            Vector2 vecDirection;
            int intDirection;

            // Get the intersecting rectangle.
            RectangleF intersection = BodyRectangle.GetIntersection(box.Rectangle());

            // If the rectangle isn't empty then use the box to calculate the direction.
            if (intersection.Width <= 0 || intersection.Height <= 0)
                vecDirection = boxDir;
            else
            {
                Vector2 interCenter = new Vector2(intersection.X + intersection.Width / 2f, intersection.Y + intersection.Height / 2f);
                vecDirection = bodyCenter - interCenter;
            }
            // Snap the push direction to its dominant axis so the knockback is purely horizontal or vertical.
            if (singleAxisPush)
            {
                if (Math.Abs(vecDirection.X) >= Math.Abs(vecDirection.Y))
                    vecDirection.Y = 0;
                else
                    vecDirection.X = 0;
            }
            // Normalize the direction vector.
            if (vecDirection.LengthSquared() > 0.000001f)
                vecDirection.Normalize();

            // If the direction was passed use that. Otherwise calculate it. This
            // is used solely to determine if a block succeeds based on direction.
            if (missileDir >= 0)
                intDirection = missileDir;
            else
                intDirection = ToDirection(vecDirection);

            // Check if it's not unblockable and that it was successfully blocked.
            bool blocked = !unblockable && WasBlocked(box, boxRect, boxCenter, bodyCenter, intDirection);

            // Try to damage the player.
            return HitPlayer(vecDirection * pushMultiplier, type, damage, blocked, damageCooldown);
        }

        public bool HitPlayer(Vector2 direction, HitType type, int damage, bool blocked, double damageCooldown = 0)
        {
            // If the invincibility cheat was enalbed, don't even hit the player.
            if (GameSettings.ChInvincibility)
                return false;

            // Check conditions where the player wouldn't take damage.
            if (_hitCount > 0 || CurrentState == State.Dying || CurrentState == State.PickingUp ||
                CurrentState == State.Drowning || CurrentState == State.Drowned || CurrentState == State.Knockout ||
                IsDiving() || Game1.GameManager.UseShockEffect || !UpdatePlayer || Hookshot.IsMoving)
            {
                return false;
            }
            // Check if the block conditions passed.
            if (blocked)
            {
                // Blocking projectiles plays a "ting" sound effect.
                if (type == HitType.Projectile)
                    Game1.AudioManager.PlaySoundEffect("D360-22-16");

                return false;
            }
            // jump a little if we get hit by a spike
            if ((type & HitType.Spikes) != 0)
            {
                _body.Velocity.Z = 1.0f;
            }
            // redirect the down force to the sides
            if (Map.Is2dMap && _body.IsGrounded && direction.Y > 0)
            {
                direction.X += Math.Sign(direction.X) * Math.Abs(direction.Y) * 0.5f;
                direction.Y = 0;
            }
            // fall down on damage taken while climbing
            if (Map.Is2dMap && _isClimbing)
                _isClimbing = false;

            // Hit velocity is responsible for knockback.
            if (!_isRafting && !_isTrapped)
                _hitVelocity += direction;
            else
                _hitVelocity = Vector2.Zero;

            if (_hitCount > 0)
                return false;

            Game1.AudioManager.PlaySoundEffect("D370-03-03");

            // Use the calculated cooldown if not set by an external call.
            if (damageCooldown != 0)
                _hitCount = damageCooldown;
            else
                _hitCount = CooldownTime;

            Game1.GameManager.InflictDamage(damage);

            // Shake the screen on damage if the user has it enabled.
            var freezeTime = 67;
            var shakeMult = (100.0f / freezeTime) * MathF.PI;
            Game1.FreezeTime = Game1.TotalGameTime + freezeTime;
            if (GameSettings.ExScreenShake)
                Game1.GameManager.ShakeScreen(freezeTime, (int)(direction.X * 2), (int)(direction.Y * 2), shakeMult, shakeMult);
            UpdateDamageShader();

            // Used for failing achievements.
            FailDamageAchievements(false);

            // Reset the boots when hit.
            if (_bootsRunning)
            {
                _bootsStop = true;
                _bootsReset = true;
            }

            return true;
        }

        private void FailDamageAchievements(bool fromHoleCheck)
        {
            // Probably gonna need this a lot.
            var saveManager = Game1.GameManager.SaveManager;

            // If taking damage during the BowWow Moblin Cave rescue.
            if (!string.IsNullOrEmpty(saveManager.GetString("moblin_cave_achievement")))
                saveManager.RemoveString("moblin_cave_achievement");

            // If taking damage during the golden leaves quest.
            if (!string.IsNullOrEmpty(saveManager.GetString("leaf_achievement_count")))
                saveManager.RemoveString("leaf_achievement_count");

            // If taking damage in pothole field when getting the slime key.
            if (!string.IsNullOrEmpty(saveManager.GetString("pothole_field_achievement")))
                saveManager.RemoveString("pothole_field_achievement");

            // If taking damage in yarna desert when getting the slime key.
            if (!fromHoleCheck && !string.IsNullOrEmpty(saveManager.GetString("yarna_desert_achievement")))
                saveManager.RemoveString("yarna_desert_achievement");

            // If taking damage at any point between the Master Stalos fights.
            if (!string.IsNullOrEmpty(saveManager.GetString("mstalfos_achievement")))
                saveManager.RemoveString("mstalfos_achievement");

            // If taking damage in ancient ruins when getting the face key.
            if (!string.IsNullOrEmpty(saveManager.GetString("ancient_ruins_achievement")))
                saveManager.RemoveString("ancient_ruins_achievement");

            // If taking damage during the dream shrine achievement fail it.
            if (!string.IsNullOrEmpty(saveManager.GetString("dream_shrine_achievement")))
            {
                saveManager.RemoveInt("dream_shrine_count");
                saveManager.RemoveString("dream_shrine_achievement");
            }
        }

        private void FailMoblinCaveAchievement()
        {
            // Fail the moblin cave achievement if jumping or using the shield.
            if (!string.IsNullOrEmpty(Game1.GameManager.SaveManager.GetString("moblin_cave_achievement")))
                Game1.GameManager.SaveManager.RemoveString("moblin_cave_achievement");
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  DEATH CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void OnDeath()
        {
            // We only need to run this once.
            if (CurrentState == State.Dying)
                return;

            // Check to see if this is death by the shop keeper.
            bool shopPunish = Game1.GameManager.SaveManager.GetString("stoleItem", "0") == "1";
            bool shopFinish = Game1.GameManager.SaveManager.GetString("punishActive", "0") == "1";

            // Has potion?
            var potion = Game1.GameManager.GetItem("potion");

            // Use the potion if available but not if the death is by the shopkeeper.
            if (potion != null && potion.Count >= 1 && !shopPunish)
            {
                Game1.GameManager.RemoveItem("potion", 1);
                Game1.GameManager.HealPlayer(99);
                ItemDrawHelper.EnableHeartAnimationSound();
                return;
            }
            // Prevent future shopkeeper visits from slaughtering the player.
            if (shopPunish)
                Game1.GameManager.SaveManager.SetString("stoleItem", "0");

            // If carrying the rooster.
            if (IsFlying())
                ReleaseCarriedObject();

            // Set the dying state which prevents this from running again.
            CurrentState = State.Dying;
            Animation.Play("dying");

            // Stop the music and play the death sound effect.
            Game1.AudioManager.StopMusic(true);
            Game1.AudioManager.PlaySoundEffect("D370-08-08");

            // Set the correct start frame depending on the direction the player is facing
            int[] dirToFrame = { 0, 2, 1, 3 };
            Animation.SetFrame(dirToFrame[Direction]);

            // Begin the game over sequence.
            if (!shopFinish)
                ((GameOverSystem)Game1.GameManager.GameSystems[typeof(GameOverSystem)]).StartDeath();
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  MOVEMENT CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UpdateIgnoresZ()
        {
            if (CurrentState == State.Swimming ||
                CurrentState == State.Hookshot ||
                CurrentState == State.TeleporterUp ||
                CurrentState == State.TeleportFallWait || _isFlying || _isGrabbed || _isClimbing)
                _body.IgnoresZ = true;
            else
                _body.IgnoresZ = false;
        }

        private void UpdateIgnoreHeight()
        {
            _body.IgnoreHeight = _isRafting || _railJump;
        }

        private void UpdateWalking()
        {
            if (DisableInput) return;

            if (CurrentState != State.Idle &&
                !IsAttackingState() &&
                !IsChargingState() &&
                !IsBlockingState() &&
                !IsPushingState() &&
                !IsSwimmingState() &&
                CurrentState != State.CarryingItem &&
                CurrentState != State.Powdering &&
                CurrentState != State.Bombing &&
                CurrentState != State.MagicRod &&
                CurrentState != State.Throwing &&
                (CurrentState != State.Carrying || _isFlying) &&
                (!IsJumpingState() || _railJump) || !CanWalk || _isRafting)
                return;

            var walkVelocity = Vector2.Zero;

            if (!_isLocked && (!IsAttackingState() || !_body.IsGrounded))
                walkVelocity = ControlHandler.GetMoveVector2();

            var walkVelLength = walkVelocity.Length();
            if (walkVelLength > 1)
                walkVelocity.Normalize();

            var vectorDirection = ToDirection(walkVelocity);

            if (_bootsRunning && (walkVelLength < 0 || vectorDirection != ReverseDirection(Direction)))
            {
                if (_bootsLastDirection != Direction)
                    _bootsStop = true;

                if (!_bootsStop)
                {
                    _moveVelocity = AnimationHelper.DirectionOffset[Direction] * BootsRunningSpeed;

                    // can move up or down while running
                    if (Direction % 2 != 0)
                        _moveVelocity.X += walkVelocity.X;
                    else if (Direction % 2 == 0)
                        _moveVelocity.Y += walkVelocity.Y;
                }
                if (_isTrapped)
                {
                    _bootsStop = true;
                    _moveVelocity = Vector2.Zero;
                }
            }
            else if (walkVelLength > 0)
            {
                // slow down in the grass
                if (_body.CurrentFieldState.HasFlag(MapStates.FieldStates.Grass) && _body.IsGrounded)
                    _currentWalkSpeed *= 0.75f;

                // slow down in the water
                if (_body.CurrentFieldState.HasFlag(MapStates.FieldStates.Water) && _body.IsGrounded)
                {
                    _currentWalkSpeed *= 0.75f;

                    _waterSoundCounter += Game1.DeltaTime;
                    if (_waterSoundCounter > 250)
                    {
                        _waterSoundCounter -= 250;
                        Game1.AudioManager.PlaySoundEffect("D360-14-0E", false);
                    }
                }

                // do not walk when trapped
                if (!_isTrapped)
                {
                    _isWalking = true;

                    if (_body.IsGrounded)
                    {
                        // after hitting the ground we still have _lastMoveVelocity
                        if (!_body.WasGrounded)
                            _moveVelocity = Vector2.Zero;

                        _moveVelocity += walkVelocity * _currentWalkSpeed;
                    }
                }
                // Update the direction the player is walking towards.
                if (!IsAttackingState() && !IsChargingState())
                {
                    Direction = ToDirection(walkVelocity);
                }
            }
            // Allow changing direction when attacking while standing still.
            else
            {
                Vector2 vecMoved = ControlHandler.GetMoveVector2();
                if ((CurrentState == State.Attacking || CurrentState == State.AttackBlocking) &&
                    !_isHoldingSword && vecMoved != Vector2.Zero && _body.IsGrounded)
                    Direction = ToDirection(vecMoved);
            }
            _lastBaseMoveVelocity = _moveVelocity;

            // Set the move vector for air movement while jumping off of a cliff.
            if (!_startedJumping && !_hasStartedJumping && _body.WasGrounded && !_body.IsGrounded)
                _lastMoveVelocity = _moveVelocity;

            // Standing on the ground, always reset the running jump variable.
            if (_body.IsGrounded && _body.Velocity.Z <= 0)
            {
                if (CurrentState == State.AttackJumping)
                    CurrentState = State.Attacking;
                _bootsRunJump = false;
            }
            else
            {
                // Detect first-frame running jump
                if (_bootsWasRunning)
                    _bootsRunJump = true;

                // Calculate target and difference
                Vector2 targetVelocity = walkVelocity * _currentWalkSpeed;
                float velocityDiff = (_lastMoveVelocity - targetVelocity).Length();
                float lerpAmount = Math.Clamp((0.05f / velocityDiff) * Game1.TimeMultiplier, 0, 1);

                if (velocityDiff > 0 && walkVelocity != Vector2.Zero)
                {
                    bool lockX = Math.Abs(_lastMoveVelocity.X) >= Math.Abs(_lastMoveVelocity.Y);

                    // Compute perpendicular Lerp as usual
                    Vector2 newMoveVelocity = Vector2.Lerp(_lastMoveVelocity, targetVelocity, lerpAmount);

                    if (_bootsRunJump)
                    {
                        // Running jump: determine locked axis and apply smooth slowdown if opposite input.
                        float lockedAxis = lockX ? _lastMoveVelocity.X : _lastMoveVelocity.Y;
                        float inputAxis = lockX ? walkVelocity.X : walkVelocity.Y;

                        lockedAxis = (Math.Sign(inputAxis) != Math.Sign(lockedAxis) && inputAxis != 0)
                            ? MathHelper.Lerp(lockedAxis, inputAxis * _currentWalkSpeed, lerpAmount)
                            : Math.Sign(lockedAxis) * _bootsMaxSpeed;

                        // Recombine axes
                        _lastMoveVelocity = lockX
                            ? new Vector2(lockedAxis, newMoveVelocity.Y)
                            : new Vector2(newMoveVelocity.X, lockedAxis);
                    }
                    else
                    {
                        // Normal jump: just use Lerp on both axes
                        _lastMoveVelocity = newMoveVelocity;
                    }
                }
                _moveVelocity = _lastMoveVelocity;
            }
        }

        private void OnMoveCollision(Values.BodyCollision collision)
        {
            // Detect hitting crystals made by the smash box created when dashing with Pegasus Boots.
            if (_bootsRunning)
            {
                var dashSmashHit = Map.Objects.Hit(this, _crystalSmashBox.Center, _crystalSmashBox, HitType.CrystalSmash, 0, false, false);
                if (dashSmashHit == Values.HitCollision.Blocking)
                    return;
            }
            // 3D: Detect colliding with a solid object and perform knockback.
            if (!Is2DMode && CurrentState == State.Idle && _bootsWasRunning)
            {
                var knockBack = false;
                var pushUpward = 2.65f;
                var pushBackward = 1.85f;
                _knockBackVelocity = Vector2.Zero;

                if ((collision & Values.BodyCollision.Horizontal) != 0 && Direction % 2 == 0)
                {
                    var dirX = (collision & Values.BodyCollision.Left) != 0 ? -1 : 1;
                    _body.Velocity.X = -dirX;
                    _knockBackVelocity.X = -dirX * pushBackward;
                    knockBack = true;

                    if (GameSettings.ScreenShake)
                        Game1.GameManager.ShakeScreen(600, 1.00f, 0.50f, 11.0f, 5.00f, dirX, 1);
                }
                if ((collision & Values.BodyCollision.Vertical) != 0 && Direction % 2 != 0)
                {
                    var dirY = (collision & Values.BodyCollision.Top) != 0 ? -1 : 1;
                    _body.Velocity.Y = -dirY;
                    _knockBackVelocity.Y = -dirY * pushBackward;
                    knockBack = true;

                    if (GameSettings.ScreenShake)
                        Game1.GameManager.ShakeScreen(600, 0.50f, 1.00f, 5.00f, 11.0f, 1, dirY);
                }
                if (knockBack)
                {
                    _bootsRunning = false;
                    _bootsCounter = 0;
                    _body.Velocity.Z = pushUpward;
                    CurrentState = State.BootKnockback;

                    var damageOrigin = BodyRectangle.Center;
                    var damageBox = _body.BodyBox.Box;
                    damageBox.X += AnimationHelper.DirectionOffset[Direction].X;
                    damageBox.Y += AnimationHelper.DirectionOffset[Direction].Y;

                    Game1.AudioManager.PlaySoundEffect("D360-11-0B");

                    Map.Objects.Hit(this, damageOrigin, damageBox, HitType.PegasusBootsPush, 0, false);
                }
            }

            // what is this?
            if ((collision & Values.BodyCollision.Floor) != 0)
            {
                _moveVelocity = _lastMoveVelocity * 0.5f;
                _lastBaseMoveVelocity = _moveVelocity;
            }

            if (CurrentState == State.BootKnockback &&
                (collision & Values.BodyCollision.Floor) != 0)
            {
                CurrentState = State.Idle;
                _body.Velocity.Z = 0;
            }

            if (Is2DMode)
                OnMoveCollision2D(collision);
            else
            {
                if (_isRotating)
                    return;

                // colliding horizontally or vertically? -> start pushing
                if ((CurrentState == State.Idle || CurrentState == State.Blocking) &&
                    _body.IsGrounded && (_body.Velocity != Vector3.Zero || _body.VelocityTarget != Vector2.Zero) &&
                    ((collision & Values.BodyCollision.Horizontal) != 0 && (Direction == 0 || Direction == 2) ||
                    (collision & Values.BodyCollision.Vertical) != 0 && (Direction == 1 || Direction == 3)))
                {
                    var box = _body.BodyBox.Box;

                    // offset by one in the walk direction
                    box.X += AnimationHelper.DirectionOffset[Direction].X;
                    box.Y += AnimationHelper.DirectionOffset[Direction].Y;
                    var cBox = Box.Empty;
                    var outBox = Box.Empty;

                    // check if the object we are walking into is actually an object where the push animation should be played
                    if (ControlHandler.GetMoveVector2() != Vector2.Zero)
                    {
                        if (Map.Objects.Collision(box, cBox, _body.CollisionTypes, Values.CollisionTypes.PushIgnore, Direction, _body.Level, ref outBox))
                        {
                            if (CurrentState == State.Blocking)
                                CurrentState = State.BlockPushing;
                            else
                                CurrentState = State.Pushing;
                        }
                    }
                }

                if (CurrentState == State.Swimming)
                {
                    if ((collision & Values.BodyCollision.Horizontal) != 0)
                        _moveVelocity.X = 0;
                    if ((collision & Values.BodyCollision.Vertical) != 0)
                        _moveVelocity.Y = 0;
                }

                // used for scripting (final stript stop at the top of the stairs)
                Game1.GameManager.SaveManager.SetString("link_move_collision", "1");

                // stop the hit velocity if the are colliding with a wall
                // this was done because the player pushes into the hitVelocity direction
                if ((collision & Values.BodyCollision.Horizontal) != 0 && _body.VelocityTarget.X == 0)
                    _hitVelocity.X = 0;
                if ((collision & Values.BodyCollision.Vertical) != 0 && _body.VelocityTarget.Y == 0)
                    _hitVelocity.Y = 0;

                // When pushing towards the wall with the sword out, start poking it with the sword.
                if (IsChargingState() &&
                    ((collision & Values.BodyCollision.Left) != 0 && Direction == 0 ||
                    (collision & Values.BodyCollision.Top) != 0 && Direction == 1 ||
                    (collision & Values.BodyCollision.Right) != 0 && Direction == 2 ||
                    (collision & Values.BodyCollision.Bottom) != 0 && Direction == 3))
                {
                    if (_swordPokeCounter <= 0)
                    {
                        IsPoking = true;
                        _pokeStart = true;

                        Animation.Play("poke_" + Direction);
                        PlayWeaponAnimation("poke", Direction);
                  
                        if (!swordpoke_keeps_charge)
                            _swordChargeCounter = sword_charge_time;

                        // If in an accompanying state -> switch to a merged state.
                        if (IsSwimmingState())
                            CurrentState = State.AttackSwimming;
                        else if (IsJumpingState())
                            CurrentState = State.AttackJumping;
                        else if (IsBlockingState())
                            CurrentState = State.AttackBlocking;
                        else
                            CurrentState = State.Attacking;
                    }
                    _swordPokeCounter -= Game1.DeltaTime;
                }
                else
                {
                    IsPoking = false;
                    _swordPokeCounter = _swordPokeTime;
                }
            }
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  MOVEMENT PHYSICS CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private static int BarrierToFacingDirection(int fieldBarrierIndex)
        {
            // The field barrier vs. the direction Link needs to be facing
            // to be knocked back into the nearby field. 
            return fieldBarrierIndex switch
            {
                0 => 3,  // - Top field, facing Down.
                1 => 1,  // - Bottom field, facing Up.
                2 => 2,  // - Left field, facing Right.
                3 => 0,  // - Right field, facing Left.
                _ => 0
            };
        }

        private void CancelBootsVelocity(int Index)
        {
            // If knockback direction would cross into another field, cancel it
            if (Direction == BarrierToFacingDirection(Index))
            {
                _knockBackVelocity = Vector2.Zero;
                _body.Velocity = Vector3.Zero;
                _body.VelocityTarget = Vector2.Zero;
                CurrentState = State.Idle;
            }
        }

        private void CancelRepelVelocities(bool cancelBodyVelocity)
        {
            // Eliminate all knockback velocities.
            _hitVelocity = Vector2.Zero;
            _repelVelocity = Vector2.Zero;
            _shieldVelocity = Vector2.Zero;
            _body.VelocityTarget = Vector2.Zero;

            // Also fully eliminate any body velocity.
            if (cancelBodyVelocity)
            {
                _body.Velocity.X = 0;
                _body.Velocity.Y = 0;
            }
        }

        private void PreventFieldKnockback()
        {
            // Do not apply this on 2D maps.
            if (Map.Is2dMap)
                return;

            // If it's null then Classic Camera is disabled.
            if (FieldBarrier == null)
                return;

            // Loop through the field barriers.
            for (int i = 0; i < FieldBarrier.Length; i++)
            {
                // Create a new box that is slightly larger than the field barrier box.
                int buffer = 2;
                Box fieldBox = new Box(FieldBarrier[i].Position.X - buffer, FieldBarrier[i].Position.Y - buffer, 0, FieldBarrier[i].Width + buffer * 2, FieldBarrier[i].Height + buffer * 2, 16);

                // If knocked into it then stop all velocities.
                if (_body.BodyBox.Box.Intersects(fieldBox))
                {
                    // Cancel repel velocities. Boots knockback uses a different function so it's possible
                    // to still be knocked back while inside a field barrier if parallel to the field.
                    if (CurrentState != State.BootKnockback)
                        CancelRepelVelocities(true);
                    else
                        CancelBootsVelocity(i);
                    return;  
                }
            }
        }

        private void UpdateMovementPhysics()
        {
            // Slows down walking movement when the player is hit.
            var moveMultiplier = MathHelper.Clamp(1f - _hitVelocity.Length(), 0, 1);

            // Calculate the movement velocity before being repelled.
            Vector2 finalMove = _moveVelocity * moveMultiplier + _hitVelocity;

            //-----------------------------------------------------------------------------------------------------
            // Sword Repel Fightback: Directional influence "fights" knockback velocity.
            //-----------------------------------------------------------------------------------------------------
            // If sword repelled, movement still partially fights the knockback.
            if (_repelVelocity.Length() > 0.01f)
            {
                // The part of the player's input pointing back into the knockback bleeds off the repel (pushing along it does nothing).
                var stickInput = ControlHandler.GetMoveVector2();
                if (stickInput.Length() > 1f)
                    stickInput.Normalize();

                var fightbackAmount = _body.IsGrounded ? 0.40f : 0.85f;

                var repelNormal = Vector2.Normalize(_repelVelocity);
                float opposing = -Vector2.Dot(stickInput * fightbackAmount, repelNormal);

                if (opposing > 0f)
                    _repelVelocity -= repelNormal * opposing * _repelCancelFactor * Game1.TimeMultiplier;

                finalMove = _repelVelocity;
            }
            //-----------------------------------------------------------------------------------------------------
            // Shield Repel Fightback: Directional influence "fights" knockback velocity.
            //-----------------------------------------------------------------------------------------------------
            // If shield repelled, movement still partially fights the knockback.
            if (_shieldVelocity.Length() > 0.01f)
            {
                var stickInput = ControlHandler.GetMoveVector2();
                if (stickInput.Length() > 1f)
                    stickInput.Normalize();

                var fightbackAmount = _body.IsGrounded ? 0.40f : 0.85f;

                var shieldRepelNormal = Vector2.Normalize(_shieldVelocity);
                float opposing = -Vector2.Dot(stickInput * fightbackAmount, shieldRepelNormal);

                if (opposing > 0f)
                    _shieldVelocity -= shieldRepelNormal * opposing * _shieldCancelFactor * Game1.TimeMultiplier;

                finalMove = _shieldVelocity;
            }
            //-----------------------------------------------------------------------------------------------------
            // Final Movement + Hookshot Velocity Cancel: Move the player unless using the hookshot.
            //-----------------------------------------------------------------------------------------------------
            // Hookshot cancels out target velocity.
            if (CurrentState != State.Hookshot)
            {
                // 2D: Get the target velocity.
                if (Map.Is2dMap)
                { 
                    // The final target velocity is calculated through combining all possible velocities.
                    _body.VelocityTarget = _moveVector2D * moveMultiplier + _hitVelocity + _repelVelocity + _shieldVelocity; 
                }
                // 3D: Get the target velocity.
                else 
                {
                    // Boots knockback overrides velocity. 
                    if (CurrentState == State.BootKnockback)
                        _body.VelocityTarget = _knockBackVelocity;

                    // Get the final move velocity.
                    else
                        _body.VelocityTarget = finalMove;
                }
            }
            // Store the current movement velocity and reset it.
            LastMoveVector = _moveVelocity;
            _moveVelocity = Vector2.Zero;

            //-----------------------------------------------------------------------------------------------------
            // Sword Repel Knockback: Overrides all other velocities.
            //-----------------------------------------------------------------------------------------------------
            // If the player is sword repelled then knock them back.
            if (_repelVelocity.Length() > 0.01f)
            {
                // Normalize the sword repel velocity.
                var repelNormal = _repelVelocity;
                repelNormal.Normalize();

                // Reduce velocity gradually while on the ground or while swimming.
                if (_body.IsGrounded || IsSwimmingState())
                {
                    float slowDownAmount = 0.12f + (_repelVelocity.Length() * 0.015f);
                    _repelVelocity -= repelNormal * slowDownAmount * Game1.TimeMultiplier;
                }
                // Also reduce velocity while in the air but only up to a certain point.
                else
                {
                    if (_repelVelocity.Length() > 1.20)
                    {
                        float slowDownAmount = 0.05f + (_repelVelocity.Length() * 0.015f);
                        _repelVelocity -= repelNormal * slowDownAmount * Game1.TimeMultiplier;
                    }
                }
                // Snap to zero when velocity reaches the threshold.
                if (_repelVelocity.Length() < 0.25f)
                {
                    _repelVelocity = Vector2.Zero;
                }
                // If the repel crosses into the field barrier then cancel the velocity.
                PreventFieldKnockback();
            }
            // Zero out sword repel velocity when it doesn't meet the thresholds.
            else
                _repelVelocity = Vector2.Zero;

            //-----------------------------------------------------------------------------------------------------
            // Shield Repel Knockback: Knockback from bumping enemy with shield. Overrides movement.
            //-----------------------------------------------------------------------------------------------------
            // If the player is shield repelled then knock them back.
            if (_shieldVelocity.Length() > 0.01f)
            {
                // Normalize the shield repel velocity.
                var shieldRepelNormal = _shieldVelocity;
                shieldRepelNormal.Normalize();

                // Reduce velocity gradually while on the ground or while swimming.
                if (_body.IsGrounded || Map.Is2dMap|| IsSwimmingState())
                {
                    float slowDownAmount = 0.12f + (_shieldVelocity.Length() * 0.015f);
                    _shieldVelocity -= shieldRepelNormal * slowDownAmount * Game1.TimeMultiplier;
                }
                // Also reduce velocity while in the air but only up to a certain point.
                else
                {
                    if (_shieldVelocity.Length() > 1.20)
                    {
                        float slowDownAmount = 0.12f + (_shieldVelocity.Length() * 0.015f);
                        _shieldVelocity -= shieldRepelNormal * slowDownAmount * Game1.TimeMultiplier;
                    }
                }
                // Snap to zero when velocity reaches the threshold.
                if (_shieldVelocity.Length() < 0.25f)
                {
                    _shieldVelocity = Vector2.Zero;
                }
                // If the repel crosses into the field barrier then cancel the velocity.
                PreventFieldKnockback();
            }
            // Zero out shield repel velocity when it doesn't meet the thresholds.
            else
                _shieldVelocity = Vector2.Zero;

            //-----------------------------------------------------------------------------------------------------
            // Damage Hit Knockback: Knockback from taking damage. Movement can somewhat counter it.
            //-----------------------------------------------------------------------------------------------------
            // If the player is hit perform a knockback.
            if (_hitCount > 0 && _hitVelocity.Length() > 0.05f * Game1.TimeMultiplier)
            {
                // Normalize the hit velocity.
                var hitNormal = _hitVelocity;
                hitNormal.Normalize();

                // Reduce velocity gradually while on the ground or while swimming.
                if (_body.IsGrounded || IsSwimmingState())
                {
                    var slowDownAmount = 0.05f + MathHelper.Clamp(_hitVelocity.Length() / 25f, 0, 0.05f);
                    _hitVelocity -= hitNormal * slowDownAmount * Game1.TimeMultiplier;
                }
                // Also reduce velocity while in the air but only up to a certain point.
                else
                {
                    if (_hitVelocity.Length() > 1.20)
                    {
                        float slowDownAmount = 0.05f + (_hitVelocity.Length() * 0.015f);
                        _hitVelocity -= hitNormal * slowDownAmount * Game1.TimeMultiplier;
                    }
                }
                // Snap to zero when velocity reaches the threshold.
                if (_hitVelocity.Length() < 0.25f)
                {
                    _hitVelocity = Vector2.Zero;
                }
                // If the hit crosses into the field barrier then cancel the velocity.
                PreventFieldKnockback();
            }
            // Zero out hit velocity when it doesn't meet the thresholds.
            else
                _hitVelocity = Vector2.Zero;

            //-----------------------------------------------------------------------------------------------------
            // Pegasus Boots Knockback: Knockback when smashing into the wall with pegasus Boots.
            //-----------------------------------------------------------------------------------------------------
            if (!Is2DMode && CurrentState == State.BootKnockback && _knockBackVelocity.Length() > 0.01f)
            {
                // Decay Z velocity over time to create an arc.
                float decayZ = 0.15f;
                _body.Velocity.Z -= decayZ * Game1.TimeMultiplier;

                // Normalize the boots knockback velocity.
                var knockbackNormal = _knockBackVelocity;
                knockbackNormal.Normalize();

                // Apply slowdown over time.
                float slowDownAmount = 0.08f;
                _knockBackVelocity -= knockbackNormal * slowDownAmount * Game1.TimeMultiplier;

                // Snap to zero when velocity reaches the threshold.
                if (_knockBackVelocity.Length() < 0.2f)
                    _knockBackVelocity = Vector2.Zero;

                // Check if player has hit the ground and remove all velocities.
                if (_body.Position.Z <= 0 && _body.Velocity.Z <= 0)
                {
                    _body.Position.Z = 0;
                    _body.Velocity.Z = 0;
                    _knockBackVelocity = Vector2.Zero;
                    CurrentState = State.Idle;
                }
                // This will probably never occur, but prevent a knockback into another field.
                PreventFieldKnockback();
            }
            else if (!Is2DMode && CurrentState == State.BootKnockback)
            {
                _knockBackVelocity = Vector2.Zero;
                CurrentState = State.Idle;
            }
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  RAIL JUMP CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        public Vector2 RailJumpTarget() => _railJumpTargetPosition;
        public float RailJumpSpeed() => _railJumpSpeed;
        public float RailJumpHeight() => _railJumpHeight;
        public float RailJumpAmount() => _railJump ? _railJumpPercentage : 0f;

        public void StartRailJump(Vector2 goalPosition, float jumpHeightMultiply, float jumpSpeedMultiply, float goalPositionZ = 0)
        {
            if (_isRafting)
                return;

            if (CurrentState == State.Swimming)
                CurrentState = State.Idle;

            if (CurrentState == State.Carrying)
                Jump(force:true, playSoundEffect:false);
            else
                if (!Jump(force:false, playSoundEffect:false))
                    return;

            Game1.AudioManager.PlaySoundEffect("D360-08-08");

            _railJump = true;

            _railJumpStartPosition = EntityPosition.Position;
            _railJumpTargetPosition = goalPosition;

            // values for distance of 16
            _railJumpSpeed = 0.045f * jumpSpeedMultiply;
            _railJumpHeight = 12 * jumpHeightMultiply;
            _railJumpPositionZ = goalPositionZ;

            _railJumpPercentage = 0;

            _body.IgnoresZ = true;
            _body.Velocity.Z = 0;
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  SWIMMING CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UpdateSwimmingPartOne()
        {
            // Used only to lift the Flying Rooster out of the water.
            if (_swimRoosterPickup)
            {
                // Keep "_swimPickup" true until "Pulling" state is finished then set to false.
                _swimRoosterPickup = CurrentState == State.Pulling;
                return;
            }
            // we cant use the field state of the body because the raft updates the state while exiting
            var fieldState = SystemBody.GetFieldState(_body);

            // start/stop swimming or drowning
            if (!_isRafting && !_isFlying && fieldState.HasFlag(MapStates.FieldStates.DeepWater) && CurrentState != State.Dying)
            {
                if (!IsJumpingState() && CurrentState != State.PickingUp && _body.IsGrounded)
                {
                    ReleaseCarriedObject();
                    var inLava = fieldState.HasFlag(MapStates.FieldStates.Lava);

                    if (HasFlippers && !inLava && CurrentState != State.Swimming)
                    {
                        // Update the state to the appropriate swimming state.
                        if (Map.Is2dMap && (CurrentState == State.Attacking || CurrentState == State.AttackSwimming))
                            CurrentState = State.AttackSwimming;
                        else if (Map.Is2dMap && (CurrentState == State.Charging || CurrentState == State.ChargeSwimming))
                            CurrentState = State.ChargeSwimming;
                        else
                            CurrentState = State.Swimming;

                        // Sometimes when jumping into water, this doesn't go false when it should.
                        _railJump = false;

                        // Reset the "was flying" state when swimming. Swimming doesn't matter if player was flying.
                        _wasFlying = false;

                        // Only push the player if he walks into the water and does not jump. Jumping is handled in another location.
                        if (!_lastFieldState.HasFlag(fieldState))
                            _body.Velocity = new Vector3(_body.VelocityTarget.X, _body.VelocityTarget.Y, 0) * 0.35f;

                        // splash effect
                        var splashAnimator = new ObjAnimator(Map, 0, 0, 0, 3, Values.LayerPlayer, "Particles/splash", "idle", true);
                        splashAnimator.EntityPosition.Set(new Vector2(
                            _body.Position.X + _body.OffsetX + _body.Width / 2f,
                            _body.Position.Y + _body.OffsetY + _body.Height - _body.Position.Z - 6));
                        Map.Objects.SpawnObject(splashAnimator);

                        Game1.AudioManager.PlaySoundEffect("D360-14-0E");

                        _diveCounter = 0;
                        _swimBoostCount = 0;
                        _swimVelocity = Vector2.Zero;
                    }
                    else if (!HasFlippers || inLava)
                    {
                        if (CurrentState != State.Drowning && CurrentState != State.Drowned)
                        {
                            // Only push Link if he walks into the water.
                            if (!_lastFieldState.HasFlag(fieldState))
                            {
                                // Use the controller move vector to determine the offset.
                                Vector2 move = ControlHandler.GetMoveVector2();
                                if (move != Vector2.Zero)
                                {
                                    move.Normalize();
                                    Vector2 offset = move * 5.5f;

                                    // The Y axis needs a lesser nudge when going down and a huge nudge going up.
                                    if (offset.Y < -5f) { offset = new Vector2(offset.X, -2f); }
                                    if (offset.Y > 5f) { offset = new Vector2(offset.X, 9f); }

                                    // Move Link to the offset position.
                                    EntityPosition.Set(EntityPosition.Position + offset);
                                }
                            }
                            // Spawn in the splash effect.
                            var splashAnimator = new ObjAnimator(Map, 0, 0, 0, 3, Values.LayerPlayer, "Particles/splash", "idle", true);
                            splashAnimator.EntityPosition.Set(new Vector2(
                                _body.Position.X + _body.OffsetX + _body.Width / 2f,
                                _body.Position.Y + _body.OffsetY + _body.Height - _body.Position.Z - 6));
                            Map.Objects.SpawnObject(splashAnimator);

                            Game1.AudioManager.PlaySoundEffect("D370-03-03");

                            CurrentState = State.Drowning;
                            _drownedInLava = inLava;

                            // Deal damage when in lava.
                            _hitCount = inLava ? CooldownTime : 0;
                        }
                    }
                }
            }
            else if (CurrentState == State.Swimming && (!IsTransitioning || !Map.Is2dMap))
                CurrentState = State.Idle;

            if (CurrentState == State.Swimming)
            {
                EntityPosition.Z = 0;
                _body.IsGrounded = true;
            }
            _lastFieldState = _body.CurrentFieldState;
        }

        private void UpdateSwimmingPartTwo()
        {
            // Update drowning.
            if (CurrentState == State.Drowning)
            {
                if (Animation.CurrentFrameIndex < 2)
                {
                    _body.Velocity = Vector3.Zero;
                    EntityPosition.Set(new Vector2(
                        MathF.Round(EntityPosition.X), MathF.Round(EntityPosition.Y)));
                }
                if (Animation.CurrentFrameIndex == 2)
                {
                    IsVisible = false;
                    CurrentState = State.Drowned;
                    _drownResetCounter = 500;
                }
            }
            // Update drowned.
            else if (CurrentState == State.Drowned)
            {
                _drownResetCounter -= Game1.DeltaTime;
                if (_drownResetCounter <= 0)
                {
                    CurrentState = State.Idle;
                    CanWalk = true;
                    IsVisible = true;

                    _hitCount = CooldownTime;

                    if (_drownedInLava)
                    {
                        if (!GameSettings.ChInvincibility)
                            Game1.GameManager.CurrentHealth -= (int)MathF.Ceiling(2 * (GameSettings.DamageFactor * 0.25f));
                        _drownedInLava = false;
                    }
                    _body.CurrentFieldState = MapStates.FieldStates.None;
                    EntityPosition.Set(_drownResetPosition);
                }
            }
            // Update swimming.
            if (CurrentState == State.Swimming)
            {
                if (_diveCounter > -100)
                {
                    _diveCounter -= Game1.DeltaTime;

                    // Stop diving.
                    if (ControlHandler.ButtonPressed(ControlHandler.CancelButton))
                        _diveCounter = 0;
                }

                // Start diving.
                else if (ControlHandler.ButtonPressed(ControlHandler.CancelButton))
                    StartDiving(1500);

                if (_swimBoostCount > -300)
                    _swimBoostCount -= Game1.DeltaTime;

                else if (ControlHandler.ButtonPressed(ControlHandler.ConfirmButton))
                {
                    _swimBoostCount = 300;
                    Game1.AudioManager.PlaySoundEffect("D360-15-0F");
                }

                if (_swimBoostCount > 0)
                    _moveVelocity *= SwimSpeedA;
                else
                    _moveVelocity *= SwimSpeed;

                var distance = _moveVelocity - _swimVelocity;
                var length = distance.Length();
                if (distance != Vector2.Zero)
                    distance.Normalize();

                if (length < 0.045f)
                    _swimVelocity = _moveVelocity;
                else
                    _swimVelocity += distance * (_swimBoostCount > 0 ? 0.06f : 0.045f) * Game1.TimeMultiplier;

                _moveVelocity = _swimVelocity;
            }
            else
            {
                _diveCounter = 0;
            }
        }

        private void StartDiving(int diveTime)
        {
            // splash effect
            var splashAnimator = new ObjAnimator(Map, 0, 0, 0, 0, Values.LayerTop, "Particles/splash", "idle", true);
            splashAnimator.EntityPosition.Set(new Vector2(
                _body.Position.X + _body.OffsetX + _body.Width / 2f,
                _body.Position.Y + _body.OffsetY + _body.Height - _body.Position.Z - 3));
            Map.Objects.SpawnObject(splashAnimator);

            Game1.AudioManager.PlaySoundEffect("D360-14-0E");

            _diveCounter = diveTime;
        }

        private void UpdateDive()
        {
            _diveCounter -= Game1.DeltaTime;
        }

        private void UpdateDrownResetPosition()
        {
            // save the last position the player is grounded to use for the reset position if the player drowns
            if (!IsJumpingState() &&
                CurrentState != State.Drowning &&
                CurrentState != State.Drowned && _body.IsGrounded)
            {
                // center the position
                // can lead to the position being inside something
                var bodyCenter = new Vector2(EntityPosition.X, EntityPosition.Y - _body.Height / 2f);
                bodyCenter.X = (int)(bodyCenter.X / 16) * 16 + 8;
                bodyCenter.Y = (int)(bodyCenter.Y / 16) * 16 + 8 + _body.Height / 2f;

                // found new reset position?
                if (!Map.GetFieldState(bodyCenter).HasFlag(MapStates.FieldStates.DeepWater))
                {
                    var bodyBox = new Box(
                        bodyCenter.X + _body.OffsetX,
                        bodyCenter.Y + _body.OffsetY, 0, _body.Width, _body.Height, _body.Depth);
                    var cBox = Box.Empty;

                    // check it the player is not standing inside something
                    if (!Map.Objects.Collision(bodyBox, Box.Empty, _body.CollisionTypes | Values.CollisionTypes.DrownExclude, 0, 0, ref cBox))
                        _drownResetPosition = bodyCenter;
                }
            }
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  ANIMATION / GRAPHICS CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        
        public void PlayWeaponAnimation(string animationName, int direction = -1)
        {
            // Support custom sword level 2 attack animations with "l2" added. So if the
            // animation is "attack_0" then the sword 2 equivalent would be "attackl2_0".
            var dirSuffix  = direction >= 0 ? "_" + direction : "";
            var basicName  = animationName + dirSuffix;
            var nameLevel2 = animationName + "l2" + dirSuffix;
            var showLevel2 = AnimatorWeapons.HasAnimation(nameLevel2) && Game1.GameManager.SwordLevel > 1;
            AnimatorWeapons.Play(showLevel2 ? nameLevel2 : basicName);
        }

        private void PlayWalkingAnimation(string shieldString, int animDirection, bool isBlocking, bool isClimbing = false)
        {
            // Default to standard walking animation.
            string targetAnimation = "walk" + shieldString + animDirection;

            // Check if the player is blocking.
            if (isBlocking)
            {
                // If walking or climbing play walk otherwise play stand.
                targetAnimation = (_isWalking || isClimbing)
                    ? "walkb" + shieldString + animDirection
                    : "standb" + shieldString + animDirection;
            }
            // If already playing the correct animation we can exit now.
            if (Animation.AnimationID == targetAnimation)
            {
                // Reset landing from a jump flag.
                _landedFromJump = false;

                // If climbing then only play the animation when player is walking.
                if (isClimbing)
                    Animation.IsPlaying = _isWalking;

                return;
            }
            // Retrieve the previous frame information.
            int prevFrame = Animation.CurrentFrameIndex;
            double prevTime = Animation.FrameCounter % Animation.CurrentFrame.FrameTime;

            // When landing from a jump, we must force the landing animation frame or it will
            // appear as if Link is "sliding" because the jump frame matches the first walk frame.
            if (_landedFromJump)
            {
                // Force the start of the second frame.
                prevFrame = 1;
                prevTime = 0;

                // Reset landing from a jump flag.
                _landedFromJump = false;
            }
            // Play the new animation and pass on the current frame and duration.
            Animation.Play(targetAnimation, prevFrame, prevTime);

            // If climbing then only play the animation when player is walking.
            if (isClimbing)
                Animation.IsPlaying = _isWalking;
        }

        private bool CheckForcePushAnimation()
        {
            // Do not force pushing animation if direction is not held.
            var input = ControlHandler.GetMoveVector2();
            if (input == Vector2.Zero || ToDirection(input) != Direction)
                return false;

            // The moving stone must be the one in front of Link.
            var depth = 24;
            var box_X = EntityPosition.X + _walkDirection[Direction].X * depth - 8;
            var box_Y = BodyRectangle.Center.Y + _walkDirection[Direction].Y * depth - 8;
            var probe = new Box(box_X, box_Y, 0, 16, 16, 16);

            // Link body box must intersect box of stone that was pushed.
            return probe.Intersects(_pushingBox);
        }

        private void UpdateAnimation()
        {
            // When transitioning fields with Classic Camera Classic Scrolling,
            // preserve the current animation (usually "walking" animation).
            if (MapManager.Camera.FieldScrolling)
                return;

            // When getting shocked, preserve the current animation during the shock.
            if (Game1.GameManager.UseShockEffect)
                return;

            // If under the effects of a vacuum, use the rotational direction.
            var animDirection = _isRotating
                ? _rotateDirection
                : Direction;

            // Include the shield in the animation string if available ("s_" for shield, "ms_" for mirror shield).
            string shieldString = CarryShield
                ? Game1.GameManager.ShieldLevel == 2 ? "ms_" : "s_"
                : "_";

            // Pegasus boots running animation.
            if (!IsTransitioning && (_bootsHolding || _bootsRunning || _forceWalking))
            {
                _swordChargeCounter = sword_charge_time;

                // Running in place charging, or run with the shield in front of the player.
                if (!_bootsRunning)
                    Animation.Play("walk" + shieldString + animDirection);
                else
                    Animation.Play((CarryShield ? "walkb" : "walk") + shieldString + animDirection);

                // Movement speed is doubled.
                if (!_forceWalking)
                    Animation.SpeedMultiplier = 2.0f;

                return;
            }
            // A backup path for when the above fails. If walking is forced, walking should happen.
            if (_forceWalking)
            {
                Animation.Play((CarryShield ? "walkb" : "walk") + shieldString + animDirection);
                return;
            }
            // When the rotation from a vacuum ends, the body and weapon animators need to be resynced.
            if ((IsChargingState() || _bootsRunning) && _wasRotating)
            {
                Direction = _rotateDirection;
                Animation.Play("stand" + shieldString + Direction);
                PlayWeaponAnimation("stand", Direction);
            }
            _wasRotating = false;

            // Restore normal animation speed.
            Animation.SpeedMultiplier = 1.0f;

            // Play animation based on Link's current state and other factors.
            if (CurrentState == State.FinalStand)
                Animation.Play("final_stand");
            else if ((CurrentState == State.Idle && !_isWalking && _body.IsGrounded) ||
                (CurrentState == State.Charging && !_isWalking) ||
                (CurrentState == State.Rafting && !_isWalking) ||
                CurrentState == State.Teleporting ||
                CurrentState == State.ShowInstrumentPart3 ||
                CurrentState == State.TeleportFall ||
                CurrentState == State.TeleporterUp ||
                CurrentState == State.FallRotateEntry)
            {
                Animation.Play("stand" + shieldString + animDirection);
            }
            else if (CurrentState == State.ChargeJumping)
                Animation.Play("cjump" + shieldString + animDirection);
            else if ((CurrentState == State.Idle || CurrentState == State.Charging || CurrentState == State.Rafting) && _isWalking)
                PlayWalkingAnimation(shieldString, animDirection, false);
            else if (CurrentState == State.Blocking || CurrentState == State.ChargeBlocking || CurrentState == State.BlockPushing)
            {
                // Hack: When "PushingTimer" is active, it forces "pushing" animation. This is because in the
                // original game, if pushing a stone while blocking, it will show the "pushing" animation while
                // the object is in motion. When it stops, it goes back to "blocking" animation. The hack tries
                // to simulate this behavior. The timer forces the animation only while it is active and while
                // the player continues to push on the original stone that started moving.
                if (PushingTimer > 0 && CheckForcePushAnimation())
                    Animation.Play("push_" + animDirection);
                else
                    PlayWalkingAnimation(shieldString, animDirection, true);
            }
            else if ((CurrentState == State.Carrying || CurrentState == State.CarryingItem) && !_isFlying)
                Animation.Play((_isWalking && _body.IsGrounded ? "walkc_" : "standc_") + animDirection);
            else if (IsFlying())
                Animation.Play("flying_" + animDirection);
            else if (CurrentState == State.Pushing)
                Animation.Play("push_" + animDirection);
            else if (CurrentState == State.Grabbing)
                Animation.Play("grab_" + animDirection);
            else if (CurrentState == State.Pulling)
                Animation.Play("pull_" + animDirection);
            else if (CurrentState == State.Swimming)
            {
                Animation.Play(_diveCounter > 0 ? "dive" : "swim_" + animDirection);
                if (_swimVelocity.Length() < 0.1 && !IsTransitioning)
                    Animation.IsPlaying = false;
            }
            else if (CurrentState == State.Drowning)
                Animation.Play("drown");

            // If anything forced walking, disable it now that the animation has played.
            if (!IsTransitioning)
                _isWalking = false;
        }

        private void UpdateDamageShader()
        {
            if (_hitCount > 0)
                _sprite.SpriteShader = (CooldownTime - _hitCount) % (BlinkTime * 2) < BlinkTime ? Resources.DamageSpriteShader0 : null;
            else
                _sprite.SpriteShader = null;
        }

        private void UpdateSpriteShadow()
        {
            // If shadows is disabled then draw a sprite shadow.
            if (!GameSettings.EnableShadows)
            {
                if (_spriteShadow == null)
                {
                    _spriteShadow = new ObjSpriteShadow(Map, this, Values.LayerPlayer, "sprshadowm");
                    Map.Objects.RegisterAlwaysAnimateObject(_spriteShadow);
                }
            }
            // Remove the sprite shadow if shadows was enabled.
            else if (_spriteShadow != null)
            {
                Map.Objects.RemoveObject(_spriteShadow);
                _spriteShadow = null;
            }
            // If the shadow is spawned but the map has changed.
            if (_spriteShadow != null && _spriteShadow.Map != Map)
            {
                // Remove the old sprite shadow.
                if (_spriteShadow.Map != null)
                    _spriteShadow.Map.Objects.RemoveObject(_spriteShadow);

                // Repawn the shadow.
                Map.Objects.SpawnObject(_spriteShadow);
                Map.Objects.RegisterAlwaysAnimateObject(_spriteShadow);
                _spriteShadow.Map = Map;
            }
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  LOW HEARTS ALARM CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        public int GetLowHPValue()
        {
            // Depending on how many max hearts Link has determines
            // when "low health" is actually triggered.
            return Game1.GameManager.MaxHearts switch
            {
                // Max Hearts => Health Threshold
                >=12 => 12,
                >=9  => 8, 
                >=6  => 4,
                _    => 2
            };
        }

        private void UpdateLowHealthFlag()
        {
            // If current health is lower than the treshold.
            IsLowHealth = Game1.GameManager.CurrentHealth <= GetLowHPValue();

            // Try to play the heart beeping sound when HP is low.
            if (IsLowHealth)
                UpdateHeartWarningSound();
        }

        private void UpdateHeartWarningSound()
        {
            // Don't play the beep if the user disabled it or the game disabled it.
            if (!GameSettings.HeartBeep || !_enableHealthBeep)
            {
                _lowHealthBeepCounter = 0;
                return;
            }
            // Increment the beep counter.
            _lowHealthBeepCounter += Game1.DeltaTime;

            // When exceeding the counter play the beep.
            if (_lowHealthBeepCounter > 800)
            {
                // Reset counter and play the sound.
                _lowHealthBeepCounter = 0;
                Game1.AudioManager.PlaySoundEffect("D370-04-04");
            }
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  FIELD / FIELD BARRIER CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void CreateFieldBarrier()
        {
            // Create the field barrier colliders.
            FieldBarrier = new ObjFieldBarrier[4];
            FieldBarrier[0] = new ObjFieldBarrier(Map, CurrentField.X - 16, CurrentField.Y - 16, Values.CollisionTypes.Field, new Rectangle(0, 0, 192, 16));
            FieldBarrier[1] = new ObjFieldBarrier(Map, CurrentField.X - 16, CurrentField.Y + 128, Values.CollisionTypes.Field, new Rectangle(0, 0, 192, 16));
            FieldBarrier[2] = new ObjFieldBarrier(Map, CurrentField.X - 16, CurrentField.Y, Values.CollisionTypes.Field, new Rectangle(0, 0, 16, 128));
            FieldBarrier[3] = new ObjFieldBarrier(Map, CurrentField.X + 160, CurrentField.Y, Values.CollisionTypes.Field, new Rectangle(0, 0, 16, 128));

            // Spawn in the field barrier colliders.
            Map.Objects.SpawnObject(FieldBarrier[0]);
            Map.Objects.SpawnObject(FieldBarrier[1]);
            Map.Objects.SpawnObject(FieldBarrier[2]);
            Map.Objects.SpawnObject(FieldBarrier[3]);
        }

        private void UpdateFieldBarrier()
        {
            // Don't update unless the field has changed.
            if (CurrentField == ContrastField) return;

            // Spawn in the field barrier rectangles.
            FieldBarrier[0].SetPosition(CurrentField.X - 16, CurrentField.Y - 16);
            FieldBarrier[1].SetPosition(CurrentField.X - 16, CurrentField.Y + 128);
            FieldBarrier[2].SetPosition(CurrentField.X - 16, CurrentField.Y);
            FieldBarrier[3].SetPosition(CurrentField.X + 160, CurrentField.Y);
        }

        private void DestroyFieldBarrier()
        {
            // Nobody likes crashes so verify it's null.
            if (FieldBarrier == null) return;

            // Destroy the current field barrier and nullify it.
            foreach (var fBarrier in FieldBarrier)
                Map.Objects.RemoveObject(fBarrier);

            PreventReset = false;
            FieldBarrier = null;
        }

        // Becomes true when a field transition is fully completed: camera has fully
        // transitioned to the new field and Link is standing in the new field.
        public bool FieldTransition(Rectangle field) => FieldChangeComplete && field != CurrentField;

        private void UpdateCurrentField()
        {
            // Latch the travel direction once per frame so "ObjLink" and the camera can't read
            // different values from "FieldScrollPoint" within the same frame.
            UpdateScrollDirection();

            // Set the current field that Link is on. When "Classic Scrolling" is disabled, Link's
            // center point is used. When enabled, the point is 4 pixels inward from his sprite boundary.
            CurrentField = Map.GetField(FieldScrollPoint);

            // We only use the field barrier when "Classic Camera" is active.
            if (Camera.ClassicMode)
            {
                // Fires for one frame after the camera finishes scrolling to the new field.
                FieldChangeComplete = false;

                if (_fieldChangePending && !MapManager.Camera.FieldScrollActive)
                {
                    FieldChangeComplete = true;
                    _fieldChangePending = false;
                }
                // If Link moved a large number of pixels (more than a single tile) he most likely
                // teleported. Detect this and use it to prevent scrolling on the next camera pan.
                var moveDelta = (EntityPosition.Position - _lastFieldCheckPosition).Length();
                var teleported = moveDelta > 16;
                _lastFieldCheckPosition = EntityPosition.Position;

                // Detect when the field has changed.
                FieldChange = CurrentField != ContrastField;

                // Store the previous field that was just left.
                if (FieldChange)
                {
                    // Set the previous field from the contrasting field which was set on the previous frame. 
                    PreviousField = ContrastField;
                    _fieldChangePending = true;

                    // Work out which way Link crossed so the push knows which edge to aim for. This has to be captured
                    // here on the frame of the crossing, because ContrastField is only valid at this exact moment.
                    if (_fieldSyncPending || IsTransitioning || teleported || ContrastField.Width <= 0 || ContrastField == Rectangle.Empty)
                    {
                        // During and immediately after a map transition "ContrastField" still belongs to the previous map so 
                        // any comparison against it is garbage. Setting to -1 prevents scrolling the next camera transition.
                        _fieldCrossDirection = -1;

                        // When a teleport happens, we need the camera snap to happen on the very next frame.
                        if (teleported && !IsTransitioning && !_fieldSyncPending)
                            Camera.SnapCameraOnce = true;
                    }
                    // Set the direction that Link is moving in for the next camera scroll.
                    else if (CurrentField.X < ContrastField.X) _fieldCrossDirection = 0; // Left
                    else if (CurrentField.Y < ContrastField.Y) _fieldCrossDirection = 1; // Up
                    else if (CurrentField.X > ContrastField.X) _fieldCrossDirection = 2; // Right
                    else if (CurrentField.Y > ContrastField.Y) _fieldCrossDirection = 3; // Down
                    else _fieldCrossDirection = -1;
                }
                // Block input for the whole scroll.
                if (MapManager.Camera.FieldScrolling)
                {
                    DisableInput = true;
                    DisableItems = true;
                }
                // Check to see if the current field has not yet been set. When a game is started,
                // the first few frames will return (0,0) for the current field position.
                if (new Vector2(CurrentField.X, CurrentField.Y) != Vector2.Zero)
                {
                    // Create the barrier if null or update if it exists.
                    if (FieldBarrier == null)
                        CreateFieldBarrier();
                    else
                        UpdateFieldBarrier();
                }
                // Prevent resetting enemies shortly after map transitions.
                if (PreventResetTimer > 0)
                {
                    PreventResetTimer -= Game1.DeltaTime;
                    if (PreventResetTimer < 0)
                        PreventReset = false;
                }
            }
            // Destroy the barrier if "Classic Camera" is not active.
            else
                DestroyFieldBarrier();
        }

        // Called from "MapManager.UpdateCamera" immediately after "Camera.Center". It has to be
        // there and not in Update, because "Objects.Update" runs before UpdateCamera and Link would
        // otherwise always trail the camera by one frame.
        public void UpdateFieldScrollPush()
        {
            // When classic scrolling is disabled, the direction is stale so clear it so
            // when it is enabled, it won't bug out the camera causing the oscillation bug.
            if (!GameSettings.ClassicScrolling)
            {
                _fieldCrossDirection = -1;
                _wasFieldScrolling = false;
                return;
            }
            // Something else owns Link's position, so scroll the camera but do not move him.
            if (PositionControlled)
            {
                _fieldCrossDirection = -1;
                _wasFieldScrolling = false;
                return;
            }
            // Get that the screen is currently scrolling.
            var scrolling = MapManager.Camera.FieldScrolling && _fieldCrossDirection >= 0;

            // Capture the start and end once, on the first frame of the scroll.
            if (scrolling && !_wasFieldScrolling)
            {
                // The start and end positions of Link when scroll starts/ends.
                _fieldScrollStart = EntityPosition.Position;
                _fieldScrollEnd = GetFieldScrollTarget();

                // The push only ever moves Link deeper into the new field. If he is already
                // past the target the lerp would drag him backwards, so skip it entirely.
                bool backwards;
                switch (_fieldCrossDirection)
                {
                    case 0:  backwards = _fieldScrollEnd.X > _fieldScrollStart.X; break; // Left
                    case 1:  backwards = _fieldScrollEnd.Y > _fieldScrollStart.Y; break; // Up
                    case 2:  backwards = _fieldScrollEnd.X < _fieldScrollStart.X; break; // Right
                    case 3:  backwards = _fieldScrollEnd.Y < _fieldScrollStart.Y; break; // Down
                    default: backwards = true; break;
                }
                if (backwards)
                {
                    _fieldCrossDirection = -1;
                    scrolling = false;
                }
            }
            // Consume the crossing once the scroll ends so it cannot be reused.
            if (!scrolling && _wasFieldScrolling)
                _fieldCrossDirection = -1;

            _wasFieldScrolling = scrolling;

            if (!scrolling)
                return;

            // Derived from the camera's own progress, so the two can never desync. Any curve
            // used here must still reach 1 when the camera arrives or Link stops short.
            var t = MapManager.Camera.FieldScrollProgress;
            t = t * t;

            // Calculate Link's new position as the field scrolls.
            var newPosition = Vector2.Lerp(_fieldScrollStart, _fieldScrollEnd, t);

            // On the raft, move the raft instead. Its position listener places Link on it,
            // so writing his position here would just fight with "OnPositionChange".
            if (_isRafting && _objRaft != null)
            {
                var delta = newPosition - EntityPosition.Position;
                _objRaft.EntityPosition.Set(_objRaft.EntityPosition.Position + delta);
                return;
            }
            // When carrying the rooster, also update the rooster's position.
            if (_isFlying && _objRooster != null)
            {
                var delta = newPosition - EntityPosition.Position;
                _objRooster.EntityPosition.Set(_objRooster.EntityPosition.Position + delta);
                return;
            }
            // Set Link's new position.
            EntityPosition.Set(newPosition);
        }

        private Vector2 GetFieldScrollTarget()
        {
            var field = CurrentField;
            var target = _fieldScrollStart;

            // Padding between Link's entity position and the visible edge of his sprite.
            const int sprLeft = 7, sprRight = 9, sprTop = 16, sprBottom = 0;

            // Only the crossing axis moves. The other axis stays where Link left it.
            switch (_fieldCrossDirection)
            {
                case 0: target.X = field.X + field.Width - 3 - sprRight; break;    // Left
                case 1: target.Y = field.Y + field.Height - 1 - sprBottom; break;  // Up
                case 2: target.X = field.X + 3 + sprLeft; break;                   // Right
                case 3: target.Y = field.Y + sprTop; break;                        // Down
            }
            return target;
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  NPC AVOIDANCE CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UpdateNPCAvoidance()
        {
            // Check if sword hitbox is within NPC hitbox.
            _npcSwordCross = CheckNPCAvoidance();

            // Tracks how far the player traveled when triggering avoidance.
            float travelDistance = 0;

            // Update the distance if avoidance took place.
            if (_npcCrossSword)
                travelDistance = Vector2.Distance(_avoidanceStartPos, EntityPosition.Position);

            // When sword is no longer within NPC hitbox and Link is holding sword, restore charging state.
            if (!_npcSwordCross && _isHoldingSword && _npcCrossSword && (_avoidanceDirection != Direction || travelDistance > 22))
            {
                _npcCrossSword = false;
                Animation.Play("stand" + Direction);
                PlayWeaponAnimation("stand", Direction);
                CurrentState = State.Charging;
                _swordChargeCounter = sword_charge_time;
                _isHoldingSword = false;
            }
            // This probably isn't the best place for this but it's where it logically needs to happen.
            WasHoleReset = false;
        }

        private bool CheckNPCAvoidance()
        {
            // Get a list of NPCs to check if sword crosses their hitbox.
            List<GameObject> npcList = new List<GameObject>();

            Map.Objects.GetComponentList(npcList,
                (int)SwordDamageBox.X, (int)SwordDamageBox.Y,
                (int)SwordDamageBox.Width, (int)SwordDamageBox.Height,
                CollisionComponent.Mask);

            // Loop through the NPCs checking for collision.
            foreach (var npc in npcList)
            {
                if (npc.IsActive)
                {
                    var collisionObject = npc.Components[CollisionComponent.Index] as CollisionComponent;
                    var collisionBody = npc.Components[CollisionComponent.Index] as BodyCollisionComponent;
                    if (collisionObject != null && collisionBody != null && collisionBody.IsActive &&
                        (collisionObject.CollisionType & Values.CollisionTypes.NPC) != 0)
                    {
                        // If the sword box and body box intersect return true.
                        var bodyObject = npc.Components[BodyComponent.Index] as BodyComponent;
                        if (bodyObject != null && SwordDamageBox.Intersects(bodyObject.BodyBox.Box))
                            return true;
                    }
                }
            }
            return false;
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  DREAM SHRINE CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        public void StartBedTransition() => _startBedTransition = true;

        private void UpdateBedTransition()
        {
            if (_startBedTransition && CurrentState == State.Idle)
            {
                CurrentState = State.BedTransition;
                _startBedTransition = false;
                Animation.Play("bed");
            }
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  INSTRUMENTS CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UpdateInstrumentSequence()
        {
            // We need to prevent overlays from being opened because they
            // do not stop the music and it would run out of sync.
            if ((ShowItem != null && ShowItem.Name.StartsWith("instrument")) || IsShowingInstrument())
                Game1.GameManager.InGameOverlay.DisableInventoryToggle = true;

            if (CurrentState == State.ShowInstrumentPart0)
            {
                // is the sound effect still playing?
                if (_instrumentPickupTime + 7500 < Game1.TotalGameTime)
                {
                    Game1.AudioManager.SetMusic(_instrumentMusicIndex[_instrumentIndex], 2);
                    Game1.AudioManager.PlayMusic();
                    Game1.AudioManager.SetMusicStopTime(8);
                    CurrentState = State.ShowInstrumentPart1;
                }
            }
            else if (CurrentState == State.ShowInstrumentPart1)
            {
                _instrumentCounter += Game1.DeltaTime;

                if (_instrumentCounter > 3500)
                {
                    _drawInstrumentEffect = true;
                    Game1.AudioManager.PlaySoundEffect("D360-43-2B", false);
                }
                if (_instrumentCounter > 8000)
                {
                    Game1.AudioManager.SetMusic(-1, 0);
                    Game1.AudioManager.SetMusic(-1, 2);
                    Game1.AudioManager.PlaySoundEffect("D378-44-2C");

                    _instrumentCounter = 0;
                    CurrentState = State.ShowInstrumentPart2;
                }
            }
            else if (CurrentState == State.ShowInstrumentPart2)
            {
                // Some update caused music to continue playing after instrument screen goes white so don't let this happen. 
                Game1.AudioManager.StopMusic(true);

                _instrumentCounter += Game1.DeltaTime;
                var transitionSystem = (MapTransitionSystem)Game1.GameManager.GameSystems[typeof(MapTransitionSystem)];
                transitionSystem.ResetTransition();
                transitionSystem.SetColorMode(Color.White, MathHelper.Clamp(_instrumentCounter / 500f, 0, 1));

                if (_instrumentCounter > 2500)
                {
                    Direction = 3;
                    UpdateAnimation();

                    CurrentState = State.ShowInstrumentPart3;
                    ShowItem = null;
                    _drawInstrumentEffect = false;

                    Game1.GameManager.StartDialogPath($"instrument{_instrumentIndex}Collected");
                }
            }
            else if (CurrentState == State.ShowInstrumentPart3)
            {
                MapTransitionStart = EntityPosition.Position;
                MapTransitionEnd = MapTransitionStart;
                TransitionOutWalking = false;

                EndPickup();

                ((MapTransitionSystem)Game1.GameManager.GameSystems[typeof(MapTransitionSystem)]).AppendMapChange("overworld.map", $"d{_instrumentIndex+1}Finished", false, true, Color.White, true);

                // Set the key-value pair to open the instrument door.
                var openDoor = $"d{_instrumentIndex+1}_cleared";
                Game1.GameManager.SaveManager.SetString(openDoor, "1");
            }
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  HOLE CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UpdateSavePosition()
        {
            Vector2 bodyCenter = _body.BodyBox.Box.Center;
            Vector3 newResetPosition = _holeResetPosition;
            Point currentTilePosition = new Point(((int)bodyCenter.X - Map.MapOffsetX * 16) / 160, ((int)bodyCenter.Y - Map.MapOffsetY * 16) / 128);
            Point tileDiff = currentTilePosition - _lastTilePosition;
            _lastTilePosition = currentTilePosition;

            // update position?
            if (tileDiff != Point.Zero)
            {
                var tileSize = 16;

                // Zero out the alternative reset position.
                _altHoleResetPosition = Vector3.Zero;

                // For X and Y check if the room has changed since last check.
                newResetPosition.X = (tileDiff.X == 0)
                    ? EntityPosition.X
                    : (int)(bodyCenter.X / tileSize + (tileDiff.X > 0 ? 0 : 1)) * tileSize;

                newResetPosition.Y = (tileDiff.Y == 0)
                    ? EntityPosition.Y
                    : (int)(bodyCenter.Y / tileSize + (tileDiff.Y > 0 ? 0 : 1)) * tileSize;

                // Add buffer to push player inward into the field. The direction determines the size of the pixel buffer
                // due to the fact that Link's body box is not perfectly centered on his sprite and has a downward bias.
                if (tileDiff.X > 0) newResetPosition.X += 8;  // Came from left → push right
                if (tileDiff.X < 0) newResetPosition.X -= 8;  // Came from right → push left
                if (tileDiff.Y > 0) newResetPosition.Y += 16; // Came from top → push down
                if (tileDiff.Y < 0) newResetPosition.Y -= 2;  // Came from bottom → push up

                // For Z check if jumping. If on ground set Z to current Z but if in air set Z to what it was before jump.
                newResetPosition.Z = _body.IsGrounded
                    ? EntityPosition.Z
                    : _jumpStartZPos;

                newResetPosition.Z = _isFlying
                    ? _flyStartZPos
                    : newResetPosition.Z;

                // Check if there is no hole at the new position.
                var bodyBox = new Box(newResetPosition.X + _body.BodyBox.OffsetX, newResetPosition.Y + _body.BodyBox.OffsetY, 0, _body.Width, _body.Height, 8);
                var outBox = Box.Empty;

                if (!Map.Objects.Collision(bodyBox, Box.Empty, Values.CollisionTypes.Hole, 0, 0, ref outBox))
                {
                    _holeResetPosition = newResetPosition;
                }
            }
        }

        private void UpdateFallingIntoHole()
        {
            // change the room?
            if (_isFallingIntoHole)
            {
                _holeFallCounter -= Game1.DeltaTime;

                if (_holeFallCounter <= 0)
                {
                    _isFallingIntoHole = false;

                    if (HoleResetRoom != null)
                    {
                        // append a map change
                        ((MapTransitionSystem)Game1.GameManager.GameSystems[
                            typeof(MapTransitionSystem)]).AppendMapChange(HoleResetRoom, HoleResetEntryId);
                    }
                    // teleport on hole fall?
                    else if (HoleTeleporterId >= 0)
                    {
                        _holeTeleportCounter = 0;
                        CurrentState = State.TeleporterUpWait;
                    }
                }
            }
            HoleTeleporterId = -1;

            // finished falling down the hole?
            if (CurrentState == State.Falling && !Animation.IsPlaying)
                OnHoleReset();
        }

        private void SetHoleResetPosition(Vector3 position)
        {
            // Sets hole reset position on map initialization.
            _holeResetPosition = position;

            var offset = Map != null ? new Point(Map.MapOffsetX, Map.MapOffsetY) : Point.Zero;
            _lastTilePosition = new Point(((int)position.X - offset.X * 16) / 160, ((int)position.Y - offset.Y * 16) / 128);
        }

        public void SetHoleResetPosition(Vector3 position, int direction)
        {
            // If Link jumped when setting the hole reset point then use the Z value before the jump started.
            float positionZ = _body.IsGrounded ? position.Z : _jumpStartZPos;

            // Sets an "alternate" reset point when walking over a "ObjHoleResetPoint".
            _altHoleResetPosition = direction switch
            {
                0 => new Vector3(position.X + MathF.Ceiling(_body.Width / 2f), position.Y + 8 + MathF.Ceiling(_body.Height / 2f), positionZ),
                1 => new Vector3(position.X + 8, position.Y + _body.Height + 1, positionZ),
                2 => new Vector3(position.X + 16 - MathF.Ceiling(_body.Width / 2f), position.Y + 8 + MathF.Ceiling(_body.Height / 2f), positionZ),
                3 => new Vector3(position.X + 8, position.Y + 16, positionZ),
                _ => Vector3.Zero
            };
            // Also used for the drown reset point. Instead of opening up the can of worms of converting the drown 
            // reset point to a Vector3 just use the X and Y coordinates from the _altHoleResetPosition.
            _drownResetPosition = new Vector2(_altHoleResetPosition.X, _altHoleResetPosition.Y);
        }

        private void OnHolePull(Vector2 direction, float percentage)
        {
            if (percentage >= 0.55f)
                _canJump = false;
        }

        private void OnHoleAbsorb()
        {
            if (CurrentState == State.Falling ||
                CurrentState == State.TeleporterUpWait ||
                CurrentState == State.TeleporterUp ||
                CurrentState == State.PickingUp ||
                CurrentState == State.Dying)
                return;

            CurrentState = State.Falling;
            HoleFalling = true;

            FreeTrappedPlayer();
            ReleaseCarriedObject();

            _railJump = false;
            _isFallingIntoHole = true;
            _holeFallCounter = 350;

            Animation.Play("fall");
            Game1.AudioManager.PlaySoundEffect("D370-12-0C");

            // Falling down holes should fail damage achievements.
            FailDamageAchievements(true);
        }

        private void OnHoleReset()
        {
            // change the room?
            if (HoleResetRoom != null)
                return;

            _isFallingIntoHole = false;

            CurrentState = State.Idle;
            CanWalk = true;

            _hitCount = CooldownTime;
            Game1.GameManager.InflictDamage(2);

            MoveToHoleResetPosition();
        }

        private void MoveToHoleResetPosition()
        {
            // Create the respawn point and move Link to it.
            Vector3 resetPosition = _holeResetPosition;
            WasHoleReset = true;
            EntityPosition.Set(resetPosition);

            // Alternative reset point.
            var cBox = Box.Empty;
            if (_altHoleResetPosition != Vector3.Zero &&
                Map.Objects.Collision(_body.BodyBox.Box, Box.Empty, _body.CollisionTypes, 0, 0, ref cBox))
            {
                resetPosition = _altHoleResetPosition;
                EntityPosition.Set(resetPosition);
            }
            HoleFalling = false;
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  RAFT CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        public void StartRaftRiding(ObjRaft objRaft)
        {
            // Throw the rooster if using it.
            if (IsFlying())
                ReleaseCarriedObject();

            if (!IsJumpingState())
                CurrentState = State.Rafting;

            _isRafting = true;
            _objRaft = objRaft;
            _body.VelocityTarget = Vector2.Zero;
        }

        private void UpdateRafting()
        {
            if (_isRafting && (CurrentState == State.Rafting || CurrentState == State.Charging || CurrentState == State.ChargeBlocking))
            {
                var moveVelocity = ControlHandler.GetMoveVector2();

                var moveVelocityLength = moveVelocity.Length();
                if (moveVelocityLength > 1)
                    moveVelocity.Normalize();

                if (moveVelocityLength > 0)
                {
                    _isWalking = true;
                    _objRaft.TargetVelocity(moveVelocity * 0.5f);

                    if (CurrentState != State.Charging && CurrentState != State.ChargeBlocking)
                    {
                        var vectorDirection = ToDirection(moveVelocity);
                        Direction = vectorDirection;
                    }
                }
            }
        }

        public void RaftJump(Vector2 targetPosition)
        {
            if (!_isRafting)
                return;

            if (IsJumpingState())
                return;

            CurrentState = State.Jumping;

            Game1.AudioManager.PlaySoundEffect("D360-13-0D");

            Direction = 3;
            Animation.Play("jump_" + Direction);

            if (_objRaft != null)
                _objRaft.Jump(targetPosition, 100);
        }

        private void StopRaft()
        {
            if (_isRafting)
            {
                _objRaft.Body.VelocityTarget = Vector2.Zero;
                _objRaft.Body.AdditionalMovementVT = Vector2.Zero;
                _objRaft.Body.LastAdditionalMovementVT = Vector2.Zero;
            }
        }

        public void ExitRaft()
        {
            CurrentState = State.Idle;

            _isRafting = false;
            _objRaft = null;

            EntityPosition.Set(new Vector2(EntityPosition.X, EntityPosition.Y - 1));
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  ITEM CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UpdateItem()
        {
            // When Classic Camera Classic Scrolling is enabled, preserve item states between transitions. 
            if (!MapManager.Camera.FieldScrolling)
            {
                // Track when the sword is spinning or not spinning.
                var swordSpinning = AnimatorWeapons.AnimationID?.StartsWith("swing") == true;
                var spinIsPlaying = AnimatorWeapons.IsPlaying;
                _isSwordSpinning = swordSpinning && spinIsPlaying;

                // When no longer blocking return to idle state.
                if (CurrentState == State.Blocking)
                    ReturnToIdle();
                else
                    _wasBlocking = false;

                // When grabbing or pulling return to idle state.
                if (CurrentState == State.Grabbing || CurrentState == State.Pulling)
                    ReturnToIdle();

                _isPulling = false;
                _isHoldingSword = false;
                _bootsHolding = false;
                _bootsButtonHeld = false;
            }
            // Currently locked by the "Mad Batter" that upgrades item capacities.
            if (!_isLocked)
            {
                // Interact with the object.
                if ((CurrentState == State.Idle || CurrentState == State.Pushing || CurrentState == State.Swimming || CurrentState == State.CarryingItem) &&
                    ControlHandler.ButtonPressed(ControlHandler.ConfirmButton) && InteractWithObject())
                {
                    InputHandler.ResetInputState();
                    return;
                }
                if (_isTrapped && !_trappedDisableItems &&
                    (ControlHandler.ButtonPressed(CButtons.A) ||
                     ControlHandler.ButtonPressed(CButtons.B) ||
                     ControlHandler.ButtonPressed(CButtons.X) ||
                     ControlHandler.ButtonPressed(CButtons.Y)))
                {
                    _trapInteractionCount--;
                    if (_trapInteractionCount <= 0)
                        FreeTrappedPlayer();
                }

                // Use/Hold/Release item.
                if (!DisableItems && (!_isTrapped || !_trappedDisableItems))
                {
                    // HACK FIX: This fixes a crash if an item is used immediately after loading a save file.
                    if (Direction < 0) { Direction = 0; }

                    for (var i = 0; i < Values.HandItemSlots; i++)
                    {
                        if (Game1.GameManager.Equipment[i] != null &&
                            ControlHandler.ButtonPressed((CButtons)((int)CButtons.A * Math.Pow(2, i))))
                            UseItem(Game1.GameManager.Equipment[i]);

                        if (Game1.GameManager.Equipment[i] != null &&
                            ControlHandler.ButtonDown((CButtons)((int)CButtons.A * Math.Pow(2, i))))
                            HoldItem(Game1.GameManager.Equipment[i]);

                        if (Game1.GameManager.Equipment[i] != null &&
                            ControlHandler.ButtonReleased((CButtons)((int)CButtons.A * Math.Pow(2, i))))
                            ReleaseItem(Game1.GameManager.Equipment[i]);
                    }
                }
                // If an "instant pickup" object was grabbed, force power bracelent pick up until the loop ends.
                if (_instantPickup) { HoldBracelet(); }

            }
            UpdatePegasusBoots();

            // shield pushing
            if (IsBlockingState() || _bootsRunning && CarryShield)
                UpdateShieldPush();

            // pick up animation
            if (CurrentState == State.PreCarrying)
            {
                _preCarryCounter += Game1.DeltaTime;

                // change the animation of the player depending on where the picked up object is
                if (_preCarryCounter > 100)
                    Animation.Play("standc_" + Direction);

                UpdatePositionCarriedObject(EntityPosition);
            }

            // stop attacking
            if (IsAttackingState() && !Animation.IsPlaying)
            {
                _isSwordSpinAttack = false;

                if (_isSwordSpinning)
                {
                    Vector2 vecMoved = ControlHandler.GetMoveVector2();
                    Direction = ToDirection(vecMoved);
                }

                if (!_isHoldingSword || _swordPoked || _stopCharging)
                    ReturnToIdle();
                else
                {
                    // If in another state when charge begins set a dual charging state.
                    CurrentState = CurrentState switch
                    {
                        State.Blocking => State.ChargeBlocking,
                        State.AttackBlocking => State.ChargeBlocking,
                        State.Jumping => State.ChargeJumping,
                        State.AttackJumping => State.ChargeJumping,
                        State.AttackSwimming => State.ChargeSwimming,
                        _ => State.Charging
                    };
                    // Play animation and add to charge counter.
                    PlayWeaponAnimation("stand", Direction);
                    _swordPokeCounter = _swordPokeTime;
                }
            }
            if (IsChargingState())
                UpdateCharging();

            if (IsAttackingState() || CurrentState == State.SwordShow1 || _bootsRunning && CarrySword)
                UpdateAttacking();

            if (!IsShowingInstrument() && !IsShowingCloak() && CurrentState != State.SwordShowLv2)
                UpdatePickup();

            if (!Animation.IsPlaying && (CurrentState == State.Powdering || CurrentState == State.Bombing || CurrentState == State.MagicRod || CurrentState == State.Throwing))
                ReturnToIdle();

            UpdateHookshot();

            if (CurrentState == State.Digging)
                UpdateDigging();

            _wasPulling = _isPulling;
        }

        private bool InteractWithObject()
        {
            var boxSize = 6;
            var interactionBox = new Box(
                EntityPosition.X + _walkDirection[Direction].X * (BodyRectangle.Width / 2 + boxSize / 2) - boxSize / 2,
                BodyRectangle.Center.Y + _walkDirection[Direction].Y * (BodyRectangle.Height / 2 + boxSize / 2) - boxSize / 2, 0,
                boxSize, boxSize, 16);

            return Map.Objects.InteractWithObject(interactionBox);
        }

        private void UseItem(GameItemCollected item)
        {
            Action useItem = item.Name switch
            {
                "sword1"        => UseSword,
                "sword2"        => UseSword,
                "feather"       => UseFeather,
                "toadstool"     => UseToadstool,
                "powder"        => UsePowder,
                "bomb"          => () => UseBomb(false),
                "bow"           => UseArrow,
                "shovel"        => UseShovel,
                "stonelifter"   => UseBracelet,
                "stonelifter2"  => UseBracelet,
                "hookshot"      => UseHookshot,
                "boomerang"     => UseBoomerang,
                "magicRod"      => UseMagicRod,
                "ocarina"       => UseOcarina,
                "pegasusBoots"  => UsePegasusBoots,
                _               => null
            };
            useItem?.Invoke();
        }

        private void HoldItem(GameItemCollected item)
        {
            Action holdItem = item.Name switch
            {
                "sword1"        => HoldSword,
                "sword2"        => HoldSword,
                "feather"       => HoldFeather,
                "shield"        => HoldShield,
                "mirrorShield"  => HoldShield,
                "stonelifter"   => HoldBracelet,
                "stonelifter2"  => HoldBracelet,
                "pegasusBoots"  => HoldPegasusBoots,
                _               => null
            };
            holdItem?.Invoke();
        }

        private void ReleaseItem(GameItemCollected item)
        {
            Action releaseItem = item.Name switch
            {
                "shield"        => ReleaseShield,
                "mirrorShield"  => ReleaseShield,
                "feather"       => ReleaseFeather,
                "stonelifter"   => ReleaseBracelet,
                "stonelifter2"  => ReleaseBracelet,
                _               => null
            };
            releaseItem?.Invoke();
        }

        public void PickUpItem(GameItemCollected itemCollected, bool showItem, bool showDialog = true, bool playSound = true)
        {
            // Exit early if the item is null for some odd reason.
            if (itemCollected == null)
                return;

            // If the player is rafting track the rupees for the 100 rupee achievement.
            if (_isRafting)
            {
                if (itemCollected.Name == "ruby10")
                    _objRaft.TrackRupees(10);
            }
            // Get the item object by name.
            var item = Game1.GameManager.ItemManager[itemCollected.Name];

            // the base item has the max count and other information
            var baseItem = Game1.GameManager.ItemManager[item.Name];

            // Save the game before entering the show animation to support exiting the game while the item is shown.
            _savedPreItemPickup = true;
            if (item.PickUpDialog != null && !Game1.GameManager.SaveManager.HistoryEnabled)
            {
                SaveGameSaveLoad.FillSaveState(Game1.GameManager);
                Game1.GameManager.SaveManager.EnableHistory();
            }
            // We aren't doing this stuff at the moment.
            _showItem = false;
            _pickingUpInstrument = false;
            _pickingUpSword = false;
            _pickingUpAnglerKey = false;

            // Used to replace sword, shield, or power bracelet with a higher level.
            var equipmentPosition = 0;

            // Picking up the Sword off the beach.
            if (item.Name == "sword1")
            {
                // The variable below freezes the world around Link and disables the inventory.
                _pickingUpSword = true;
                Game1.AudioManager.SetMusic(14, 2);

                // Freeze the game. The "sword1Collected:0" event in "scripts.zScript" will unfreeze after a time.
                FreezeAnimations(true);
            }

            // Level 2 Sword was collected from Seashell Mansion.
            else if (item.Name == "sword2")
            {
                equipmentPosition = Game1.GameManager.GetEquipmentSlot("sword1");
                Game1.GameManager.RemoveItem("sword1", 99);
                Game1.GameManager.CollectItem(itemCollected, equipmentPosition);
                Game1.AudioManager.SetMusic(14, 2);
            }

            // The Angler Key (level 3 dungeon) was collected.
            else if (item.Name == "dkey3")
            {
                // Don't temporarily freeze the desert quicksand when picking up the key.
                _pickingUpAnglerKey = true;
            }
            
            // A Shield was collected.
            else if (baseItem.Name == "shield")
            {
                // The mirror shield may be cheat-granted (not legitimately owned). Record
                // this acquisition into the ledger so a later disable can restore the shield.
                var mirrorShield = Game1.GameManager.GetItem("mirrorShield");
                if (mirrorShield != null)
                {
                    if (!Game1.GameManager.SaveManager.ContainsValue("store_shield"))
                        Game1.GameManager.SaveManager.SetString("store_shield", "1");

                    Game1.AudioManager.PlaySoundEffect(item.SoundEffectName, true, 1, 0, item.TurnDownMusic);
                    return;
                }
            }

            // The Mirror Shield was collected.
            else if (item.Name == "mirrorShield")
            {
                // Replace the shield with the mirror shield.
                equipmentPosition = Game1.GameManager.GetEquipmentSlot("shield");
                Game1.GameManager.RemoveItem("shield", 99);
                Game1.GameManager.CollectItem(itemCollected, equipmentPosition);
            }

            // The level 2 Power Bracelet was collected.
            else if (itemCollected.Name == "stonelifter2")
            {
                equipmentPosition = Game1.GameManager.GetEquipmentSlot("stonelifter");
                Game1.GameManager.RemoveItem("stonelifter", 99);
                Game1.GameManager.CollectItem(itemCollected, equipmentPosition);
            }

            // A Piece of Heart was collected.
            else if (itemCollected.Name == "heartMeter")
            {
                // Check if a full heart container is finished and start "heartMeterFilled" path in "script.zScript". 
                var heart = Game1.GameManager.GetItem("heartMeter");
                if (heart?.Count == 3 && !GameSettings.NoHelperText)
                    _additionalPickupDialog = "heartMeterFilled";
            }

            // A full Heart Container was collected.
            else if (itemCollected.Name == "heartMeterFull")
            {
                // Interestingly, this starts track 36 which is the heart container pickup sound + the "dungeon cleared" music that follows. But the only part of this
                // that is played is the heart pickup sound. The music is started more quickly in "script.zScript" where it plays track 23 which is the dungeon cleared
                // music without the sound effect. This allows the music to play more quickly after the heart pickup, as there is a delay after the sound in track 36.
                Game1.AudioManager.SetMusic(36, 2);
            }

            // A Seashell present at the mansion was collected.
            else if (itemCollected.Name == "shellPresent")
            {
                // Get the number of shell presents the player has collected.
                var currentShellPresents = Game1.GameManager.SaveManager.GetString("shell_presents", "0");

                // Add to the total number of shell presents that have been collected so far. These are tracked for "Nothing is Missable" options so that the player can
                // collect the shell presents even if the number of shells exceeds the number required at Seashell Mansion (for example: 7 shells spawns 5 shell present).
                if (int.TryParse(currentShellPresents, out int shellPresents))
                {
                    shellPresents++;
                    Game1.GameManager.SaveManager.SetString("shell_presents", shellPresents.ToString());
                }
            }

            // A Heart was collected.
            if (item.Name == "heart")
            {
                // Play the healing sound effect if HP is lower than current max.
                if (Game1.GameManager.CurrentHealth < Game1.GameManager.MaxHearts * 4)
                    Game1.AudioManager.PlaySoundEffect("D370-06-06");

                // Add 4 hit points to current health.
                Game1.GameManager.CurrentHealth += itemCollected.Count * 4;

                // If the amount of healing exceeds max health then correct it to max.
                if (Game1.GameManager.CurrentHealth > Game1.GameManager.MaxHearts * 4)
                    Game1.GameManager.CurrentHealth = Game1.GameManager.MaxHearts * 4;
            }

            // The item picked up is an accessory.
            else if ((item.ShowAnimation == 1 || item.ShowAnimation == 2) && showItem)
            {
                // Reset the block button sound effect.
                _blockButton = false;

                // Stop all player movement.
                _moveVelocity = Vector2.Zero;
                _hitVelocity = Vector2.Zero;
                _repelVelocity = Vector2.Zero;
                _shieldVelocity = Vector2.Zero;
                _knockBackVelocity = Vector2.Zero;
                _body.Velocity.X = 0;
                _body.Velocity.Y = 0;
                _body.VelocityTarget = Vector2.Zero;

                // Sets the item Link holds over his head.
                ShowItem = item;

                // Hold the item over the head with one or two hands (to the left side or the middle).
                _showItemOffset.X = item.ShowAnimation == 1 ? 0 : -4;
                _showItemOffset.Y = -15;

                // Despawn boomerang or hookshot if it collected an item that is shown.
                if (ShowItem.Name != null)
                {
                    if (Hookshot != null)
                        Hookshot.Despawn();
                    if (Boomerang != null)
                        Boomerang.Despawn();
                }
                // If it's a Guardian Acorn initialize the powerup state for acorn.
                if (ShowItem.Name == "guardianAcorn")
                {
                    Game1.AudioManager.InitGuardianAcorn();
                }
                // Show Link holding the sword when picking up a Piece of Power.
                else if (ShowItem.Name == "pieceOfPower")
                {
                    if (Game1.GameManager.SwordLevel == 1)
                        ShowItem = Game1.GameManager.ItemManager["sword1PoP"];
                    else if (Game1.GameManager.SwordLevel == 2)
                        ShowItem = Game1.GameManager.ItemManager["sword2PoP"];

                    Game1.AudioManager.InitPieceOfPower();
                }
                // Make sure to use the right source rectangle if the shown item does not have one.
                var sourceRectangle = ShowItem.SourceRectangle ?? baseItem.SourceRectangle.Value;
                if (ShowItem.MapSprite != null)
                    sourceRectangle = ShowItem.MapSprite.SourceRectangle;
                else if (baseItem.MapSprite != null)
                    sourceRectangle = baseItem.MapSprite.SourceRectangle;

                // Spawn pickup animation.
                if (item.ShowEffect)
                    Map.Objects.SpawnObject(new ObjPickupAnimation(Map,
                        EntityPosition.X + _showItemOffset.X, EntityPosition.Y - EntityPosition.Z + _showItemOffset.Y - sourceRectangle.Height / 2));

                _showItemOffset -= new Vector2(sourceRectangle.Width / 2f, sourceRectangle.Height);

                // Spawn the converging powerup sparks aimed at the shown item's visual center.
                if (ShowItem.Name == "guardianAcorn" || ShowItem.Name == "sword1PoP" || ShowItem.Name == "sword2PoP")
                {
                    var itemCenter = new Vector2(
                        EntityPosition.X + _showItemOffset.X + sourceRectangle.Width / 2f,
                        EntityPosition.Y + _showItemOffset.Y + sourceRectangle.Height / 2f);

                    Map.Objects.SpawnObject(new ObjPowerupSparks(Map, EntityPosition.Position));
                }
                // Track if picking up while swimming: workaround for Angler Fish heart container.
                _pickupWhileSwimming = IsInWater2D();

                // If a carriable object is currently being held.
                if (CurrentState == State.Carrying)
                {
                    // Hide the object until done showing the item.
                    if (_carriedGameObject is IHasSpriteVisibility carriedObject)
                        carriedObject.Sprite.IsVisible = false;

                    // Store that an object was being carried before grabbing an item.
                    _itemPickupWasCarrying = true;
                }
                // Set the state to picking up and set some other stuff.
                CurrentState = State.PickingUp;
                Game1.GameManager.SaveManager.SetString("player_shows_item", "1");
                Animation.Play("show" + item.ShowAnimation);
                _itemShowCounter = item.ShowTime;
                _showItem = true;

                // Make sure to collect the item the player is currently showing.
                if (_collectedShowItem != null)
                    Game1.GameManager.CollectItem(_collectedShowItem, 0);

                _collectedShowItem = itemCollected;

                if (ShowItem.Name == "sword2")
                {
                    _shownSwordLv2Dialog = false;
                    _showSwordL2ParticleCounter = 0;
                    CurrentState = State.SwordShowLv2;
                }
            }
            else
            {
                // Just run the normal collection method.
                Game1.GameManager.CollectItem(itemCollected, equipmentPosition);
            }
            // Special handling if the item is an instrument.
            if (item.Name.StartsWith("instrument"))
            {
                Game1.AudioManager.SetMusic(26, 2);
                _instrumentPickupTime = Game1.TotalGameTime;
                _instrumentIndex = int.Parse(item.Name.Replace("instrument", ""));
                _pickingUpInstrument = true;
            }
            // If there is a dialog associated with the item show it.
            if (item.PickUpDialog != null && !_showItem && showDialog)
                Game1.GameManager.StartDialogPath(item.PickUpDialog);
            
            // If a sound effect was assigned play it.
            if (playSound && item.SoundEffectName != null)
                Game1.AudioManager.PlaySoundEffect(item.SoundEffectName, true, 1, 0, item.TurnDownMusic);

            // If a music change was assigned play it.
            if (item.MusicName >= 0)
                Game1.AudioManager.SetMusic(item.MusicName, 1);
        }

        private void UpdatePickup()
        {
            // If the item is null then do nothing here.
            if (ShowItem == null)
                return;

            // Disable the inventory while showing the item.
            Game1.GameManager.InGameOverlay.DisableInventoryToggle = true;

            // Decrement the show counter.
            _itemShowCounter -= Game1.DeltaTime;

            // Keep looping until the counter is zero.
            if (_itemShowCounter > 0)
                return;

            // If the item is to be held over Link's head.
            if (_showItem)
            {
                // We only want this section to run once.
                _showItem = false;

                // Show the pickup dialog if the item has one.
                if (ShowItem.PickUpDialog != null)
                {
                    // Check for override text before the normal text.
                    if (string.IsNullOrEmpty(_pickupDialogOverride))
                    {
                        // If the body is grounded or the player is swimming, show the dialog. I don't think there
                        // is actually a situation for swimming, but protect against it just in case there is.
                        if (_body.IsGrounded || _pickupWhileSwimming)
                            Game1.GameManager.StartDialogPath(ShowItem.PickUpDialog);

                        // If in the air, repeat the loop until the body is grounded.
                        else
                        {
                            _showItem = true;
                            return;
                        }
                    }
                    else
                    {
                        Game1.GameManager.StartDialogPath(_pickupDialogOverride);
                        _pickupDialogOverride = null;
                    }
                    // Check for a second message to show after the first.
                    if (!string.IsNullOrEmpty(_additionalPickupDialog))
                    {
                        Game1.GameManager.StartDialogPath(_additionalPickupDialog);
                        _additionalPickupDialog = null;
                    }
                }
                // Additional time after the dialog.
                if (ShowItem.Name == "sword1")
                {
                    _itemShowCounter = 5650;
                    CurrentState = State.SwordShow0;
                }
                else if (ShowItem.Name.StartsWith("instrument"))
                    _itemShowCounter = 1000;
                else
                    _itemShowCounter = 250;
            }
            else
            {
                // Used in "script.zScript" when learning ocarina songs or obtaining the pineapple.
                Game1.GameManager.SaveManager.SetString("player_shows_item", "0");

                // Add the item to the player's inventory.
                if (_collectedShowItem != null)
                {
                    Game1.GameManager.CollectItem(_collectedShowItem, 0);
                    _collectedShowItem = null;
                }
                // If item was a follower then spawn them into the world.
                UpdateFollower(false);

                // Spin the sword after picking it up off the beach.
                if (ShowItem.Name == "sword1")
                {
                    Game1.AudioManager.PlaySoundEffect("D378-03-03");
                    Animation.Play("swing_3");
                    AnimatorWeapons.Play("swing_3");
                    CurrentState = State.SwordShow1;
                    _swordChargeCounter = 1;
                    ShowItem = null;
                }
                // If it's an instrument stop powerup music and set vars for instrument sequence.
                else if (ShowItem.Name.StartsWith("instrument"))
                {
                    Game1.AudioManager.StopPieceOfPower();
                    Game1.AudioManager.StopGuardianAcorn();

                    _itemShowCounter = 0;
                    _instrumentCounter = 0;
                    CurrentState = State.ShowInstrumentPart0;
                }
                // Showing the item is finished.
                else
                {
                    // If the player was carrying an item restore it.
                    if (_itemPickupWasCarrying)
                    {
                        // Show the sprite that was made invisible.
                        if (_carriedGameObject is IHasSpriteVisibility carriedObject)
                            carriedObject.Sprite.IsVisible = true;

                        // Restore the carrying state and reset the variable.
                        CurrentState = State.Carrying;
                        _itemPickupWasCarrying = false;
                    }
                    // The state should still be "PickingUp" so if it is, return to idle state.
                    else if (CurrentState == State.PickingUp)
                        ReturnToIdle();
                    
                    // Set the stored shown item to null.
                    ShowItem = null;

                    // Reset the swimming + picking up tracker.
                    _pickupWhileSwimming = false;
                }
            }
        }

        private void EndPickup()
        {
            _savedPreItemPickup = false;
            SaveGameSaveLoad.ClearSaveState();
            Game1.GameManager.SaveManager.DisableHistory();
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  SWORD CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UpdateSwordSequence()
        {
            if (CurrentState == State.SwordShow1)
            {
                if (!Animation.IsPlaying)
                {
                    Animation.Play("show2");
                    _showSwordLv2Counter = 500;
                    CurrentState = State.SwordShow2;
                    Game1.AudioManager.PlaySoundEffect("D360-07-07");
                    var animation = new ObjSparkingEffect(Map, 0, 0, 0, 0);
                    animation.EntityPosition.Set(new Vector2(BodyRectangle.X, EntityPosition.Y - EntityPosition.Z - 30));
                    Map.Objects.SpawnObject(animation);

                    // "GetObjectsOfType" is not something that should be used often, but in this case it's fine.
                    List<GameObject> objects = Map.Objects.GetObjectsOfType(typeof(ObjMusicTile));

                    // Update that the sword has been collected on the music tile.
                    foreach (var musicTile in objects.OfType<ObjMusicTile>())
                        musicTile.SwordCollected();
                }
                else
                    return;
            }
            else if (CurrentState == State.SwordShow2)
            {
                _showSwordLv2Counter -= Game1.DeltaTime;
                if (_showSwordLv2Counter < 0)
                    CurrentState = State.Idle;
            }
        }

        private void UseSword()
        {
            // Prevent using the sword while it is spinning.
            if (_isSwordSpinning || 
                CurrentState == State.PreCarrying || 
                CurrentState == State.Carrying)
                return;

            // Workaround when charging sword and walking into an NPC.
            if (_npcCrossSword)
                _npcCrossSword = false;

            // Workaround for keeping direction left or right when attacking in 2D maps.
            Direction = GetCorrectDirection(Direction);

            // Skip sword if any of the below is happening.
            if (!IsAttackingState() &&
                !IsBlockingState() &&
                !IsPushingState() &&
                CurrentState != State.Idle &&
                CurrentState != State.Rafting &&
                (CurrentState != State.Jumping || _railJump) &&
                (CurrentState != State.Swimming || !Map.Is2dMap))
                return;

            // Play a random sword slash sound effect.
            var slashSounds = new[] { "D378-02-02", "D378-20-14", "D378-21-15", "D378-24-18" };
            Game1.AudioManager.PlaySoundEffect(slashSounds[Game1.RandomNumber.Next(0, 4)]);

            // Play the attack and weapon animation.
            Animation.Stop();
            AnimatorWeapons.Stop();
            Animation.Play("attack_" + Direction);
            PlayWeaponAnimation("attack", Direction);

            // Set up some states.
            IsPoking = false;
            _pokeStart = false;
            _stopCharging = false;
            _swordPoked = false;
            _shotSword = false;
            _isSwordSpinAttack = false;

            _swordChargeCounter = sword_charge_time;
            _beamDirection = Direction;

            // If the raft is moving stop it during attacks.
            StopRaft();

            // If in an accompanying state -> switch to a merged state.
            CurrentState = CurrentState switch
            {
                State.Blocking => State.AttackBlocking,
                State.Swimming => State.AttackSwimming,
                State.Jumping => State.AttackJumping,
                _ => State.Attacking
            };
        }

        private void HoldSword()
        {
            // Since there is no state to know when the sword is held this
            // variable can be referenced to perform that check.
            _isHoldingSword = true;
        }

        private void UpdateCharging()
        {
            //  Keep the charging state until rail jump has finished.
            if (_railJump && CurrentState == State.ChargeJumping)
                _isHoldingSword = true;

            // stop charging
            if (_isHoldingSword)
            {
                // A workaround when repelling against certain objects that forces
                // poke animation and needs to return to the "stand" animation.
                if (_pokeRepelFix)
                {
                    if (_pokeRepelTimer > 0)
                        _pokeRepelTimer -= Game1.DeltaTime;

                    if (_pokeRepelTimer <= 0)
                    {
                        _pokeRepelFix = false;
                        Animation.Play("stand_" + Direction);
                        PlayWeaponAnimation("stand", Direction);
                    }
                }
                // poke objects that walk into the sowrd
                RectangleF collisionRectangle = AnimatorWeapons.CollisionRectangle;
                var damageOrigin = BodyRectangle.Center;
                SwordDamageBox = GetSwordDamageBox(collisionRectangle);

                // Calculate the damage dealt.
                var hitType = Game1.GameManager.SwordLevel == 1 ? HitType.Sword1 : HitType.Sword2;
                var damage = Game1.GameManager.SwordLevel == 1 ? 1 : 2;

                // If a sword modifier is active double the damage.
                if (Game1.GameManager.CloakType == GameManager.CloakRed || Game1.GameManager.PieceOfPowerIsActive)
                    damage *= 2;

                var pieceOfPower = Game1.GameManager.PieceOfPowerIsActive || Game1.GameManager.CloakType == GameManager.CloakRed;
                var hitCollision = Map.Objects.Hit(this, damageOrigin, SwordDamageBox, hitType | HitType.SwordHold, damage, pieceOfPower, out var direction, true);

                // If the sword is pointed at an NPC.
                if (_npcSwordCross)
                {
                    // Force Link into idle state and put the sword away.
                    _npcCrossSword = true;
                    CurrentState = State.Idle;
                    _isHoldingSword = false;
                    _avoidanceDirection = Direction;
                    _avoidanceStartPos = new Vector2(EntityPosition.X, EntityPosition.Y);
                    return;
                }
                // Start poking?
                if (hitCollision != Values.HitCollision.None && hitCollision != Values.HitCollision.NoneBlocking)
                {
                    // Change the strength of the knockback based on swimming state.
                    var knockback = IsSwimmingState() ? _swimRepelStrength : _baseRepelStrength;

                    // If it's repelling and the player is charging, don't interrupt the charge.
                    if ((hitCollision & Values.HitCollision.RepellingParticle) != 0 && IsChargingState())
                    {
                        // Only spawn a hit particle if the hit actually includes the Particle bit.
                        if ((hitCollision & Values.HitCollision.Particle) != 0 && _hitParticleTime + 225 < Game1.TotalGameTime)
                        {
                            // Use the direction to determine the offset.
                            Point offset = Direction switch
                            {
                                0 => new Point(-8,0),
                                1 => new Point(2,-11),
                                2 => new Point(10,0),
                                3 => new Point(0,5),
                                _ => new Point(0,0),
                            };
                            _hitParticleTime = Game1.TotalGameTime;
                            SpawnRepelParticle(collisionRectangle, offset.X, offset.Y);
                        }
                        // When repelling against sworded enemies.
                        if ((hitCollision & Values.HitCollision.Repelling2) != 0)
                        {
                            CurrentState = State.Attacking;

                            // Play the poking animation when making contact.
                            Animation.Play("poke_" + Direction);
                            PlayWeaponAnimation("poke", Direction);

                            // Reset the sword charge counter.
                            _swordChargeCounter = sword_charge_time;

                            // A workaround when repelling against certain objects that forces
                            // poke animation and needs to return to the "stand" animation.
                            _pokeRepelFix = true;
                            _pokeRepelTimer = 128;
                        }
                    }
                    // If it's a standard sword attack or <other>?
                    else
                    {
                        // Set the sword was poked and play the poke animation.
                        _swordPoked = true;
                        Animation.Play("poke_" + Direction);
                        PlayWeaponAnimation("poke", Direction);

                        // If in an accompanying state then switch to a merged state.
                        if (CurrentState == State.Blocking)
                            CurrentState = State.AttackBlocking;
                        else
                            CurrentState = State.Attacking;
                    }
                    // Knock the player backwards.
                    RepelPlayer(hitCollision, direction, knockback);
                }
                // If there is charge time remaining.
                else if (_swordChargeCounter > 0)
                {
                    // Pause sword charging while camera field is classic scrolling.
                    if (!MapManager.Camera.FieldScrolling)
                    {
                        // Reduce the charge count.
                        _swordChargeCounter -= Game1.DeltaTime;

                        // Finished charging?
                        if (_swordChargeCounter <= 0)
                            Game1.AudioManager.PlaySoundEffect("D360-04-04");
                    }
                }
            }
            else
            {
                // Start the sword spin attack.
                if (_swordChargeCounter <= 0)
                    StartSwordSpin();
                else
                {
                    // If cancelling a charge in the air, resume jumping animation. This
                    // method of charge cancelling works for both 2D and 3D maps. 
                    if (!_railJump && !_body.IsGrounded)
                    {
                        CurrentState = State.Jumping;
                        Animation.Play("jump_" + Direction);
                    }
                    // Otherwise return to idle state.
                    else
                        ReturnToIdle();
                }
            }
            // Probably a hacky way of updating the sword position while swimming in 2D mode.
            var moveVector = ControlHandler.GetMoveVector2();
            var moveDirX = moveVector.X switch
            {
                < 0 => _lastSwimDirection = 0,
                > 0 => _lastSwimDirection = 2,
                _   => _lastSwimDirection
            };
            if (CurrentState == State.ChargeSwimming && moveDirX % 2 == 0)
                PlayWeaponAnimation("stand", moveDirX);
        }

        private void StartSwordSpin()
        { 
            // If in an accompanying state -> switch to a merged state.
            if (IsSwimmingState())
                CurrentState = State.AttackSwimming;
            else if (IsJumpingState())
                CurrentState = State.AttackJumping;
            else if (IsBlockingState())
                CurrentState = State.AttackBlocking;
            else
                CurrentState = State.Attacking;

            Animation.Play("swing_" + Direction);
            PlayWeaponAnimation("swing", Direction);

            Game1.AudioManager.PlaySoundEffect("D378-03-03");

            _swordChargeCounter = sword_charge_time;
            _isSwordSpinAttack = true;
        }

        public bool ClassicSword { get => GameSettings.ClassicSword && !_isSwordSpinning; }

        private Box GetSwordClassicTile(Box box)
        {
            const int TileSize = 16;

            // Use center point of the box.
            float centerX = box.X + box.Width  * 0.5f;
            float centerY = box.Y + box.Height * 0.5f;

            // Offset the center when facing up or down.
            if (Direction == 1)
                centerX += 3;
            else if (Direction == 3)
                centerX -= 3;

            // Get the tile position.
            int tileX = (int)Math.Floor(centerX / TileSize);
            int tiley = (int)Math.Floor(centerY / TileSize);

            return new Box(tileX * TileSize, tiley * TileSize, box.Z, TileSize, TileSize, box.Depth);
        }

        private Box GetSwordDamageBox(RectangleF collisionRectangle) => 
            new Box(
                collisionRectangle.X + EntityPosition.X + _animationOffsetX,
                collisionRectangle.Y + EntityPosition.Y - EntityPosition.Z + _animationOffsetY, -8,
                collisionRectangle.Width,
                collisionRectangle.Height, 16);

        private void UpdateAttacking()
        {
            // If the player is dashing, hold the sword out front.
            if (_bootsRunning && CarrySword)
            {
                if (_isRotating)
                    PlayWeaponAnimation("stand", _rotateDirection);
                else
                    PlayWeaponAnimation("stand", Direction);
            }
            // If the sword is not out just exit.
            if (AnimatorWeapons.CollisionRectangle.IsEmpty)
                return;

            // Get the damage origin point.
            var damageOrigin = BodyRectangle.Center;
            if (Map.Is2dMap)
                damageOrigin.Y -= 4;

            // Get the base damage type of hit to try to hit enemies with.
            var hitType = _bootsRunning 
                ? HitType.PegasusBootsSword 
                : Game1.GameManager.SwordLevel == 1 
                    ? HitType.Sword1 
                    : HitType.Sword2;

            // Get the base damage depending on the sword's level.
            var damage = Game1.GameManager.SwordLevel == 1 ? 1 : 2;

            // Check if a multipler is enabled.
            if (_isSwordSpinAttack || _bootsRunning || Game1.GameManager.PieceOfPowerIsActive || Game1.GameManager.CloakType == GameManager.CloakRed)
                damage *= 2;

            // If it's a sword spin add "SwordSpin" damage type.
            if (_isSwordSpinAttack)
                hitType |= HitType.SwordSpin;

            // Track if a "Piece of Power" is active or if the red tunic is equipped. This is used for the "damage launch" effect.
            var pieceOfPower = Game1.GameManager.PieceOfPowerIsActive || Game1.GameManager.CloakType == GameManager.CloakRed;

            // Get the sword's damage box using the sprite's animation rectangle.
            RectangleF collisionRectangle = AnimatorWeapons.CollisionRectangle;
            SwordDamageBox = GetSwordDamageBox(collisionRectangle);
            SwordClassicBox = Box.Empty;

            // If "Classic Sword" is enabled get the tile the sword overlaps with the most.
            if (ClassicSword)
            {
                // Only the final frame can hit.
                if (AnimatorWeapons.CurrentFrameIndex == 2)
                {
                    // Reduce it to the single dominant tile.
                    SwordClassicBox = GetSwordClassicTile(SwordDamageBox);
                }
            }
            // For the "normal" hit lerp the collision box between the three frames of the attack.
            if (AnimatorWeapons.CurrentAnimation.Frames.Length > AnimatorWeapons.CurrentFrameIndex + 1)
            {
                var frameState = (float)(AnimatorWeapons.FrameCounter / AnimatorWeapons.CurrentFrame.FrameTime);
                var collisionRectangleNextFrame = AnimatorWeapons.GetCollisionBox(AnimatorWeapons.CurrentAnimation.Frames[AnimatorWeapons.CurrentFrameIndex + 1]);

                collisionRectangle = new RectangleF(
                    MathHelper.Lerp(collisionRectangle.X, collisionRectangleNextFrame.X, frameState),
                    MathHelper.Lerp(collisionRectangle.Y, collisionRectangleNextFrame.Y, frameState),
                    MathHelper.Lerp(collisionRectangle.Width, collisionRectangleNextFrame.Width, frameState),
                    MathHelper.Lerp(collisionRectangle.Height, collisionRectangleNextFrame.Height, frameState));
            }
            // We need these outside the next check because they are referenced later.
            var hitCollision = Values.HitCollision.None;
            var direction = Vector2.Zero;

            // This fixes an extremely hard to fix issue with poking things to destroy them, like bushes and crystals. The problem is that the "Hit" functions
            // below are firing per-frame, so we need to block the hits during the entire poke sequence, otherwise they will hit everything or nothing. We only
            // want a SINGLE hit on a SINGLE poke, so it can be properly filtered out. To achieve this, do not run the hit functions during any "poke" animation.
            if (!_pokeStart && !Animation.AnimationID.Contains("poke"))
            {
                // Try to hit enemies with the "normal" hit. This fires whether "Classic Sword" is enabled or not.
                hitCollision = Map.Objects.Hit(this, damageOrigin, SwordDamageBox, hitType, damage, pieceOfPower, out var outDirection, true);

                // If "Classic Sword" is enabled, also hit with "ClassicSword" damage type. This will only try to
                // hit bushes, grass, and crystals. Bombs will also not react to this or the sword hit type if enabled.
                if (ClassicSword && !SwordClassicBox.IsEmpty)
                    Map.Objects.Hit(this, damageOrigin, SwordClassicBox, HitType.ClassicSword, damage, pieceOfPower, true);

                // Both directional hits should match, so just use the hit from the first one.
                direction = outDirection;
            }

            // Poking starts in "OnMoveCollision" when pushing towards the wall during charging state.
            if (_pokeStart)
            {
                // This is only good for one poke. Holding towards the wall will set it again for the next poke.
                _pokeStart = false;

                // Perform a new "Hit" so that we can filter out the poke in enemy/object "OnHit" responses.
                hitType |= HitType.SwordPoke;
                hitCollision = Map.Objects.Hit(this, damageOrigin, SwordDamageBox, hitType, damage, pieceOfPower, out var outDirection, false);
                direction = outDirection;

                // If something was poked then we will have collision.
                if (hitCollision != Values.HitCollision.NoneBlocking)
                {
                    var swordRectangle = AnimatorWeapons.CollisionRectangle;
                    var swordBox = new Box(
                        swordRectangle.X + EntityPosition.X + _animationOffsetX,
                        swordRectangle.Y + EntityPosition.Y - EntityPosition.Z + _animationOffsetY, 0,
                        swordRectangle.Width, swordRectangle.Height, 4);

                    // Play the "tink" sound effect.
                    Game1.AudioManager.PlaySoundEffect("D360-07-07");

                    // If the wall can be bombed then play an additional "ting" sound effect.
                    if (IsDestroyableWall(swordBox))
                        Game1.AudioManager.PlaySoundEffect("D378-23-17");

                    // Spawn the sparking effect at the location of the poke.
                    var pokeParticle = new ObjSparkingEffect(Map, 0, 0, 0, 0);
                    pokeParticle.EntityPosition.X = EntityPosition.X + _pokeAnimationOffset[Direction].X;
                    pokeParticle.EntityPosition.Y = EntityPosition.Y + _pokeAnimationOffset[Direction].Y;
                    Map.Objects.SpawnObject(pokeParticle);
                }
            }
            // If something was hit then stop charging.
            if (hitCollision != Values.HitCollision.None && hitCollision != Values.HitCollision.NoneBlocking)
                _stopCharging = true;

            // Use the direction that was set when the attack took place.
            var beamDirection = _beamDirection;

            // Shoot the sword if the player has the Level 2 sword and full health.
            if (!_shotSword && (Game1.GameManager.SwordLevel == 2 || swordbeam_level1) && (Game1.GameManager.CurrentHealth >= Game1.GameManager.MaxHearts * 4 || swordbeam_always) && AnimatorWeapons.CurrentFrameIndex == 2)
            {
                _shotSword = true;
                var objSwordShot = new ObjSwordShot(Map, EntityPosition, _shootSwordOffset[beamDirection], Game1.GameManager.SwordLevel, beamDirection);
                Map.Objects.SpawnObject(objSwordShot);
                Map.Objects.RegisterAlwaysAnimateObject(objSwordShot);
            }
            // Spawn hit particle?
            if ((hitCollision & Values.HitCollision.Particle) != 0 && _hitParticleTime + 225 < Game1.TotalGameTime)
            {
                _hitParticleTime = Game1.TotalGameTime;
                SpawnRepelParticle(collisionRectangle);
            }
            // Some targets repel Link backwards when hit with the sword. This effect is reduced when underwater.
            var knockback = IsSwimmingState() ? _swimRepelStrength : _baseRepelStrength;

            // Try to repel the player if the hit collision is a repel type.
            RepelPlayer(hitCollision, direction, knockback);
        }

        private void RepelPlayer(Values.HitCollision collisionType, Vector2 direction, float customMultiplier = 0f)
        {
            // Repel the player.
            if ((collisionType & Values.HitCollision.Repelling) != 0 && _hitRepelTime + 225 < Game1.TotalGameTime)
            {
                _hitRepelTime = Game1.TotalGameTime;

                var multiplier = Map.Is2dMap ? 1.5f : (_bootsRunning ? 1.5f : 1.0f);

                if ((collisionType & Values.HitCollision.Repelling0) != 0)
                    multiplier = 3.00f;
                else if ((collisionType & Values.HitCollision.Repelling1) != 0)
                    multiplier = 2.00f;
                else if ((collisionType & Values.HitCollision.Repelling2) != 0)
                    multiplier = 1.50f;
                else if (customMultiplier > 0f)
                    multiplier = customMultiplier;

                if (_bootsRunning)
                    _bootsStop = true;

                _repelVelocity = new Vector2(-direction.X, -direction.Y) * multiplier;
            }
            PreventFieldKnockback();
        }

        private void SpawnRepelParticle(RectangleF collisionRectangle, int OffsetX = 0, int OffsetY = 0)
        {
            Game1.AudioManager.PlaySoundEffect("D360-07-07");

            // Spawn the poke particle.
            var posX = (int)(EntityPosition.X - 8 + collisionRectangle.X + collisionRectangle.Width / 2 + OffsetX);
            var posY = (int)(EntityPosition.Y - 15 + collisionRectangle.Y + collisionRectangle.Height / 2 + OffsetY);
            var pokeParticle = new ObjSparkingEffect(Map, posX, posY, 0, 0);
            Map.Objects.SpawnObject(pokeParticle);
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  SHIELD CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        
        private void HoldShield()
        {
            if ((CurrentState != State.Idle &&
                CurrentState != State.Attacking &&
                CurrentState != State.Pushing &&
                CurrentState != State.Rafting &&
                CurrentState != State.Charging) ||
                _bootsRunning)
                return;

            // If not already blocking play the sound effect.
            if (!_wasBlocking & !_blockButton)
                Game1.AudioManager.PlaySoundEffect("D378-22-16");

            _wasBlocking = _blockButton = true;

            // Set the blocking state.
            if (CurrentState == State.Attacking)
                CurrentState = State.AttackBlocking;
            else if (CurrentState == State.Charging)
                CurrentState = State.ChargeBlocking;
            else if (CurrentState == State.Pushing)
                CurrentState = State.BlockPushing;
            else
                CurrentState = State.Blocking;
            
            // Fail the moblin cave achievement if shielding.
            FailMoblinCaveAchievement();
        }

        private void ReleaseShield()
        {
            _blockButton = false;

            if (!IsBlockingState())
                return;

            if (CurrentState == State.AttackBlocking)
                CurrentState = State.Attacking;
            else if (CurrentState == State.ChargeBlocking)
                CurrentState = State.Charging;
            else if (CurrentState == State.BlockPushing)
                CurrentState = State.Pushing;
        }

        private Box GetShieldBox()
        {
            // The mirror shield requires a slightly different offset than the normal shield
            // when facing south. I'm guessing that it's actually one pixel larger facing down.
            var mirrorShield = Game1.GameManager.GetItem("mirrorShield");
            var hasMirrorShield = mirrorShield?.Count >= 1;
            var rect = Animation.CollisionRectangle;
            var key = (Direction, hasMirrorShield);

            var offsets = key switch
            {
                (1, _)     => ( -9, -18, +4, +2), // Up
                (2, _)     => (-11, -16, +4, +2), // Right
                (3, true)  => ( -8, -18, +4, +3), // Down (Mirror Shield)
                (3, false) => ( -9, -18, +4, +3), // Down
                (_, _)     => ( -7, -16, +4, +2), // Left
            };
            // Assign the results of the switch.
            var (xOff, yOff, wOff, hOff) = offsets;

            // Return the proper shield box based on direction.
            return new Box(
                EntityPosition.X + rect.X + xOff,
                EntityPosition.Y + rect.Y + yOff, 0,
                rect.Width + wOff,
                rect.Height + hOff, 12);
        }

        private void UpdateShieldPush()
        {
            // Check if the collision rectangle is empty or if the player is trapped (Like-Like / Anti-Kirby).
            if (Animation.CollisionRectangle.IsEmpty || _isTrapped)
                return;

            // Get the shield rectangle.
            ShieldBlockBox = GetShieldBox();
            var pushedRectangle = Map.Objects.PushObject(ShieldBlockBox, _walkDirection[Direction] + _body.VelocityTarget * 0.5f, PushableComponent.PushType.Impact);

            // Push the object and get repelled from the pushed object.
            if (pushedRectangle != null)
            {
                _bootsRunning = false;
                _bootsCounter = 0;

                // Use the contact point to determine which direction Link gets pushed back.
                var repelDirection = pushedRectangle.LastPushDirection;

                // If we get something greater than 0 (to the 10 thousandths) use it. If not fallback to walk direction.
                if (repelDirection.LengthSquared() > 0.0001f)
                    repelDirection.Normalize();
                else
                    repelDirection = _walkDirection[Direction];

                // Soften the sideways component so a glancing bump doesn't slide the player too far.
                if (System.Math.Abs(_walkDirection[Direction].X) > 0.5f)
                    repelDirection.Y *= _shieldNudgeScale;
                else
                    repelDirection.X *= _shieldNudgeScale;

                repelDirection.Normalize();
                repelDirection = -repelDirection;

                // Only apply velocity if it's already zero so it can properly decay over time.
                if (_shieldVelocity == Vector2.Zero || Vector2.Dot(_shieldVelocity, repelDirection) < 0f)
                    _shieldVelocity = repelDirection * pushedRectangle.RepelMultiplier;

                // Spawn the "poke" particle.
                if (pushedRectangle.RepelParticle)
                {
                    var posX = (int)(pushedRectangle.PushableBox.Box.X + pushedRectangle.PushableBox.Box.Width / 2);
                    var posY = (int)(pushedRectangle.PushableBox.Box.Y + pushedRectangle.PushableBox.Box.Height / 2);
                    Map.Objects.SpawnObject(new ObjSparkingEffect(Map, posX, posY, 0, 0));
                    Game1.AudioManager.PlaySoundEffect("D360-07-07");
                }
                // Play the "bumping" sound effect.
                else
                    Game1.AudioManager.PlaySoundEffect("D360-09-09");
            }
            PreventFieldKnockback();
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  ROCS FEATHER CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UseFeather()
        {
            if (Is2DMode)
                Jump2D();
            else
                Jump();
        }

        private void HoldFeather()
        {
            // Set when holding in jump button. This is used to track when the
            // button was released. Holding button longer = higher jumping.
            _jump2DHold = Map.Is2dMap;
        }

        private void ReleaseFeather()
        {
            // Track when the jump button is released.
            _jump2DHold = false;
        }

        private bool Jump(bool force = false, bool playSoundEffect = true)
        {
            // Situations where a jump was triggered but should be blocked.
            if ((!force && (
                !IsPushingState() &&
                CurrentState != State.Idle &&
                CurrentState != State.Attacking &&
                CurrentState != State.AttackBlocking &&
                CurrentState != State.Charging &&
                CurrentState != State.ChargeBlocking &&
                CurrentState != State.Blocking &&
                CurrentState != State.Rafting)) ||
                _isTrapped || !_canJump)
            {
                // The jump sound will still play when inside a Like-Like.
                if (_isTrapped && playSoundEffect)
                    Game1.AudioManager.PlaySoundEffect("D360-13-0D");

                // The jump didn't happen.
                return false;
            }
            // Can't jump when not on the ground.
            if (!_body.IsGrounded)
                return false;

            // Fail the moblin cave achievement if jumping.
            FailMoblinCaveAchievement();

            // Play the jumping sound effect.
            if (playSoundEffect)
                Game1.AudioManager.PlaySoundEffect("D360-13-0D");

            // When on the raft, don't move while jumping.
            if (_isRafting)
            {
                _moveVelocity = Vector2.Zero;
                _lastMoveVelocity = Vector2.Zero;
                StopRaft();
            }
            // Base move velocity does not contain the velocity added in the air. So when we hit the
            // floor and directly jump afterwards, we do not get the velocity of the previous jump.
            else _lastMoveVelocity = _lastBaseMoveVelocity;

            // Track the jump started and apply the feather velocity.
            _startedJumping = true;
            _body.Velocity.Z = feather_velocity;
            _jumpStartZPos = _body.Position.Z;

            // If attacking while jumping transfer the jump to the attacking state.
            if (CurrentState == State.Attacking)
                CurrentState = State.AttackJumping;

            // If charging while jumping transfer the jump to the charging state.
            else if (CurrentState == State.Charging || CurrentState == State.ChargeBlocking)
                CurrentState = State.ChargeJumping;

            // If in any other state.
            else
            {
                // If jumping while running, track that running took place.
                if (!_bootsRunning)
                    _bootsWasRunning = true;
                
                // Play the jump animation and change to jumping state.
                if (CurrentState != State.Carrying)
                {
                    Animation.Play("jump_" + Direction);
                    CurrentState = State.Jumping;
                }
                // Track the direction the player jumped in while carrying.
                else { _carryJumpDirection = Direction; }
            }
            return true;
        }

        private void UpdateJump()
        {
            // It's possible to jump off a cliff while carrying.
            var carryState = CurrentState == State.Carrying;

            // This is pretty hacky. When railjumping while carrying, skip some of the
            // stuff that is normally done when jumping so it doesn't bug out.
            if (!carryState)
            {
                // Catch when an attack ends just before a jump which fails to set the jumping state.
                if (CurrentState == State.Idle && !_body.IsGrounded && _body.Velocity.Z > 1.85f)
                {
                    CurrentState = State.Jumping;
                    Animation.Play("jump_" + Direction);
                }
                // If not in a jumping state return early.
                if (!IsJumpingState())
                {
                    if (_railJump)
                        _railJump = false;
                    return;
                }
            }
            // Handle when rail jumping.
            if (_railJump)
            {
                // Force the direction the jump started in.
                if (carryState)
                    Direction = _carryJumpDirection;

                // Hold the jump mid air while the camera scrolls to the new field.
                if (MapManager.Camera.FieldScrolling)
                {
                    Animation.Pause();
                    return;
                }
                // Resume the animation if it was paused mid-loop.
                Animation.Continue();

                // The player is "teleported" frame by frame by calculating their position.
                _railJumpPercentage += Game1.TimeMultiplier * _railJumpSpeed;
                var amount = MathF.Sin(_railJumpPercentage * (MathF.PI * 0.3f)) / MathF.Sin(MathF.PI * 0.3f);
                var newPosition = Vector2.Lerp(_railJumpStartPosition, _railJumpTargetPosition, amount);
                EntityPosition.Set(newPosition);

                // Update the player's Z position.
                EntityPosition.Z = MathF.Sin(_railJumpPercentage * MathF.PI) * _railJumpHeight + _railJumpPercentage * _railJumpPositionZ;

                // The rail jump has reached it's conclusion.
                if (_railJumpPercentage >= 1)
                {
                    _railJump = false;
                    _body.IgnoresZ = false;
                    _body.Velocity.Z = -1f;
                    _body.JumpStartHeight = _railJumpPositionZ;
                    EntityPosition.Set(_railJumpTargetPosition);
                    EntityPosition.Z = _railJumpPositionZ;
                    _lastMoveVelocity = Vector2.Zero;
                }
            }
            // None of the stuff below concerns when jumping with an object in hand.
            if (carryState)
                return;

            // Touched the ground.
            if (_body.IsGrounded && _body.Velocity.Z <= 0)
            {
                // Only push the player if he jumps into the water and does not walk. Walking is handled in another location.
                if (SystemBody.GetFieldState(_body).HasFlag(MapStates.FieldStates.DeepWater))
                {
                    _body.Velocity = new Vector3(_body.VelocityTarget.X, _body.VelocityTarget.Y, 0) * 0.5f;
                }
                // HACK: Jumping then just before landing plays the same frame of animation as the first
                // frame in walking. This timer forces "stand" animation for a few frames.
                else if (!_railJump)
                    _landedFromJump = true;

                // Reset the jump starting Z position to 0.
                _jumpStartZPos = 0;

                // If not ending a rail jump.
                if (!_railJump)
                {
                    // Keep the charging state if it was held during a jump.
                    if (CurrentState == State.ChargeJumping)
                        CurrentState = State.Charging;
                    else
                        ReturnToIdle();
                }
            }
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  MAGIC POWDER CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UseToadstool()
        {
            if (CurrentState != State.Idle &&
                CurrentState != State.Rafting &&
                CurrentState != State.Pushing &&
                (CurrentState != State.Swimming || !Map.Is2dMap))
                return;

            var findWitch = new List<GameObject>();
            var boxSize = 14;
            var interactionBox = new Box(
                EntityPosition.X + _walkDirection[Direction].X * (BodyRectangle.Width / 2 + boxSize / 2) - boxSize / 2,
                BodyRectangle.Center.Y + _walkDirection[Direction].Y * (BodyRectangle.Height / 2 + boxSize / 2) - boxSize / 2, 0,
                boxSize, boxSize, 16);

            Map.Objects.GetComponentList(findWitch, (int)interactionBox.X, (int)interactionBox.Y, (int)interactionBox.Width, (int)interactionBox.Height, InteractComponent.Mask);

            foreach (var obj in findWitch)
            {
                if (obj is ObjPerson theWitch && theWitch.PersonID == "witch")
                {
                    // Check if the player is within a certain range of the witch.
                    var dotWitch = theWitch.EntityPosition.Position - EntityPosition.Position;
                    if (Vector2.Dot(dotWitch, _walkDirection[Direction]) > 15f)
                        continue;

                    // Create a small rectangle just outside of Link's body box that searches for witch body rect.
                    var witchRect = theWitch.Body.BodyBox.Box.Rectangle();
                    var checkRectX = EntityPosition.X + _walkDirection[Direction].X * (_body.Width / 2 + 5) - 1;
                    var checkRectY = EntityPosition.Y - _body.Height / 2 + _walkDirection[Direction].Y * (_body.Height / 2 + 5) - 1;
                    var _checkRectangle = new RectangleF(checkRectX, checkRectY, 2, 2);

                    // Check if the witch is in front of Link.
                    if (!_checkRectangle.Intersects(witchRect))
                        continue;

                    theWitch.ForceInteract();
                    return;
                }
            }
            CurrentState = State.ShowToadstool;
            Animation.Play("show2");
            Game1.GameManager.StartDialogPath("toadstool_hold");
        }

        private void UsePowder()
        {
            if (CurrentState != State.Idle &&
                CurrentState != State.Jumping &&
                CurrentState != State.Rafting &&
                CurrentState != State.Pushing &&
                (CurrentState != State.Swimming || !Map.Is2dMap))
                return;

            // Remove one powder from the inventory.
            if (!Game1.GameManager.RemoveItem("powder", 1))
                return;

            var direction = GetCorrectDirection(Direction);

            var spawnPosition = new Vector2(EntityPosition.X, EntityPosition.Y) + _powderOffset[direction];
            Map.Objects.SpawnObject(new ObjPowder(Map, spawnPosition.X, spawnPosition.Y, EntityPosition.Z, true));

            if (CurrentState != State.Jumping)
            {
                StopRaft();

                CurrentState = State.Powdering;
                Animation.Play("powder_" + direction);
            }
            // Try to spawn a new toadstool if out of powder.
            Game1.GameManager.StartDialogPath("toadstool_check");
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  BOMBS CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UseBomb(bool autoPickup = false)
        {
            // Rail jumping and setting down or throwing a
            // bomb will cause Link to get stuck in the wall.
            if (_railJump)
                return;

            // If Link is carrying an object throw it.
            if (_carriedGameObject != null)
            {
                ThrowCarriedObject();
                return;
            }
            // Return if not in any of the current states.
            if (!autoPickup &&
                CurrentState != State.Idle &&
                CurrentState != State.Rafting &&
                CurrentState != State.Pushing &&
                CurrentState != State.Jumping &&
                (CurrentState != State.Swimming || !Map.Is2dMap))
                return;

            // Pick up the bomb if there is one infront of the player.
            var bombRectX = EntityPosition.X + _walkDirection[Direction].X * (_body.Width / 2) - 4;
            var bombRectY = EntityPosition.Y - _body.Height / 2 + _walkDirection[Direction].Y * (_body.Height / 2) - 4;
            var bombRectW = 8;
            var bombRectH = 8;
            var _bombGrabRectangle = new RectangleF(bombRectX, bombRectY, bombRectW, bombRectH);

            // Don't pick up bombs when airborne.
            if (_body.IsGrounded)
            {
                // Loop through the list of bombs and pick up the first bomb found.
                foreach (var objBomb in BombList)
                {
                    // If the bomb is inactive or not in range don't pick it up.
                    var carriableComponent = objBomb.Components[CarriableComponent.Index] as CarriableComponent;
                    if (!carriableComponent.IsActive || !carriableComponent.Rectangle.Rectangle.Intersects(_bombGrabRectangle))
                        continue;

                    // Pick up the bomb.
                    carriableComponent?.StartGrabbing?.Invoke();
                    StartPickup(carriableComponent);
                    Animation.Play("pull_" + Direction);
                    return;
                }
            }
            // The inside logic only runs if the player has "Remote Bombs" enabled.
            if (BombList.Count > 0)
            {
                // Loop through the active bombs.
                foreach (ObjBomb objBomb in BombList.ToList())
                {
                    // We can't get the value until we get a bomb object.
                    if (objBomb.RemoteBombs)
                    {
                        // Detonate the bomb.
                        objBomb.Explode();
                        return;
                    }
                }
            }
            // Remove 1 bomb from the inventory.
            if (!Game1.GameManager.RemoveItem("bomb", 1))
            {
                if (!Game1.AudioManager.IsPlaying("D360-29-1D"))
                    Game1.AudioManager.PlaySoundEffect("D360-29-1D");
                return;
            }
            var direction = GetCorrectDirection(Direction);

            // Spawn the bomb into the game world.
            var spawnPosition = new Vector2(EntityPosition.X, EntityPosition.Y) + _bombOffset[direction];
            var bombObject = new ObjBomb(Map, spawnPosition.X, spawnPosition.Y, true, false, 2000);
            Map.Objects.SpawnObject(bombObject);

            // Set Link's current state.
            CurrentState = State.Bombing;

            // Play the animation to set down a bomb which is shared with magic powder.
            Animation.Play("powder_" + direction);

            // If the user enabled auto-pickup, immediately run this again to instantly pick up the bomb.
            if (bombObject.AutoPickup && !autoPickup && _body.IsGrounded)
                UseBomb(true);
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  BOW & ARROWS CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        
        private void UseArrow()
        {
            if (CurrentState != State.Idle &&
                CurrentState != State.Jumping &&
                CurrentState != State.Rafting &&
                CurrentState != State.Bombing &&
                CurrentState != State.Pushing &&
                (CurrentState != State.Swimming || !Map.Is2dMap))
                return;

            // Remove one arrow from the inventory,
            if (!Game1.GameManager.RemoveItem("bow", 1))
            {
                if (!Game1.AudioManager.IsPlaying("D360-29-1D"))
                    Game1.AudioManager.PlaySoundEffect("D360-29-1D");
                return;
            }

            var direction = GetCorrectDirection(Direction);

            var arrow = new ObjArrow(Map, EntityPosition, _arrowOffset[direction], direction);
            Map.Objects.SpawnObject(arrow);
            Map.Objects.RegisterAlwaysAnimateObject(arrow);

            if (CurrentState != State.Jumping)
            {
                StopRaft();

                CurrentState = State.Powdering;
                Animation.Play("powder_" + direction);
            }

            Game1.AudioManager.PlaySoundEffect("D378-10-0A");
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  SHOVEL CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UseShovel()
        {
            if (CurrentState != State.Idle || _isClimbing)
                return;

            CurrentState = State.Digging;
            _hasDug = false;

            // play animation
            Animation.Play("dig_" + Direction);

            _digPosition = new Point(
                (int)((EntityPosition.X + _shovelOffset[Direction].X) / Values.TileSize),
                (int)((EntityPosition.Y + _shovelOffset[Direction].Y) / Values.TileSize));

            _canDig = Map.CanDig(_digPosition);

            if (_canDig)
                Game1.AudioManager.PlaySoundEffect("D378-14-0E");
            else
                Game1.AudioManager.PlaySoundEffect("D360-07-07");
        }

        private void UpdateDigging()
        {
            if (Animation.CurrentFrameIndex > 0 && !_hasDug)
            {
                _hasDug = true;
                if (_canDig)
                    Map.Dig(_digPosition, EntityPosition.Position, Direction);
            }
            if (!Animation.IsPlaying)
                CurrentState = State.Idle;
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  POWER BRACELET CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UseBracelet()
        {
            // A new press always clears the throw lock. This must happen before any early return so a
            // throw performed with another button can't leave the lock stuck on.
            _braceletThrowLock = false;

            if (_carriedComponent == null || 
                CurrentState != State.Carrying || 
                CurrentState == State.Attacking || 
                CurrentState == State.Jumping)
                return;

            // Rail jumping and throwing an object will end the rail 
            // jump early, causing Link to get stuck inside the wall.
            if (!_railJump)
                ThrowCarriedObject();
        }

        private void ReleaseBracelet()
        {
            // The throw lock is lifted once the button comes back up.
            _braceletThrowLock = false;
        }

        private RectangleF GetGrabRectangle(int dir, float size)
        {
            float bias = 0.16f;
            float recX = EntityPosition.X + _walkDirection[dir].X * (_body.Width / 2) - 1 - (bias / 2);
            float recY = EntityPosition.Y - _body.Height / 2 + _walkDirection[dir].Y * (_body.Height / 2) - 1 - (bias / 2);

            return new RectangleF(recX, recY, Math.Min(size, 3) + bias, size + bias);
        }

        private void HoldBracelet()
        {
            // An object was thrown on this button press. Grabbing requires a new press.
            if (_braceletThrowLock)
                return;

            // Part One: Grabbing the object. State must be idle, pushing, or swimming (for Flying Rooster) to continue.
            if (CurrentState != State.Idle && 
                CurrentState != State.Pushing && 
                CurrentState != State.Swimming && 
                CurrentState != State.Throwing)
                return;

            // Stores the grabbed object.
            GameObject grabbedObject = null;

            // Try to grab an object in range and not yet carrying an object.
            if (_carriedComponent == null && _instantPickupObject == null)
            {
                // 2D: A hack to grab 2D pots when standing on top of them.
                if (Map.Is2dMap)
                {
                    // We need a grab rectangle that can reach under Link's feet.
                    GrabRectangle = GetGrabRectangle(Direction, 8.00f);
                    grabbedObject = Map.Objects.GetCarryableObjects(GrabRectangle);

                    // Get the carry component of the 2D pot if available.
                    var carriableComponent2D = grabbedObject?.Components?[CarriableComponent.Index] as CarriableComponent;

                    // The only 2D object with `IsInstant` is the 2D pot, walls and other objects fail this check.
                    if (carriableComponent2D == null || !carriableComponent2D.IsInstant)
                    {
                        GrabRectangle = GetGrabRectangle(Direction, 2.00f);
                        grabbedObject = Map.Objects.GetCarryableObjects(GrabRectangle);
                    }
                }
                // 3D: The normal routine for grabbing objects.
                else
                {
                    // Tiny rectangle that finds objects in front of Link that can be grabbed.
                    GrabRectangle = GetGrabRectangle(Direction, 2.00f);

                    // Find's any possible objects within the rectangle.
                    grabbedObject = Map.Objects.GetCarryableObjects(GrabRectangle);
                }
                // A grabble object has been found.
                if (grabbedObject != null)
                {
                    // Get the carry component of the grabbable object.
                    var carriableComponent = grabbedObject?.Components?[CarriableComponent.Index] as CarriableComponent;

                    // Do not try to grab an object that was just thrown.
                    if (carriableComponent == null || carriableComponent.Thrown == true)
                        return;

                    // Don't grab the rooster if it's too high in the air.
                    if (grabbedObject is ObjCock cock)
                    {
                        if (cock.EntityPosition.Z > 8)
                            return;
                    }
                    // If the component is active then grab the object.
                    if (carriableComponent.IsActive)
                    {
                        // Allow picking up the rooster in the water. Otherwise don't try to lift.
                        if (CurrentState == State.Swimming)
                        {
                            if (grabbedObject is not ObjCock) return;
                            _swimRoosterPickup = true;
                        }
                        // Grabbing state is used to determine part two.
                        CurrentState = State.Grabbing;

                        if (!carriableComponent.IsHeavy || Game1.GameManager.StoneGrabberLevel > 1)
                            carriableComponent?.StartGrabbing?.Invoke();
                    }
                }
            }
            // If the previous run of this method started a pull increment the counter or otherwise reset it.
            if (_wasPulling)
                _pullCounter += Game1.DeltaTime;
            else
                _pullCounter = 0;

            // If an instant pickup object was grabbed, restore it from the previous loop.
            if (_instantPickupObject != null)
                grabbedObject = _instantPickupObject;

            // Part Two: An object was found above and the state was set to grabbing.
            if (CurrentState == State.Grabbing || _instantPickup)
            {
                // Gets the carriable component from the object found above.
                var carriableComponent = grabbedObject.Components[CarriableComponent.Index] as CarriableComponent;

                // If the carriable component becomes inactive or null then cancel the pickup. 
                if (carriableComponent == null || !carriableComponent.IsActive)
                {
                    _instantPickupObject = null;
                    _instantPickup = false;
                    _pullCounter = 0;
                    _isPulling = false;
                    CurrentState = State.Idle;
                    return;
                }

                // If not in the middle of an "instant pickup" loop, try to see if the current object is an instant pickup type.
                if (!_instantPickup)
                {
                    // One of these checks must pass for an object to be instant pickup.
                    bool canLiftWeight = !carriableComponent.IsHeavy || Game1.GameManager.StoneGrabberLevel > 1;
                    bool playerEnabled = bracelet_fast_pickup && canLiftWeight;
                    bool isInstantType = carriableComponent.IsInstant;

                    // Collision can not be picked up instantly.
                    if (!carriableComponent.IsCollision && (playerEnabled || isInstantType))
                    {
                        _instantPickupObject = grabbedObject;
                        _instantPickup = true;
                    }
                }
                // Get the direction of the analog stick.
                var moveVec = ControlHandler.GetMoveVector2();

                // Get if the object is being pulled and it's not null.
                if (carriableComponent?.Pull != null)
                {
                    // If being pulled get the vector. If not then reset it.
                    Vector2 pullVector = _pullCounter > 0
                        ? moveVec
                        : Vector2.Zero;

                    // If the pull has failed and the pull counter is below zero, reset the pull counter.
                    // PullResetTime is (-133). During this time the animation is not played.
                    if (!carriableComponent.Pull(pullVector) && _pullCounter < 0)
                        _pullCounter = PullResetTime;
                }
                // The pull vector must be over half the range of the analog stick.
                if (moveVec.Length() > 0.5 || _instantPickup)
                {
                    // Get the direction of the pull vector.
                    var moveDir = AnimationHelper.GetDirection(moveVec);

                    // The player must be pulling in the opposite direction.
                    if (ReverseDirection(moveDir) == Direction || _instantPickup)
                    {
                        // Do not show the pull animation while resetting.
                        if (_pullCounter >= 0)
                            CurrentState = State.Pulling;

                        // Used to determine if pulling was done on the next frame. This sets
                        // "_wasPulling" which counts up on the pull timer.
                        _isPulling = true;

                        // It's not a heavy object or the Power Bracelet is greater than level 1.
                        if (!carriableComponent.IsHeavy || Game1.GameManager.StoneGrabberLevel > 1)
                        {
                            // The pull counter exceeds the PullTime (100) and the object is not null.
                            if (_pullCounter >= PullTime && grabbedObject != null)
                            {
                                // Pick up the object and reset instant pickup variables.
                                StartPickup(carriableComponent);
                                _instantPickupObject = null;
                                _instantPickup = false;
                            }
                            // Reset the pull counter if it exceeds the maximum (400) and the carriable component has "IsStruggle"
                            // set on it. This is to reset the animation if pulling on the lever in level 4 and 7 dungeons.
                            if (_pullCounter > PullMaxTime && carriableComponent.IsStruggle)
                                _pullCounter = PullResetTime;
                        }
                    }
                }
            }
        }

        private void UpdatePositionCarriedObject(CPosition newPosition)
        {
            if (_carriedComponent == null)
                return;

            var targetPosition = new Vector3(EntityPosition.X, EntityPosition.Y, EntityPosition.Z + _carriedComponent.CarryHeight);

            if (CurrentState == State.PreCarrying)
            {
                // finished pickup animation?
                if (_preCarryCounter >= PreCarryTime)
                {
                    _preCarryCounter = PreCarryTime;
                    CurrentState = State.Carrying;
                }
                var pickupTime = 1 - MathF.Cos((_preCarryCounter / PreCarryTime) * (MathF.PI / 2));

                var carryPositionXY = Vector2.Lerp(
                    new Vector2(_carryStartPosition.X, _carryStartPosition.Y),
                    new Vector2(targetPosition.X, targetPosition.Y),
                    1 - MathF.Cos(pickupTime * (MathF.PI / 2)));
                var carryPositionZ = MathHelper.Lerp(_carryStartPosition.Z, targetPosition.Z,
                    MathF.Sin(pickupTime * (MathF.PI / 2)));

                if (!_carriedComponent.UpdatePosition(new Vector3(carryPositionXY.X, carryPositionXY.Y, carryPositionZ)))
                {
                    CurrentState = State.Idle;
                    ReleaseCarriedObject();
                }
            }
            else if (!_isFlying)
            {
                // move the carried object up/down with the walk animation
                if (Direction % 2 == 0)
                    targetPosition.Z += _isWalking ? Animation.CurrentFrameIndex : 1;
                else if (Map.Is2dMap)
                    targetPosition.Z += 1;

                if (!_carriedComponent.UpdatePosition(targetPosition))
                {
                    CurrentState = State.Idle;
                    ReleaseCarriedObject();
                }
            }
        }

        private void ThrowCarriedObject()
        {
            // Throw the object being carried.
            _carriedComponent.Throw(_walkDirection[Direction] * 3f);
            RemoveCarriedObject();

            // Prevents throwing an object and picking up a new one on the same button press
            _braceletThrowLock = true;

            // Play the throwing sound effect.
            Game1.AudioManager.PlaySoundEffect("D360-08-08");

            // Play the throwing animation.
            Animation.Play("throw_" + Direction);
            CurrentState = State.Throwing;
        }

        private void StartPickup(CarriableComponent carriableComponent)
        {
            if (carriableComponent?.Init == null)
                return;

            // Don't commit to a pickup against a component that was deactivated after the grab began.
            if (!carriableComponent.IsActive)
                return;

            _carriedComponent = carriableComponent;

            Game1.AudioManager.PlaySoundEffect("D370-02-02");

            _carryStartPosition = _carriedComponent.Init();
            _carriedComponent.IsPickedUp = true;
            CurrentState = State.PreCarrying;
            _preCarryCounter = 0;

            _carriedGameObject = carriableComponent.Owner;
            _carriedObjDrawComp = carriableComponent.Owner.Components[DrawComponent.Index] as DrawComponent;
            if (_carriedObjDrawComp != null)
                _carriedObjDrawComp.IsActive = false;
        }

        public void ReleaseCarriedObject()
        {
            // let the carried item fall down
            if (_carriedComponent == null)
                return;

            _carriedComponent.Throw(new Vector2(0, 0));
            RemoveCarriedObject();
            ReturnToIdle();
        }

        private void RemoveCarriedObject()
        {
            _carriedComponent.IsPickedUp = false;
            _carriedComponent = null;
            _carriedGameObject = null;

            if (_carriedObjDrawComp != null)
            {
                _carriedObjDrawComp.IsActive = true;
                _carriedObjDrawComp = null;
            }
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  HOOKSHOT CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UseHookshot()
        {
            // If the cooldown is active then force exit.
            if (_hookshotCooldown > 0)
                return;

            // Only run if idle or using hookshot for the forced return.
            if (CurrentState != State.Idle &&
                CurrentState != State.Rafting &&
                CurrentState != State.Pushing &&
                CurrentState != State.Hookshot &&
                (!Map.Is2dMap || CurrentState != State.Swimming))
                return;

            // If hookshot is in progress force a comeback.
            if (_hookshotActive)
            {
                Hookshot.ForceComeback();
                return;
            }
            // Fire the shot, make active, and reset activation counter.
            _hookshotActive = true;
            _hookshotCounter = 0;

            // Get the current direction to fire the hookshot.
            var direction = GetCorrectDirection(Direction);
            var hookshotDirection = CurrentState == State.Swimming ? _swimDirection : direction;

            // Spawn in the hookshot object and track it via "Hookshot" variable.
            var spawnPosition = new Vector3(
                EntityPosition.X + _hookshotOffset[hookshotDirection].X,
                EntityPosition.Y + _hookshotOffset[hookshotDirection].Y, EntityPosition.Z);
            Hookshot.Start(Map, spawnPosition, AnimationHelper.DirectionOffset[hookshotDirection]);
            Map.Objects.SpawnObject(Hookshot);
            Map.Objects.RegisterAlwaysAnimateObject(Hookshot);

            // Set the current state and reset values.
            CurrentState = State.Hookshot;
            _body.VelocityTarget = Vector2.Zero;
            _body.HoleAbsorption = Vector2.Zero;
            _body.IgnoreHoles = true;
            StopRaft();

            // Play Link's animation ("powder" is used for several items).
            Animation.Play("powder_" + hookshotDirection);
        }

        private void UpdateHookshot()
        {
            // If cooldown is active reduce it.
            if (_hookshotCooldown > 0)
                _hookshotCooldown -= Game1.DeltaTime;

            // Increase the reactivation counter.
            _hookshotCounter += Game1.DeltaTime;

            // After a period, force the hookshot to be inactive.
            if (_hookshotCounter > 1350)
            {
                _hookshotCounter = 0;

                // This is a workaround to the hookshot sometimes getting "stuck"
                // after usage caused by various interruptions to it finishing.
                if (_hookshotActive)
                {
                    _hookshotActive = false;
                    Hookshot.Despawn();
                    ReturnToIdle();
                    return;
                }
            }
            // Hookshot is in progress.
            if (CurrentState == State.Hookshot)
            {
                // If currently moving continue moving.
                if (Hookshot.IsMoving)
                    return;

                // If it's returned, reset values and add brief cooldown.
                _hookshotActive = false;
                _hookshotCounter = 0;
                _hookshotCooldown = 75;
                _body.IgnoreHoles = false;
                ReturnToIdle();
            }
        }

        public void StartHookshotPull()
        {
            _hookshotPull = true;
            if (Map.Is2dMap)
            {
                _body.Velocity.Y = 0;
                _body.LastVelocityCollision = Values.BodyCollision.None;
            }

            // if the player is on the upper level he will not get pulled through water and we can move through colliders
            if ((_body.CurrentFieldState & MapStates.FieldStates.UpperLevel) != 0)
            {
                _body.IsGrounded = false;
                _body.Level = MapStates.GetLevel(_body.CurrentFieldState);
            }
        }

        public bool UpdateHookshotPull()
        {
            var distance = _body.BodyBox.Box.Center - Hookshot.HookshotPosition.Position;
            var pullVector = AnimationHelper.DirectionOffset[Direction];

            // Reached the end of the hook or collided with an object before.
            var check01 = distance.Length() < (distance + pullVector).Length();
            var check02 = _body.LastVelocityCollision != Values.BodyCollision.None && (_body.SlideOffset == Vector2.Zero || _body.BodyBox.Box.Contains(Hookshot.HookshotPosition.Position));
            var check03 = IsDying();

            // If any of the checks pass, stop the hookshot pull.
            if (check01 || check02 || check03)
            {
                _hookshotPull = false;
                _body.IgnoresZ = false;
                _body.IgnoreHoles = false;
                _body.Level = 0;
                return false;
            }
            _body.VelocityTarget = pullVector * 3;

            return true;
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  BOOMERANG CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UseBoomerang()
        {
            if ((CurrentState != State.Idle &&
                CurrentState != State.Jumping &&
                CurrentState != State.Pushing &&
                CurrentState != State.Rafting &&
                (CurrentState != State.Swimming || !Map.Is2dMap)) || Boomerang.Map != null)
                return;

            var spawnPosition = new Vector3(EntityPosition.X + _boomerangOffset[Direction].X, EntityPosition.Y + _boomerangOffset[Direction].Y, EntityPosition.Z);

            // can throw into multiple directions
            var boomerangVector = ControlHandler.GetMoveVector2();
            if (boomerangVector != Vector2.Zero)
                boomerangVector.Normalize();
            else
                boomerangVector = _lastBaseMoveVelocity;
            if (boomerangVector != Vector2.Zero)
                boomerangVector.Normalize();
            else
                boomerangVector = _walkDirection[Direction];

            var direction = GetCorrectDirection(Direction);

            Boomerang.Start(Map, spawnPosition, boomerangVector);
            Map.Objects.SpawnObject(Boomerang);
            Map.Objects.RegisterAlwaysAnimateObject(Boomerang);

            if (CurrentState != State.Jumping &&
                CurrentState != State.ChargeJumping)
            {
                CurrentState = State.Powdering;
                Animation.Play("powder_" + direction);
            }
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  MAGIC ROD CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        private void UseMagicRod()
        {
            if (CurrentState != State.Idle &&
                CurrentState != State.Rafting &&
                CurrentState != State.Pushing &&
                (CurrentState != State.Swimming || !Map.Is2dMap) &&
                (CurrentState != State.Jumping || _railJump))
                return;

            var direction = GetCorrectDirection(Direction);

            var magicShot = new ObjMagicRodShot(Map, EntityPosition, _magicRodOffset[direction], direction);
            Map.Objects.SpawnObject(magicShot);
            Map.Objects.RegisterAlwaysAnimateObject(magicShot);

            CurrentState = State.MagicRod;
            _swordChargeCounter = sword_charge_time;

            Game1.AudioManager.PlaySoundEffect("D378-13-0D");
            StopRaft();

            // play animation
            Animation.Play("rod_" + direction);
            PlayWeaponAnimation("rod", direction);
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  OCARINA CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UseOcarina()
        {
            if ((CurrentState != State.Idle && CurrentState != State.Pushing && CurrentState != State.Ocarina) || _isClimbing)
                return;

            // Cancel playing the ocarina if pressed again.
            if (CurrentState == State.Ocarina)
            {
                StopOcarina();
                return;
            }
            // Used when drawing the notes.
            _ocarinaNoteIndex = 0;
            _ocarinaCounter = 0;

            // Pause whatever music is playing.
            Game1.AudioManager.PauseMusic();
            Game1.AudioManager.MusicLocked = true;

            // Set the selected ocarina song integer.
            _ocarinaSong = Game1.GameManager.SelectedOcarinaSong;

            // Get the song that has been selected.
            string ocarinaSong = _ocarinaSong switch
            {
                0 => "D370-09-09",  // Ballad of the Windfish
                1 => "D370-11-0B",  // Manbo's Mambo
                2 => "D370-10-0A",  // Frog's Song of Soul
                _ => "D370-21-15"   // Bad Playing
            };
            // Play the selected song.
            Game1.AudioManager.PlaySoundEffect(ocarinaSong);

            // Set the state, face Link forward, and show the animation.
            _preOcarinaDirection = Direction;
            CurrentState = State.Ocarina;
            Direction = 3;
            Animation.Play("ocarina");

            // Prevent Link from taking hits during this time.
            PreventDamageTimer = 8000;

            // Freeze the game world while the song is played.
            FreezeAnimations(true);
        }

        private void StopOcarina()
        {
            // Try to cancel the ocarina songs.
            Game1.AudioManager.StopSoundEffect("D370-09-09");
            Game1.AudioManager.StopSoundEffect("D370-11-0B");
            Game1.AudioManager.StopSoundEffect("D370-10-0A");
            Game1.AudioManager.StopSoundEffect("D370-21-15");

            // Resume the background music.
            Game1.AudioManager.PlayMusic();
            Game1.AudioManager.MusicLocked = false;

            // This will not become some kind of exploit.
            PreventDamageTimer = 0;

            // Return to idle state.
            Direction = _preOcarinaDirection;
            CurrentState = State.Idle;

            // Unfreeze the game world.
            FreezeAnimations(false);
        }

        private void UpdateOcarina()
        {
            // Ocarina is still being played.
            if (CurrentState == State.Ocarina)
            {
                // Disable the inventory while the ocarina plays.
                Game1.GameManager.InGameOverlay.DisableInventoryToggle = true;

                // Finished playing the ocarina.
                if (!Animation.IsPlaying)
                {
                    FinishedOcarinaSong();
                    return;
                }
                // Update animation.
                UpdateOcarinaAnimation();
            }
            // Manbo's Mambo teleport is currently in progress.
            else if (CurrentState == State.OcarinaTeleport)
            {
                // Show the animation while teleporting.
                CurrentState = State.Idle;
            }
        }

        public void StartOcarinaDuo()
        {
            CurrentState = State.Ocarina;

            _ocarinaNoteIndex = 0;
            _ocarinaCounter = 0;

            Animation.Play("ocarina_duo");
        }

        public void StopOcarinaDuo()
        {
            CurrentState = State.Idle;
        }

        private void UpdateOcarinaAnimation()
        {
            if (CurrentState != State.Ocarina)
                return;

            _ocarinaCounter += Game1.DeltaTime;
            if (_ocarinaCounter > 100 + _ocarinaNoteIndex * 910)
            {
                _ocarinaNoteIndex++;

                var dir = _ocarinaNoteIndex % 2 == 1 ? -1 : 1;
                var objNote = new ObjNote(Map, new Vector2(EntityPosition.X + dir * 7, EntityPosition.Y), dir);
                Map.Objects.SpawnObject(objNote);
            }
        }

        private void FinishedOcarinaSong()
        {
            // Set the direction to whatever it was before playing the ocarina.
            Direction = _preOcarinaDirection;

            // Set the timer to make damage happen again.
            PreventDamageTimer = 200;

            // Unfreeze the game world when the song is finished.
            FreezeAnimations(false);

            // Allow music to continue.
            Game1.AudioManager.MusicLocked = false;

            // The song is anything other than Manbo's song.
            if (_ocarinaSong != 1)
            {
                // Continue playing the music.
                Game1.AudioManager.PlayMusic();

                // Get Marin's current internal state.
                var mariaState = Game1.GameManager.SaveManager.GetString("maria_state");

                // If she is active (aka not in a dungeon) and following the player.
                if (_objMarin != null && _objMarin.IsActive && (mariaState == "3" || mariaState == "8"))
                {
                    // Show the dialog of Marin saying the playing isn't very good.
                    Game1.GameManager.StartDialogPath("ocarina_bad_marin");
                }
            }
            // Bad ocarina song was played.
            if (_ocarinaSong == -1)
            {
                ReturnToIdle();
                Game1.GameManager.StartDialogPath("ocarina_bad");
                return;
            }
            // Manbo's Mambo was played.
            if (_ocarinaSong == 1)
            {
                // The value for MapTeleport must be 2 or 3 to use the map to warp with Manbo's song.
                if (GameSettings.MapTeleport >= 2 && !Map.IsDungeon && !Map.IsEgg)
                {
                    // Open a new instance of the map overlay and set the flag 'ManboTeleport' that signifies it was an ocarina warp.
                    ManboTeleport = true;
                    Game1.GameManager.InGameOverlay.StartSequence("map");
                    Game1.AudioManager.PlayMusic();
                    ReturnToIdle();
                    return;
                }
                // Freeze the game during the transition.
                FreezeAnimations(true);

                CurrentState = State.OcarinaTeleport;
                MapTransitionStart = EntityPosition.Position;
                MapTransitionEnd = EntityPosition.Position;
                TransitionOutWalking = false;

                Game1.AudioManager.PlaySoundEffect("D360-44-2C");

                // load the map
                var transitionSystem = (MapTransitionSystem)Game1.GameManager.GameSystems[typeof(MapTransitionSystem)];
                transitionSystem.ResetTransition();

                if (Map.IsDungeon || Map.IsEgg)
                {
                    // HACK: If the player used the warp above level 8 and entered the dungeon, the save position is set to the warp
                    // rather than the dungeon 8 entrance. So if the last position is the warp, overwrite it with dungeon 8 entrance.
                    if (SavePosition == new Vector2(280, 102) && SaveMap == "overworld.map")
                    {
                        SavePosition = new Vector2(576, 1028);
                        SaveMap = "dungeon8.map";
                    }
                    // Respawn at the dungeon entrance.
                    SetNextMapPosition(SavePosition);
                    transitionSystem.AppendMapChange(SaveMap, null, false, false, Color.White, true);
                    OcarinaDungeonTeleport = true;
                }
                else
                {
                    // Append a map change.
                    transitionSystem.AppendMapChange("overworld.map", "ocarina_entry", false, false, Color.White, true);
                }
                transitionSystem.StartTeleportTransition = true;
                return;
            }
            ReturnToIdle();

            // Update Ocarina Listeners.
            var field = CurrentField;

            // Get objects around Link to see if they have ocarina listeners.
            _ocarinaList.Clear();
            Map.Objects.GetComponentList(_ocarinaList, field.X, field.Y, field.Width, field.Height, OcarinaListenerComponent.Mask);

            foreach (var objOcarinaListener in _ocarinaList)
            {
                var ocarinaComponent = (OcarinaListenerComponent)objOcarinaListener.Components[OcarinaListenerComponent.Index];

                // The listener's position must fall within Link's field.
                if (field.Contains((int)objOcarinaListener.EntityPosition.X, (int)objOcarinaListener.EntityPosition.Y))
                    ocarinaComponent.OcarinaPlayedFunction?.Invoke(Game1.GameManager.SelectedOcarinaSong);
            }
        }
       
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  PEGASUS BOOTS CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UsePegasusBoots()
        {
            if (!_bootsHolding & _bootsRunning)
                _bootsStop = true;
        }

        private void HoldPegasusBoots()
        {
            // Track when the boots button is held to reduce 2D height when releasing.
            _bootsButtonHeld = true;

            // If in knockback or when trapped stop boots running.
            if (CurrentState == State.BootKnockback || _isTrapped)
                return;

            // Track when holding the boots button (has different purpose from above).
            _bootsHolding = true;
        }

        private Box GetCrystalSmashBox()
        {
            // The crystal smash box is used to smash crystals. Use
            // the current direction to determine the box offsets.
            var key = Direction;
            var offsets = key switch
            {
                1 => (  -7, -16, +14,  +5),
                2 => (  +5, -12,  +5, +14),
                3 => (  -7,  +1, +14,  +5),
                _ => ( -10, -12,  +5, +14)
            };
            // Assign the results of the switch.
            var (xOff, yOff, wOff, hOff) = offsets;

            // Return the box used to smash crystals with.
            return new Box(
                EntityPosition.X + xOff,
                EntityPosition.Y + yOff, 4,
                wOff, hOff, 4);
        }

        private void UpdatePegasusBoots()
        {
            _bootsWasRunning = _bootsRunning;
            if ((CurrentState != State.Blocking && CurrentState != State.Idle) || _isClimbing || Map.Is2dMap && Direction % 2 != 0)
            {
                _bootsHolding = false;
                _bootsRunning = false;
                _bootsCounter = 0;
                return;
            }
            // stop running but start charging with a time boost
            if (_bootsStop && _body.Velocity.Length() < 0.25f)
            {
                _bootsStop = false;
                _bootsRunning = false;

                // Over/equals 500 = subtract 300. Above zero = halve it. At 0 = use value.
                _bootsCounter = boots_charge_time >= 500
                    ? boots_charge_time - 300
                    : boots_charge_time > 0
                        ? boots_charge_time / 2
                        : boots_charge_time;

                // If the reset flag was set disregard all of that.
                if (_bootsReset)
                {
                    _bootsReset = false;
                    _bootsCounter = 0;
                }
            }
            if (_bootsHolding || _bootsRunning)
            {
                var lastCounter = _bootsCounter;
                _bootsCounter += Game1.DeltaTime;

                // Spawn particles: dust or water particles.
                if (_bootsCounter % _bootsParticleTime < lastCounter % _bootsParticleTime)
                {
                    // Water splash particles.
                    if (_body.CurrentFieldState.HasFlag(MapStates.FieldStates.Water))
                    {
                        Game1.AudioManager.PlaySoundEffect("D360-14-0E");

                        var splashAnimator = new ObjAnimator(_body.Owner.Map, 0, 0, 0, 3, 1, "Particles/splash", "idle", true);
                        splashAnimator.EntityPosition.Set(new Vector2(
                            _body.Position.X + _body.OffsetX + _body.Width / 2f,
                            _body.Position.Y + _body.OffsetY + _body.Height - _body.Position.Z - 3));
                        Map.Objects.SpawnObject(splashAnimator);
                    }
                    // Ground dust particles.
                    else
                    {
                        Game1.AudioManager.PlaySoundEffect("D378-07-07");

                        var animator = new ObjAnimator(Map, (int)EntityPosition.X, (int)(EntityPosition.Y + 1),
                            0, -1 - (int)EntityPosition.Z, Values.LayerPlayer, "Particles/run", "spawn", true);
                        Map.Objects.SpawnObject(animator);
                    }
                }
                // Start running when the counter exceeds the charge time.
                if (!_bootsRunning && _bootsCounter > boots_charge_time)
                {
                    _bootsLastDirection = Direction;
                    _bootsRunning = true;
                    _bootsWasRunning = true;
                    _bootsStop = false;
                }
                // Spawn the smash box while running.
                if (_bootsRunning)
                    _crystalSmashBox = GetCrystalSmashBox();
            }
            else
            {
                _crystalSmashBox = Box.Empty;
                _bootsCounter = 0;
            }
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  ITEM SHOP CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UpdateStoreItemPosition(CPosition position)
        {
            _storePickupPosition.X = position.X - _storeItemWidth / 2f;
            _storePickupPosition.Y = position.Y - EntityPosition.Z - 14 - _storeItemHeight;
        }

        public void StartHoldingItem(GameItem item)
        {
            CurrentState = State.CarryingItem;

            StoreItem = item;

            _storeItemWidth = item.SourceRectangle.Value.Width;
            _storeItemHeight = item.SourceRectangle.Value.Height;

            EntityPosition.AddPositionListener(typeof(ObjLink), UpdateStoreItemPosition);
            UpdateStoreItemPosition(EntityPosition);

            Game1.GameManager.SaveManager.SetString("holdItem", "1");
        }

        public void StopHoldingItem()
        {
            CurrentState = State.Idle;

            StoreItem = null;

            // this removes all listeners with the ObjLink as a key
            EntityPosition.PositionChangedDict.Remove(typeof(ObjLink));

            Game1.GameManager.SaveManager.SetString("holdItem", "0");
        }

        private void StealItem()
        {
            StopHoldingItem();

            // used in ObjStoreItem to not return the item to the shelf
            Game1.GameManager.SaveManager.SetString("result", "0");

            // Rename the player to "Thief".
            Game1.GameManager.ThiefState = true;

            // add the item to the inventory
            var strItem = Game1.GameManager.SaveManager.GetString("itemShopItem");
            var strCount = Game1.GameManager.SaveManager.GetString("itemShopCount");

            var item = new GameItemCollected(strItem)
            {
                Count = int.Parse(strCount)
            };
            // gets picked up
            PickUpItem(item, false, false);

            Game1.GameManager.SaveManager.SetString("stoleItem", "1");
            _showStealMessage = true;
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  FOLLOWER CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UpdateFollower(bool mapInit)
        {
            var hasFollower = false;

            // Check if marin is following the player.
            var itemMarin = Game1.GameManager.GetItem("marin");
            if (itemMarin != null && itemMarin.Count > 0)
            {
                // For some reason, the "ObjFollower" is always null after a map transition. Most of the time, we can just safely respawn Marin
                // so this isn't a problem. This code is also what works around the beach issue where she doesn't spawn. But, it doesn't work if
                // leaving a dungeon and she's currently waiting for Link to come out. So in that case, don't run this code, use the backup path.
                if (!Followers.Contains(_objMarin) && !_objMarin.DungeonLeaveSequence)
                    SpawnMarin();
                else
                    Followers.Add(_objMarin);

                hasFollower = true;
            }

            // Check if the rooster is following the player.
            var itemRooster = Game1.GameManager.GetItem("rooster");
            if (itemRooster != null && itemRooster.Count > 0)
            {
                Followers.Add(_objRooster);
                hasFollower = true;
            }

            // Check if the ghost is following the player.
            var itemGhost = Game1.GameManager.GetItem("ghost");
            if (itemGhost != null && itemGhost.Count > 0)
            {
                Followers.Add(_objGhost);
                hasFollower = true;
            }
            // Check if there is a follower and it is already spawned.
            if (hasFollower)
            {
                foreach (var follower in Followers)
                {
                    if (follower.Map != Map)
                    {
                        if (mapInit && NextMapPositionStart.HasValue)
                            follower.EntityPosition.Set(NextMapPositionStart.Value);
                        else
                            follower.EntityPosition.Set(EntityPosition.Position);

                        follower.Map = Map;
                        Map.Objects.SpawnObject(follower);
                    }
                    Map.Objects.RegisterAlwaysAnimateObject(follower);
                }
            }
            // Remove the current follower from the map.
            else if (Followers.Count > 0)
            {
                foreach (var follower in Followers.ToList())
                {
                    Followers.Remove(follower);
                    Map.Objects.DeleteObjects.Add(follower);
                }
            }
        }

        private static Vector2 GetMarinSpawnOffset(int direction, float distance)
        {
            return direction switch
            {
                0 => new Vector2(distance, 0),
                1 => new Vector2(0, distance),
                2 => new Vector2(-distance, 0),
                3 => new Vector2(0, -distance),
                _ => Vector2.Zero
            };
        }

        private void SpawnMarin()
        {
            Vector2 offset = GetMarinSpawnOffset(Direction, 13f);
            Vector2 marinSpawnPos = new Vector2(_body.Position.X, _body.Position.Y) + offset;
            _objMarin = new ObjMarin(Map, (int)EntityPosition.X, (int)EntityPosition.Y);
            Map.Objects.SpawnObject(_objMarin);
            Map.Objects.RegisterAlwaysAnimateObject(_objMarin);
            _objMarin.SetPosition(marinSpawnPos);
            _objMarin.SetFacingDirection(Direction);
            Followers.Add(_objMarin);
        }

        private void UpdateGhostSpawn()
        {
            if (!_spawnGhost || !Map.IsOverworld)
                return;

            var dungeonEntryPosition = new Vector2(1840, 272);
            var distance = Position - dungeonEntryPosition;
            if (MathF.Abs(distance.X) > 512 || MathF.Abs(distance.Y) > 256)
            {
                _spawnGhost = false;
                Game1.GameManager.SaveManager.RemoveString("spawn_ghost");
                Game1.GameManager.CollectItem(new GameItemCollected("ghost") { Count = 1 }, 0);
                UpdateFollower(false);
                _objGhost.StartFollowing();
            }
        }

        public void StartFlying(ObjCock objCock)
        {
            _isFlying = true;
            _wasFlying = false;
            _objRooster = objCock;
            _flyStartZPos = MathF.Truncate(EntityPosition.Z);
        }

        public void StopFlying(Vector2 velocity)
        {
            _isFlying = false;
            _wasFlying = true;

            _body.IgnoresZ = false;
            _body.IsGrounded = false;
            _body.JumpStartHeight = 0;

            _flyStartZPos = 0;
            _lastMoveVelocity = Vector2.Zero;

            if (_objRooster != null)
                _objRooster.StopFlying();

            _body.Velocity = new Vector3(velocity.X * 4, velocity.Y * 4, 0);
        }

        private void UpdateFlying()
        {
            // Freeze the rooster's travel while the camera scrolls. Input is disabled during the scroll so
            // "TargetVelocity" is never called, which would otherwise leave the old velocity running.
            if (_isFlying && _objRooster != null)
            {
                // Freeze the rooster's travel while the camera scrolls between fields.
                _objRooster.PauseFlying(MapManager.Camera.FieldScrolling);

                // Do not allow input during the field scroll to lock the rooster's facing.
                if (MapManager.Camera.FieldScrolling)
                    return;
            }
            // Player is currently carrying the rooster around.
            if (IsFlying())
            {
                // The hit velocity is added to the movement (*3) for the flame trap knockback on the way 
                // to level 8 as the normal value sent back is not strong enough to knock it back.
                var moveVelocity = ControlHandler.GetMoveVector2() + (_hitVelocity * 3);

                var moveVelocityLength = moveVelocity.Length();
                if (moveVelocityLength > 1)
                    moveVelocity.Normalize();

                if (moveVelocityLength > 0)
                {
                    _objRooster.TargetVelocity(moveVelocity, _flyingSpeed, Direction);
                    var vectorDirection = ToDirection(moveVelocity);
                    Direction = vectorDirection;
                }
            }

            // The player hit the ground or water after throwing the rooster.
            if (CurrentState == State.Idle && _body.IsGrounded && _wasFlying)
            {
                // Check if the player is over water.
                var fieldState = SystemBody.GetFieldState(_body);

                // Reduce velocity by 50% hitting water. Reduce it to zero when hitting land.
                if (fieldState.HasFlag(MapStates.FieldStates.DeepWater))
                    _body.Velocity = _body.Velocity * 0.5f;
                else
                    _body.Velocity = Vector3.Zero;

                _wasFlying = false;
            }
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  MAP CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        public void UpdateSaveLocation()
        {
            SaveMap = Map.MapName;
            SavePosition = EntityPosition.Position;
            SaveDirection = Direction;
        }

        public void StartIntro()
        {
            // set the music
            Game1.AudioManager.SetMusic(27, 2);

            CurrentState = State.Intro;

            Animation.Play("intro");

            NextMapPositionStart = null;
            NextMapPositionEnd = null;
            SetPosition(new Vector2(56, 51));
            MapManager.Camera.ForceUpdate(Game1.GameManager.MapManager.GetCameraTarget());

            SaveMap = Map.MapName;
            SavePosition = new Vector2(70, 70);
            SaveDirection = 3;
        }

        private void UpdateIntro()
        {
            if (CurrentState == State.Intro)
            {
                var walkVelocity = ControlHandler.GetMoveVector2();

                Game1.GameManager.InGameOverlay.DisableInventoryToggle = true;

                if (Animation.CurrentAnimation.Id == "intro_sit" &&
                    !Game1.GameManager.InGameOverlay.TextboxOverlay.IsOpen && walkVelocity.Length() > 0)
                {
                    CurrentState = State.Idle;
                    Direction = 2;
                    StartRailJump(EntityPosition.Position + new Vector2(12, 4), 1, 1);
                    Animation.Play("intro_jump");

                    Game1.GameManager.SaveManager.SetString("played_intro", "1");
                }
                return;
            }
        }

        public void InitGame()
        {
            Animation.Play((CarryShield ? "stands_" : "stand_") + Direction);
            SpriteTransparency = 1;

            NextMapFallStart = false;
            NextMapFallRotateStart = false;

            Game1.GameManager.SwordLevel = 0;
            Game1.GameManager.ShieldLevel = 0;
            Game1.GameManager.StoneGrabberLevel = 0;
            Game1.GameManager.SelectedOcarinaSong = -1;
            Game1.GameManager.OcarinaSongs[0] = 0;
            Game1.GameManager.OcarinaSongs[1] = 0;
            Game1.GameManager.OcarinaSongs[2] = 0;
            Game1.GameManager.HasMagnifyingLens = false;

            _spawnGhost = false;
            HasFlippers = false;
            StoreItem = null;

            _body.IsActive = true;
            _forceWalking = false;

            _objMarin = new ObjMarin(Map, 0, 0);
            _objRooster = new ObjCock(Map, 0, 0, null);
            _objGhost = new ObjGhost(Map, 0, 0);

            MapInit();

            CurrentState = State.Idle;

            Game1.InProgress = true;
        }

        public void InitEnabledCheats()
        {
            // Enable the "Disable Clipping" cheat.
            if (GameSettings.ChNoClipping)
                CheatSystem.ToggleNoClipping();

            // Enable the "Give All Items" cheat.
            if (GameSettings.ChGiveAllItems)
                CheatSystem.EnableGiveAllItems();
            else
                CheatSystem.RestoreMissingTrackedItems();
        }

        public void MapInit()
        {
            if (!IsSwimmingState() &&
                CurrentState != State.OcarinaTeleport)
                CurrentState = State.Idle;

            Hookshot.Reset();

            _hookshotPull = false;

            _railJump = false;
            IsVisible = true;

            _fieldCrossDirection = -1;
            _wasFieldScrolling = false;
            _fieldSyncPending = true;
            _lastFieldCheckPosition = EntityPosition.Position;
            _fieldChangePending = false;
            _scrollDirection = Direction;

            _isRafting = false;
            _isFlying = false;
            _wasFlying = false;
            _isClimbing = false;

            _isTrapped = false;
            _shadowComponent.IsActive = true;
            _isGrabbed = false;

            ShowItem = null;
            _collectedShowItem = null;
            Followers.Clear();

            _hitRepelTime = 0;
            _hitParticleTime = 0;

            _hitCount = 0;
            _sprite.SpriteShader = null;

            _moveVelocity = Vector2.Zero;
            _lastMoveVelocity = Vector2.Zero;
            _hitVelocity = Vector2.Zero;
            _body.Velocity = Vector3.Zero;

            _body.IgnoreHoles = false;
            _body.DeepWaterOffset = -3;
            _body.Level = 0;
            _body.IsGrounded = true;
            _body.CornerCorrection = !Is2DMode;

            _bootsHolding = false;
            _bootsRunning = false;
            _bootsCounter = 0;

            _carriedGameObject = null;
            _carriedComponent = null;
            _carriedObjDrawComp = null;

            _drawInstrumentEffect = false;

            _diveCounter = 0;
            _swimVelocity = Vector2.Zero;
            _inWater = false;
            _pickupWhileSwimming = false;

            PreventDamageTimer = 0;

            if (NextMapFallStart)
            {
                EntityPosition.Z = 64;

                _body.Velocity.Z = -3.75f;
                _body.IgnoresZ = false;
                _body.JumpStartHeight = EntityPosition.Z;

                NextMapFallStart = false;
            }

            if (NextMapFallRotateStart)
            {
                EntityPosition.Z = 160;

                _body.Velocity.Z = -3.75f;
                _body.IgnoresZ = false;
                _body.IsGrounded = false;
                _body.JumpStartHeight = EntityPosition.Z;

                _fallEntryCounter = 0;
                CurrentState = State.FallRotateEntry;

                NextMapFallRotateStart = false;
            }

            if (NextMapPositionEnd.HasValue)
                SetHoleResetPosition(new Vector3(NextMapPositionEnd.Value.X, NextMapPositionEnd.Value.Y, 0));

            if (Is2DMode)
                MapInit2D();

            // Stop Guardian Acorn and Piece of Power during certain transitions.
            if (Map != null && _previousMap != null)
            {
                bool isOverworld = Map.IsOverworld || _previousMap.IsOverworld;
                bool mapIsCave = Map.IsCave || _previousMap.IsCave;
                bool mapIsDungeon = Map.IsDungeon || Map.IsCastle || Map.IsEgg;

                if (isOverworld || !mapIsCave && !mapIsDungeon)
                {
                    Game1.AudioManager.StopGuardianAcorn();
                    Game1.AudioManager.StopPieceOfPower();
                }
            }
            // The BowWow object is designed to automatically set to "_objBowWow" so it needs to be
            // terminated when it is not supposed to be in use or we get an invisible BowWow following.
            if ((Map != null && Map.IsDungeon) || Game1.GameManager.SaveManager.GetString("has_bowWow", "0") != "1")
                _objBowWow = null;

            // Set the state of no clipping based on the state of the cheat.
            if (Map != null)
                _body.CollisionTypes = GameSettings.ChNoClipping && !Map.Is2dMap
                    ? Values.CollisionTypes.Enemy | Values.CollisionTypes.PlayerItem | Values.CollisionTypes.LadderTop
                    : Values.CollisionTypes.Normal | Values.CollisionTypes.Enemy | Values.CollisionTypes.PlayerItem | Values.CollisionTypes.LadderTop;

            // Reset shock effect.
            Game1.GameManager.UseShockEffect = false;

            // Shouldn't happen, but just in case it gets stuck.
            Game1.AudioManager.MusicLocked = false;

            // If Classic Camera was forced at a boss fight.
            Camera.ForceClassic = false;
        }

        public void FinishLoadingMap(Map.Map map)
        {
            Map = map;
            Is2DMode = map.Is2dMap;
            _body.EnableStepUp = Is2DMode;

            if (NextMapPositionStart.HasValue)
                SetPosition(NextMapPositionStart.Value);

            MapInit();

            UpdateFollower(true);

            if (Followers.Count > 0)
                foreach (var follower in Followers)
                    follower.EntityPosition.Set(NextMapPositionStart.Value);
            
            if (_spriteShadow != null)
                _spriteShadow.EntityPosition.Set(NextMapPositionStart.Value);
        }
        
        public void Respawn()
        {
            Animation.Play((CarryShield ? "stands_" : "stand_") + Direction);

            StoreItem = null;
            _body.IsActive = true;

            var hearts = 3;
            if (Game1.GameManager.MaxHearts >= 14)
                hearts = 10;
            else if (Game1.GameManager.MaxHearts >= 10)
                hearts = 7;
            else if (Game1.GameManager.MaxHearts >= 6)
                hearts = 5;

            Game1.GameManager.CurrentHealth = hearts * 4;
            Game1.GameManager.DeathCount++;

            MapInit();
        }

        public void SetPosition(Vector2 newPosition)
        {
            _body.VelocityTarget = Vector2.Zero;
            EntityPosition.Set(new Vector2(newPosition.X, newPosition.Y));
        }

        public void FreezePlayer()
        {
            UpdatePlayer = false;
            _isWalking = false;
            _bootsRunning = false;
            _bootsHolding = false;

            // stop movement
            // on the boat the player should still move up/down while playing the sequence
            if (Map != null && !Map.Is2dMap)
            {
                // make sure to fall down when jumping into a game sequence
                _body.Velocity.X = 0;
                _body.Velocity.Y = 0;
                if (IsJumpingState() || CurrentState == State.Powdering)
                    CurrentState = State.Idle;
            }
            _body.VelocityTarget = Vector2.Zero;
            _moveVelocity = Vector2.Zero;
            _hitVelocity = Vector2.Zero;
            _swimVelocity = Vector2.Zero;

            // stop push animation
            if (IsPushingState())
                CurrentState = State.Idle;

            if (Map != null && Map.Is2dMap)
                UpdateAnimation2D();
            else
                UpdateAnimation();
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  TELEPORT TRANSITION CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        private void UpdateTeleporting()
        {
            if (CurrentState == State.Teleporting)
            {
                if (_teleportCounterFull < 1250 || Direction <= 2)
                    _teleportCounter += Game1.DeltaTime;

                _teleportCounterFull += Game1.DeltaTime;
                var rotationSpeed = 150 - (float)Math.Sin((_teleportCounterFull / 2000f) * Math.PI) * 50;
                if (_teleportCounter > rotationSpeed)
                {
                    _teleportCounter -= rotationSpeed;
                    Direction = (Direction + 1) % 4;
                    UpdateAnimation();
                }
                var transitionSystem = (MapTransitionSystem)Game1.GameManager.GameSystems[typeof(MapTransitionSystem)];
                transitionSystem.ResetTransition();

                if (_teleportState == 0 && _teleportCounterFull >= 1250)
                {
                    if (_teleporter != null)
                    {
                        _teleportState = 1;

                        EntityPosition.Set(_teleporter.TeleportPosition);
                        _teleporter.Lock();

                        var goalPosition = Game1.GameManager.MapManager.GetCameraTarget();
                        MapManager.Camera.SoftUpdate(goalPosition);
                    }
                    else if (Direction == 3 && _teleportCounterFull >= 1450)
                    {
                        MapTransitionStart = EntityPosition.Position;
                        MapTransitionEnd = EntityPosition.Position;
                        TransitionOutWalking = false;

                        transitionSystem.AppendMapChange(_teleportMap, _teleporterId, false, true, Color.White, true);
                    }
                    transitionSystem.SetColorMode(Color.White, 1);
                }
                var fadeOutTime = 250.0f;
                var fadeoutStart = 1750;
                var fadeoutEnd = 1750 + fadeOutTime;

                // Teleport fade in.
                if (_teleportCounterFull >= 750 && _teleportCounterFull < 1250)
                {
                    transitionSystem.SetColorMode(Color.White, (_teleportCounterFull - 750) / 500f);
                }
                // Teleport fade out.
                else if (_teleportState == 1 && _teleportCounterFull >= fadeoutStart && _teleportCounterFull < fadeoutEnd)
                {
                    transitionSystem.SetColorMode(Color.White, 1 - (_teleportCounterFull - fadeoutStart) / fadeOutTime);
                }
                // Teleport has finished.
                else if (_teleportState == 1 && _teleportCounterFull >= fadeoutEnd)
                {
                    _drawBody.Layer = Values.LayerPlayer;
                    transitionSystem.SetColorMode(Color.White, 0);
                    CurrentState = State.Idle;
                    Camera.SnapCamera = false;
                }
            }
        }

        public void StartTeleportation(ObjDungeonTeleporter teleporter)
        {
            _teleporter = teleporter;

            CurrentState = State.Teleporting;
            _drawBody.Layer = Values.LayerTop;

            _teleportState = 0;
            _teleportCounter = 0;
            _teleportCounterFull = 0;

            ReleaseCarriedObject();

            if (Camera.ClassicMode)
                Camera.SnapCamera = true;
        }

        public void StartTeleportation(string teleportMap, string teleporterId)
        {
            _teleporter = null;

            CurrentState = State.Teleporting;
            _drawBody.Layer = Values.LayerTop;

            _teleportMap = teleportMap;
            _teleporterId = teleporterId;
            _teleportState = 0;
            _teleportCounter = 0;
            _teleportCounterFull = 0;

            ReleaseCarriedObject();

            if (Camera.ClassicMode)
                Camera.SnapCamera = true;
        }

        private const float FollowerTeleportFallDelay = 650;
        private const float FollowerTeleportRiseDuration = 400f;

        public void StartWorldTelportation(Vector2 newPosition)
        {
            CurrentState = State.TeleportFallWait;

            var positionDistance = EntityPosition.Position - newPosition;
            var fallPositionV2 = new Vector2(newPosition.X, newPosition.Y);
            var fallPositionV3 = new Vector3(newPosition.X, newPosition.Y, 128);
            EntityPosition.Set(fallPositionV3);

            HoleFalling = false;

            if (Followers.Count > 0)
            {
                var itemGhost = Game1.GameManager.GetItem("ghost");
                if (itemGhost != null && itemGhost.Count >= 0)
                    _objGhost.EntityPosition.Set(fallPositionV2);
                else
                {
                    foreach (var follower in Followers)
                    {
                        follower.EntityPosition.Set(fallPositionV3);

                        if (follower is ObjMarin marin)
                            marin.HoldForWorldTeleportFall(FollowerTeleportFallDelay);
                        else if (follower is ObjCock cock)
                            cock.HoldForWorldTeleportFall(FollowerTeleportFallDelay);
                    }
                }
            }
            if (_objBowWow != null)
            {
                _objBowWow.EntityPosition.Set(fallPositionV3);
                _objBowWow.HoldForWorldTeleportFall(FollowerTeleportFallDelay);
            }
            if (_spriteShadow != null)
                _spriteShadow.EntityPosition.Set(fallPositionV2);

            // Only jump to the new position if it is a different teleporter at a different location.
            if (!Camera.ClassicMode && positionDistance.Length() > 64)
                MapManager.Camera.ForceUpdate(Game1.GameManager.MapManager.GetCameraTarget());
            else
                Camera.SnapCamera = true;
        }

        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //  MAP TRANSITION CODE
        //---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        public void SetNextMapPosition(Vector2 playerPosition)
        {
            // this will be used to set the position of the player after loading the map
            // one of them should always be null
            // the playerPosition is used after loading a savestate
            NextMapPositionStart = playerPosition;
            NextMapPositionEnd = playerPosition;
            NextMapPositionId = null;
        }

        public void SetNextMapPosition(string nextMapPositionId)
        {
            // this will be used to set the position of the player after loading the map
            // one of them should always be null
            // the nextMapPositionId is used after going though a door
            NextMapPositionId = nextMapPositionId;
            NextMapPositionStart = null;
            NextMapPositionEnd = null;
        }

        public void OnAppendMapChange()
        {
            if (_objMarin != null)
                _objMarin.OnAppendMapChange();
        }

        public void StartTransitioning()
        {
            // Store that a transition is taking place.
            IsTransitioning = true;

            // Despawn the boomerang so it doesn't bug out.
            Boomerang.Despawn();

            // Interesting bug: when repelled into a staircase, it's possible to get stuck
            // in a loop of entering/exiting until the velocity fully decays.
            CancelRepelVelocities(false);

            // Set the previous map and draw Link over everything.
            _previousMap = Map;
            _drawBody.Layer = Values.LayerTop;

            // Force walking and stop boots running.
            _isWalking = true;
            _bootsRunning = false;

            // The player shouldn't take damage while transitioning.
            PreventDamageTimer = 3000;

            // If the player stole an item.
            if (StoreItem != null)
                StealItem();

            // Drop any items if being carried.
            ReleaseCarriedObject();

            // If Link is flying with the rooster then stop flying.
            if (IsFlying())
                StopFlying(Vector2.Zero);

            // Restore current state to "Idle".
            if (MapTransitionStart.HasValue && MapTransitionEnd.HasValue && !IsSwimmingState() && 
                CurrentState != State.BedTransition && CurrentState != State.Knockout && CurrentState != State.OcarinaTeleport) 
            {   
                CurrentState = State.Idle;
            }
            // Remove the player's velocity.
            _body.VelocityTarget = Vector2.Zero;

            // If it's a 2D map set up additional parameters.
            if (Map.Is2dMap)
            {
                if (_ladderCollision)
                {
                    _isClimbing = true;
                    Direction = 1;
                }
                _body.IgnoresZ = true;
                _body.Velocity.Y = 0.0f;
            }
            else
            {
                _body.Velocity = Vector3.Zero;
            }
            // Used when tracking animal kills in Mabe Village.
            Game1.GameManager.SaveManager.RemoveInt("animal_kills");
        }

        public void UpdateMapTransitionOut(float state)
        {
            // Track when transitioning out of a map.
            TransitioningOut = true;

            // Move Link to the new position on the map.
            if (MapTransitionStart.HasValue && MapTransitionEnd.HasValue)
            {
                var newPosition = Vector2.Lerp(MapTransitionStart.Value, MapTransitionEnd.Value, state);
                SetPosition(newPosition);
            }
            // Lock the camera while transitioning.
            if (!Map.Is2dMap || Direction == 1)
                Game1.GameManager.MapManager.UpdateCameraY = MapTransitionStart == MapTransitionEnd;

            // Play the walking animation if set.
            _isWalking = TransitionOutWalking;

            // Force north facing when climbing down a ladder into a
            // door. No clue what the proper way to fix this would be.
            if (Map.Is2dMap && _isClimbing)
                Direction = 1;

            // Disable hole falling logic.
            HoleFalling = false;

            // Force animation updates since Link is being moved incrementally.
            if (Is2DMode)
                UpdateAnimation2D();
            else
                UpdateAnimation();
        }

        public void UpdateMapTransitionIn(float state)
        {
            // Track when transitioning into a map.
            TransitioningIn = true;
            TransitioningOut = false;

            // Check if the transition state is "state 0".
            if (state == 0)
            {
                // Make sure to not start falling while transitioning into a 2d map with a ladder.
                if (Map.Is2dMap)
                    _body.IgnoresZ = true;
            }
            if (DirectionEntry >= 0)
                Direction = DirectionEntry;

            // Make sure the transition has both a start and end position.
            if (NextMapPositionStart.HasValue && NextMapPositionEnd.HasValue)
            {
                var newPosition = Vector2.Lerp(NextMapPositionStart.Value, NextMapPositionEnd.Value, state);
                SetPosition(newPosition);

                // Transition the follower out of the map.
                if (Followers.Count > 0)
                {
                    foreach (var follower in Followers)
                    {
                        var followerPosition = Vector2.Lerp(NextMapPositionStart.Value, NextMapPositionEnd.Value, state * 0.5f);
                        follower.SetPosition(followerPosition);

                        // Disable followers on maps that contain the "NoFollowers" map object.
                        if ((Map.NoFollowers || Map.Is2dMap) && Followers.Count > 0)
                            follower.IsActive = false;

                        // Marin has her own method of respawning. Not doing it this way breaks her dungeon transition.
                        else if (follower != _objMarin)
                            follower.IsActive = true;
                    }
                }
            }
            // Lock the camera while transitioning.
            if (!Map.Is2dMap || Direction == 1)
                Game1.GameManager.MapManager.UpdateCameraY = NextMapPositionStart == NextMapPositionEnd;

            // Keep Link walking during the transition in.
            _isWalking = TransitionInWalking;

            // Set the hole and water reset position to be at the transition entrance.
            _holeResetPosition = EntityPosition.ToVector3();
            _drownResetPosition = EntityPosition.Position;

            // Handles swimming transitions.
            UpdateSwimmingPartOne();
            UpdateIgnoresZ();

            // Force animation updates since Link is being moved incrementally.
            if (Is2DMode)
                UpdateAnimation2D();
            else
                UpdateAnimation();
        }

        public void EndTransitioning()
        {
            // Transition is over.
            IsTransitioning = false;
            TransitioningIn = false;

            // Classic Field Scrolling. A crossing interrupted by a door must not survive it.
            _fieldCrossDirection = -1;
            _wasFieldScrolling = false;

            // Used on ObjMusicTile to alert that a transition just happened so don't fade the music.
            NoFadeObjMusicTile = true;

            // Force block button released.
            _blockButton = false;

            // I'm not sure why this is set to zero but here it is.
            _body.HoleAbsorption = Vector2.Zero;

            // The player can take damage again.
            PreventDamageTimer = 0;

            // If using Manbo's song in a dungeon, force the player to face north.
            if (OcarinaDungeonTeleport)
            {
                Direction = 1;
                Animation.Play("stand_" + Direction);
                OcarinaDungeonTeleport = false;
            }
            if (!Map.Is2dMap)
            {
                _body.Velocity.X = 0;
                _body.Velocity.Y = 0;
            }
            // This is because the water is deeper than 0.
            if ((SystemBody.GetFieldState(_body) & MapStates.FieldStates.DeepWater) == 0 && CurrentState != State.Swimming && !_isClimbing)
                _body.IgnoresZ = false;

            // Restore the player's draw layer.
            _drawBody.Layer = Values.LayerPlayer;

            // Restore the camera following the player.
            MapManager.Camera.CameraFollowMultiplier = 1.0f;

            // Used solely to show the message after the player steals from the shop.
            if (_showStealMessage)
            {
                _showStealMessage = false;
                Game1.GameManager.StartDialogPath("shopkeeper_steal");
            }
            // Restart the music.
            if (!GameSettings.MutePowerups && MapManager.ObjLink.HasPowerup)
                Game1.AudioManager.StartPowerupMusic(1);

            // Destroy the field barrier after a transition so it can be recreated.
            DestroyFieldBarrier();

            // Manbo's song transition can freeze the game so unfreeze it now.
            FreezeAnimations(false);

            // When classic camera is enabled don't reset objects immediately after transition. 
            if (Camera.ClassicMode)
            {
                PreventReset = true;
                PreventResetTimer = 200f;
            }
            // Disable black screen override for modern camera.
            else
                BlackScreenOverride = false;

            // Clear a pending field change for safety reasons.
            _fieldChangePending = false;
            _scrollDirection = Direction;
        }
    }
}
