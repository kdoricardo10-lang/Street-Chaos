using Godot;
using System.Collections.Generic;

namespace StreetChaos
{
    public partial class PlayerController : CharacterBody3D
    {
        [ExportGroup("Movement")]
        [Export] public float WalkSpeed { get; set; } = 5.0f;
        [Export] public float SprintSpeed { get; set; } = 9.0f;
        [Export] public float Acceleration { get; set; } = 8.0f;
        [Export] public float AirControl { get; set; } = 0.3f;
        [Export] public float JumpVelocity { get; set; } = 5.0f;
        [Export] public float RotationSpeed { get; set; } = 15.0f;

        [ExportGroup("Camera")]
        [Export] public float MouseSensitivity { get; set; } = 0.002f;

        [ExportGroup("Combat")]
        [Export] public FightingStyleData DefaultStyle { get; set; }
        [Export] public PackedScene HitboxScene { get; set; }
        [Export] public float KnockbackDeceleration { get; set; } = 10f;
        [Export] public float StunDuration { get; set; } = 0.5f;

        [ExportGroup("Model")]
        [Export] public float ModelHeightOffset { get; set; } = 0.5f;

        [ExportGroup("References")]
        [Export] public Node3D HitboxContainer { get; set; }
        [Export] public string EquipBoneName { get; set; } = "hand_r";

        private Camera3D _camera;
        private Node3D _cameraPivot;
        private SpringArm3D _springArm;
        private Node3D _modelPivot;
        private Node3D _modelRoot;
        private Skeleton3D _skeleton;
        private Area3D _interactionSensor;

        public Node3D ModelPivot => _modelPivot;
        public Camera3D Camera => _camera;

        private Vector2 _inputDir;
        private bool _wantsSprint;
        private bool _wantsJump;

        private HealthComponent _health;
        private CombatComponent _combat;
        private StateComponent _state;

        private Vector3 _knockbackVelocity;
        private float _stunTimer;
        private float _speedMultiplier = 1f;
        private float _speedBuffTimer;
        private float _damageBuffValue;
        private float _damageBuffTimer;
        private float _invisibilityBuffTimer;
        private float _superJumpTimer;
        private Vector3 _originalScale;
        private Vector3 _pivotOriginalPos;
        private Tween _shakeTween;
        private ItemData _equippedItem;
        private Node3D _equippedItemNode;

        private const int WeaponSlotCount = 2;
        private const int InventorySlotCount = 4;
        private readonly ItemData[] _weaponSlots = new ItemData[WeaponSlotCount];
        private readonly ItemData[] _inventorySlots = new ItemData[InventorySlotCount];
        private readonly float[] _specialActiveTimers = new float[WeaponSlotCount];
        private readonly float[] _specialCooldowns = new float[WeaponSlotCount];
        private int _selectedWeaponSlot = -1;
        private int _selectedInventorySlot = -1;

        private bool _canSprint = true;
        private bool _wantsBlock;
        private bool _wantsDodge;
        private bool _wantsGrab;
        private bool _wantsInteract;
        private bool _wantsCrouch;
        private int _comboStep = -1;
        private float _comboTimer;
        private bool _comboActive;

        private const float ComboInterval = 0.45f;

        private static readonly AttackInput[] ComboSequence =
        {
            AttackInput.Light,
            AttackInput.Heavy,
            AttackInput.Light,
            AttackInput.Heavy,
        };

        private string _currentAnim = "idle";
        private AnimationPlayer _currentAnimPlayer;
        private string _currentUpperAnim;
        private float _animSpeed = 1f;
        private bool _wasOnGround = true;
        private bool _jumpStartDone = true;
        private bool _jumpLandDone = true;
        private float _idleTimer;

        private AnimationPlayer _upperPlayer;

        private float _syncTimer;
        private float _hudTimer;
        private InventoryScreen _inventoryScreen;
        private EscapeMenu _escapeMenu;
        private Vector3 _syncPosition;
        private Vector3 _syncVelocity;
        private float _syncYaw;
        private PlayerState _syncState;
        private bool _syncOnFloor;
        private float _syncAnimSpeed = 1f;

        private readonly Dictionary<string, (AnimationPlayer player, StringName animName)> _animPlayers = new();

        private Node _lastAttacker;
        private bool _isSpectating;
        private Node3D _spectateTarget;
        private int _spectateIndex;
        private List<Node3D> _spectateCandidates = new();
        private PlayerController _revivingTarget;


        private bool _isDowned;
        private bool _isFullyDead;
        private float _downedTimer = 60f;
        private const float DownedDuration = 60f;
        private Label3D _downedCountdownLabel;
        private Area3D _reviveDetectionArea;
        private bool _isBeingRevived;
        private float _reviveProgress;
        private PlayerController _revivingAlly;
        private const float ReviveHoldDuration = 5f;

        private const string GodotAnimationLibraryPath =
            "res://Godot/AnimationLibrary_Godot_Standard.glb";

        public override void _Ready()
        {
            GD.Print("PlayerController._Ready() with Godot Standard skeleton");
            _cameraPivot = GetNode<Node3D>("CameraPivot");
            _springArm = _cameraPivot?.GetNode<SpringArm3D>("SpringArm3D");
            _camera = _springArm?.GetNode<Camera3D>("Camera3D");
            _modelPivot = GetNode<Node3D>("ModelPivot");
            HitboxContainer = GetNodeOrNull<Node3D>("HitboxContainer");
            if (HitboxContainer == null)
                GD.PrintErr("PlayerController: Missing HitboxContainer node in scene");

            if (_springArm != null)
                _springArm.SpringLength = 4.0f;

            _health = new HealthComponent();
            _health.Name = "HealthComponent";
            AddChild(_health);

            _combat = new CombatComponent();
            _combat.Name = "CombatComponent";
            AddChild(_combat);

            _state = new StateComponent();
            _state.Name = "StateComponent";
            AddChild(_state);

            _interactionSensor = new Area3D();
            _interactionSensor.Name = "InteractionSensor";
            _interactionSensor.Monitoring = true;
            _interactionSensor.Monitorable = false;
            _interactionSensor.CollisionMask = 1;
            var interactionShape = new CollisionShape3D
            {
                Shape = new CapsuleShape3D
                {
                    Radius = 2.0f,
                    Height = 2.0f
                }
            };
            _interactionSensor.AddChild(interactionShape);
            AddChild(_interactionSensor);

            if (_combat != null && DefaultStyle != null)
                _combat.SetFightingStyle(DefaultStyle);

            if (_health != null)
            {
                _health.Died += OnDied;
                _health.TookDamage += OnTookDamage;
            }

            SetupCharacter();
            LoadAnimations();
            SetupHurtbox();

            AddToGroup("players");
            Input.MouseMode = Input.MouseModeEnum.Captured;

            _originalScale = _modelPivot?.Scale ?? Vector3.One;
            _pivotOriginalPos = (_modelPivot?.Position ?? Vector3.Zero) + new Vector3(0, ModelHeightOffset, 0);

            if (_animPlayers.TryGetValue("Idle", out var idleEntry))
            {
                idleEntry.player.Play(idleEntry.animName);
                _currentAnimPlayer = idleEntry.player;
                _currentAnim = "Idle";
            }

            SyncInventoryHud();
        }

        private void SetupHurtbox()
        {
            var hurtbox = new Area3D();
            hurtbox.Name = "Hurtbox";
            hurtbox.CollisionLayer = 2;
            hurtbox.CollisionMask = 0;
            hurtbox.Monitoring = false;
            hurtbox.Monitorable = true;

            var shape = new CollisionShape3D();
            shape.Shape = new CylinderShape3D { Radius = 0.3f, Height = 1.8f };
            hurtbox.AddChild(shape);

            AddChild(hurtbox);
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey { Pressed: true, Keycode: Key.F })
            {
                TryInteract();
                return;
            }

            if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
            {
                if (_inventoryScreen != null)
                {
                    CloseInventory();
                    return;
                }

                if (_escapeMenu != null)
                {
                    _escapeMenu.Close();
                    return;
                }

                if (_isSpectating)
                {
                    ShowDeathScreen();
                    return;
                }

                if (_isFullyDead)
                {
                    OpenEscapeMenu();
                    return;
                }

                if (!_isDowned)
                {
                    OpenEscapeMenu();
                    return;
                }

                Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                    ? Input.MouseModeEnum.Visible
                    : Input.MouseModeEnum.Captured;
                return;
            }

            if (@event is InputEventKey { Pressed: true, Keycode: Key.Tab })
            {
                if (_inventoryScreen != null)
                    CloseInventory();
                else
                    OpenInventory();
                return;
            }

            if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
            {
                if (_cameraPivot != null && !_isSpectating)
                {
                    _cameraPivot.RotateY(-mouseMotion.Relative.X * MouseSensitivity);
                    Vector3 rot = _cameraPivot.Rotation;
                    rot.X += mouseMotion.Relative.Y * MouseSensitivity;
                    rot.X = Mathf.Clamp(rot.X, Mathf.DegToRad(-70), Mathf.DegToRad(70));
                    _cameraPivot.Rotation = rot;
                }
            }
        }

        public override void _Process(double delta)
        {
            if (_isSpectating)
            {
                UpdateSpectate((float)delta);
                return;
            }

            if (_isDowned)
            {
                if (IsMultiplayerAuthority() && !_isFullyDead)
                {
                    _inputDir = Input.GetVector("move_left", "move_right", "move_back", "move_forward");

                    if (Input.IsActionJustPressed("drop_item"))
                        DropEquippedItem();

                    if (Input.IsActionJustPressed("slot_1")) UseSpecialSlot(0);
                    if (Input.IsActionJustPressed("slot_2")) UseSpecialSlot(1);
                    if (Input.IsActionJustPressed("slot_3")) UseInventorySlot(0);
                    if (Input.IsActionJustPressed("slot_4")) UseInventorySlot(1);
                    if (Input.IsActionJustPressed("slot_5")) UseInventorySlot(2);
                    if (Input.IsActionJustPressed("slot_6")) UseInventorySlot(3);

                    CheckReviveHold();
                }
                UpdateDowned((float)delta);
                return;
            }

            if (IsMultiplayerAuthority())
            {
                if (_inventoryScreen == null)
                {
                    if (Input.IsActionJustPressed("slot_1")) UseSpecialSlot(0);
                    if (Input.IsActionJustPressed("slot_2")) UseSpecialSlot(1);
                    if (Input.IsActionJustPressed("slot_3")) UseInventorySlot(0);
                    if (Input.IsActionJustPressed("slot_4")) UseInventorySlot(1);
                    if (Input.IsActionJustPressed("slot_5")) UseInventorySlot(2);
                    if (Input.IsActionJustPressed("slot_6")) UseInventorySlot(3);

                    _inputDir = Input.GetVector("move_left", "move_right", "move_back", "move_forward");
                    _wantsSprint = Input.IsActionPressed("sprint");
                    _wantsCrouch = Input.IsKeyPressed(Key.Shift);
                    _wantsBlock = Input.IsActionPressed("block");
                    _wantsDodge = Input.IsActionJustPressed("dodge");
                    _wantsGrab = Input.IsActionJustPressed("grab");

                    _wantsJump = Input.IsActionJustPressed("jump");

                    if (Input.IsActionJustPressed("drop_item"))
                        DropEquippedItem();

                    HandleCombatInput();
                    CheckReviveHold();
                }
                else
                {
                    _inputDir = Vector2.Zero;
                    _wantsSprint = false;
                    _wantsCrouch = false;
                    _wantsJump = false;
                    _wantsBlock = false;
                    _wantsDodge = false;
                    _wantsGrab = false;
                }

                TickCooldowns((float)delta);
                UpdateState(delta);
                UpdateAnimation();
                UpdateSpeedBuff((float)delta);

                _syncTimer += (float)delta;
                if (_syncTimer >= 0.05f && Multiplayer.MultiplayerPeer != null)
                {
                    _syncTimer = 0f;
                    Rpc(nameof(SyncState), GlobalPosition, Velocity, _modelPivot?.Rotation.Y ?? 0f,
                        (int)_state.CurrentState, IsOnFloor(), _animSpeed);
                }
            }
            else
            {
                UpdateRemoteAnimation();
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            if (_isSpectating) return;

            if (!IsMultiplayerAuthority())
            {
                GlobalPosition = GlobalPosition.Lerp(_syncPosition, 10f * dt);
                if (_modelPivot != null)
                {
                    float currentYaw = _modelPivot.Rotation.Y;
                    _modelPivot.Rotation = new Vector3(0,
                        (float)Mathf.LerpAngle(currentYaw, _syncYaw, 10f * dt), 0);
                }
                Velocity = Velocity.Lerp(_syncVelocity, 5f * dt);
                MoveAndSlide();
                return;
            }

            HandleKnockback(dt);

            if (_isDowned)
            {
                Vector3 crawlDir = GetCameraRelativeInput();
                float crawlSpeed = WalkSpeed * 0.3f;
                Velocity = crawlDir * crawlSpeed;
                if (crawlDir.Length() > 0.01f && _modelPivot != null)
                {
                    float targetYaw = Mathf.Atan2(crawlDir.X, crawlDir.Z);
                    float currentYaw = _modelPivot.Rotation.Y;
                    _modelPivot.Rotation = new Vector3(0,
                        (float)Mathf.LerpAngle(currentYaw, targetYaw, 10f * dt), 0);
                }
                MoveAndSlide();
                return;
            }

            if (_state != null && _state.IsImmobilized())
            {
                MoveAndSlide();
                return;
            }

            _canSprint = (IsOnFloor() && _wantsSprint) || _speedMultiplier > 1f;
            if (_canSprint && _health != null && _inputDir.Length() > 0.01f && _speedMultiplier <= 1f)
            {
                const float sprintDrain = 15f;
                if (!_health.UseStamina(sprintDrain * dt))
                    _canSprint = false;
            }

            // Stamina drain while blocking
            bool isBlocking = _combat != null && _combat.IsBlocking;
            if (isBlocking && _health != null)
            {
                const float blockDrain = 5f;
                if (!_health.UseStamina(blockDrain * dt))
                    _combat.StopBlock();
            }

            float speed = SprintSpeed;
            speed *= _speedMultiplier;

            if (_canSprint)
                speed *= 2.0f;

            _animSpeed = speed / SprintSpeed;

            if (_combat != null && _combat.IsBlocking)
                speed = 0f;

            if (_combat != null && _combat.IsAttacking)
                speed *= 0.7f;

            if (_wantsCrouch)
                speed = WalkSpeed * 0.35f;

            Vector3 moveDirection = GetCameraRelativeInput();
            bool isMoving = moveDirection.Length() > 0.01f;

            if (_combat != null && _combat.IsGrabbing)
            {
                moveDirection = Vector3.Zero;
                isMoving = false;
            }

            Vector3 velocity = Velocity;

            if (_wantsJump)
            {
                if (IsOnFloor())
                {
                    if (_superJumpTimer > 0f)
                    {
                        Vector3 dir = _camera != null
                            ? -_camera.GlobalTransform.Basis.Z
                            : -GlobalTransform.Basis.Z;
                        velocity = dir * 15f + Vector3.Up * 25f;
                    }
                    else
                    {
                        velocity.Y = JumpVelocity;
                    }
                }
                _wantsJump = false;
            }

            if (isMoving && _modelPivot != null)
            {
                float targetYaw = Mathf.Atan2(moveDirection.X, moveDirection.Z);
                float currentYaw = _modelPivot.Rotation.Y;
                float newYaw = (float)Mathf.LerpAngle(currentYaw, targetYaw, RotationSpeed * dt);
                _modelPivot.Rotation = new Vector3(0, newYaw, 0);
            }

            Vector3 targetVelocity = moveDirection * speed;
            targetVelocity.Y = velocity.Y;

            float control = IsOnFloor() ? Acceleration : Acceleration * AirControl;
            if (!isMoving && IsOnFloor())
                control *= 4.0f;
            velocity = velocity.Lerp(targetVelocity, control * dt);

            velocity.Y += GetGravity().Y * dt;

            Velocity = velocity;
            MoveAndSlide();
        }

        private void HandleCombatInput()
        {
            if (_combat == null || _state == null) return;

            if (!_state.CanAttack() && !_state.IsGrounded()) return;

            if (_wantsBlock)
            {
                if (_health == null || _health.CurrentStamina > 5f)
                    _combat.StartBlock();
                return;
            }

            if (Input.IsActionJustReleased("block"))
                _combat.StopBlock();

            if (_wantsDodge)
            {
                _combat.TryDodge();
                return;
            }

            if (_wantsGrab && !_combat.IsAttacking)
            {
                TryGrabTarget();
                return;
            }

            if (Input.IsActionJustPressed("attack_light") && !_comboActive)
            {
                _comboActive = true;
                _comboStep = 0;
                _comboTimer = 0.05f;
            }

            // Auto-avanÃ§o do combo
            if (_comboActive && _comboStep >= 0)
            {
                _comboTimer -= (float)GetProcessDeltaTime();
                if (_comboTimer <= 0)
                {
                    _combat.ChainAttack(ComboSequence[_comboStep], false);
                    _comboStep++;
                    if (_comboStep >= ComboSequence.Length)
                    {
                        _comboActive = false;
                        _comboStep = -1;
                    }
                    else
                    {
                        _comboTimer = ComboInterval;
                    }
                }
            }

            if (Input.IsActionJustPressed("attack_heavy"))
                _combat.PerformAttack(AttackInput.Heavy);

            if (Input.IsActionJustPressed("kick_heavy"))
                _combat.PerformAttack(AttackInput.KickHeavy);

            if (Input.IsActionJustPressed("uppercut"))
                _combat.PerformAttack(AttackInput.Uppercut);
        }

        private void TryGrabTarget()
        {
            var space = GetWorld3D().DirectSpaceState;
            Vector3 origin = GlobalPosition + Vector3.Up * 1f;
            Vector3 forward = -GlobalTransform.Basis.Z;
            var query = PhysicsRayQueryParameters3D.Create(origin, origin + forward * 2.5f);
            query.CollisionMask = 1;
            var result = space.IntersectRay(query);

            if (result.Count > 0)
            {
                Node hit = (Node)result["collider"];
                Node owner = hit.GetOwner();
                if (owner != null && owner != this && owner is PlayerController target)
                {
                    _combat.TryGrab(target);
                }
            }
        }

        private void TryInteract()
        {
            if (_isDowned) return;
            Node interactableNode = null;

            var space = GetWorld3D().DirectSpaceState;
            Vector3 origin = _camera != null ? _camera.GlobalPosition : GlobalPosition + Vector3.Up * 1.2f;
            Vector3 forward = _camera != null ? -_camera.GlobalTransform.Basis.Z : -GlobalTransform.Basis.Z;
            var query = PhysicsRayQueryParameters3D.Create(origin, origin + forward * 3f);
            query.CollisionMask = 1;
            query.CollideWithAreas = true;
            var result = space.IntersectRay(query);

            if (result.Count > 0)
            {
                Node collider = (Node)result["collider"];
                interactableNode = ResolveInteractable(collider);
            }

            if (interactableNode == null)
                interactableNode = FindClosestInteractable();

            if (interactableNode is InteractableObject interactable)
            {
                interactable.Interact(this);
                return;
            }

            if (interactableNode is PickupItem pickup)
            {
                pickup.Collect(this);
                return;
            }

            // Check for downed player to revive
            PlayerController nearestDowned = null;
            float closestDist = 3f;
            foreach (var p in GetTree().GetNodesInGroup("downed_players"))
            {
                if (p == this || p is not PlayerController downed) continue;
                float d = GlobalPosition.DistanceTo(downed.GlobalPosition);
                if (d < closestDist)
                {
                    closestDist = d;
                    nearestDowned = downed;
                }
            }

            if (nearestDowned != null)
            {
                nearestDowned._revivingAlly = this;
                nearestDowned._isBeingRevived = true;
                _revivingTarget = nearestDowned;
                nearestDowned.ReviveTick(0f);
            }
        }

        private Node FindClosestInteractable()
        {
            Node closest = null;
            float closestDist = float.MaxValue;

            var interactables = GetTree().GetNodesInGroup("interactables");
            foreach (Node node in interactables)
            {
                if (!(node is Node3D n3d)) continue;
                float dist = GlobalPosition.DistanceSquaredTo(n3d.GlobalPosition);
                if (dist < closestDist && dist <= 6.25f)
                {
                    closestDist = dist;
                    closest = node;
                }
            }

            if (closest != null)
                return closest;

            var space = GetWorld3D().DirectSpaceState;
            var sphereQuery = new PhysicsShapeQueryParameters3D
            {
                Shape = new SphereShape3D { Radius = 2.5f },
                Transform = new Transform3D(Basis.Identity, GlobalPosition),
                CollisionMask = 1,
                CollideWithBodies = true,
                CollideWithAreas = true
            };
            var results = space.IntersectShape(sphereQuery);

            foreach (Godot.Collections.Dictionary result in results)
            {
                Node collider = (Node)result["collider"];
                Node resolved = ResolveInteractable(collider);
                if (resolved == null) continue;
                float dist = GlobalPosition.DistanceSquaredTo(collider is Node3D n3d ? n3d.GlobalPosition : GlobalPosition);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = resolved;
                }
            }

            if (closest != null)
                return closest;

            foreach (Node3D body in _interactionSensor.GetOverlappingBodies())
            {
                Node resolved = ResolveInteractable(body);
                if (resolved == null) continue;
                float dist = GlobalPosition.DistanceSquaredTo(body.GlobalPosition);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = resolved;
                }
            }

            foreach (Area3D area in _interactionSensor.GetOverlappingAreas())
            {
                Node resolved = ResolveInteractable(area);
                if (resolved == null) continue;
                float dist = GlobalPosition.DistanceSquaredTo(area.GlobalPosition);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = resolved;
                }
            }

            return closest;
        }

        private static Node ResolveInteractable(Node node)
        {
            Node current = node;
            while (current != null)
            {
                if (current is InteractableObject || current is PickupItem)
                    return current;

                current = current.GetParent();
            }

            return node?.GetOwner();
        }

        private void UpdateState(double delta)
        {
            if (_state == null) return;

            if (_health != null && !_health.IsAlive())
            {
                _state.TransitionTo(PlayerState.Dead);
                return;
            }

            if (_stunTimer > 0)
            {
                _stunTimer -= (float)delta;
                if (_stunTimer <= 0)
                    _state.TransitionTo(PlayerState.Idle);
                return;
            }

            if (_knockbackVelocity.Length() > 0.1f)
            {
                _state.TransitionTo(PlayerState.KnockedDown);
                return;
            }

            if (_combat != null)
            {
                if (_combat.IsGrabbed)
                {
                    _state.TransitionTo(PlayerState.Grabbed);
                    return;
                }
                if (_combat.IsGrabbing)
                {
                    _state.TransitionTo(PlayerState.Grabbing);
                    return;
                }
                if (_combat.IsBlocking)
                {
                    _state.TransitionTo(PlayerState.Blocking);
                    return;
                }
                if (_combat.IsAttacking)
                {
                    if (_state.CurrentState != PlayerState.LightAttack
                        && _state.CurrentState != PlayerState.HeavyAttack
                        && _state.CurrentState != PlayerState.Kick
                        && _state.CurrentState != PlayerState.Uppercut)
                    {
                        _state.TransitionTo(PlayerState.LightAttack);
                    }
                    return;
                }
            }

            if (!IsOnFloor())
            {
                _state.TransitionTo(Velocity.Y > 0 ? PlayerState.Jumping : PlayerState.Falling);
                return;
            }

            if (_inputDir.Length() > 0.01f)
                _state.TransitionTo(PlayerState.Running);
            else
                _state.TransitionTo(PlayerState.Idle);
        }

        private void HandleKnockback(float delta)
        {
            if (_knockbackVelocity.Length() < 0.1f) return;

            Vector3 vel = Velocity;
            vel = _knockbackVelocity;
            vel.Y += GetGravity().Y * delta;
            _knockbackVelocity = _knockbackVelocity.Lerp(Vector3.Zero, KnockbackDeceleration * delta);

            Velocity = vel;
            MoveAndSlide();

            if (_knockbackVelocity.Length() < 0.3f)
            {
                _knockbackVelocity = Vector3.Zero;
                if (_state != null)
                    _state.TransitionTo(PlayerState.Idle);
            }
        }

        private static string AnimForState(PlayerState state)
        {
            return state switch
            {
                PlayerState.Idle => "Idle",
                PlayerState.Blocking => "Punch_Enter",
                PlayerState.Walking => "Walk",
                PlayerState.Running => "Jog_Fwd",
                PlayerState.Jumping => "Jump",
                PlayerState.Falling => "Jump",
                PlayerState.LightAttack => "Punch_Jab",
                PlayerState.HeavyAttack => "Punch_Cross",
                PlayerState.Kick => "Punch_Jab",
                PlayerState.Uppercut => "Punch_Cross",
                PlayerState.Stunned => "Hit_Chest",
                PlayerState.KnockedDown => "Hit_Head",
                PlayerState.GettingUp => "Idle",
                PlayerState.Dodging => "Roll",
                PlayerState.Grabbing => "Push",
                PlayerState.Grabbed => "Hit_Chest",
                PlayerState.Thrown => "Hit_Head",
                PlayerState.Dead => "Death01",
                PlayerState.Downed => "Slide",
                _ => "Idle",
            };
        }

        private void UpdateAnimation()
        {
            if (_state == null) return;
            if (_isDowned) return;

            bool onGround = IsOnFloor();
            bool justLanded = onGround && !_wasOnGround;
            bool justLeftGround = _wasOnGround && !onGround;
            PlayerState curState = _state.CurrentState;
            bool isAttack = curState is PlayerState.LightAttack or PlayerState.HeavyAttack
                or PlayerState.Kick or PlayerState.Uppercut;

            string bodyAnim;

            if (justLeftGround)
            {
                bodyAnim = "Jump_Start";
                _jumpStartDone = false;
                _jumpLandDone = false;
            }
            else if (!onGround)
            {
                bodyAnim = _jumpStartDone ? "Jump" : "Jump_Start";
            }
            else if (justLanded && !_jumpLandDone)
            {
                bodyAnim = "Jump_Land";
            }
            else
            {
                _jumpStartDone = true;
                _jumpLandDone = true;

                if (isAttack)
                {
                    if (_wantsCrouch)
                        bodyAnim = _inputDir.Length() > 0.01f ? "Crouch_Fwd" : "Crouch_Idle";
                    else if (_canSprint && Input.IsActionPressed("sprint"))
                        bodyAnim = "Sprint";
                    else if (_inputDir.Length() > 0.01f)
                        bodyAnim = "Jog_Fwd";
                    else
                        bodyAnim = "Idle";
                }
                else if (curState == PlayerState.Blocking)
                {
                    bodyAnim = "Punch_Enter";
                }
                else
                {
                    bodyAnim = AnimForState(curState);

                    if (_wantsCrouch && curState is PlayerState.Idle
                        or PlayerState.Walking or PlayerState.Running)
                        bodyAnim = _inputDir.Length() > 0.01f ? "Crouch_Fwd" : "Crouch_Idle";

                    if ((_canSprint || _speedMultiplier > 1f) && curState == PlayerState.Running)
                        bodyAnim = "Sprint";
                }

                if (bodyAnim == "Idle")
                {
                    _idleTimer += (float)GetProcessDeltaTime();
                    if (_idleTimer > 8f)
                    {
                        bodyAnim = "Idle_Talking";
                        if (_idleTimer > 12f) _idleTimer = 0f;
                    }
                }
                else
                {
                    _idleTimer = 0f;
                }
            }

            _wasOnGround = onGround;

            float targetSpeedScale = 1f;
            if (bodyAnim is "Jog_Fwd" or "Sprint")
            {
                float refSpeed = bodyAnim == "Sprint" ? SprintSpeed * 2f : SprintSpeed;
                targetSpeedScale = _animSpeed * (SprintSpeed / refSpeed);
            }

            PlayBodyAnim(bodyAnim, 0.15f, targetSpeedScale);

            // Upper body overlay: combat animations (waist up only)
            if (isAttack)
            {
                string upperAnim = AnimForState(curState);
                PlayUpperAnim(upperAnim, 0.3f, 0.8f);
            }
            else if (_currentUpperAnim != null)
            {
                _upperPlayer?.Stop();
                _currentUpperAnim = null;
            }
        }

        private bool PlayBodyAnim(string targetAnim, float crossfade, float speedScale)
        {
            if (!_animPlayers.TryGetValue(targetAnim, out var entry)) { GD.PrintErr($"PlayBodyAnim: '{targetAnim}' not found in _animPlayers (keys: {string.Join(",", new System.Collections.Generic.List<string>(_animPlayers.Keys))})"); return false; }
            if (targetAnim == _currentAnim && Mathf.Abs(entry.player.SpeedScale - speedScale) <= 0.001f) return true;

            if (targetAnim != _currentAnim)
            {
                _currentAnimPlayer?.Stop();
                float blend = targetAnim is "Jump_Start" or "Jump_Land" ? 0f : crossfade;
                entry.player.Play(entry.animName, blend);
                _currentAnimPlayer = entry.player;
                _currentAnim = targetAnim;

                if (targetAnim == "Jump_Start") _jumpStartDone = false;
                if (targetAnim == "Jump_Land") _jumpLandDone = false;
            }

            entry.player.SpeedScale = speedScale;
            return true;
        }

        private void PlayUpperAnim(string targetAnim, float crossfade, float speedScale)
        {
            if (_upperPlayer == null) return;
            if (targetAnim == _currentUpperAnim)
            {
                if (Mathf.Abs(_upperPlayer.SpeedScale - speedScale) > 0.001f)
                    _upperPlayer.SpeedScale = speedScale;
                return;
            }

            if (_currentUpperAnim != null)
                _upperPlayer.Stop();

            var animation = _upperPlayer.GetAnimation(targetAnim);
            if (animation == null) return;

            _upperPlayer.Play(targetAnim, crossfade);
            _upperPlayer.SpeedScale = speedScale;
            _currentUpperAnim = targetAnim;
        }

        public Vector2 GetInputDir() => _inputDir;

        public void SetAnimationState(string state)
        {
            if (_state == null || string.IsNullOrWhiteSpace(state))
                return;

            switch (state)
            {
                case "idle":
                    _state.TransitionTo(PlayerState.Idle);
                    break;
                case "attack":
                    if (_combat?.CurrentAttack != null)
                    {
                        switch (_combat.CurrentAttack.Type)
                        {
                            case AttackType.LightPunch:
                                _state.TransitionTo(PlayerState.LightAttack);
                                break;
                            case AttackType.HeavyPunch:
                            case AttackType.Uppercut:
                                _state.TransitionTo(PlayerState.HeavyAttack);
                                break;
                            case AttackType.LightKick:
                            case AttackType.HeavyKick:
                                _state.TransitionTo(PlayerState.Kick);
                                break;
                            default:
                                _state.TransitionTo(PlayerState.LightAttack);
                                break;
                        }
                    }
                    else
                    {
                        _state.TransitionTo(PlayerState.LightAttack);
                    }
                    break;
                case "block":
                    _state.TransitionTo(PlayerState.Blocking);
                    break;
                case "dodge":
                    _state.TransitionTo(PlayerState.Dodging);
                    break;
                case "grab":
                    _state.TransitionTo(PlayerState.Grabbing);
                    break;
                case "grabbed":
                    _state.TransitionTo(PlayerState.Grabbed);
                    break;
            }
        }

        public void ApplyStun(float duration)
        {
            _stunTimer = duration;
            _state?.TransitionTo(PlayerState.Stunned);
        }

        public void ApplyKnockback(Vector3 force)
        {
            _knockbackVelocity = force;
            _state?.TransitionTo(PlayerState.KnockedDown);
        }

        public void OnGrabbed(Node grabber)
        {
            _combat?.OnGrabbedBy(grabber);
        }

        public void OnReleased()
        {
            _combat?.OnReleased();
        }

        public void OnThrown(Vector3 velocity)
        {
            _combat?.OnThrown(velocity);
        }

        public void ModifySpeedMultiplier(float amount, float duration)
        {
            _speedMultiplier += amount;
            _speedBuffTimer = duration;
        }

        private void UpdateSpeedBuff(float delta)
        {
            if (_speedBuffTimer > 0f)
            {
                _speedBuffTimer -= delta;
                if (_speedBuffTimer <= 0f)
                    _speedMultiplier = 1f;
            }

            if (_damageBuffTimer > 0f)
            {
                _damageBuffTimer -= delta;
                if (_damageBuffTimer <= 0f)
                {
                    if (_combat?.CurrentStyle != null)
                        _combat.CurrentStyle.DamageMultiplier -= _damageBuffValue;
                    _damageBuffValue = 0f;
                    if (_modelPivot != null)
                        _modelPivot.Scale = _originalScale;
                }
            }

            if (_invisibilityBuffTimer > 0f)
            {
                _invisibilityBuffTimer -= delta;
                if (_invisibilityBuffTimer <= 0f)
                {
                    RemoveInvisibility();
                    if (Multiplayer.MultiplayerPeer != null)
                        Rpc(nameof(SyncInvisibilityRpc), false);
                }
            }

            if (_superJumpTimer > 0f)
            {
                _superJumpTimer -= delta;
                if (_superJumpTimer <= 0f)
                    _superJumpTimer = 0f;
            }
        }

        public void EquipItem(ItemData item)
        {
            if (_equippedItem == item)
                return;

            if (_equippedItemNode != null)
            {
                _equippedItemNode.QueueFree();
                _equippedItemNode = null;
            }

            _equippedItem = item;

            if (item?.WorldModel != null && _skeleton != null)
            {
                int boneIdx = FindEquipBone();
                if (boneIdx == -1)
                {
                    GD.PrintErr($"Equip bone not found for '{EquipBoneName}'");
                }
                else
                {
                    var attachment = new BoneAttachment3D();
                    attachment.Name = "EquipAttachment";
                    attachment.BoneName = _skeleton.GetBoneName(boneIdx);
                    _skeleton.AddChild(attachment);

                    var model = item.WorldModel.Instantiate<Node3D>();
                    model.Name = "EquippedModel";
                    attachment.AddChild(model);

            if (item is WeaponData)
            {
                model.Position = new Vector3(0.02f, -0.02f, 0.12f);
                model.Rotation = new Vector3(Mathf.DegToRad(-90), 0, 0);
                model.Scale = Vector3.One * 0.8f;
            }
            else if (item is HealingData)
            {
                model.Position = new Vector3(0, -0.03f, 0.05f);
                model.Scale = Vector3.One * 0.65f;
            }
            else if (item is BuffData)
            {
                model.Position = new Vector3(0, -0.02f, 0.04f);
                model.Scale = Vector3.One * 0.6f;
            }

                    _equippedItemNode = attachment;
                }
            }

        }

        private int FindEquipBone()
        {
            string[] candidates =
            {
                EquipBoneName,
                "hand_r", "hand_l",
                "lowerarm_r", "lowerarm_l",
                "hand.R", "hand.L",
                "forearm.R", "forearm.L",
                "RightHand", "LeftHand",
                "DEF-hand.R", "DEF-hand.L",
            };
            foreach (var name in candidates)
            {
                int idx = _skeleton.FindBone(name);
                if (idx != -1)
                    return idx;
            }
            return -1;
        }

        public bool AcquireItem(ItemData item)
        {
            if (item == null)
                return false;

            // Special items (buff/technique) go to slots 1-2
            if (item.Type == ItemType.Buff || item.Type == ItemType.Technique)
            {
                for (int i = 0; i < WeaponSlotCount; i++)
                {
                    if (_weaponSlots[i] != null)
                        continue;

                    _weaponSlots[i] = item;
                    SyncInventoryHud();
                    return true;
                }

                GD.Print("Inventario cheio");
                return false;
            }

            // Regular items (weapon, healing, armor) go to slots 3-6
            for (int i = 0; i < InventorySlotCount; i++)
            {
                if (_inventorySlots[i] != null)
                    continue;

                _inventorySlots[i] = item;
                if (_selectedInventorySlot == -1)
                {
                    _selectedInventorySlot = i;
                    if (item is WeaponData weapon)
                        _combat?.EquipWeapon(weapon);
                    EquipItem(item);
                }
                SyncInventoryHud();
                return true;
            }

            // Inventory full â€” swap with equipped item if any
            if (_selectedInventorySlot >= 0 && _selectedInventorySlot < InventorySlotCount)
            {
                var oldItem = _inventorySlots[_selectedInventorySlot];
                SpawnPickupOnGround(oldItem);

                _inventorySlots[_selectedInventorySlot] = item;
                if (item is WeaponData newWeapon)
                    _combat?.EquipWeapon(newWeapon);
                else
                    _combat?.UnequipWeapon();
                EquipItem(item);
                SyncInventoryHud();
                return true;
            }

            GD.Print("Inventario cheio");
            return false;
        }

        private void DropEquippedItem()
        {
            if (_selectedInventorySlot < 0 || _selectedInventorySlot >= InventorySlotCount)
                return;

            var item = _inventorySlots[_selectedInventorySlot];
            if (item == null)
                return;

            _inventorySlots[_selectedInventorySlot] = null;
            if (item is WeaponData)
                _combat?.UnequipWeapon();
            EquipItem(null);
            _selectedInventorySlot = -1;

            SpawnPickupOnGround(item);
            SyncInventoryHud();
        }

        private void SpawnPickupOnGround(ItemData item)
        {
            var pickupScene = GD.Load<PackedScene>("res://pickups/items/PickupItem.tscn");
            if (pickupScene == null) return;

            var pickup = pickupScene.Instantiate<PickupItem>();
            var scene = GetTree().CurrentScene;
            if (scene != null)
                scene.AddChild(pickup);
            else
                AddChild(pickup);
            Vector3 forward = GlobalTransform.Basis.Z;
            if (_modelPivot != null)
            {
                float yaw = _modelPivot.Rotation.Y;
                forward = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
            }
            pickup.GlobalPosition = GlobalPosition + forward * 1.5f;
            pickup.GlobalPosition = new Vector3(pickup.GlobalPosition.X, 0.5f, pickup.GlobalPosition.Z);
            pickup.AssignItemData(item);
        }

        public ItemData GetWeaponSlot(int index)
        {
            if (index < 0 || index >= WeaponSlotCount) return null;
            return _weaponSlots[index];
        }

        public ItemData GetInventorySlot(int index)
        {
            if (index < 0 || index >= InventorySlotCount) return null;
            return _inventorySlots[index];
        }

        public int GetSelectedWeaponSlot() => _selectedWeaponSlot;

        public int GetSelectedInventorySlot() => _selectedInventorySlot;

        public void SwapSlots(string fromType, int fromIndex, string toType, int toIndex)
        {
            if (fromType == toType && fromIndex == toIndex) return;

            if (fromType == "weapon" && toType == "weapon")
            {
                (_weaponSlots[fromIndex], _weaponSlots[toIndex]) = (_weaponSlots[toIndex], _weaponSlots[fromIndex]);
                if (_selectedWeaponSlot == fromIndex) _selectedWeaponSlot = toIndex;
                else if (_selectedWeaponSlot == toIndex) _selectedWeaponSlot = fromIndex;
            }
            else if (fromType == "inventory" && toType == "inventory")
            {
                (_inventorySlots[fromIndex], _inventorySlots[toIndex]) = (_inventorySlots[toIndex], _inventorySlots[fromIndex]);
                if (_selectedInventorySlot == fromIndex) _selectedInventorySlot = toIndex;
                else if (_selectedInventorySlot == toIndex) _selectedInventorySlot = fromIndex;
            }

            SyncInventoryHud();
            _inventoryScreen?.SyncFromPlayer();
        }

        public void DropItemAtSlot(string slotType, int index)
        {
            ItemData item = null;

            if (slotType == "weapon" && index >= 0 && index < WeaponSlotCount)
            {
                item = _weaponSlots[index];
                if (item == null) return;
                _weaponSlots[index] = null;
                if (_selectedWeaponSlot == index)
                {
                    _selectedWeaponSlot = FindFirstOccupiedWeaponSlot();
                    var newItem = _selectedWeaponSlot >= 0 ? _weaponSlots[_selectedWeaponSlot] : null;
                    if (newItem is WeaponData w) _combat?.EquipWeapon(w);
                    else _combat?.UnequipWeapon();
                    EquipItem(newItem);
                }
            }
            else if (slotType == "inventory" && index >= 0 && index < InventorySlotCount)
            {
                item = _inventorySlots[index];
                if (item == null) return;
                _inventorySlots[index] = null;
                if (_selectedInventorySlot == index)
                {
                    _combat?.UnequipWeapon();
                    EquipItem(null);
                    _selectedInventorySlot = -1;
                }
            }

            if (item != null)
                SpawnPickupOnGround(item);

            SyncInventoryHud();
            _inventoryScreen?.SyncFromPlayer();
        }

        public void OpenInventory()
        {
            if (_inventoryScreen != null) return;

            _state?.TransitionTo(PlayerState.Idle);

            var canvasLayer = new CanvasLayer();
            canvasLayer.Name = "InventoryLayer";
            canvasLayer.Layer = 2;
            var currentScene = GetTree().CurrentScene;
            if (currentScene != null)
                currentScene.AddChild(canvasLayer);
            else
                AddChild(canvasLayer);

            var scene = GD.Load<PackedScene>("res://scenes/ui/InventoryScreen.tscn");
            if (scene != null)
                _inventoryScreen = scene.Instantiate<InventoryScreen>();
            else
                _inventoryScreen = new InventoryScreen();

            canvasLayer.AddChild(_inventoryScreen);

            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        public void CloseInventory()
        {
            if (_inventoryScreen == null) return;

            var layer = _inventoryScreen.GetParent();
            _inventoryScreen.QueueFree();
            _inventoryScreen = null;
            layer?.QueueFree();

            Input.MouseMode = Input.MouseModeEnum.Captured;
            SyncInventoryHud();
        }

        private void UseSpecialSlot(int slot)
        {
            if (slot < 0 || slot >= WeaponSlotCount)
                return;

            if (_specialActiveTimers[slot] > 0f || _specialCooldowns[slot] > 0f)
                return;

            var item = _weaponSlots[slot];
            if (item == null)
                return;

            float duration = 0f;
            switch (item)
            {
                case BuffData buff:
                    ApplyBuff(buff);
                    duration = buff.BuffDuration;
                    break;
                default:
                    return;
            }

            _specialActiveTimers[slot] = duration;
            SyncInventoryHud();
        }

        private void TickCooldowns(float delta)
        {
            bool anyTimer = false;
            for (int i = 0; i < WeaponSlotCount; i++)
            {
                if (_specialActiveTimers[i] > 0f)
                {
                    anyTimer = true;
                    _specialActiveTimers[i] -= delta;
                    if (_specialActiveTimers[i] <= 0f)
                    {
                        _specialCooldowns[i] = 60f;
                        SyncInventoryHud();
                    }
                }
                else if (_specialCooldowns[i] > 0f)
                {
                    anyTimer = true;
                    _specialCooldowns[i] -= delta;
                    if (_specialCooldowns[i] <= 0f)
                    {
                        _specialCooldowns[i] = 0f;
                        SyncInventoryHud();
                    }
                }
            }

            if (anyTimer)
            {
                _hudTimer += delta;
                if (_hudTimer >= 0.1f)
                {
                    _hudTimer = 0f;
                    SyncInventoryHud();
                }
            }
            else
            {
                _hudTimer = 0f;
            }
        }

        private void SelectWeaponSlot(int slot)
        {
            if (slot < 0 || slot >= WeaponSlotCount)
                return;

            _selectedWeaponSlot = slot;
            _selectedInventorySlot = -1;
            var item = _weaponSlots[slot];

            if (item is WeaponData weapon)
                _combat?.EquipWeapon(weapon);
            else
                _combat?.UnequipWeapon();

            EquipItem(item);
            SyncInventoryHud();
        }

        private void RemoveWeaponItem(int slot)
        {
            if (slot < 0 || slot >= WeaponSlotCount)
                return;

            _weaponSlots[slot] = null;

            if (_selectedWeaponSlot == slot)
            {
                _selectedWeaponSlot = FindFirstOccupiedWeaponSlot();
                var item = _selectedWeaponSlot >= 0 ? _weaponSlots[_selectedWeaponSlot] : null;

                if (item is WeaponData weapon)
                    _combat?.EquipWeapon(weapon);
                else
                    _combat?.UnequipWeapon();

                EquipItem(item);
            }

            SyncInventoryHud();
        }

        private int FindFirstOccupiedWeaponSlot()
        {
            for (int i = 0; i < WeaponSlotCount; i++)
            {
                if (_weaponSlots[i] != null)
                    return i;
            }

            return -1;
        }

        private void UseInventorySlot(int index)
        {
            if (index < 0 || index >= InventorySlotCount)
                return;

            var item = _inventorySlots[index];
            if (item == null)
                return;

            switch (item)
            {
                case WeaponData weapon:
                    if (_selectedInventorySlot == index)
                    {
                        _combat?.UnequipWeapon();
                        EquipItem(null);
                        _selectedInventorySlot = -1;
                    }
                    else
                    {
                        _selectedWeaponSlot = -1;
                        _selectedInventorySlot = index;
                        _combat?.EquipWeapon(weapon);
                        EquipItem(item);
                    }
                    break;
                case HealingData healing:
                    _health?.Heal(healing.HealAmount);
                    _inventorySlots[index] = null;
                    if (_selectedInventorySlot == index)
                    {
                        _combat?.UnequipWeapon();
                        EquipItem(null);
                        _selectedInventorySlot = -1;
                    }
                    break;
                case BuffData buff:
                    ApplyBuff(buff);
                    _inventorySlots[index] = null;
                    if (_selectedInventorySlot == index)
                    {
                        _combat?.UnequipWeapon();
                        EquipItem(null);
                        _selectedInventorySlot = -1;
                    }
                    break;
                default:
                    return;
            }

            SyncInventoryHud();
        }

        public void SyncInventoryHud()
        {
            var hud = GameManager.Instance?.CurrentHUD;
            if (hud == null) return;

            hud.UpdateWeaponSlots(_weaponSlots, _selectedWeaponSlot, _specialActiveTimers, _specialCooldowns);
            hud.UpdateInventorySlots(_inventorySlots, _selectedInventorySlot);
        }

        private void ApplyBuff(BuffData buff)
        {
            if (buff == null)
                return;

            switch (buff.BuffType)
            {
                case BuffType.Damage:
                    if (_combat?.CurrentStyle != null)
                    {
                        _damageBuffValue = buff.BuffValue;
                        _damageBuffTimer = buff.BuffDuration;
                        _combat.CurrentStyle.DamageMultiplier += buff.BuffValue;
                        if (_modelPivot != null)
                            _modelPivot.Scale = _originalScale * 1.15f;
                    }
                    break;
                case BuffType.Speed:
                    ModifySpeedMultiplier(buff.BuffValue, buff.BuffDuration);
                    break;
                case BuffType.Defense:
                    if (_combat?.CurrentStyle != null)
                        _combat.CurrentStyle.DefenseMultiplier += buff.BuffValue;
                    break;
                case BuffType.Invisibility:
                    _invisibilityBuffTimer = buff.BuffDuration;
                    ApplyInvisibility();
                    if (Multiplayer.MultiplayerPeer != null)
                        Rpc(nameof(SyncInvisibilityRpc), true);
                    break;
                case BuffType.Jump:
                    _superJumpTimer = buff.BuffDuration;
                    break;
            }
        }

        private void OnDied()
        {
            OnDowned();
        }

        private void OnDowned()
        {
            GD.Print("OnDowned called");
            if (_isDowned) { GD.Print("OnDowned: already downed, returning"); return; }
            _isDowned = true;
            _isFullyDead = false;
            _downedTimer = DownedDuration;
            _reviveProgress = 0f;
            _isBeingRevived = false;
            _revivingAlly = null;

            _state?.TransitionTo(PlayerState.Downed);
            PlayBodyAnim("Slide", 0.15f, 1f);
            if (_modelPivot != null)
                _modelPivot.Position = _pivotOriginalPos;

            RemoveInvisibility();
            if (Multiplayer.MultiplayerPeer != null)
                Rpc(nameof(SyncInvisibilityRpc), false);
            _invisibilityBuffTimer = 0f;

            SetCollisionLayerValue(1, false);
            SetCollisionMaskValue(1, false);

            var hurtbox = GetNodeOrNull<Area3D>("Hurtbox");
            if (hurtbox != null)
                hurtbox.SetDeferred(Area3D.PropertyName.Monitorable, false);

            AddToGroup("downed_players");

            _downedCountdownLabel = new Label3D();
            _downedCountdownLabel.Text = Mathf.CeilToInt(_downedTimer).ToString();
            _downedCountdownLabel.FontSize = 48;
            _downedCountdownLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            _downedCountdownLabel.Modulate = new Color(1, 0.3f, 0.3f, 1);
            _downedCountdownLabel.NoDepthTest = true;
            _downedCountdownLabel.Position = new Vector3(0, 1.0f, 0);
            AddChild(_downedCountdownLabel);
        }

        private void OnFullyDead()
        {
            if (_isFullyDead) return;
            _isFullyDead = true;

            var gameManager = GameManager.Instance;
            gameManager?.OnPlayerDied(this);

            RemoveFromGroup("downed_players");

            if (_downedCountdownLabel != null)
            {
                _downedCountdownLabel.QueueFree();
                _downedCountdownLabel = null;
            }

            _state?.TransitionTo(PlayerState.Dead);
            ShowDeathScreen();
        }

        private void ShowDeathScreen()
        {
            if (NetworkManager.Instance?.IsDedicatedServer == true) return;
            _isSpectating = false;

            var hud = GameManager.Instance?.CurrentHUD;
            if (hud != null)
                hud.Visible = false;

            var canvasLayer = new CanvasLayer();
            canvasLayer.Name = "DeathScreenLayer";
            canvasLayer.Layer = 3;
            var currentScene = GetTree().CurrentScene;
            if (currentScene != null)
                currentScene.AddChild(canvasLayer);
            else
                AddChild(canvasLayer);

            var scene = GD.Load<PackedScene>("res://scenes/ui/DeathScreen.tscn");
            DeathScreen screen;
            if (scene != null)
                screen = scene.Instantiate<DeathScreen>();
            else
                screen = new DeathScreen();

            screen.SetPlayer(this);
            canvasLayer.AddChild(screen);

            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        private void OpenEscapeMenu()
        {
            if (NetworkManager.Instance?.IsDedicatedServer == true) return;
            if (_escapeMenu != null) return;

            var canvasLayer = new CanvasLayer();
            canvasLayer.Name = "EscapeMenuLayer";
            canvasLayer.Layer = 3;
            var currentScene = GetTree().CurrentScene;
            if (currentScene != null)
                currentScene.AddChild(canvasLayer);
            else
                AddChild(canvasLayer);

            var scene = GD.Load<PackedScene>("res://scenes/ui/EscapeMenu.tscn");
            EscapeMenu menu;
            if (scene != null)
                menu = scene.Instantiate<EscapeMenu>();
            else
                menu = new EscapeMenu();

            menu.SetPlayer(this);
            canvasLayer.AddChild(menu);
            _escapeMenu = menu;

            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        public void ResumeFromMenu()
        {
            _escapeMenu = null;
            if (!_isFullyDead)
                Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        private void SnapToGround()
        {
            var space = GetWorld3D().DirectSpaceState;
            var query = PhysicsRayQueryParameters3D.Create(
                GlobalPosition + Vector3.Up * 0.5f,
                GlobalPosition + Vector3.Down * 5f,
                CollisionMask);
            var result = space.IntersectRay(query);
            if (result.Count > 0 && result.ContainsKey("position"))
            {
                Vector3 hitPos = (Vector3)result["position"];
                Vector3 pos = GlobalPosition;
                pos.Y = hitPos.Y;
                GlobalPosition = pos;
            }
        }

        private void OnRevived()
        {
            _isDowned = false;
            _isBeingRevived = false;
            _revivingAlly = null;
            _reviveProgress = 0f;

            if (_downedCountdownLabel != null)
            {
                _downedCountdownLabel.QueueFree();
                _downedCountdownLabel = null;
            }
            RemoveFromGroup("downed_players");

            SetCollisionLayerValue(1, true);
            SetCollisionMaskValue(1, true);
            if (_modelPivot != null)
                _modelPivot.Position = _pivotOriginalPos;

            var hurtbox = GetNodeOrNull<Area3D>("Hurtbox");
            if (hurtbox != null)
                hurtbox.SetDeferred(Area3D.PropertyName.Monitorable, true);

            if (_health != null)
            {
                _health.Heal(_health.MaxHealth * 0.5f);
                _health.RestoreStamina(_health.MaxStamina);
            }

            _state?.TransitionTo(PlayerState.Idle);
            Velocity = Vector3.Zero;
        }

        private void UpdateDowned(float delta)
        {
            if (!_isDowned) return;
            if (_isFullyDead) return;

            _downedTimer -= delta;

            if (_isBeingRevived)
            {
                if (_revivingAlly != null && IsInstanceValid(_revivingAlly)
                    && GlobalPosition.DistanceTo(_revivingAlly.GlobalPosition) > 3f)
                {
                    _isBeingRevived = false;
                    _revivingAlly = null;
                    _reviveProgress = 0f;
                }

                if (_downedCountdownLabel != null)
                {
                    int pct = Mathf.RoundToInt(_reviveProgress / ReviveHoldDuration * 100);
                    _downedCountdownLabel.Text = $"Revivendo... {pct}%";
                    _downedCountdownLabel.Modulate = new Color(0.2f, 0.8f, 0.2f, 1);
                }

                if (_reviveProgress >= ReviveHoldDuration)
                {
                    OnRevived();
                    return;
                }
            }
            else
            {
                _reviveProgress = 0f;
                _revivingAlly = null;

                if (_downedCountdownLabel != null)
                {
                    int secs = Mathf.Max(0, Mathf.CeilToInt(_downedTimer));
                    _downedCountdownLabel.Text = secs.ToString();
                    _downedCountdownLabel.Modulate = new Color(1, 0.3f, 0.3f, 1);
                    if (secs <= 10)
                        _downedCountdownLabel.Modulate = new Color(1, 0.1f, 0.1f, 1);
                }

                if (_downedTimer <= 0)
                {
                    PlayBodyAnim("Death01", 0.15f, 1f);
                    OnFullyDead();
                }
            }

            UpdateAnimation();
        }

        public void ReviveTick(float delta)
        {
            if (!_isDowned || _downedTimer <= 0) return;
            if (!_isBeingRevived) return;

            bool allyNearby = _revivingAlly != null && IsInstanceValid(_revivingAlly)
                && GlobalPosition.DistanceTo(_revivingAlly.GlobalPosition) <= 3f;

            if (!allyNearby)
            {
                _isBeingRevived = false;
                _revivingAlly = null;
                _reviveProgress = 0f;
                return;
            }

            _reviveProgress += delta;
        }

        private void CheckReviveHold()
        {
            if (_isDowned || _isSpectating) return;

            if (!Input.IsActionPressed("interact"))
            {
                if (_revivingTarget != null)
                {
                    var target = _revivingTarget;
                    target._isBeingRevived = false;
                    target._revivingAlly = null;
                    target._reviveProgress = 0f;
                    _revivingTarget = null;
                }
                return;
            }

            if (_revivingTarget != null && IsInstanceValid(_revivingTarget))
            {
                if (GlobalPosition.DistanceTo(_revivingTarget.GlobalPosition) <= 3f)
                {
                    _revivingTarget.ReviveTick((float)GetProcessDeltaTime());
                }
                else
                {
                    _revivingTarget._isBeingRevived = false;
                    _revivingTarget._revivingAlly = null;
                    _revivingTarget._reviveProgress = 0f;
                    _revivingTarget = null;
                }
            }
        }

        public void EnterSpectateMode()
        {
            if (_health != null && _health.IsAlive()) return;
            if (NetworkManager.Instance?.IsDedicatedServer == true) return;
            _isSpectating = true;

            var hud = GameManager.Instance?.CurrentHUD;
            if (hud != null)
                hud.Visible = true;

            _spectateCandidates.Clear();

            foreach (var p in GetTree().GetNodesInGroup("players"))
            {
                if (p == this || p is not Node3D node) continue;
                var h = node.GetNodeOrNull<HealthComponent>("HealthComponent");
                if (h != null && h.IsAlive())
                    _spectateCandidates.Add(node);
            }

            foreach (var n in GetTree().GetNodesInGroup("npcs"))
            {
                if (n is not Node3D node) continue;
                var h = node.GetNodeOrNull<HealthComponent>("HealthComponent");
                if (h != null && h.IsAlive())
                    _spectateCandidates.Add(node);
            }

            Node3D killer = _lastAttacker as Node3D;
            if (killer != null && _spectateCandidates.Contains(killer))
            {
                _spectateTarget = killer;
                _spectateIndex = _spectateCandidates.IndexOf(killer);
            }
            else if (_spectateCandidates.Count > 0)
            {
                Node3D nearest = null;
                float nearestDist = float.MaxValue;
                Vector3 myPos = GlobalPosition;
                foreach (var c in _spectateCandidates)
                {
                    float d = myPos.DistanceTo(c.GlobalPosition);
                    if (d < nearestDist)
                    {
                        nearestDist = d;
                        nearest = c;
                    }
                }
                _spectateTarget = nearest ?? _spectateCandidates[0];
                _spectateIndex = _spectateCandidates.IndexOf(_spectateTarget);
            }

            hud?.HideDeathMessage();
            if (_spectateTarget != null)
                hud?.SetSpectateTarget(_spectateTarget);
        }

        private void UpdateSpectate(float delta)
        {
            if (!IsInstanceValid(_spectateTarget))
                CycleSpectateTarget(1);

            if (IsInstanceValid(_spectateTarget))
            {
                var targetCamPivot = _spectateTarget.GetNodeOrNull<Node3D>("CameraPivot");
                if (targetCamPivot != null)
                {
                    _cameraPivot.GlobalPosition = _cameraPivot.GlobalPosition.Lerp(
                        targetCamPivot.GlobalPosition, 10f * delta);
                    _cameraPivot.GlobalRotation = _cameraPivot.GlobalRotation.Lerp(
                        targetCamPivot.GlobalRotation, 10f * delta);
                }
                else
                {
                    Vector3 targetPos = _spectateTarget.GlobalPosition;
                    _cameraPivot.GlobalPosition = _cameraPivot.GlobalPosition.Lerp(
                        new Vector3(targetPos.X, targetPos.Y + 1.5f, targetPos.Z), 5f * delta);
                }
            }

            if (Input.IsActionJustPressed("interact"))
                CycleSpectateTarget(1);
        }

        private void CycleSpectateTarget(int direction)
        {
            if (_spectateCandidates.Count == 0) return;
            _spectateIndex = (_spectateIndex + direction + _spectateCandidates.Count) % _spectateCandidates.Count;
            _spectateTarget = _spectateCandidates[_spectateIndex];

            if (!IsInstanceValid(_spectateTarget) || !_spectateTarget.GetNodeOrNull<HealthComponent>("HealthComponent")?.IsAlive() == true)
            {
                _spectateCandidates.RemoveAt(_spectateIndex);
                if (_spectateIndex >= _spectateCandidates.Count)
                    _spectateIndex = 0;
                CycleSpectateTarget(0);
            }

            var hud = GameManager.Instance?.CurrentHUD;
            if (_spectateTarget != null)
                hud?.SetSpectateTarget(_spectateTarget);
        }

        private void OnTookDamage(float damage, Node attacker)
        {
            if (attacker != null) _lastAttacker = attacker;
            ShakeEffect();
        }

        private void ShakeEffect()
        {
            if (_modelPivot == null) return;

            _shakeTween?.Kill();
            Vector3 targetOrigin = _isDowned ? _pivotOriginalPos + new Vector3(0, -0.7f, 0) : _pivotOriginalPos;
            _modelPivot.Position = targetOrigin;

            var original = targetOrigin;
            _shakeTween = CreateTween();
            for (int i = 0; i < 8; i++)
            {
                float t = i / 8f;
                float intensity = 0.08f * (1f - t);
                float sx = (float)(GD.Randf() * 2f - 1f) * intensity;
                float sz = (float)(GD.Randf() * 2f - 1f) * intensity;
                _shakeTween.TweenProperty(_modelPivot, "position",
                    original + new Vector3(sx, 0, sz), 0.15f / 8f);
            }
            _shakeTween.TweenProperty(_modelPivot, "position", original, 0.03f);
        }

        private Vector3 GetCameraRelativeInput()
        {
            if (_inputDir.Length() < 0.01f || _camera == null)
                return Vector3.Zero;

            Basis cameraBasis = _camera.GlobalTransform.Basis;
            Vector3 forward = -cameraBasis.Z;
            Vector3 right = cameraBasis.X;
            forward.Y = 0;
            right.Y = 0;
            forward = forward.Normalized();
            right = right.Normalized();

            return (forward * _inputDir.Y + right * _inputDir.X).Normalized();
        }

        private void SetupCharacter()
        {
            // Remove any existing children from ModelPivot
            foreach (var child in _modelPivot.GetChildren())
            {
                _modelPivot.RemoveChild(child);
                child.QueueFree();
            }

            var charScene = ResourceLoader.Load<PackedScene>(GodotAnimationLibraryPath);
            if (charScene == null) { GD.PrintErr("Failed to load character scene"); return; }

            _modelRoot = charScene.Instantiate<Node3D>();
            _modelPivot.AddChild(_modelRoot);
            _skeleton = FindSkeleton(_modelRoot);

            if (_skeleton != null)
                GD.Print($"Skeleton loaded: {_skeleton.Name}, bones: {_skeleton.GetBoneCount()}");
            else
                GD.PrintErr("No Skeleton3D found in character model!");

            int meshCount = CountMeshes(_modelRoot);
            GD.Print($"Character meshes found: {meshCount}");
        }

        private static Skeleton3D FindSkeleton(Node node)
        {
            if (node is Skeleton3D skeleton) return skeleton;
            foreach (var child in node.GetChildren())
            {
                var result = FindSkeleton(child);
                if (result != null) return result;
            }
            return null;
        }

        private static int CountMeshes(Node node)
        {
            int count = node is MeshInstance3D ? 1 : 0;
            foreach (var child in node.GetChildren())
                count += CountMeshes(child);
            return count;
        }

        private void ApplyInvisibility()
        {
            float alpha = 0.3f;
            foreach (var mi in FindMeshInstances(_modelRoot))
            {
                if (mi.Mesh == null) continue;
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                {
                    var mat = mi.GetSurfaceOverrideMaterial(i) ?? mi.Mesh.SurfaceGetMaterial(i);
                    if (mat is BaseMaterial3D bm)
                    {
                        var clone = (BaseMaterial3D)bm.Duplicate();
                        clone.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                        var c = clone.AlbedoColor;
                        clone.AlbedoColor = new Color(c.R, c.G, c.B, alpha);
                        mi.SetSurfaceOverrideMaterial(i, clone);
                    }
                }
            }
        }

        private void RemoveInvisibility()
        {
            foreach (var mi in FindMeshInstances(_modelRoot))
            {
                if (mi.Mesh == null) continue;
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                    mi.SetSurfaceOverrideMaterial(i, null);
            }
        }

        private static List<MeshInstance3D> FindMeshInstances(Node node)
        {
            var list = new List<MeshInstance3D>();
            FindMeshInstancesRecursive(node, list);
            return list;
        }

        private static void FindMeshInstancesRecursive(Node node, List<MeshInstance3D> list)
        {
            if (node is MeshInstance3D mi)
                list.Add(mi);
            foreach (var child in node.GetChildren())
                FindMeshInstancesRecursive(child, list);
        }

        private void LoadAnimations()
        {
            if (_skeleton == null) return;

            var animPlayer = FindAnimationPlayer(_modelRoot);
            if (animPlayer == null) { GD.PrintErr("No AnimationPlayer in character model"); return; }

            // Use default root node path resolution (relative to AnimationPlayer's parent)

            // Create second AnimationPlayer for upper body combat overlay
            _upperPlayer = new AnimationPlayer();
            _upperPlayer.Name = "UpperBodyPlayer";
            _upperPlayer.RootNode = animPlayer.RootNode;
            _modelRoot.AddChild(_upperPlayer);
            _upperPlayer.AddAnimationLibrary("", new AnimationLibrary());

            // Strip _Loop suffix and Rig| prefix from animation names
            var lib = animPlayer.GetAnimationLibrary("");
            if (lib != null)
            {
                var namesToFix = new System.Collections.Generic.List<(string oldName, string newName)>();
                foreach (var name in lib.GetAnimationList())
                {
                    string newName = name;
                    if (newName.StartsWith("Rig|"))
                        newName = newName.Substring(4);
                    if (newName.EndsWith("_Loop"))
                        newName = newName.Substring(0, newName.Length - 5);
                    if (newName == "Roll_RM")
                        newName = "Roll";
                    if (newName != name)
                        namesToFix.Add((name, newName));
                }
                foreach (var (oldName, newName) in namesToFix)
                {
                    if (lib.HasAnimation(newName))
                        continue;
                    lib.RenameAnimation(oldName, newName);
                }

                // Create fallbacks for missing animations
                if (!lib.HasAnimation("Punch_Enter") && lib.HasAnimation("Punch_Jab"))
                {
                    var punchEnter = (Animation)lib.GetAnimation("Punch_Jab").Duplicate(true);
                    lib.AddAnimation("Punch_Enter", punchEnter);
                    GD.Print("Created fallback Punch_Enter from Punch_Jab");
                }

                // Create "Walk" from "Jog_Fwd" (AL_Standard has Jog_Fwd but not Walk)
                if (!lib.HasAnimation("Walk") && lib.HasAnimation("Jog_Fwd"))
                {
                    var walk = (Animation)lib.GetAnimation("Jog_Fwd").Duplicate(true);
                    lib.AddAnimation("Walk", walk);
                    GD.Print("Created fallback Walk from Jog_Fwd");
                }

                // Create "Sprint" from "Jog_Fwd" (speed multiplier handles the speed difference)
                if (!lib.HasAnimation("Sprint") && lib.HasAnimation("Jog_Fwd"))
                {
                    var sprint = (Animation)lib.GetAnimation("Jog_Fwd").Duplicate(true);
                    lib.AddAnimation("Sprint", sprint);
                    GD.Print("Created fallback Sprint from Jog_Fwd");
                }
            }

            GD.Print("Loaded Godot Standard animations");

            var animList = animPlayer.GetAnimationList();
            foreach (var animName in animList)
            {
                var animation = animPlayer.GetAnimation(animName);
                if (animation != null)
                {
                    string nameStr = animName;
                    if (nameStr is "Jump_Start" or "Jump_Land" or "Punch_Enter" or "Death01")
                        animation.LoopMode = Animation.LoopModeEnum.None;
                    else
                        animation.LoopMode = Animation.LoopModeEnum.Linear;

                    if (nameStr is "Idle" or "Idle_Talking" or "Idle_Torch" or "Walk" or "Jog_Fwd"
                        or "Sprint" or "Crouch_Fwd" or "Crouch_Idle"
                        or "Punch_Jab" or "Punch_Cross" or "Punch_Enter")
                        RemoveRootMotion(animation);

                    // Create upper-body-only copy for combat/block animations
                    if (nameStr is "Punch_Jab" or "Punch_Cross" or "Punch_Enter")
                    {
                        var upperAnim = (Animation)animation.Duplicate(true);
                        StripLowerBodyTracks(upperAnim);

                        // Block stance plays once and holds (no loop)
                        if (nameStr == "Punch_Enter")
                            upperAnim.LoopMode = Animation.LoopModeEnum.None;

                        _upperPlayer.GetAnimationLibrary("").AddAnimation(nameStr, upperAnim);
                    }
                }

                string nameStr2 = animName;
                _animPlayers[nameStr2] = (animPlayer, animName);
                GD.Print($"Animation ready: {nameStr2}");
            }

            animPlayer.AnimationFinished += (StringName animName) =>
            {
                string name = animName;
                if (name == "Jump_Start") _jumpStartDone = true;
                if (name == "Jump_Land") _jumpLandDone = true;
            };

            // Load Slide from UAL2 and retarget to Godot Standard skeleton
            if (lib.HasAnimation("Slide"))
            {
                GD.Print("Slide already exists in library, skipping UAL2 load");
            }
            else
            {
                GD.Print("UAL2: Starting Slide retarget");
                string ual2Path = "res://Universal Animation Library 2[Standard]/Unreal-Godot/UAL2_Standard.glb";
                var ual2Scene = ResourceLoader.Load<PackedScene>(ual2Path);
                GD.Print($"UAL2: Scene loaded = {ual2Scene != null}");
                if (ual2Scene != null)
                {
                    var ual2Instance = ual2Scene.Instantiate();
                    var ual2AnimPlayer = FindAnimationPlayer(ual2Instance);
                    GD.Print($"UAL2: AnimPlayer found = {ual2AnimPlayer != null}");
                    if (ual2AnimPlayer != null)
                    {
                        var ual2Lib = ual2AnimPlayer.GetAnimationLibrary("");
                        GD.Print($"UAL2: Library = {ual2Lib != null}, animations count = {ual2Lib.GetAnimationList().Count}");
                        if (ual2Lib != null)
                        {
                            string ual2Prefix = AnimationRetargeter.DetectPrefix(ual2Lib);
                            string godotPrefix = AnimationRetargeter.DetectPrefix(lib);
                            GD.Print($"UAL2: ual2Prefix='{ual2Prefix}', godotPrefix='{godotPrefix}'");
                            if (ual2Prefix != null && godotPrefix != null)
                            {
                                var boneMap = AnimationRetargeter.DetectBoneMap(ual2Lib);
                                GD.Print($"UAL2: boneMap has {boneMap.Count} entries");
                                string ual2SlideName = ual2Lib.HasAnimation("Slide")
                                    ? "Slide"
                                    : (ual2Lib.HasAnimation("Slide_Loop") ? "Slide_Loop" : null);
                                GD.Print($"UAL2: Slide anim name = '{ual2SlideName}'");
                                if (ual2SlideName != null)
                                {
                                    var sourceSlide = ual2Lib.GetAnimation(ual2SlideName);
                                    var remapped = AnimationRetargeter.RemapAnimationPaths(sourceSlide, ual2Prefix, godotPrefix, boneMap);
                                    GD.Print($"UAL2: Remapped tracks: {remapped.GetTrackCount()} (from {sourceSlide.GetTrackCount()})");
                                    if (remapped.GetTrackCount() > 0) GD.Print($"UAL2: First remapped track path: {remapped.TrackGetPath(0)}");
                                    remapped.LoopMode = Animation.LoopModeEnum.Linear;
                                    StripRotationTracks(remapped);
                                    RemoveRootMotion(remapped);
                                    lib.AddAnimation("Slide", remapped);
                                    _animPlayers["Slide"] = (animPlayer, "Slide");
                                    GD.Print("UAL2: Retargeted UAL2 Slide -> Godot Standard");
                                }
                            }
                            else
                            {
                                GD.PrintErr($"UAL2: ual2Prefix='{ual2Prefix}', godotPrefix='{godotPrefix}' - cannot retarget Slide");
                            }
                        }
                    }
                    ual2Instance.QueueFree();
                }
                else
                {
                    GD.PrintErr("UAL2: Could not load UAL2 scene for Slide retargeting");
                }
            }

            // Debug: list all available animations in the main library
            var allAnimNames = new System.Collections.Generic.List<string>();
            foreach (var n in lib.GetAnimationList()) allAnimNames.Add(n);
            GD.Print($"Animations available: {string.Join(", ", allAnimNames)}");
        }

        private void RemoveRootMotion(Animation animation)
        {
            string rootBoneName = _skeleton.GetBoneName(0);
            string rootBoneSimple = rootBoneName.Contains(':')
                ? rootBoneName.Substring(rootBoneName.LastIndexOf(':') + 1)
                : rootBoneName;

            for (int i = animation.GetTrackCount() - 1; i >= 0; i--)
            {
                var trackType = animation.TrackGetType(i);
                string trackPath = animation.TrackGetPath(i);

                int colonIdx = trackPath.IndexOf(':');
                if (colonIdx < 0) continue;

                string fullBoneName = trackPath.Substring(colonIdx + 1);
                string simpleBoneName = fullBoneName.Contains(':')
                    ? fullBoneName.Substring(fullBoneName.LastIndexOf(':') + 1)
                    : fullBoneName;

                bool isRootBone = fullBoneName == rootBoneName || simpleBoneName == rootBoneSimple;

                if (isRootBone
                    && (trackType == Animation.TrackType.Position3D
                        || trackType == Animation.TrackType.Scale3D))
                    animation.RemoveTrack(i);
            }
        }

        private void StripRotationTracks(Animation animation)
        {
            for (int i = animation.GetTrackCount() - 1; i >= 0; i--)
            {
                if (animation.TrackGetType(i) == Animation.TrackType.Rotation3D)
                    animation.RemoveTrack(i);
            }
        }

        private void StripLowerBodyTracks(Animation animation)
        {
            for (int i = animation.GetTrackCount() - 1; i >= 0; i--)
            {
                string trackPath = animation.TrackGetPath(i);
                int colonIdx = trackPath.IndexOf(':');
                if (colonIdx < 0) continue;

                string fullBoneName = trackPath.Substring(colonIdx + 1);
                string simpleBoneName = fullBoneName.Contains(':')
                    ? fullBoneName.Substring(fullBoneName.LastIndexOf(':') + 1)
                    : fullBoneName;

                if (IsLowerBodyBone(simpleBoneName))
                    animation.RemoveTrack(i);
            }
        }

        private void StripHeadTracks(Animation animation)
        {
            for (int i = animation.GetTrackCount() - 1; i >= 0; i--)
            {
                string trackPath = animation.TrackGetPath(i);
                int colonIdx = trackPath.IndexOf(':');
                if (colonIdx < 0) continue;

                string fullBoneName = trackPath.Substring(colonIdx + 1);
                string simpleBoneName = fullBoneName.Contains(':')
                    ? fullBoneName.Substring(fullBoneName.LastIndexOf(':') + 1)
                    : fullBoneName;

                if (IsHeadBone(simpleBoneName))
                    animation.RemoveTrack(i);
            }
        }

        private void StripUpperBodyTracks(Animation animation)
        {
            for (int i = animation.GetTrackCount() - 1; i >= 0; i--)
            {
                string trackPath = animation.TrackGetPath(i);
                int colonIdx = trackPath.IndexOf(':');
                if (colonIdx < 0) continue;

                string fullBoneName = trackPath.Substring(colonIdx + 1);
                string simpleBoneName = fullBoneName.Contains(':')
                    ? fullBoneName.Substring(fullBoneName.LastIndexOf(':') + 1)
                    : fullBoneName;

                if (IsUpperBodyBone(simpleBoneName))
                    animation.RemoveTrack(i);
            }
        }

        private void StripAllExceptHeadSpine(Animation animation)
        {
            for (int i = animation.GetTrackCount() - 1; i >= 0; i--)
            {
                string trackPath = animation.TrackGetPath(i);
                int colonIdx = trackPath.IndexOf(':');
                if (colonIdx < 0) continue;

                string fullBoneName = trackPath.Substring(colonIdx + 1);
                string simpleBoneName = fullBoneName.Contains(':')
                    ? fullBoneName.Substring(fullBoneName.LastIndexOf(':') + 1)
                    : fullBoneName;

                string name = simpleBoneName.ToLower();
                bool keep = name.Contains("spine") || name.Contains("neck") || name == "head";
                if (!keep)
                    animation.RemoveTrack(i);
            }
        }

        private static bool IsLowerBodyBone(string boneName)
        {
            string name = boneName.ToLower();
            return name.Contains("thigh")
                || name.Contains("calf")
                || name.Contains("shin")
                || name.Contains("foot")
                || name.Contains("ball")
                || name.Contains("toe")
                || name.Contains("pelvis")
                || name.Contains("hips")
                || name.Contains("upperleg")
                || name.Contains("lowerleg")
                || name.Contains("ik_foot");
        }

        private static bool IsHeadBone(string boneName)
        {
            string name = boneName.ToLower();
            return name.Contains("neck") || name == "head" || name.EndsWith("_head");
        }

        private static bool IsUpperBodyBone(string boneName)
        {
            string name = boneName.ToLower();
            if (IsLowerBodyBone(boneName)) return false;
            if (name is "root" or "pelvis") return false;
            return name.Contains("spine") || name.Contains("chest")
                || name.Contains("shoulder") || name.Contains("arm")
                || name.Contains("forearm") || name.Contains("elbow")
                || name.Contains("hand") || name.Contains("finger")
                || name.Contains("thumb") || name.Contains("clavicle")
                || name.Contains("neck") || name == "head"
                || name.Contains("ik_hand");
        }

        private static void SetChildrenVisible(Node node, bool visible)
        {
            if (node is Node3D node3d && node is not AnimationPlayer)
                node3d.Visible = visible;
            foreach (var child in node.GetChildren())
                SetChildrenVisible(child, visible);
        }

        private static AnimationPlayer FindAnimationPlayer(Node node)
        {
            if (node is AnimationPlayer player) return player;
            foreach (var child in node.GetChildren())
            {
                var result = FindAnimationPlayer(child);
                if (result != null) return result;
            }
            return null;
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void SyncInvisibilityRpc(bool invisible)
        {
            if (_modelPivot != null)
                _modelPivot.Visible = !invisible;
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
        private void SyncState(Vector3 position, Vector3 velocity, float yaw, int state, bool onFloor, float animSpeed)
        {
            _syncPosition = position;
            _syncVelocity = velocity;
            _syncYaw = yaw;
            _syncState = (PlayerState)state;
            _syncOnFloor = onFloor;
            _syncAnimSpeed = animSpeed;

            if (_state != null)
                _state.SetState(_syncState);
        }

        private void UpdateRemoteAnimation()
        {
            if (_state == null) return;

            bool onGround = _syncOnFloor;
            string targetAnim;
            float crossfade = 0.15f;

            bool justLanded = onGround && !_wasOnGround;
            bool justLeftGround = _wasOnGround && !onGround;

            if (justLeftGround)
            {
                targetAnim = "Jump_Start";
                _jumpStartDone = false;
                _jumpLandDone = false;
            }
            else if (!onGround)
            {
                targetAnim = _jumpStartDone ? "Jump" : "Jump_Start";
            }
            else if (justLanded && !_jumpLandDone)
            {
                targetAnim = "Jump_Land";
            }
            else
            {
                _jumpStartDone = true;
                _jumpLandDone = true;
                targetAnim = AnimForState(_syncState);

                if (_syncAnimSpeed > 1f && _syncState == PlayerState.Running)
                    targetAnim = "Sprint";

                if (targetAnim == "Idle")
                {
                    _idleTimer += (float)GetProcessDeltaTime();
                    if (_idleTimer > 8f)
                    {
                        targetAnim = "Idle_Talking";
                        if (_idleTimer > 12f) _idleTimer = 0f;
                    }
                }
                else
                {
                    _idleTimer = 0f;
                }
            }

            _wasOnGround = onGround;

            if (_animPlayers.TryGetValue(targetAnim, out var nextEntry))
            {
                if (targetAnim == _currentAnim)
                {
                    if (targetAnim is "Jog_Fwd" or "Sprint")
                    {
                        float refSpeed = targetAnim == "Sprint" ? SprintSpeed * 2f : SprintSpeed;
                        _currentAnimPlayer.SpeedScale = _syncAnimSpeed * (SprintSpeed / refSpeed);
                    }
                    return;
                }
            _currentAnimPlayer?.Stop();
            float blend = targetAnim is "Jump_Start" or "Jump_Land" ? 0f : crossfade;
            nextEntry.player.Play(nextEntry.animName, blend);
            _currentAnimPlayer = nextEntry.player;
            _currentAnim = targetAnim;

            if (targetAnim == "Jump_Start") _jumpStartDone = false;
            if (targetAnim == "Jump_Land") _jumpLandDone = false;

            if (targetAnim is "Jog_Fwd" or "Sprint")
            {
                float refSpeed = targetAnim == "Sprint" ? SprintSpeed * 2f : SprintSpeed;
                _currentAnimPlayer.SpeedScale = _syncAnimSpeed * (SprintSpeed / refSpeed);
            }
            else
            {
                _currentAnimPlayer.SpeedScale = 1f;
            }
            }
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
        public void ApplyDamage(float amount, int attackerPeerId)
        {
            if (_health == null || !_health.IsAlive()) return;
            _health.TakeDamage(amount);
        }

        // â”€â”€ Multiplayer hit reporting â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Called by the ATTACKER's client on the attacker's PlayerController puppet (server-side).
        /// Server validates and applies damage to the victim.
        /// </summary>
        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
        public void ReportHitServer(int victimPeerId, float damage, bool breaksGuard)
        {
            if (!Multiplayer.IsServer()) return;

            var victim = MatchManager.Instance?.GetPlayerByPeerId(victimPeerId);
            if (victim == null || !IsInstanceValid(victim)) return;

            var victimHealth = victim.GetNode<HealthComponent>("HealthComponent");
            if (victimHealth == null) return;

            // Basic validation: check distance
            float dist = GlobalPosition.DistanceTo(victim is Node3D n ? n.GlobalPosition : GlobalPosition);
            if (dist > 6f) return;

            victimHealth.ServerTakeDamage(damage, this, breaksGuard);
        }

        // â”€â”€ Inventory sync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
        public void RequestPickupServer(int pickupNetId)
        {
            if (!Multiplayer.IsServer()) return;
            World.Instance?.ServerCollectPickup(pickupNetId, GetMultiplayerAuthority());
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
        public void RpcSyncInventory(int selectedSlot, int weaponCount, int[] weaponTypes, int invCount, int[] invTypes)
        {
            // Client receives inventory state from server
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
        public void RpcSetSpectating()
        {
            _isSpectating = true;
            var hud = GameManager.Instance?.CurrentHUD;
            if (hud != null)
                hud.Visible = true;
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
        public void RpcRespawn(Vector3 position)
        {
            _isDowned = false;
            _isFullyDead = false;
            GlobalPosition = position;
            if (_modelPivot != null)
                _modelPivot.Position = _pivotOriginalPos;
            if (_health != null)
            {
                _health.Heal(_health.MaxHealth);
                _health.RestoreStamina(_health.MaxStamina);
            }
        }
    }
}

