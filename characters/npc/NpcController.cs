using Godot;
using System.Collections.Generic;

namespace StreetChaos
{
    public partial class NpcController : CharacterBody3D
    {
        private const string GodotAnimationLibraryPath =
            "res://Godot/AnimationLibrary_Godot_Standard.glb";
        private const string DefaultFightingStylePath =
            "res://resources/fighting_styles/street_brawler.tres";

        private const string ClimbingDownRightPath =
            "res://.godot/imported/anim_Climbing_Down_Right.fbx-310a91a67a05e8343c485d12468c20c0.scn";

        private const string ClimbingUpLeftPath =
            "res://.godot/imported/anim_Climbing_Up_Left.fbx-62ccaa7646f0b3fc1a7371fb4e387f39.scn";

        private const string FbxSkeletonPrefix = "SKM_Manny_Simple/Skeleton3D:";
        private const string Ue5SkeletonPrefix = "Rig/Skeleton3D:";

        private const float MoveSpeed = 4f;
        private const float RotationSpeed = 12f;
        private const float DetectionRadius = 25f;
        private const float AttackRange = 1.2f;
        private const float RotationThreshold = 0.1f;
        private const float HealThreshold = 0.3f;

        public int NetworkId { get; set; }
        public bool IsProxy { get; private set; }
        public bool IsDowned => _isDowned;
        public bool IsDead => _isDead;
        public Node3D ModelPivot => _modelPivot;

        private Node3D _modelRoot;
        private Node3D _modelPivot;
        private Skeleton3D _skeleton;
        private HealthComponent _health;
        private FightingStyleData _fightingStyle;
        private AttackData _currentAttack;
        private Node3D _target;
        private float _idleTimer;

        private readonly Dictionary<string, (AnimationPlayer player, StringName animName)> _animPlayers = new();
        private AnimationPlayer _currentAnimPlayer;
        private string _currentAnim = "Idle";
        private float _animSpeed = 1f;

        private SubViewport _healthViewport;
        private ColorRect _healthBarFill;
        private Sprite3D _healthBarSprite;
        private Vector3 _pivotOriginalPos;
        private Tween _shakeTween;

        private Area3D _hurtbox;
        private Area3D _hitbox;
        private bool _isInHitFrame;
        private bool _isDead;
        private bool _isDowned;
        private float _downedTimer;
        private float _reviveProgress;
        private bool _isBeingRevived;
        private Node _reviver;
        private Label3D _downedCountdownLabel;
        private const float DownedDuration = 60f;
        private const float ReviveHoldDuration = 5f;
        private float _hitWindowTimer;
        private float _animLockTimer;
        private readonly HashSet<Node> _hitTargets = new();

        private Area3D _interactionSensor;
        private float _attackCooldownTimer;
        private float _comboTimer;
        private int _comboStep;
        private bool _comboActive;

        private Node3D _hitboxContainer;

        private ItemData[] _inventorySlots = new ItemData[4];
        private int _selectedInventorySlot = -1;
        private ItemData _equippedItem;
        private Node3D _equippedItemNode;

        private static readonly AttackInput[] ComboSequence = { AttackInput.Light, AttackInput.Heavy, AttackInput.Light, AttackInput.Heavy };

        private const float ShakeIntensity = 0.08f;
        private const float ShakeDuration = 0.15f;

        public override void _Process(double delta)
        {
            if (!IsProxy) return;
            // Proxy NPCs on client: animate based on velocity
            UpdateAnimation((float)delta);
        }

        public void SetAsProxy(bool proxy)
        {
            IsProxy = proxy;
            if (proxy)
            {
                SetPhysicsProcess(false);
                SetProcess(true);
                if (_skeleton != null)
                    _skeleton.Visible = true;
            }
        }

        public override void _Ready()
        {
            _health = new HealthComponent();
            _health.Name = "HealthComponent";
            _health.MaxHealth = 1000f;
            AddChild(_health);
            _health.Died += OnDied;
            _health.TookDamage += OnTookDamage;
            _health.HealthChanged += OnHealthChanged;

            _fightingStyle = ResourceLoader.Load<FightingStyleData>(DefaultFightingStylePath);

            SetupCollision();
            SetupCharacter();
            LoadAnimations();
            SetupHitbox();
            SetupHurtbox();
            SetupHealthBar();
            SetupInteractionSensor();

            _pivotOriginalPos = _modelPivot?.Position ?? Vector3.Zero;
            AddToGroup("npcs");
            PlayBodyAnim("Idle", 0f, 1f);

            if (IsProxy)
                SetAsProxy(true);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (IsProxy) return;
            float dt = (float)delta;
            if (_health != null && !_health.IsAlive())
            {
                if (_isDead)
                {
                    MoveAndSlide();
                    UpdateAnimation(dt);
                }
                else if (_isDowned)
                {
                    UpdateDowned(dt);
                }
                return;
            }

            _hitWindowTimer -= dt;
            if (_hitWindowTimer <= 0 && _isInHitFrame)
                DisableHitbox();

            if (_animLockTimer > 0)
            {
                _animLockTimer -= dt;
                if (_animLockTimer <= 0 && _currentAttack != null && !_isInHitFrame)
                    _currentAttack = null;
            }

            // Try to revive downed NPCs nearby
            if (TryRevive(dt))
            {
                MoveAndSlide();
                UpdateAnimation(dt);
                return;
            }

            FindTarget();

            if (_target != null)
            {
                Vector3 dir = (_target.GlobalPosition - GlobalPosition);
                float dist = dir.Length();
                dir.Y = 0;
                dir = dir.Normalized();

                bool inAttackRange = dist <= AttackRange;

                if (dir.Length() > RotationThreshold && _modelPivot != null)
                {
                    float targetYaw = Mathf.Atan2(dir.X, dir.Z);
                    float currentYaw = _modelPivot.Rotation.Y;
                    float newYaw = (float)Mathf.LerpAngle(currentYaw, targetYaw, RotationSpeed * dt);
                    _modelPivot.Rotation = new Vector3(0, newYaw, 0);
                }

                if (inAttackRange)
                {
                    if (_isInHitFrame)
                        Velocity = new Vector3(0, Velocity.Y + GetGravity().Y * dt, 0);
                    else
                        Velocity = new Vector3(dir.X * MoveSpeed * 0.5f, Velocity.Y + GetGravity().Y * dt, dir.Z * MoveSpeed * 0.5f);
                    TryAttack(dt);
                }
                else
                {
                    Velocity = new Vector3(dir.X * MoveSpeed, Velocity.Y + GetGravity().Y * dt, dir.Z * MoveSpeed);
                }

                TryCollectNearbyItems();
                TryAutoUseItems();
            }
            else
            {
                Velocity = new Vector3(0, Velocity.Y + GetGravity().Y * dt, 0);
            }

            MoveAndSlide();
            UpdateAnimation(dt);
        }

        private void FindTarget()
        {
            _target = null;
            float closestDist = DetectionRadius;

            foreach (var p in GetTree().GetNodesInGroup("players"))
            {
                if (p == this || p is not Node3D node) continue;
                var h = node.GetNodeOrNull<HealthComponent>("HealthComponent");
                if (h == null || !h.IsAlive()) continue;
                float d = GlobalPosition.DistanceTo(node.GlobalPosition);
                if (d < closestDist) { closestDist = d; _target = node; }
            }

            foreach (var n in GetTree().GetNodesInGroup("npcs"))
            {
                if (n == this || n is not Node3D node) continue;
                var h = node.GetNodeOrNull<HealthComponent>("HealthComponent");
                if (h == null || !h.IsAlive()) continue;
                float d = GlobalPosition.DistanceTo(node.GlobalPosition);
                if (d < closestDist) { closestDist = d; _target = node; }
            }
        }

        private bool TryRevive(float delta)
        {
            NpcController nearestDowned = null;
            float closestDist = 3f;

            foreach (var n in GetTree().GetNodesInGroup("downed_npcs"))
            {
                if (n == this || n is not NpcController npc) continue;
                float d = GlobalPosition.DistanceTo(npc.GlobalPosition);
                if (d < closestDist)
                {
                    closestDist = d;
                    nearestDowned = npc;
                }
            }

            if (nearestDowned == null) return false;

            if (closestDist > 2f)
            {
                Vector3 dir = (nearestDowned.GlobalPosition - GlobalPosition).Normalized();
                dir.Y = 0;
                Velocity = new Vector3(dir.X * MoveSpeed, Velocity.Y + GetGravity().Y * delta, dir.Z * MoveSpeed);
                return true;
            }

            nearestDowned.StartRevive(this);
            nearestDowned.ReviveTick(delta);
            Velocity = new Vector3(0, Velocity.Y + GetGravity().Y * delta, 0);
            return true;
        }

        private void TryAttack(float delta)
        {
            if (_currentAttack != null) return;

            _attackCooldownTimer -= delta;
            if (_attackCooldownTimer > 0) return;

            if (_fightingStyle == null) return;

            _comboTimer -= delta;
            if (_comboTimer <= 0)
                _comboActive = false;

            AttackInput input = AttackInput.Light;

            if (!_comboActive)
            {
                _comboActive = true;
                _comboStep = 0;
                _comboTimer = 0.05f;
            }

            if (_comboActive && _comboStep >= 0 && _comboStep < ComboSequence.Length)
            {
                _comboTimer = 0.45f;
                input = ComboSequence[_comboStep];
                _comboStep++;
            }

            StartAttack(input);
        }

        private void StartAttack(AttackInput input)
        {
            if (_fightingStyle == null) return;
            var attack = _fightingStyle.GetAttack(input);
            if (attack == null) return;

            _currentAttack = attack;
            _attackCooldownTimer = attack.RecoveryTime;
            _animLockTimer = 0.6f;

            SetupHitboxPosition(attack);
            PlayBodyAnim(AnimForAttack(attack.Type), 0.15f, attack.Speed);
        }

        private static string AnimForAttack(AttackType type) => type switch
        {
            AttackType.LightPunch => "Punch_Jab",
            AttackType.HeavyPunch => "Punch_Cross",
            AttackType.LightKick => "Punch_Jab",
            AttackType.HeavyKick => "Punch_Cross",
            AttackType.Uppercut => "Punch_Cross",
            _ => "Punch_Jab",
        };

        private void SetupHitboxPosition(AttackData attack)
        {
            if (_hitbox == null) return;

            if (_hitbox.GetChild(0) is CollisionShape3D cs && cs.Shape is BoxShape3D box)
            {
                Vector3 size = attack.HitboxSize;
                size.Z = Mathf.Min(size.Z, 0.6f);
                box.Size = size;
            }

            float closeRange = Mathf.Min(attack.Range, 1.0f);
            float yaw = _modelPivot?.Rotation.Y ?? 0f;
            _hitbox.Position = new Vector3(0, 0.5f, closeRange / 2f).Rotated(Vector3.Up, yaw);
            _hitbox.Rotation = new Vector3(0, yaw, 0);
            _hitWindowTimer = attack.HitDuration;
            EnableHitbox();
        }

        private void EnableHitbox()
        {
            if (_hitbox == null) return;
            _isInHitFrame = true;
            _hitbox.Monitoring = true;
            _hitbox.Monitorable = true;
            _hitTargets.Clear();

            Callable.From(() =>
            {
                if (_isInHitFrame && _hitbox != null)
                    foreach (var area in _hitbox.GetOverlappingAreas())
                        ProcessHit(area);
            }).CallDeferred();
        }

        private void DisableHitbox()
        {
            if (_hitbox == null) return;
            _isInHitFrame = false;
            _hitbox.Monitoring = false;
            _hitbox.Monitorable = false;
        }

        private void OnHitboxEntered(Area3D area)
        {
            if (!_isInHitFrame || _currentAttack == null) return;
            ProcessHit(area);
        }

        private void ProcessHit(Area3D area)
        {
            if (!_isInHitFrame || _currentAttack == null) return;

            Node target = ResolveHitTarget(area);
            if (target == null || target == this) return;
            if (!_hitTargets.Add(target)) return;

            float damage = _currentAttack.Damage;
            if (_fightingStyle != null)
                damage *= _fightingStyle.DamageMultiplier;

            if (target is PlayerController targetPlayer)
            {
                var targetHealth = targetPlayer.GetNode<HealthComponent>("HealthComponent");
                if (targetHealth != null)
                {
                    targetHealth.TakeDamage(damage, this, _currentAttack.BreaksGuard);
                }
            }
            else if (target is NpcController npc)
            {
                var npcHealth = npc.GetNode<HealthComponent>("HealthComponent");
                if (npcHealth != null)
                    npcHealth.TakeDamage(damage, this, _currentAttack.BreaksGuard);
            }
        }

        private static Node ResolveHitTarget(Area3D area)
        {
            Node owner = area.GetOwner();
            if (owner is PlayerController || owner is NpcController)
                return owner;

            Node current = area.GetParent();
            while (current != null)
            {
                if (current is PlayerController || current is NpcController)
                    return current;
                current = current.GetParent();
            }
            return null;
        }

        private void SetupCollision()
        {
            var collisionShape = new CollisionShape3D();
            collisionShape.Shape = new CapsuleShape3D { Radius = 0.2f, Height = 1.8f };
            AddChild(collisionShape);
        }

        private void SetupCharacter()
        {
            var charScene = ResourceLoader.Load<PackedScene>(GodotAnimationLibraryPath);
            if (charScene == null) { GD.PrintErr("NpcController: Failed to load character scene"); return; }

            _modelPivot = new Node3D();
            _modelPivot.Name = "ModelPivot";
            _modelPivot.Position = new Vector3(0, -0.9f, 0);
            AddChild(_modelPivot);

            _pivotOriginalPos = _modelPivot.Position;

            _hitboxContainer = new Node3D();
            _hitboxContainer.Name = "HitboxContainer";
            AddChild(_hitboxContainer);

            _modelRoot = charScene.Instantiate<Node3D>();
            _modelPivot.AddChild(_modelRoot);
            _skeleton = FindSkeleton(_modelRoot);

            ApplyRedTint();
        }

        private void SetupHitbox()
        {
            _hitbox = new Area3D();
            _hitbox.Name = "AttackHitbox";
            _hitbox.CollisionMask = 2;
            _hitbox.CollisionLayer = 0;

            var shape = new CollisionShape3D();
            shape.Shape = new BoxShape3D();
            _hitbox.AddChild(shape);

            _hitboxContainer.AddChild(_hitbox);
            _hitbox.Monitoring = false;
            _hitbox.Monitorable = false;
            _hitbox.AreaEntered += OnHitboxEntered;
        }

        private void SetupHurtbox()
        {
            _hurtbox = new Area3D();
            _hurtbox.Name = "Hurtbox";
            _hurtbox.CollisionLayer = 2;
            _hurtbox.CollisionMask = 0;
            _hurtbox.Monitoring = false;
            _hurtbox.Monitorable = true;

            var shape = new CollisionShape3D();
            shape.Shape = new CylinderShape3D { Radius = 0.3f, Height = 1.8f };
            _hurtbox.AddChild(shape);

            AddChild(_hurtbox);
        }

        private void SetupInteractionSensor()
        {
            _interactionSensor = new Area3D();
            _interactionSensor.Name = "InteractionSensor";
            _interactionSensor.Monitoring = true;
            _interactionSensor.Monitorable = false;
            _interactionSensor.CollisionMask = 1;

            var shape = new CollisionShape3D();
            shape.Shape = new SphereShape3D { Radius = 2.5f };
            _interactionSensor.AddChild(shape);

            AddChild(_interactionSensor);
        }

        private void SetupHealthBar()
        {
            _healthViewport = new SubViewport();
            _healthViewport.Name = "HealthBarViewport";
            _healthViewport.Size = new Vector2I(100, 10);
            _healthViewport.TransparentBg = true;
            _healthViewport.Disable3D = true;
            _healthViewport.HandleInputLocally = false;
            _healthViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            AddChild(_healthViewport);

            var root = new Control();
            root.Size = new Vector2(100, 10);
            _healthViewport.AddChild(root);

            var bg = new ColorRect();
            bg.Color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
            bg.Size = new Vector2(100, 10);
            root.AddChild(bg);

            _healthBarFill = new ColorRect();
            _healthBarFill.Color = new Color(0.2f, 0.8f, 0.2f, 1f);
            _healthBarFill.Size = new Vector2(100, 10);
            root.AddChild(_healthBarFill);

            _healthBarSprite = new Sprite3D();
            _healthBarSprite.Name = "HealthBarSprite";
            _healthBarSprite.Position = new Vector3(0, 2.6f, 0);
            _healthBarSprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            _healthBarSprite.PixelSize = 0.004f;
            _healthBarSprite.Centered = true;
            _healthBarSprite.Texture = _healthViewport.GetTexture();
            AddChild(_healthBarSprite);
        }

        private void TryCollectNearbyItems()
        {
            if (_interactionSensor == null) return;
            foreach (var area in _interactionSensor.GetOverlappingAreas())
            {
                if (area is PickupItem pickup && GodotObject.IsInstanceValid(pickup) && pickup.Monitoring)
                {
                    if (AcquireItem(pickup.ItemData))
                    {
                        pickup.Monitoring = false;
                        pickup.Visible = false;
                        pickup.QueueFree();
                        break;
                    }
                }
            }
        }

        private void TryAutoUseItems()
        {
            if (_health == null || _inventorySlots == null) return;
            float hpRatio = _health.CurrentHealth / _health.MaxHealth;

            if (hpRatio < HealThreshold)
            {
                for (int i = 0; i < _inventorySlots.Length; i++)
                {
                    if (_inventorySlots[i] is HealingData heal)
                    {
                        _health.Heal(heal.HealAmount);
                        _inventorySlots[i] = null;
                        SyncInventoryHud();
                        break;
                    }
                }
            }

            if (_selectedInventorySlot == -1 || _inventorySlots[_selectedInventorySlot] == null)
            {
                for (int i = 0; i < _inventorySlots.Length; i++)
                {
                    if (_inventorySlots[i] is WeaponData weapon)
                    {
                        _selectedInventorySlot = i;
                        EquipItem(weapon);
                        SyncInventoryHud();
                        break;
                    }
                }
            }
        }

        public bool AcquireItem(ItemData item)
        {
            if (item == null) return false;

            for (int i = 0; i < _inventorySlots.Length; i++)
            {
                if (_inventorySlots[i] != null) continue;
                _inventorySlots[i] = item;
                if (_selectedInventorySlot == -1)
                {
                    _selectedInventorySlot = i;
                    if (item is WeaponData weapon)
                        EquipItem(weapon);
                }
                SyncInventoryHud();
                return true;
            }

            if (_selectedInventorySlot >= 0 && _selectedInventorySlot < _inventorySlots.Length)
            {
                var oldItem = _inventorySlots[_selectedInventorySlot];
                _inventorySlots[_selectedInventorySlot] = item;
                if (item is WeaponData newWeapon)
                    EquipItem(newWeapon);
                else
                    UnequipItem();
                SyncInventoryHud();
                return true;
            }
            return false;
        }

        private void EquipItem(ItemData item)
        {
            if (_equippedItem == item || _skeleton == null) return;

            if (_equippedItemNode != null)
            {
                _equippedItemNode.QueueFree();
                _equippedItemNode = null;
            }
            _equippedItem = item;

            if (item?.WorldModel != null)
            {
                int boneIdx = -1;
                for (int i = 0; i < _skeleton.GetBoneCount(); i++)
                {
                    string name = _skeleton.GetBoneName(i);
                    if (name.Contains("hand_r") || name.Contains("hand.R") || name.Contains("Hand_R"))
                    { boneIdx = i; break; }
                }
                if (boneIdx == -1) return;

                var attachment = new BoneAttachment3D();
                attachment.BoneName = _skeleton.GetBoneName(boneIdx);
                _skeleton.AddChild(attachment);

                var model = item.WorldModel.Instantiate<Node3D>();
                attachment.AddChild(model);

                model.Position = new Vector3(0.02f, -0.02f, 0.15f);
                model.Rotation = new Vector3(Mathf.DegToRad(-90), 0, 0);
                model.Scale = Vector3.One * 0.5f;

                _equippedItemNode = attachment;
            }
        }

        private void UnequipItem()
        {
            if (_equippedItemNode != null)
            {
                _equippedItemNode.QueueFree();
                _equippedItemNode = null;
            }
            _equippedItem = null;
        }

        private void SyncInventoryHud() { }

        private void LoadAnimations()
        {
            if (_skeleton == null) return;

            var animPlayer = FindAnimationPlayer(_modelRoot);
            if (animPlayer == null) { GD.PrintErr("NpcController: No AnimationPlayer in character model"); return; }

            // Use default root node path resolution

            // Strip UE5 prefix (Rig|) and _Loop suffix from animation names
            var lib = animPlayer.GetAnimationLibrary("");
            if (lib != null)
            {
                var namesToFix = new System.Collections.Generic.List<string>();
                foreach (var name in lib.GetAnimationList())
                    if (((string)name).StartsWith("Rig|"))
                        namesToFix.Add((string)name);
                foreach (var name in namesToFix)
                {
                    string newName = name.Substring(4);
                    if (newName.EndsWith("_Loop"))
                        newName = newName.Substring(0, newName.Length - 5);
                    if (newName == "Roll_RM")
                        newName = "Roll";
                    if (lib.HasAnimation(newName))
                        continue;
                    lib.RenameAnimation(name, newName);
                }

                // Punch_Enter is missing in AL_Standard; duplicate Punch_Jab as fallback
                if (!lib.HasAnimation("Punch_Enter") && lib.HasAnimation("Punch_Jab"))
                {
                    var punchEnter = (Animation)lib.GetAnimation("Punch_Jab").Duplicate(true);
                    lib.AddAnimation("Punch_Enter", punchEnter);
                    GD.Print("Created fallback Punch_Enter from Punch_Jab");
                }

                // Create "Walk" from "Jog_Fwd"
                if (!lib.HasAnimation("Walk") && lib.HasAnimation("Jog_Fwd"))
                {
                    var walk = (Animation)lib.GetAnimation("Jog_Fwd").Duplicate(true);
                    lib.AddAnimation("Walk", walk);
                    GD.Print("Created fallback Walk from Jog_Fwd");
                }

                // Create "Sprint" from "Jog_Fwd"
                if (!lib.HasAnimation("Sprint") && lib.HasAnimation("Jog_Fwd"))
                {
                    var sprint = (Animation)lib.GetAnimation("Jog_Fwd").Duplicate(true);
                    lib.AddAnimation("Sprint", sprint);
                    GD.Print("Created fallback Sprint from Jog_Fwd");
                }
            }

            LoadClimbingAnimations(animPlayer);

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
                        or "Punch_Jab" or "Punch_Cross" or "Punch_Enter"
                        or "Death01")
                        RemoveRootMotion(animation);

                    _animPlayers[nameStr] = (animPlayer, animName);
                }
            }
        }

        private void RemoveRootMotion(Animation animation)
        {
            if (animation == null || _skeleton == null) return;
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

        private void LoadClimbingAnimations(AnimationPlayer animPlayer)
        {
            // Detect actual prefix from AL_Standard animations
            string targetPrefix = "Rig/Skeleton3D:";
            var mainLib = animPlayer.GetAnimationLibrary("");
            if (mainLib != null)
            {
                foreach (var name in mainLib.GetAnimationList())
                {
                    var anim = mainLib.GetAnimation(name);
                    if (anim != null && anim.GetTrackCount() > 0)
                    {
                        string trackPath = anim.TrackGetPath(0);
                        int colonIdx = trackPath.LastIndexOf(':');
                        if (colonIdx >= 0)
                        {
                            targetPrefix = trackPath.Substring(0, colonIdx + 1);
                        }
                        break;
                    }
                }
            }

            string[] climbingPaths = { ClimbingDownRightPath, ClimbingUpLeftPath };

            foreach (var scenePath in climbingPaths)
            {
                var scene = ResourceLoader.Load<PackedScene>(scenePath);
                if (scene == null) { GD.PrintErr($"Failed to load climbing scene: {scenePath}"); continue; }

                var instance = scene.Instantiate();
                var fbxAnimPlayer = FindAnimationPlayer(instance);
                if (fbxAnimPlayer == null) { GD.PrintErr($"No AnimationPlayer in climbing scene: {scenePath}"); instance.QueueFree(); continue; }

                var animList = fbxAnimPlayer.GetAnimationList();
                foreach (var animName in animList)
                {
                    var sourceAnim = fbxAnimPlayer.GetAnimation(animName);
                    if (sourceAnim == null) continue;

                    var remapped = AnimationRetargeter.RemapAnimationPaths(sourceAnim, FbxSkeletonPrefix, targetPrefix);
                    if (remapped.GetTrackCount() == 0) { GD.Print($"No tracks remained after retargeting: {animName}"); continue; }

                    remapped.LoopMode = Animation.LoopModeEnum.None;
                    var lib = animPlayer.GetAnimationLibrary("");
                    if (lib == null)
                    {
                        lib = new AnimationLibrary();
                        animPlayer.AddAnimationLibrary("", lib);
                    }
                    lib.AddAnimation(animName, remapped);
                    _animPlayers[animName] = (animPlayer, animName);
                    GD.Print($"Npc climbing animation loaded: {animName} ({remapped.GetTrackCount()} tracks)");
                }

                instance.QueueFree();
            }
        }

        private void UpdateAnimation(float delta)
        {
            if (_isDead || _isDowned) return;
            if (_isInHitFrame && _currentAttack != null || _animLockTimer > 0)
                return;

            bool isMoving = Velocity.Length() > 0.2f;

            string targetAnim;
            if (isMoving)
                targetAnim = "Jog_Fwd";
            else
                targetAnim = "Idle";

            if (targetAnim == "Idle")
            {
                if (!IsProxy)
                {
                    _idleTimer += delta;
                    if (_idleTimer > 8f) { targetAnim = "Idle_Talking"; if (_idleTimer > 12f) _idleTimer = 0f; }
                }
            }
            else
                _idleTimer = 0f;

            PlayBodyAnim(targetAnim, 0.15f, 1f);
        }

        private void PlayBodyAnim(string targetAnim, float crossfade, float speedScale)
        {
            if (!_animPlayers.TryGetValue(targetAnim, out var entry)) return;
            if (targetAnim == _currentAnim && Mathf.Abs(entry.player.SpeedScale - speedScale) <= 0.001f) return;

            if (targetAnim != _currentAnim)
            {
                _currentAnimPlayer?.Stop();
                float blend = targetAnim is "Jump_Start" or "Jump_Land" ? 0f : crossfade;
                entry.player.Play(entry.animName, blend);
                _currentAnimPlayer = entry.player;
                _currentAnim = targetAnim;
            }
            entry.player.SpeedScale = speedScale;
        }

        private void ApplyRedTint()
        {
            foreach (var mi in FindMeshInstances(_modelRoot))
            {
                if (mi.Mesh == null) continue;
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                {
                    var mat = mi.GetSurfaceOverrideMaterial(i) ?? mi.Mesh.SurfaceGetMaterial(i);
                    if (mat is BaseMaterial3D bm)
                    {
                        var clone = (BaseMaterial3D)bm.Duplicate();
                        var c = clone.AlbedoColor;
                        clone.AlbedoColor = new Color(c.R * 2f, c.G * 0.3f, c.B * 0.3f, c.A);
                        mi.SetSurfaceOverrideMaterial(i, clone);
                    }
                }
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            if (_healthBarFill == null) return;
            float ratio = Mathf.Clamp(current / max, 0f, 1f);
            _healthBarFill.Size = new Vector2(100 * ratio, 10);

            if (ratio > 0.5f)
                _healthBarFill.Color = new Color(0.2f, 0.8f, 0.2f, 1f);
            else if (ratio > 0.25f)
                _healthBarFill.Color = new Color(0.8f, 0.8f, 0.2f, 1f);
            else
                _healthBarFill.Color = new Color(0.8f, 0.2f, 0.2f, 1f);

            if (_healthViewport != null)
                _healthViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        }

        private void OnTookDamage(float damage, Node attacker)
        {
            SpawnDamageNumber(damage);
            ShakeEffect();
        }

        private void ShakeEffect()
        {
            if (_modelPivot == null) return;

            _shakeTween?.Kill();
            _modelPivot.Position = _pivotOriginalPos;

            var original = _pivotOriginalPos;
            _shakeTween = CreateTween();

            for (int i = 0; i < 8; i++)
            {
                float t = i / 8f;
                float intensity = ShakeIntensity * (1f - t);
                float sx = (float)(GD.Randf() * 2f - 1f) * intensity;
                float sz = (float)(GD.Randf() * 2f - 1f) * intensity;
                _shakeTween.TweenProperty(_modelPivot, "position",
                    original + new Vector3(sx, 0, sz), ShakeDuration / 8f);
            }
            _shakeTween.TweenProperty(_modelPivot, "position", original, 0.03f);
        }

        private void SpawnDamageNumber(float damage)
        {
            int dmg = Mathf.RoundToInt(damage);
            var label = new Label3D();
            label.Text = dmg.ToString();
            label.FontSize = 48;
            label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            label.Modulate = new Color(1, 0.8f, 0.2f, 1);
            label.NoDepthTest = true;

            var root = GetTree().Root;
            root.AddChild(label);
            label.GlobalPosition = GlobalPosition + new Vector3(0, 1.5f, 0);

            float floatHeight = 2f;
            float duration = 1.0f;

            var tween = root.CreateTween();
            tween.BindNode(label);
            tween.SetParallel(true);
            tween.TweenMethod(Callable.From((Vector3 pos) => label.GlobalPosition = pos),
                label.GlobalPosition, label.GlobalPosition + new Vector3(0, floatHeight, 0), duration);
            tween.TweenProperty(label, "modulate:a", 0f, duration);
            tween.TweenCallback(Callable.From(() => label.QueueFree()));
        }

        /// <summary>
        /// Called by the server on proxy NPCs to trigger downed state visually.
        /// </summary>
        public void TriggerDowned()
        {
            if (_isDowned) return;
            _isDowned = true;
            PlayBodyAnim("Death01", 0.15f, 1f);
        }

        /// <summary>
        /// Called by the server on proxy NPCs to mark fully dead.
        /// </summary>
        public void TriggerDead()
        {
            _isDowned = false;
            _isDead = true;
        }

        private void OnDied()
        {
            if (IsProxy)
            {
                TriggerDowned();
                return;
            }
            OnDowned();
        }

        private void OnDowned()
        {
            if (_isDowned) return;
            _isDowned = true;
            _downedTimer = DownedDuration;
            _reviveProgress = 0f;
            _isBeingRevived = false;
            _reviver = null;

            SetCollisionLayerValue(1, false);
            SetCollisionMaskValue(1, false);

            if (_hurtbox != null)
                _hurtbox.SetDeferred(Area3D.PropertyName.Monitorable, false);

            if (_healthBarSprite != null)
                _healthBarSprite.Visible = false;

            AddToGroup("downed_npcs");

            PlayBodyAnim("Death01", 0.15f, 1f);

            _downedCountdownLabel = new Label3D();
            _downedCountdownLabel.Text = Mathf.CeilToInt(_downedTimer).ToString();
            _downedCountdownLabel.FontSize = 48;
            _downedCountdownLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            _downedCountdownLabel.Modulate = new Color(1, 0.3f, 0.3f, 1);
            _downedCountdownLabel.NoDepthTest = true;
            _downedCountdownLabel.Position = new Vector3(0, 2.8f, 0);
            AddChild(_downedCountdownLabel);
        }

        private void OnFullyDead()
        {
            _isDowned = false;
            _isDead = true;

            if (_downedCountdownLabel != null)
            {
                _downedCountdownLabel.QueueFree();
                _downedCountdownLabel = null;
            }
            RemoveFromGroup("downed_npcs");

            // Server: remove dead NPC after a short delay
            if (NetworkManager.Instance?.IsServer == true)
            {
                var tween = CreateTween();
                tween.TweenCallback(Callable.From(QueueFree)).SetDelay(3f);
            }
        }

        private void OnRevived()
        {
            _isDowned = false;
            _isBeingRevived = false;
            _reviver = null;
            _reviveProgress = 0f;

            if (_downedCountdownLabel != null)
            {
                _downedCountdownLabel.QueueFree();
                _downedCountdownLabel = null;
            }
            RemoveFromGroup("downed_npcs");

            SetCollisionLayerValue(1, true);
            SetCollisionMaskValue(1, true);

            if (_hurtbox != null)
                _hurtbox.Monitorable = true;

            if (_healthBarSprite != null)
                _healthBarSprite.Visible = true;

            if (_health != null)
            {
                _health.Heal(_health.MaxHealth * 0.5f);
                _health.RestoreStamina(_health.MaxStamina);
            }

            PlayBodyAnim("Idle", 0.15f, 1f);
        }

        private void UpdateDowned(float dt)
        {
            if (!_isDowned) return;
            _downedTimer -= dt;

            if (_isBeingRevived)
            {
                if (_reviver != null && IsInstanceValid(_reviver)
                    && GlobalPosition.DistanceTo(_reviver is Node3D n ? n.GlobalPosition : GlobalPosition) > 3f)
                {
                    _isBeingRevived = false;
                    _reviver = null;
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
                _reviver = null;

                if (_downedCountdownLabel != null)
                {
                    int secs = Mathf.Max(0, Mathf.CeilToInt(_downedTimer));
                    _downedCountdownLabel.Text = secs.ToString();
                    _downedCountdownLabel.Modulate = new Color(1, 0.3f, 0.3f, 1);
                    if (secs <= 10)
                        _downedCountdownLabel.Modulate = new Color(1, 0.1f, 0.1f, 1);
                }

                if (_downedTimer <= 0)
                    OnFullyDead();
            }

            Velocity = Vector3.Zero;
            MoveAndSlide();
            UpdateAnimation(dt);
        }

        public void StartRevive(Node reviver)
        {
            if (!_isDowned) return;
            _reviver = reviver;
            _isBeingRevived = true;
        }

        public void ReviveTick(float delta)
        {
            if (!_isDowned || _isDead || _downedTimer <= 0) return;
            if (!_isBeingRevived) return;

            bool reviverNearby = _reviver != null && IsInstanceValid(_reviver)
                && GlobalPosition.DistanceTo(_reviver is Node3D n ? n.GlobalPosition : GlobalPosition) <= 3f;

            if (!reviverNearby)
            {
                _isBeingRevived = false;
                _reviver = null;
                _reviveProgress = 0f;
                return;
            }

            _reviveProgress += delta;
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

        private static AnimationPlayer FindAnimationPlayer(Node node)
        {
            if (node is AnimationPlayer ap) return ap;
            foreach (var child in node.GetChildren())
            {
                var result = FindAnimationPlayer(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}
